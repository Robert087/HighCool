using ERP.Application.Common.Pagination;

namespace ERP.Application.MasterData.Items;

public sealed record ItemListQuery(
    string? Search,
    bool? IsActive,
    bool? IsSellable,
    Guid? CategoryId,
    Guid? BaseUomId,
    int Page = 1,
    int PageSize = 20,
    string? SortBy = null,
    SortDirection SortDirection = SortDirection.Asc);
