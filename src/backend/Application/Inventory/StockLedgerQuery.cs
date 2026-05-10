using ERP.Application.Common.Pagination;
using ERP.Domain.Inventory;

namespace ERP.Application.Inventory;

public sealed record StockLedgerQuery(
    string? Search,
    Guid? ItemId,
    Guid? WarehouseId,
    StockTransactionType? TransactionType,
    DateTime? FromDate,
    DateTime? ToDate,
    int Page = 1,
    int PageSize = 20,
    string? SortBy = null,
    SortDirection SortDirection = SortDirection.Desc);
