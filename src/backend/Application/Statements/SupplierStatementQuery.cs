using ERP.Application.Common.Pagination;
using ERP.Domain.Statements;

namespace ERP.Application.Statements;

public sealed record SupplierStatementQuery(
    string? Search,
    Guid? SupplierId,
    SupplierStatementEffectType? EffectType,
    SupplierStatementSourceDocumentType? SourceDocType,
    DateTime? FromDate,
    DateTime? ToDate,
    int Page = 1,
    int PageSize = 20,
    string? SortBy = null,
    SortDirection SortDirection = SortDirection.Desc);
