using ERP.Application.Common.Pagination;
using ERP.Domain.Common;
using ERP.Domain.Purchasing;

namespace ERP.Application.Purchasing.PurchaseOrders;

public sealed record PurchaseOrderListQuery(
    string? Search,
    DocumentStatus? Status,
    PurchaseOrderReceiptProgressStatus? ReceiptProgressStatus,
    DateTime? FromDate,
    DateTime? ToDate,
    int Page = 1,
    int PageSize = 20,
    string? SortBy = null,
    SortDirection SortDirection = SortDirection.Desc);
