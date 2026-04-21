namespace ERP.Application.MasterData.Customers;

public sealed record UpdateCustomerRequest(
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
    bool IsActive);
