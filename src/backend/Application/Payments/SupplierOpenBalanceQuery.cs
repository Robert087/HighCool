using ERP.Domain.Payments;

namespace ERP.Application.Payments;

public sealed record SupplierOpenBalanceQuery(
    Guid SupplierId,
    PaymentDirection Direction,
    string? Search,
    DateTime? FromDate,
    DateTime? ToDate);
