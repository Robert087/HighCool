using ERP.Application.Payments;
using ERP.Application.Statements;
using ERP.Domain.Common;
using ERP.Domain.Statements;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Payments;

public sealed class SupplierPaymentPostingService(
    AppDbContext dbContext,
    IPaymentQueryService queryService,
    IPaymentAllocationService allocationService,
    ISupplierStatementPostingService supplierStatementPostingService) : ISupplierPaymentPostingService
{
    public async Task<PaymentDto?> PostAsync(Guid id, string actor, CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var payment = await dbContext.Payments
            .Include(entity => entity.Supplier)
            .Include(entity => entity.Allocations.OrderBy(allocation => allocation.AllocationOrder))
            .SingleOrDefaultAsync(entity => entity.Id == id, cancellationToken);

        if (payment is null)
        {
            return null;
        }

        if (payment.Status == DocumentStatus.Posted)
        {
            await transaction.CommitAsync(cancellationToken);
            return await queryService.GetAsync(id, cancellationToken);
        }

        if (payment.Status != DocumentStatus.Draft)
        {
            throw new InvalidOperationException("Only Draft payments can be posted.");
        }

        var statementEffectsExist = await dbContext.SupplierStatementEntries
            .AnyAsync(
                entity => entity.SourceDocType == SupplierStatementSourceDocumentType.Payment &&
                          entity.SourceDocId == payment.Id,
                cancellationToken);

        if (statementEffectsExist)
        {
            throw new InvalidOperationException("Supplier statement effects already exist for this payment.");
        }

        await allocationService.ValidateForPostingAsync(payment, cancellationToken);
        await supplierStatementPostingService.CreatePaymentEntriesAsync(payment, actor, cancellationToken);

        payment.Status = DocumentStatus.Posted;
        payment.UpdatedBy = actor;

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return await queryService.GetAsync(payment.Id, cancellationToken);
    }
}
