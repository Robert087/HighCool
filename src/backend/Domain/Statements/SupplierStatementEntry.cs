using ERP.Domain.Common;
using ERP.Domain.MasterData;

namespace ERP.Domain.Statements;

public sealed class SupplierStatementEntry : OrganizationScopedAuditableEntity
{
    public Guid SupplierId { get; set; }

    public Supplier? Supplier { get; set; }

    public SupplierStatementEffectType EffectType { get; set; }

    public SupplierStatementSourceDocumentType SourceDocType { get; set; }

    public Guid SourceDocId { get; set; }

    public Guid? SourceLineId { get; set; }

    public decimal Debit { get; set; }

    public decimal Credit { get; set; }

    public decimal RunningBalance { get; set; }

    public string? Currency { get; set; }

    public DateTime EntryDate { get; set; }

    public string? Notes { get; set; }
}
