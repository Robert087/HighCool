namespace ERP.Application.MasterData.Suppliers;

public sealed record UpsertSupplierRequest(
    string Code,
    string Name,
    string StatementName,
    string? Phone,
    string? Email,
    bool IsActive);
