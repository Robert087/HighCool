namespace ERP.Application.MasterData.Suppliers;

public sealed record SupplierDto(
    Guid Id,
    string Code,
    string Name,
    string StatementName,
    string? Phone,
    string? Email,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? UpdatedAt);
