using ERP.Application.Common.Pagination;
using ERP.Application.Payments;
using ERP.Domain.Common;
using ERP.Domain.Payments;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Payments;

public sealed class PaymentQueryService(AppDbContext dbContext) : IPaymentQueryService
{
    public async Task<PagedResult<PaymentListItemDto>> ListAsync(PaymentListQuery query, CancellationToken cancellationToken)
    {
        var pagination = new PaginationRequest(query.Page, query.PageSize);

        var paymentsQuery = dbContext.Payments
            .AsNoTracking()
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

        paymentsQuery = ApplySorting(paymentsQuery, query);

        var totalCount = await paymentsQuery.CountAsync(cancellationToken);
        var pageRows = await paymentsQuery
            .Skip(pagination.Skip)
            .Take(pagination.NormalizedPageSize)
            .Select(entity => new PaymentListItemDto(
                entity.Id,
                entity.PaymentNo,
                entity.PartyType,
                entity.PartyId,
                entity.Supplier != null ? entity.Supplier.Code : string.Empty,
                entity.Supplier != null ? entity.Supplier.Name : string.Empty,
                entity.Direction,
                entity.Amount,
                0m,
                0m,
                entity.PaymentDate,
                entity.Currency,
                entity.PaymentMethod,
                entity.ReferenceNote,
                entity.Status,
                entity.Allocations.Count(),
                entity.ReversalDocumentId,
                entity.ReversedAt,
                entity.CreatedAt,
                entity.UpdatedAt))
            .ToListAsync(cancellationToken);

        var paymentIds = pageRows.Select(entity => entity.Id).ToArray();
        var allocatedAmountsByPaymentId = paymentIds.Length == 0
            ? new Dictionary<Guid, decimal>()
            : (await dbContext.PaymentAllocations
                .AsNoTracking()
                .Where(entity => paymentIds.Contains(entity.PaymentId))
                .Select(entity => new { entity.PaymentId, entity.AllocatedAmount })
                .ToListAsync(cancellationToken))
                .GroupBy(entity => entity.PaymentId)
                .ToDictionary(group => group.Key, group => group.Sum(entity => entity.AllocatedAmount));

        var items = pageRows
            .Select(entity =>
            {
                var allocatedAmount = allocatedAmountsByPaymentId.GetValueOrDefault(entity.Id, 0m);
                return entity with
                {
                    AllocatedAmount = allocatedAmount,
                    UnallocatedAmount = entity.Amount - allocatedAmount
                };
            })
            .ToList();

        return new PagedResult<PaymentListItemDto>(
            items,
            pagination.NormalizedPage,
            pagination.NormalizedPageSize,
            totalCount,
            CalculateTotalPages(totalCount, pagination.NormalizedPageSize),
            new
            {
                query.Search,
                query.SupplierId,
                query.Direction,
                query.Status,
                query.PaymentMethod,
                query.FromDate,
                query.ToDate
            },
            new PagedSort(ResolveSortBy(query.SortBy), query.SortDirection));
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
            payment.ReversalDocumentId,
            payment.ReversedAt,
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

        var targetStates = await new SupplierFinancialTargetStateService(dbContext)
            .GetByTargetAsync(
                allocations
                    .Select(allocation => (allocation.TargetDocType, allocation.TargetDocId))
                    .Distinct()
                    .ToArray(),
                cancellationToken);

        return allocations.Select(allocation =>
        {
            var targetKey = SupplierFinancialTargetStateService.BuildTargetKey(allocation.TargetDocType, allocation.TargetDocId);
            if (!targetStates.TryGetValue(targetKey, out var state))
            {
                state = new SupplierFinancialTargetState(
                    allocation.TargetDocType,
                    allocation.TargetDocId,
                    payment.PartyId,
                    string.Empty,
                    string.Empty,
                    allocation.TargetDocId.ToString(),
                    payment.PaymentDate,
                    0m,
                    0m,
                    0m,
                    0m,
                    0m,
                    "Closed",
                    null,
                    null);
            }

            var alreadyAllocated = state.AllocatedAmount;
            var openAmount = state.OpenAmount;

            if (payment.Status != DocumentStatus.Posted)
            {
                openAmount = ClampToZero(Round(state.OpenAmount - allocation.AllocatedAmount));
            }

            return new PaymentAllocationDto(
                allocation.Id,
                allocation.TargetDocType,
                allocation.TargetDocId,
                allocation.TargetLineId,
                state.TargetDocumentNo,
                state.TargetDocumentDate,
                state.OriginalAmount,
                state.AdjustedAmount,
                state.NetAmount,
                alreadyAllocated,
                openAmount,
                state.Status,
                allocation.AllocatedAmount,
                allocation.AllocationOrder,
                allocation.CreatedAt,
                allocation.CreatedBy);
        }).ToArray();
    }

    private static decimal Round(decimal value)
    {
        return decimal.Round(value, 6, MidpointRounding.AwayFromZero);
    }

    private static decimal ClampToZero(decimal value)
    {
        return value < 0m ? 0m : value;
    }

    private static IQueryable<Payment> ApplySorting(IQueryable<Payment> query, PaymentListQuery request)
    {
        var sortBy = ResolveSortBy(request.SortBy);
        var ascending = request.SortDirection == SortDirection.Asc;

        return (sortBy, ascending) switch
        {
            ("paymentNo", true) => query.OrderBy(entity => entity.PaymentNo).ThenBy(entity => entity.Id),
            ("paymentNo", false) => query.OrderByDescending(entity => entity.PaymentNo).ThenByDescending(entity => entity.Id),
            ("partyName", true) => query.OrderBy(entity => entity.Supplier!.Name).ThenBy(entity => entity.PaymentDate),
            ("partyName", false) => query.OrderByDescending(entity => entity.Supplier!.Name).ThenByDescending(entity => entity.PaymentDate),
            ("amount", true) => query.OrderBy(entity => entity.Amount).ThenBy(entity => entity.PaymentDate),
            ("amount", false) => query.OrderByDescending(entity => entity.Amount).ThenByDescending(entity => entity.PaymentDate),
            ("status", true) => query.OrderBy(entity => entity.Status).ThenBy(entity => entity.PaymentDate),
            ("status", false) => query.OrderByDescending(entity => entity.Status).ThenByDescending(entity => entity.PaymentDate),
            _ when ascending => query.OrderBy(entity => entity.PaymentDate).ThenBy(entity => entity.CreatedAt),
            _ => query.OrderByDescending(entity => entity.PaymentDate).ThenByDescending(entity => entity.CreatedAt)
        };
    }

    private static string ResolveSortBy(string? sortBy)
    {
        return string.IsNullOrWhiteSpace(sortBy) ? "paymentDate" : sortBy.Trim();
    }

    private static int CalculateTotalPages(int totalCount, int pageSize)
    {
        return totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize);
    }
}
