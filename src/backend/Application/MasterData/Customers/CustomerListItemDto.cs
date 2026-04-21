namespace ERP.Application.MasterData.Customers;

public sealed record CustomerListItemDto(
    Guid Id,
    string Code,
    string Name,
    string? Phone,
    string? Email,
    string? City,
    string? Area,
    decimal CreditLimit,
    string? PaymentTerms,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? UpdatedAt);
