using ERP.Domain.Common;
using ERP.Domain.MasterData;
using ERP.Domain.Shortages;

namespace ERP.Domain.Purchasing;

public sealed class PurchaseReceiptLineComponent : OrganizationScopedAuditableEntity
{
    public Guid PurchaseReceiptLineId { get; set; }

    public PurchaseReceiptLine? PurchaseReceiptLine { get; set; }

    public Guid ComponentItemId { get; set; }

    public Item? ComponentItem { get; set; }

    public decimal ExpectedQty { get; set; }

    public decimal ActualReceivedQty { get; set; }

    public Guid UomId { get; set; }

    public Uom? Uom { get; set; }

    public Guid? ShortageReasonCodeId { get; set; }

    public ShortageReasonCode? ShortageReasonCode { get; set; }

    public string? Notes { get; set; }
}
