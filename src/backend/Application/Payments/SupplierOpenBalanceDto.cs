using ERP.Domain.Payments;

namespace ERP.Application.Payments;

public sealed record SupplierOpenBalanceDto(
    PaymentTargetDocumentType TargetDocType,
    Guid TargetDocId,
    Guid SupplierId,
    string SupplierCode,
    string SupplierName,
    string TargetDocumentNo,
    DateTime TargetDocumentDate,
    decimal OriginalAmount,
    decimal AllocatedAmount,
    decimal OpenAmount,
    string? Currency,
    string? Notes);
