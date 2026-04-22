using ERP.Domain.Statements;

namespace ERP.Application.Statements;

public sealed record SupplierStatementSummaryQuery(
    Guid SupplierId,
    SupplierStatementEffectType? EffectType,
    SupplierStatementSourceDocumentType? SourceDocType,
    DateTime? FromDate,
    DateTime? ToDate);
