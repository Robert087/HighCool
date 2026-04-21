namespace ERP.Application.MasterData.Customers;

public sealed record CustomerDto(
    Guid Id,
    string Code,
    string Name,
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
