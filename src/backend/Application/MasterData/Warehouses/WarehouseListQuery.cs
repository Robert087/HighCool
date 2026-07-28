using ERP.Application.Common.Pagination;

namespace ERP.Application.MasterData.Warehouses;

public sealed record WarehouseListQuery(
    string? Search,
    bool? IsActive,
    int Page = 1,
    int PageSize = 20,
    string? SortBy = null,
    SortDirection SortDirection = SortDirection.Asc);
