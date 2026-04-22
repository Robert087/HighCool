namespace ERP.Application.MasterData.Suppliers;

public sealed record UpsertSupplierRequest(
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
    bool IsActive);
