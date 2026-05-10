using ERP.Domain.Common;
using ERP.Domain.MasterData;

namespace ERP.Domain.Shortages;

public sealed class ShortageLedgerEntry : OrganizationScopedAuditableEntity
{
    public Guid PurchaseReceiptId { get; set; }

    public ERP.Domain.Purchasing.PurchaseReceipt? PurchaseReceipt { get; set; }

    public Guid PurchaseReceiptLineId { get; set; }

    public ERP.Domain.Purchasing.PurchaseReceiptLine? PurchaseReceiptLine { get; set; }

    public Guid? PurchaseOrderId { get; set; }

    public ERP.Domain.Purchasing.PurchaseOrder? PurchaseOrder { get; set; }

    public Guid? PurchaseOrderLineId { get; set; }

    public ERP.Domain.Purchasing.PurchaseOrderLine? PurchaseOrderLine { get; set; }

    public Guid ItemId { get; set; }

    public Item? Item { get; set; }

    public Guid ComponentItemId { get; set; }

    public Item? ComponentItem { get; set; }

    public decimal ExpectedQty { get; set; }

    public decimal ActualQty { get; set; }

    public decimal ShortageQty { get; set; }

    public decimal ResolvedPhysicalQty { get; set; }

    public decimal ResolvedFinancialQtyEquivalent { get; set; }

    public decimal OpenQty { get; set; }

    public decimal? ShortageValue { get; set; }

    public decimal ResolvedAmount { get; set; }

    public decimal? OpenAmount { get; set; }

    public Guid? ShortageReasonCodeId { get; set; }

    public ShortageReasonCode? ShortageReasonCode { get; set; }

    public bool AffectsSupplierBalance { get; set; }

    public string ApprovalStatus { get; set; } = "NotRequired";

    public ShortageEntryStatus Status { get; set; } = ShortageEntryStatus.Open;

    public ICollection<ShortageResolutionAllocation> Allocations { get; set; } = new List<ShortageResolutionAllocation>();
}
