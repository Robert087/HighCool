namespace ERP.Application.MasterData.Suppliers;

public sealed record SupplierDto(
    Guid Id,
    string Code,
    string Name,
    string StatementName,
    string? Phone,
    string? Email,
    string? TaxNumber,
    string? Address,
    string? City,
    string? Area,
    decimal CreditLimit,
    string? PaymentTerms,
    string? Notes,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? UpdatedAt);
