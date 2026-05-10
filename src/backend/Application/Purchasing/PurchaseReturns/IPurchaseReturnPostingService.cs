namespace ERP.Application.Purchasing.PurchaseReturns;

public interface IPurchaseReturnPostingService
{
    Task<PurchaseReturnDto?> PostAsync(Guid id, string actor, CancellationToken cancellationToken);
}
