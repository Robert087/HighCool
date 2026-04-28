using ERP.Application.Common.Pagination;
using ERP.Domain.Common;
using ERP.Domain.Shortages;

namespace ERP.Application.Shortages;

public sealed record ShortageResolutionListQuery(
    string? Search,
    Guid? SupplierId,
    ShortageResolutionType? ResolutionType,
    DocumentStatus? Status,
    DateTime? FromDate,
    DateTime? ToDate,
    int Page = 1,
    int PageSize = 20,
    string? SortBy = null,
    SortDirection SortDirection = SortDirection.Desc);
