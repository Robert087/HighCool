namespace ERP.Application.MasterData.Customers;

public sealed record CustomerListQuery(string? Search, bool? IsActive);
