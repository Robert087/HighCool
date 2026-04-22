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
    DateTime? ToDate);
