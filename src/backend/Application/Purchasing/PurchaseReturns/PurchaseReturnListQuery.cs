using ERP.Application.Common.Pagination;
using ERP.Domain.Common;

namespace ERP.Application.Purchasing.PurchaseReturns;

public sealed record PurchaseReturnListQuery(
    string? Search,
    DocumentStatus? Status,
    DateTime? FromDate,
    DateTime? ToDate,
    int Page = 1,
    int PageSize = 20,
    string? SortBy = null,
    SortDirection SortDirection = SortDirection.Desc);
