namespace ERP.Application.Statements;

public sealed record SupplierStatementSummaryDto(
    Guid SupplierId,
    string SupplierCode,
    string SupplierName,
    DateTime? FromDate,
    DateTime? ToDate,
    decimal CurrentBalance,
    decimal OpeningBalance,
    decimal ClosingBalance,
    decimal TotalDebit,
    decimal TotalCredit,
    int EntryCount);
