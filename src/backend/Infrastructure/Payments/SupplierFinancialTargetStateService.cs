using ERP.Application.Payments;
using ERP.Domain.Common;
using ERP.Domain.Payments;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Payments;

public sealed class SupplierFinancialTargetStateService(AppDbContext dbContext)
{
    public async Task<IReadOnlyList<SupplierFinancialTargetState>> ListAsync(
        SupplierOpenBalanceQuery query,
        CancellationToken cancellationToken)
    {
        var states = query.Direction == PaymentDirection.OutboundToParty
            ? await ListReceiptStatesAsync(query, cancellationToken)
            : await ListShortageResolutionStatesAsync(query, cancellationToken);

        return states
            .Where(state => state.OpenAmount > 0m)
            .ToArray();
    }

    public async Task<IReadOnlyDictionary<string, SupplierFinancialTargetState>> GetByTargetAsync(
        IReadOnlyCollection<(PaymentTargetDocumentType TargetDocType, Guid TargetDocId)> targets,
        CancellationToken cancellationToken)
    {
        if (targets.Count == 0)
        {
            return new Dictionary<string, SupplierFinancialTargetState>();
        }

        var receiptIds = targets
            .Where(target => target.TargetDocType == PaymentTargetDocumentType.PurchaseReceipt)
            .Select(target => target.TargetDocId)
            .Distinct()
            .ToArray();

        var resolutionIds = targets
            .Where(target => target.TargetDocType == PaymentTargetDocumentType.ShortageResolution)
            .Select(target => target.TargetDocId)
            .Distinct()
            .ToArray();

        var states = new Dictionary<string, SupplierFinancialTargetState>();

        if (receiptIds.Length > 0)
        {
            var receipts = await dbContext.PurchaseReceipts
                .AsNoTracking()
                .Include(entity => entity.Supplier)
                .Where(entity => receiptIds.Contains(entity.Id))
                .ToListAsync(cancellationToken);

            var receiptStates = await BuildReceiptStatesAsync(
                receipts,
                cancellationToken);

            foreach (var state in receiptStates)
            {
                states[BuildTargetKey(state.TargetDocType, state.TargetDocId)] = state;
            }
        }

        if (resolutionIds.Length > 0)
        {
            var resolutions = await dbContext.ShortageResolutions
                .AsNoTracking()
                .Include(entity => entity.Supplier)
                .Where(entity => resolutionIds.Contains(entity.Id))
                .ToListAsync(cancellationToken);

            var resolutionStates = await BuildShortageResolutionStatesAsync(
                resolutions,
                cancellationToken);

            foreach (var state in resolutionStates)
            {
                states[BuildTargetKey(state.TargetDocType, state.TargetDocId)] = state;
            }
        }

        return states;
    }

    private async Task<IReadOnlyList<SupplierFinancialTargetState>> ListReceiptStatesAsync(
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

        return await BuildReceiptStatesAsync(receipts, cancellationToken);
    }

    private async Task<IReadOnlyList<SupplierFinancialTargetState>> BuildReceiptStatesAsync(
        IReadOnlyCollection<Domain.Purchasing.PurchaseReceipt> receipts,
        CancellationToken cancellationToken)
    {
        if (receipts.Count == 0)
        {
            return [];
        }

        var receiptIds = receipts.Select(entity => entity.Id).ToArray();
        var returnedByReceiptId = await dbContext.SupplierStatementEntries
            .AsNoTracking()
            .Where(entity =>
                entity.SourceDocType == Domain.Statements.SupplierStatementSourceDocumentType.PurchaseReturn &&
                entity.EffectType == Domain.Statements.SupplierStatementEffectType.PurchaseReturn &&
                entity.SourceLineId.HasValue &&
                (entity.Debit > 0m || entity.Credit > 0m) &&
                receiptIds.Contains(entity.SourceLineId.Value))
            .Select(entity => new
            {
                ReceiptId = entity.SourceLineId!.Value,
                Amount = entity.Debit - entity.Credit
            })
            .ToListAsync(cancellationToken);

        var returnedAmounts = returnedByReceiptId
            .GroupBy(entity => entity.ReceiptId)
            .ToDictionary(group => group.Key, group => Round(group.Sum(entity => entity.Amount)));

        var allocatedByReceiptId = await dbContext.PaymentAllocations
            .AsNoTracking()
            .Where(entity =>
                entity.TargetDocType == PaymentTargetDocumentType.PurchaseReceipt &&
                receiptIds.Contains(entity.TargetDocId) &&
                entity.Payment!.Status == DocumentStatus.Posted &&
                entity.Payment.ReversalDocumentId == null &&
                entity.Payment.Direction == PaymentDirection.OutboundToParty)
            .GroupBy(entity => entity.TargetDocId)
            .ToDictionaryAsync(
                group => group.Key,
                group => Round(group.Sum(entity => entity.AllocatedAmount)),
                cancellationToken);

        return receipts
            .Select(receipt =>
            {
                var adjustedAmount = returnedAmounts.TryGetValue(receipt.Id, out var returned) ? returned : 0m;
                var netAmount = ClampToZero(Round(receipt.SupplierPayableAmount - adjustedAmount));
                var allocatedAmount = allocatedByReceiptId.TryGetValue(receipt.Id, out var allocated) ? allocated : 0m;
                var openAmount = receipt.ReversalDocumentId.HasValue
                    ? 0m
                    : ClampToZero(Round(netAmount - allocatedAmount));

                return new SupplierFinancialTargetState(
                    PaymentTargetDocumentType.PurchaseReceipt,
                    receipt.Id,
                    receipt.SupplierId,
                    receipt.Supplier?.Code ?? string.Empty,
                    receipt.Supplier?.Name ?? string.Empty,
                    receipt.ReceiptNo,
                    receipt.ReceiptDate,
                    receipt.SupplierPayableAmount,
                    adjustedAmount,
                    netAmount,
                    allocatedAmount,
                    openAmount,
                    receipt.ReversalDocumentId.HasValue
                        ? "Reversed"
                        : ResolveStatus(openAmount, allocatedAmount, adjustedAmount),
                    null,
                    receipt.Notes);
            })
            .ToArray();
    }

