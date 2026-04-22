using ERP.Domain.Statements;

namespace ERP.Application.Statements;

public sealed record SupplierStatementEntryDto(
    Guid Id,
    Guid SupplierId,
    string SupplierCode,
    string SupplierName,
    DateTime EntryDate,
    SupplierStatementSourceDocumentType SourceDocType,
    Guid SourceDocId,
    Guid? SourceLineId,
    int? SourceSequenceNo,
    string SourceDocumentNo,
    SupplierStatementEffectType EffectType,
    decimal Debit,
    decimal Credit,
    decimal RunningBalance,
    string? Currency,
    string? Notes,
    DateTime CreatedAt,
    string CreatedBy);
