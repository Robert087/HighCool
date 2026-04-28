using ERP.Application.Reversals;
using ERP.Application.Statements;
using ERP.Domain.Common;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Reversals;

public sealed class PaymentReversalService(
    AppDbContext dbContext,
    ISupplierStatementPostingService statementPostingService) : IPaymentReversalService
{
    public async Task<DocumentReversalDto?> ReverseAsync(Guid paymentId, ReverseDocumentRequest request, string actor, CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var payment = await dbContext.Payments
            .AsSplitQuery()
            .Include(entity => entity.Supplier)
            .Include(entity => entity.Allocations.OrderBy(allocation => allocation.AllocationOrder))
            .SingleOrDefaultAsync(entity => entity.Id == paymentId, cancellationToken);

        if (payment is null)
        {
            return null;
        }

        EnsurePostedAndNotReversed(payment.Status, payment.ReversalDocumentId, "payment");

        var reversal = await DocumentReversalSupport.CreateAsync(
            dbContext,
            BusinessDocumentType.Payment,
            payment.Id,
            request,
            actor,
            cancellationToken);

        await statementPostingService.CreatePaymentReversalEntriesAsync(payment, reversal, actor, cancellationToken);

        payment.ReversalDocumentId = reversal.Id;
        payment.ReversedAt = reversal.ReversalDate;
        payment.UpdatedBy = actor;

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return DocumentReversalSupport.ToDto(reversal);
    }

    private static void EnsurePostedAndNotReversed(DocumentStatus status, Guid? reversalDocumentId, string label)
    {
        if (status != DocumentStatus.Posted)
        {
            throw new InvalidOperationException($"Only Posted {label}s can be reversed.");
        }

        if (reversalDocumentId.HasValue)
        {
            throw new InvalidOperationException($"This {label} has already been reversed.");
        }
    }
}
