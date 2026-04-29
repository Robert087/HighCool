using ERP.Application.Purchasing.PurchaseOrders;
using ERP.Domain.Common;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Purchasing.PurchaseOrders;

public sealed class PurchaseOrderPostingService(
    AppDbContext dbContext,
    IPurchaseOrderService purchaseOrderService) : IPurchaseOrderPostingService
{
    public async Task<PurchaseOrderDto?> PostAsync(Guid id, string actor, CancellationToken cancellationToken)
    {
        var purchaseOrder = await dbContext.PurchaseOrders
            .Include(entity => entity.Supplier)
            .Include(entity => entity.Lines)
            .SingleOrDefaultAsync(entity => entity.Id == id, cancellationToken);

        if (purchaseOrder is null)
        {
            return null;
        }

        if (purchaseOrder.Status == DocumentStatus.Posted)
        {
            return await purchaseOrderService.GetAsync(id, cancellationToken);
        }

        if (purchaseOrder.Status != DocumentStatus.Draft)
        {
            throw new InvalidOperationException("Only Draft purchase orders can be posted.");
        }

        if (purchaseOrder.Supplier is null || !purchaseOrder.Supplier.IsActive)
        {
            throw new InvalidOperationException("Supplier was not found.");
        }

        if (purchaseOrder.Lines.Count == 0)
        {
            throw new InvalidOperationException("At least one purchase order line is required before posting.");
        }

        if (purchaseOrder.Lines.Any(line => line.UnitPrice < 0m))
        {
            throw new InvalidOperationException("Purchase order line unit prices cannot be negative.");
        }

        purchaseOrder.Status = DocumentStatus.Posted;
        purchaseOrder.UpdatedBy = actor;
        await dbContext.SaveChangesAsync(cancellationToken);

        return await purchaseOrderService.GetAsync(id, cancellationToken);
    }
}
