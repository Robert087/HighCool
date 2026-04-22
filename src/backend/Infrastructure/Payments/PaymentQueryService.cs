using ERP.Application.Payments;
using ERP.Domain.Common;
using ERP.Domain.Payments;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Payments;

public sealed class PaymentQueryService(AppDbContext dbContext) : IPaymentQueryService
{
    public async Task<IReadOnlyList<PaymentListItemDto>> ListAsync(PaymentListQuery query, CancellationToken cancellationToken)
    {
        var paymentsQuery = dbContext.Payments
            .AsNoTracking()
            .Include(entity => entity.Supplier)
            .Include(entity => entity.Allocations)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            paymentsQuery = paymentsQuery.Where(entity =>
                entity.PaymentNo.Contains(search) ||
                entity.Supplier!.Code.Contains(search) ||
                entity.Supplier.Name.Contains(search) ||
                (entity.ReferenceNote != null && entity.ReferenceNote.Contains(search)) ||
                (entity.Notes != null && entity.Notes.Contains(search)));
        }

        if (query.SupplierId.HasValue)
        {
            paymentsQuery = paymentsQuery.Where(entity => entity.PartyId == query.SupplierId.Value);
        }

        if (query.Direction.HasValue)
        {
            paymentsQuery = paymentsQuery.Where(entity => entity.Direction == query.Direction.Value);
        }

        if (query.Status.HasValue)
        {
            paymentsQuery = paymentsQuery.Where(entity => entity.Status == query.Status.Value);
        }

        if (query.PaymentMethod.HasValue)
        {
            paymentsQuery = paymentsQuery.Where(entity => entity.PaymentMethod == query.PaymentMethod.Value);
        }

        if (query.FromDate.HasValue)
        {
            paymentsQuery = paymentsQuery.Where(entity => entity.PaymentDate >= query.FromDate.Value);
        }

        if (query.ToDate.HasValue)
        {
            paymentsQuery = paymentsQuery.Where(entity => entity.PaymentDate <= query.ToDate.Value);
        }

        var payments = await paymentsQuery
            .OrderByDescending(entity => entity.PaymentDate)
            .ThenByDescending(entity => entity.CreatedAt)
            .ToListAsync(cancellationToken);

