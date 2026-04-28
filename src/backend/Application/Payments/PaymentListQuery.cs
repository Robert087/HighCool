using ERP.Application.Common.Pagination;
using ERP.Domain.Common;
using ERP.Domain.Payments;

namespace ERP.Application.Payments;

public sealed record PaymentListQuery(
    string? Search,
    Guid? SupplierId,
    PaymentDirection? Direction,
    DocumentStatus? Status,
    PaymentMethod? PaymentMethod,
    DateTime? FromDate,
    DateTime? ToDate,
    int Page = 1,
    int PageSize = 20,
    string? SortBy = null,
    SortDirection SortDirection = SortDirection.Desc);
