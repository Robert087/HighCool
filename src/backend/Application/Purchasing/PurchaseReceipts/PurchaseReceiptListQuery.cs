using ERP.Application.Common.Pagination;
using ERP.Domain.Common;

namespace ERP.Application.Purchasing.PurchaseReceipts;

public sealed record PurchaseReceiptListQuery(
    string? Search,
    DocumentStatus? Status,
    bool? LinkedToPurchaseOrder,
    DateTime? FromDate,
    DateTime? ToDate,
    int Page = 1,
    int PageSize = 20,
    string? SortBy = null,
    SortDirection SortDirection = SortDirection.Desc);
