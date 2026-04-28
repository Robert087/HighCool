using ERP.Application.Common.Pagination;
using ERP.Domain.Payments;

namespace ERP.Application.Payments;

public sealed record SupplierOpenBalanceQuery(
    Guid SupplierId,
    PaymentDirection Direction,
    string? Search,
    DateTime? FromDate,
    DateTime? ToDate,
    int Page = 1,
    int PageSize = 20,
    string? SortBy = null,
    SortDirection SortDirection = SortDirection.Asc);
