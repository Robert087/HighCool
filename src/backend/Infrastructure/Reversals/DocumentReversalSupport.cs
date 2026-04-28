using ERP.Application.Reversals;
using ERP.Domain.Common;
using ERP.Domain.Reversals;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Reversals;

internal static class DocumentReversalSupport
{
    public static async Task<DocumentReversal> CreateAsync(
        AppDbContext dbContext,
        BusinessDocumentType documentType,
        Guid documentId,
        ReverseDocumentRequest request,
        string actor,
        CancellationToken cancellationToken)
    {
        var existing = await dbContext.DocumentReversals
            .SingleOrDefaultAsync(
                entity => entity.ReversedDocumentType == documentType && entity.ReversedDocumentId == documentId,
                cancellationToken);

        if (existing is not null)
        {
            throw new InvalidOperationException("This document has already been reversed.");
        }

        var reversal = new DocumentReversal
        {
            ReversalNo = await ResolveReversalNoAsync(dbContext, cancellationToken),
            ReversedDocumentType = documentType,
            ReversedDocumentId = documentId,
            ReversalDate = request.ReversalDate!.Value,
            ReversalReason = request.ReversalReason!.Trim(),
            CreatedBy = actor
        };

        dbContext.DocumentReversals.Add(reversal);
        return reversal;
    }

    public static DocumentReversalDto ToDto(DocumentReversal reversal)
    {
        return new DocumentReversalDto(
            reversal.Id,
            reversal.ReversalNo,
            reversal.ReversedDocumentType,
            reversal.ReversedDocumentId,
            reversal.ReversalDate,
            reversal.ReversalReason,
            reversal.CreatedAt,
            reversal.CreatedBy);
    }

    private static async Task<string> ResolveReversalNoAsync(AppDbContext dbContext, CancellationToken cancellationToken)
    {
        var prefix = $"REV-{DateTime.UtcNow:yyyyMMdd}-";
        var latest = await dbContext.DocumentReversals
            .AsNoTracking()
            .Where(entity => entity.ReversalNo.StartsWith(prefix))
            .OrderByDescending(entity => entity.ReversalNo)
            .Select(entity => entity.ReversalNo)
            .FirstOrDefaultAsync(cancellationToken);

        var nextSequence = 1;
        if (!string.IsNullOrWhiteSpace(latest))
        {
            var tail = latest[prefix.Length..];
            if (int.TryParse(tail, out var parsed))
            {
                nextSequence = parsed + 1;
            }
        }

        return $"{prefix}{nextSequence:D4}";
    }
}
