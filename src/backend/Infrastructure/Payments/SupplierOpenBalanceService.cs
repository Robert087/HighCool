using ERP.Application.Payments;
using ERP.Domain.Common;
using ERP.Domain.Payments;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Payments;

public sealed class SupplierOpenBalanceService(AppDbContext dbContext) : ISupplierOpenBalanceService
{
    public async Task<IReadOnlyList<SupplierOpenBalanceDto>> ListAsync(SupplierOpenBalanceQuery query, CancellationToken cancellationToken)
    {
        return query.Direction == PaymentDirection.OutboundToParty
            ? await ListPurchaseReceiptBalancesAsync(query, cancellationToken)
            : await ListShortageResolutionBalancesAsync(query, cancellationToken);
    }

    private async Task<IReadOnlyList<SupplierOpenBalanceDto>> ListPurchaseReceiptBalancesAsync(
        SupplierOpenBalanceQuery query,
        CancellationToken cancellationToken)
    {
        var receiptsQuery = dbContext.PurchaseReceipts
            .AsNoTracking()
            .Include(entity => entity.Supplier)
            .Where(entity =>
                entity.Status == DocumentStatus.Posted &&
                entity.SupplierId == query.SupplierId &&
                entity.SupplierPayableAmount > 0m)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            receiptsQuery = receiptsQuery.Where(entity =>
                entity.ReceiptNo.Contains(search) ||
                (entity.Notes != null && entity.Notes.Contains(search)));
        }

        if (query.FromDate.HasValue)
        {
            receiptsQuery = receiptsQuery.Where(entity => entity.ReceiptDate >= query.FromDate.Value);
        }

        if (query.ToDate.HasValue)
        {
            receiptsQuery = receiptsQuery.Where(entity => entity.ReceiptDate <= query.ToDate.Value);
        }

        var receipts = await receiptsQuery
            .OrderBy(entity => entity.ReceiptDate)
            .ThenBy(entity => entity.CreatedAt)
            .ToListAsync(cancellationToken);

        var receiptIds = receipts.Select(entity => entity.Id).ToArray();
        var allocatedByReceiptId = receiptIds.Length == 0
            ? new Dictionary<Guid, decimal>()
            : await dbContext.PaymentAllocations
                .AsNoTracking()
                .Where(entity =>
                    entity.TargetDocType == PaymentTargetDocumentType.PurchaseReceipt &&
                    receiptIds.Contains(entity.TargetDocId) &&
                    entity.Payment!.Status == DocumentStatus.Posted &&
                    entity.Payment.Direction == PaymentDirection.OutboundToParty)
                .GroupBy(entity => entity.TargetDocId)
                .ToDictionaryAsync(
                    group => group.Key,
                    group => Round(group.Sum(entity => entity.AllocatedAmount)),
                    cancellationToken);

        return receipts
            .Select(receipt =>
            {
                var allocatedAmount = allocatedByReceiptId.TryGetValue(receipt.Id, out var allocated) ? allocated : 0m;
                var openAmount = ClampToZero(Round(receipt.SupplierPayableAmount - allocatedAmount));

                return new SupplierOpenBalanceDto(
                    PaymentTargetDocumentType.PurchaseReceipt,
                    receipt.Id,
                    receipt.SupplierId,
                    receipt.Supplier?.Code ?? string.Empty,
                    receipt.Supplier?.Name ?? string.Empty,
                    receipt.ReceiptNo,
                    receipt.ReceiptDate,
                    receipt.SupplierPayableAmount,
                    allocatedAmount,
                    openAmount,
                    null,
                    receipt.Notes);
            })
            .Where(entity => entity.OpenAmount > 0m)
            .ToArray();
    }

    private async Task<IReadOnlyList<SupplierOpenBalanceDto>> ListShortageResolutionBalancesAsync(
        SupplierOpenBalanceQuery query,
        CancellationToken cancellationToken)
    {
        var resolutionsQuery = dbContext.ShortageResolutions
            .AsNoTracking()
            .Include(entity => entity.Supplier)
            .Where(entity =>
                entity.Status == DocumentStatus.Posted &&
                entity.SupplierId == query.SupplierId &&
                entity.ResolutionType == Domain.Shortages.ShortageResolutionType.Financial &&
                entity.TotalAmount.HasValue &&
                entity.TotalAmount.Value > 0m)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            resolutionsQuery = resolutionsQuery.Where(entity =>
                entity.ResolutionNo.Contains(search) ||
                (entity.Notes != null && entity.Notes.Contains(search)));
        }

        if (query.FromDate.HasValue)
        {
            resolutionsQuery = resolutionsQuery.Where(entity => entity.ResolutionDate >= query.FromDate.Value);
        }

        if (query.ToDate.HasValue)
        {
            resolutionsQuery = resolutionsQuery.Where(entity => entity.ResolutionDate <= query.ToDate.Value);
        }

        var resolutions = await resolutionsQuery
            .OrderBy(entity => entity.ResolutionDate)
            .ThenBy(entity => entity.CreatedAt)
            .ToListAsync(cancellationToken);

        var resolutionIds = resolutions.Select(entity => entity.Id).ToArray();
        var allocatedByResolutionId = resolutionIds.Length == 0
            ? new Dictionary<Guid, decimal>()
            : await dbContext.PaymentAllocations
                .AsNoTracking()
                .Where(entity =>
                    entity.TargetDocType == PaymentTargetDocumentType.ShortageResolution &&
                    resolutionIds.Contains(entity.TargetDocId) &&
                    entity.Payment!.Status == DocumentStatus.Posted &&
                    entity.Payment.Direction == PaymentDirection.InboundFromParty)
                .GroupBy(entity => entity.TargetDocId)
                .ToDictionaryAsync(
                    group => group.Key,
                    group => Round(group.Sum(entity => entity.AllocatedAmount)),
                    cancellationToken);

        return resolutions
            .Select(resolution =>
            {
                var originalAmount = Round(resolution.TotalAmount ?? 0m);
                var allocatedAmount = allocatedByResolutionId.TryGetValue(resolution.Id, out var allocated) ? allocated : 0m;
                var openAmount = ClampToZero(Round(originalAmount - allocatedAmount));

                return new SupplierOpenBalanceDto(
                    PaymentTargetDocumentType.ShortageResolution,
                    resolution.Id,
                    resolution.SupplierId,
                    resolution.Supplier?.Code ?? string.Empty,
                    resolution.Supplier?.Name ?? string.Empty,
                    resolution.ResolutionNo,
                    resolution.ResolutionDate,
                    originalAmount,
                    allocatedAmount,
                    openAmount,
                    resolution.Currency,
                    resolution.Notes);
            })
            .Where(entity => entity.OpenAmount > 0m)
            .ToArray();
    }

    private static decimal Round(decimal value)
    {
        return decimal.Round(value, 6, MidpointRounding.AwayFromZero);
    }

    private static decimal ClampToZero(decimal value)
    {
        return value < 0m ? 0m : value;
    }
}