    private async Task<IReadOnlyList<SupplierFinancialTargetState>> ListShortageResolutionStatesAsync(
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

        return await BuildShortageResolutionStatesAsync(resolutions, cancellationToken);
    }

    private async Task<IReadOnlyList<SupplierFinancialTargetState>> BuildShortageResolutionStatesAsync(
        IReadOnlyCollection<Domain.Shortages.ShortageResolution> resolutions,
        CancellationToken cancellationToken)
    {
        if (resolutions.Count == 0)
        {
            return [];
        }

        var resolutionIds = resolutions.Select(entity => entity.Id).ToArray();
        var allocatedByResolutionId = await dbContext.PaymentAllocations
            .AsNoTracking()
            .Where(entity =>
                entity.TargetDocType == PaymentTargetDocumentType.ShortageResolution &&
                resolutionIds.Contains(entity.TargetDocId) &&
                entity.Payment!.Status == DocumentStatus.Posted &&
                entity.Payment.ReversalDocumentId == null &&
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
                var openAmount = resolution.ReversalDocumentId.HasValue
                    ? 0m
                    : ClampToZero(Round(originalAmount - allocatedAmount));

                return new SupplierFinancialTargetState(
                    PaymentTargetDocumentType.ShortageResolution,
                    resolution.Id,
                    resolution.SupplierId,
                    resolution.Supplier?.Code ?? string.Empty,
                    resolution.Supplier?.Name ?? string.Empty,
                    resolution.ResolutionNo,
                    resolution.ResolutionDate,
                    originalAmount,
                    0m,
                    originalAmount,
                    allocatedAmount,
                    openAmount,
                    resolution.ReversalDocumentId.HasValue
                        ? "Reversed"
                        : ResolveStatus(openAmount, allocatedAmount, 0m),
                    resolution.Currency,
                    resolution.Notes);
            })
            .ToArray();
    }

    public static string BuildTargetKey(PaymentTargetDocumentType targetDocType, Guid targetDocId)
    {
        return $"{targetDocType}:{targetDocId}";
    }

    private static string ResolveStatus(decimal openAmount, decimal allocatedAmount, decimal adjustedAmount)
    {
        if (openAmount <= 0m)
        {
            return "Settled";
        }

        return allocatedAmount > 0m || adjustedAmount > 0m
            ? "PartiallySettled"
            : "Open";
    }

    private static decimal Round(decimal value)
    {
        return decimal.Round(value, 6, MidpointRounding.AwayFromZero);
    }

    private static decimal ClampToZero(decimal value)
    {
        return Math.Abs(value) < 0.000001m ? 0m : Math.Max(value, 0m);
    }
}

public sealed record SupplierFinancialTargetState(
    PaymentTargetDocumentType TargetDocType,
    Guid TargetDocId,
    Guid SupplierId,
    string SupplierCode,
    string SupplierName,
    string TargetDocumentNo,
    DateTime TargetDocumentDate,
    decimal OriginalAmount,
    decimal AdjustedAmount,
    decimal NetAmount,
    decimal AllocatedAmount,
    decimal OpenAmount,
    string Status,
    string? Currency,
    string? Notes);
