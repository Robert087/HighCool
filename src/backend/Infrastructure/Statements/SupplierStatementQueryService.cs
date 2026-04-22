using ERP.Application.Statements;
using ERP.Domain.Payments;
using ERP.Domain.Statements;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Statements;

public sealed class SupplierStatementQueryService(AppDbContext dbContext) : ISupplierStatementQueryService
{
    public async Task<IReadOnlyList<SupplierStatementEntryDto>> ListAsync(SupplierStatementQuery query, CancellationToken cancellationToken)
    {
        var entriesQuery = dbContext.SupplierStatementEntries
            .AsNoTracking()
            .Include(entity => entity.Supplier)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            entriesQuery = entriesQuery.Where(entity =>
                entity.Supplier!.Code.Contains(search) ||
                entity.Supplier.Name.Contains(search) ||
                (entity.Notes != null && entity.Notes.Contains(search)));
        }

        if (query.SupplierId.HasValue)
        {
            entriesQuery = entriesQuery.Where(entity => entity.SupplierId == query.SupplierId.Value);
        }

        if (query.EffectType.HasValue)
        {
            entriesQuery = entriesQuery.Where(entity => entity.EffectType == query.EffectType.Value);
        }

        if (query.SourceDocType.HasValue)
        {
            entriesQuery = entriesQuery.Where(entity => entity.SourceDocType == query.SourceDocType.Value);
        }

        if (query.FromDate.HasValue)
        {
            entriesQuery = entriesQuery.Where(entity => entity.EntryDate >= query.FromDate.Value);
        }

        if (query.ToDate.HasValue)
        {
            entriesQuery = entriesQuery.Where(entity => entity.EntryDate <= query.ToDate.Value);
        }

        var entries = await entriesQuery
            .OrderByDescending(entity => entity.EntryDate)
            .ThenByDescending(entity => entity.CreatedAt)
            .ThenByDescending(entity => entity.Id)
            .ToListAsync(cancellationToken);

        var purchaseReceiptIds = entries
            .Where(entity => entity.SourceDocType == SupplierStatementSourceDocumentType.PurchaseReceipt)
            .Select(entity => entity.SourceDocId)
            .Distinct()
            .ToArray();

        var shortageResolutionIds = entries
            .Where(entity => entity.SourceDocType == SupplierStatementSourceDocumentType.ShortageResolution)
            .Select(entity => entity.SourceDocId)
            .Distinct()
            .ToArray();

        var paymentIds = entries
            .Where(entity => entity.SourceDocType == SupplierStatementSourceDocumentType.Payment)
            .Select(entity => entity.SourceDocId)
            .Distinct()
            .ToArray();

        var receiptNumbers = purchaseReceiptIds.Length == 0
            ? new Dictionary<Guid, string>()
            : await dbContext.PurchaseReceipts
                .AsNoTracking()
                .Where(entity => purchaseReceiptIds.Contains(entity.Id))
                .ToDictionaryAsync(entity => entity.Id, entity => entity.ReceiptNo, cancellationToken);

        var resolutionNumbers = shortageResolutionIds.Length == 0
            ? new Dictionary<Guid, string>()
            : await dbContext.ShortageResolutions
                .AsNoTracking()
                .Where(entity => shortageResolutionIds.Contains(entity.Id))
                .ToDictionaryAsync(entity => entity.Id, entity => entity.ResolutionNo, cancellationToken);

        var paymentNumbers = paymentIds.Length == 0
            ? new Dictionary<Guid, string>()
            : await dbContext.Payments
                .AsNoTracking()
                .Where(entity => paymentIds.Contains(entity.Id))
                .ToDictionaryAsync(entity => entity.Id, entity => entity.PaymentNo, cancellationToken);

        var allocationIds = entries
            .Where(entity => entity.SourceDocType == SupplierStatementSourceDocumentType.ShortageResolution && entity.SourceLineId.HasValue)
            .Select(entity => entity.SourceLineId!.Value)
            .Distinct()
            .ToArray();

        var allocationSequences = allocationIds.Length == 0
            ? new Dictionary<Guid, int>()
            : await dbContext.ShortageResolutionAllocations
                .AsNoTracking()
                .Where(entity => allocationIds.Contains(entity.Id))
                .ToDictionaryAsync(entity => entity.Id, entity => entity.SequenceNo, cancellationToken);

        var paymentAllocationIds = entries
            .Where(entity => entity.SourceDocType == SupplierStatementSourceDocumentType.Payment && entity.SourceLineId.HasValue)
            .Select(entity => entity.SourceLineId!.Value)
            .Distinct()
            .ToArray();

        var paymentAllocationSequences = paymentAllocationIds.Length == 0
            ? new Dictionary<Guid, int>()
            : await dbContext.PaymentAllocations
                .AsNoTracking()
                .Where(entity => paymentAllocationIds.Contains(entity.Id))
                .ToDictionaryAsync(entity => entity.Id, entity => entity.AllocationOrder, cancellationToken);

        return entries.Select(entity => new SupplierStatementEntryDto(
                entity.Id,
                entity.SupplierId,
                entity.Supplier!.Code,
                entity.Supplier.Name,
                entity.EntryDate,
                entity.SourceDocType,
                entity.SourceDocId,
                entity.SourceLineId,
                ResolveSourceSequenceNo(entity, allocationSequences, paymentAllocationSequences),
                ResolveSourceDocumentNo(entity, receiptNumbers, resolutionNumbers, paymentNumbers),
                entity.EffectType,
                entity.Debit,
                entity.Credit,
                entity.RunningBalance,
                entity.Currency,
                entity.Notes,
                entity.CreatedAt,
                entity.CreatedBy))
            .ToList();
    }

    private static string ResolveSourceDocumentNo(
        SupplierStatementEntry entity,
        IReadOnlyDictionary<Guid, string> receiptNumbers,
        IReadOnlyDictionary<Guid, string> resolutionNumbers,
        IReadOnlyDictionary<Guid, string> paymentNumbers)
    {
        return entity.SourceDocType switch
        {
            SupplierStatementSourceDocumentType.PurchaseReceipt when receiptNumbers.TryGetValue(entity.SourceDocId, out var receiptNo) => receiptNo,
            SupplierStatementSourceDocumentType.ShortageResolution when resolutionNumbers.TryGetValue(entity.SourceDocId, out var resolutionNo) => resolutionNo,
            SupplierStatementSourceDocumentType.Payment when paymentNumbers.TryGetValue(entity.SourceDocId, out var paymentNo) => paymentNo,
            _ => entity.SourceDocId.ToString()
        };
    }

    private static int? ResolveSourceSequenceNo(
        SupplierStatementEntry entity,
        IReadOnlyDictionary<Guid, int> shortageAllocationSequences,
        IReadOnlyDictionary<Guid, int> paymentAllocationSequences)
    {
        if (!entity.SourceLineId.HasValue)
        {
            return null;
        }

        return entity.SourceDocType switch
        {
            SupplierStatementSourceDocumentType.ShortageResolution when shortageAllocationSequences.TryGetValue(entity.SourceLineId.Value, out var shortageSequenceNo) => shortageSequenceNo,
            SupplierStatementSourceDocumentType.Payment when paymentAllocationSequences.TryGetValue(entity.SourceLineId.Value, out var paymentSequenceNo) => paymentSequenceNo,
            _ => null
        };
    }
}