        return payments
            .Select(entity =>
            {
                var allocatedAmount = Round(entity.Allocations.Sum(allocation => allocation.AllocatedAmount));

                return new PaymentListItemDto(
                    entity.Id,
                    entity.PaymentNo,
                    entity.PartyType,
                    entity.PartyId,
                    entity.Supplier?.Code ?? string.Empty,
                    entity.Supplier?.Name ?? string.Empty,
                    entity.Direction,
                    entity.Amount,
                    allocatedAmount,
                    Round(entity.Amount - allocatedAmount),
                    entity.PaymentDate,
                    entity.Currency,
                    entity.PaymentMethod,
                    entity.ReferenceNote,
                    entity.Status,
                    entity.Allocations.Count,
                    entity.CreatedAt,
                    entity.UpdatedAt);
            })
            .ToArray();
    }

    public async Task<PaymentDto?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var payment = await dbContext.Payments
            .AsNoTracking()
            .AsSplitQuery()
            .Include(entity => entity.Supplier)
            .Include(entity => entity.Allocations.OrderBy(allocation => allocation.AllocationOrder))
            .SingleOrDefaultAsync(entity => entity.Id == id, cancellationToken);

        if (payment is null)
        {
            return null;
        }

        var allocations = await BuildAllocationDtosAsync(payment, cancellationToken);
        var allocatedAmount = Round(allocations.Sum(entity => entity.AllocatedAmount));

        return new PaymentDto(
            payment.Id,
            payment.PaymentNo,
            payment.PartyType,
            payment.PartyId,
            payment.Supplier?.Code ?? string.Empty,
            payment.Supplier?.Name ?? string.Empty,
            payment.Direction,
            payment.Amount,
            allocatedAmount,
            Round(payment.Amount - allocatedAmount),
            payment.PaymentDate,
            payment.Currency,
            payment.ExchangeRate,
            payment.PaymentMethod,
            payment.ReferenceNote,
            payment.Notes,
            payment.Status,
            allocations,
            payment.CreatedAt,
            payment.UpdatedAt);
    }

    public async Task<IReadOnlyList<PaymentAllocationDto>> GetAllocationsAsync(Guid id, CancellationToken cancellationToken)
    {
        var payment = await dbContext.Payments
            .AsNoTracking()
            .Include(entity => entity.Allocations.OrderBy(allocation => allocation.AllocationOrder))
            .SingleOrDefaultAsync(entity => entity.Id == id, cancellationToken);

        return payment is null
            ? []
            : await BuildAllocationDtosAsync(payment, cancellationToken);
    }

    private async Task<IReadOnlyList<PaymentAllocationDto>> BuildAllocationDtosAsync(Payment payment, CancellationToken cancellationToken)
    {
        var allocations = payment.Allocations
            .OrderBy(entity => entity.AllocationOrder)
            .ToArray();

        if (allocations.Length == 0)
        {
            return [];
        }

        var targetSnapshots = await LoadTargetSnapshotsAsync(allocations, cancellationToken);
        var postedAllocatedTotals = await LoadPostedAllocationTotalsAsync(allocations, cancellationToken);

        return allocations.Select(allocation =>
        {
            var targetKey = BuildTargetKey(allocation.TargetDocType, allocation.TargetDocId, allocation.TargetLineId);
            if (!targetSnapshots.TryGetValue(targetKey, out var snapshot))
            {
                snapshot = new TargetSnapshot(
                    allocation.TargetDocType,
                    allocation.TargetDocId,
                    allocation.TargetLineId,
                    allocation.TargetDocId.ToString(),
                    payment.PaymentDate,
                    0m,
                    null);
            }

            var totalAllocated = postedAllocatedTotals.TryGetValue(targetKey, out var allocatedTotal) ? allocatedTotal : 0m;
            if (payment.Status != DocumentStatus.Posted)
            {
                totalAllocated = Round(totalAllocated + allocation.AllocatedAmount);
            }

            var alreadyAllocated = Round(Math.Max(totalAllocated - allocation.AllocatedAmount, 0m));
            var openAmount = ClampToZero(Round(snapshot.OriginalAmount - totalAllocated));

            return new PaymentAllocationDto(
                allocation.Id,
                allocation.TargetDocType,
                allocation.TargetDocId,
                allocation.TargetLineId,
                snapshot.DocumentNo,
                snapshot.DocumentDate,
                snapshot.OriginalAmount,
                alreadyAllocated,
                openAmount,
                allocation.AllocatedAmount,
                allocation.AllocationOrder,
                allocation.CreatedAt,
                allocation.CreatedBy);
        }).ToArray();
    }

    private async Task<Dictionary<string, TargetSnapshot>> LoadTargetSnapshotsAsync(
        IReadOnlyCollection<PaymentAllocation> allocations,
        CancellationToken cancellationToken)
    {
        var receiptIds = allocations
            .Where(entity => entity.TargetDocType == PaymentTargetDocumentType.PurchaseReceipt)
            .Select(entity => entity.TargetDocId)
            .Distinct()
            .ToArray();

        var resolutionIds = allocations
            .Where(entity => entity.TargetDocType == PaymentTargetDocumentType.ShortageResolution)
            .Select(entity => entity.TargetDocId)
            .Distinct()
            .ToArray();

        var snapshots = new Dictionary<string, TargetSnapshot>();

        if (receiptIds.Length > 0)
        {
            var receipts = await dbContext.PurchaseReceipts
                .AsNoTracking()
                .Where(entity => receiptIds.Contains(entity.Id))
                .Select(entity => new
                {
                    entity.Id,
                    entity.ReceiptNo,
                    entity.ReceiptDate,
                    entity.SupplierPayableAmount,
                    entity.Notes
                })
                .ToListAsync(cancellationToken);

            foreach (var receipt in receipts)
            {
                snapshots[BuildTargetKey(PaymentTargetDocumentType.PurchaseReceipt, receipt.Id, null)] =
                    new TargetSnapshot(
                        PaymentTargetDocumentType.PurchaseReceipt,
                        receipt.Id,
                        null,
                        receipt.ReceiptNo,
                        receipt.ReceiptDate,
                        receipt.SupplierPayableAmount,
                        receipt.Notes);
            }
        }

        if (resolutionIds.Length > 0)
        {
            var resolutions = await dbContext.ShortageResolutions
                .AsNoTracking()
                .Where(entity => resolutionIds.Contains(entity.Id))
                .Select(entity => new
                {
                    entity.Id,
                    entity.ResolutionNo,
                    entity.ResolutionDate,
                    entity.TotalAmount,
                    entity.Notes
                })
                .ToListAsync(cancellationToken);

            foreach (var resolution in resolutions)
            {
                snapshots[BuildTargetKey(PaymentTargetDocumentType.ShortageResolution, resolution.Id, null)] =
                    new TargetSnapshot(
                        PaymentTargetDocumentType.ShortageResolution,
                        resolution.Id,
                        null,
                        resolution.ResolutionNo,
                        resolution.ResolutionDate,
                        Round(resolution.TotalAmount ?? 0m),
                        resolution.Notes);
            }
        }

        return snapshots;
    }

    private async Task<Dictionary<string, decimal>> LoadPostedAllocationTotalsAsync(
        IReadOnlyCollection<PaymentAllocation> allocations,
        CancellationToken cancellationToken)
    {
        var receiptIds = allocations
            .Where(entity => entity.TargetDocType == PaymentTargetDocumentType.PurchaseReceipt)
            .Select(entity => entity.TargetDocId)
            .Distinct()
            .ToArray();

        var resolutionIds = allocations
            .Where(entity => entity.TargetDocType == PaymentTargetDocumentType.ShortageResolution)
            .Select(entity => entity.TargetDocId)
            .Distinct()
            .ToArray();

        var postedAllocations = await dbContext.PaymentAllocations
            .AsNoTracking()
            .Where(entity =>
                entity.Payment!.Status == DocumentStatus.Posted &&
                ((entity.TargetDocType == PaymentTargetDocumentType.PurchaseReceipt && receiptIds.Contains(entity.TargetDocId)) ||
                 (entity.TargetDocType == PaymentTargetDocumentType.ShortageResolution && resolutionIds.Contains(entity.TargetDocId))))
            .Select(entity => new
            {
                entity.TargetDocType,
                entity.TargetDocId,
                entity.TargetLineId,
                entity.AllocatedAmount
            })
            .ToListAsync(cancellationToken);

        return postedAllocations
            .GroupBy(entity => BuildTargetKey(entity.TargetDocType, entity.TargetDocId, entity.TargetLineId))
            .ToDictionary(group => group.Key, group => Round(group.Sum(entry => entry.AllocatedAmount)));
    }

    private static string BuildTargetKey(PaymentTargetDocumentType targetDocType, Guid targetDocId, Guid? targetLineId)
    {
        return $"{targetDocType}:{targetDocId}:{targetLineId?.ToString() ?? "header"}";
    }

    private static decimal Round(decimal value)
    {
        return decimal.Round(value, 6, MidpointRounding.AwayFromZero);
    }

    private static decimal ClampToZero(decimal value)
    {
        return value < 0m ? 0m : value;
    }

    private sealed record TargetSnapshot(
        PaymentTargetDocumentType TargetDocType,
        Guid TargetDocId,
        Guid? TargetLineId,
        string DocumentNo,
        DateTime DocumentDate,
        decimal OriginalAmount,
        string? Notes);
}
