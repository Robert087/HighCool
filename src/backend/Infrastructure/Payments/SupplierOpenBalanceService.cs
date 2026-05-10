using ERP.Application.Common.Pagination;
using ERP.Application.Payments;

namespace ERP.Infrastructure.Payments;

public sealed class SupplierOpenBalanceService(SupplierFinancialTargetStateService stateService) : ISupplierOpenBalanceService
{
    public async Task<PagedResult<SupplierOpenBalanceDto>> ListAsync(SupplierOpenBalanceQuery query, CancellationToken cancellationToken)
    {
        var pagination = new PaginationRequest(query.Page, query.PageSize);
        var states = await stateService.ListAsync(query, cancellationToken);
        var sortedStates = ApplySorting(states, query);
        var totalCount = sortedStates.Count;

        var items = sortedStates
            .Skip(pagination.Skip)
            .Take(pagination.NormalizedPageSize)
            .Select(state => new SupplierOpenBalanceDto(
                state.TargetDocType,
                state.TargetDocId,
                state.SupplierId,
                state.SupplierCode,
                state.SupplierName,
                state.TargetDocumentNo,
                state.TargetDocumentDate,
                state.OriginalAmount,
                state.AdjustedAmount,
                state.NetAmount,
                state.AllocatedAmount,
                state.OpenAmount,
                state.Status,
                state.Currency,
                state.Notes))
            .ToArray();

        return new PagedResult<SupplierOpenBalanceDto>(
            items,
            pagination.NormalizedPage,
            pagination.NormalizedPageSize,
            totalCount,
            totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pagination.NormalizedPageSize),
            new
            {
                query.SupplierId,
                query.Direction,
                query.Search,
                query.FromDate,
                query.ToDate
            },
            new PagedSort(ResolveSortBy(query.SortBy), query.SortDirection));
    }

    private static IReadOnlyList<SupplierFinancialTargetState> ApplySorting(IReadOnlyList<SupplierFinancialTargetState> states, SupplierOpenBalanceQuery query)
    {
        var sortBy = ResolveSortBy(query.SortBy);
        var ascending = query.SortDirection == SortDirection.Asc;

        return (sortBy, ascending) switch
        {
            ("targetDocumentNo", true) => states.OrderBy(entity => entity.TargetDocumentNo).ThenBy(entity => entity.TargetDocId).ToArray(),
            ("targetDocumentNo", false) => states.OrderByDescending(entity => entity.TargetDocumentNo).ThenByDescending(entity => entity.TargetDocId).ToArray(),
            ("netAmount", true) => states.OrderBy(entity => entity.NetAmount).ThenBy(entity => entity.TargetDocumentDate).ToArray(),
            ("netAmount", false) => states.OrderByDescending(entity => entity.NetAmount).ThenByDescending(entity => entity.TargetDocumentDate).ToArray(),
            ("openAmount", true) => states.OrderBy(entity => entity.OpenAmount).ThenBy(entity => entity.TargetDocumentDate).ToArray(),
            ("openAmount", false) => states.OrderByDescending(entity => entity.OpenAmount).ThenByDescending(entity => entity.TargetDocumentDate).ToArray(),
            _ when ascending => states.OrderBy(entity => entity.TargetDocumentDate).ThenBy(entity => entity.TargetDocumentNo).ToArray(),
            _ => states.OrderByDescending(entity => entity.TargetDocumentDate).ThenByDescending(entity => entity.TargetDocumentNo).ToArray()
        };
    }

    private static string ResolveSortBy(string? sortBy)
    {
        return string.IsNullOrWhiteSpace(sortBy) ? "targetDocumentDate" : sortBy.Trim();
    }
}
