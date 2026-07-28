using ERP.Application.Common.Pagination;

namespace ERP.Application.MasterData.Uoms;

public sealed record UomListQuery(
    string? Search,
    bool? IsActive,
    int Page = 1,
    int PageSize = 20,
    string? SortBy = null,
    SortDirection SortDirection = SortDirection.Asc);
