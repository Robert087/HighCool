using ERP.Application.Purchasing.PurchaseOrders;
using ERP.Domain.Common;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Purchasing.PurchaseOrders;

public sealed class PurchaseOrderCancellationService(
    AppDbContext dbContext,
    IPurchaseOrderService purchaseOrderService) : IPurchaseOrderCancellationService
{
    public async Task<PurchaseOrderDto?> CancelAsync(Guid id, string actor, CancellationToken cancellationToken)
    {
        var purchaseOrder = await dbContext.PurchaseOrders
            .SingleOrDefaultAsync(entity => entity.Id == id, cancellationToken);

        if (purchaseOrder is null)
        {
            return null;
        }

        if (purchaseOrder.Status == DocumentStatus.Canceled)
        {
            return await purchaseOrderService.GetAsync(id, cancellationToken);
        }

        if (purchaseOrder.Status != DocumentStatus.Posted)
        {
            throw new InvalidOperationException("Only Posted purchase orders can be canceled.");
        }

        var hasPostedReceipts = await dbContext.PurchaseReceipts
            .AnyAsync(entity =>
                entity.PurchaseOrderId == id &&
                entity.Status == DocumentStatus.Posted &&
                entity.ReversalDocumentId == null,
                cancellationToken);

        if (hasPostedReceipts)
        {
            throw new InvalidOperationException("Purchase orders with posted receipts cannot be canceled.");
        }

        purchaseOrder.Status = DocumentStatus.Canceled;
        purchaseOrder.UpdatedBy = actor;
        await dbContext.SaveChangesAsync(cancellationToken);

        return await purchaseOrderService.GetAsync(id, cancellationToken);
    }
}
