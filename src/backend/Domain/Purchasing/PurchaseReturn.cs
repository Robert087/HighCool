using ERP.Domain.Common;
using ERP.Domain.MasterData;

namespace ERP.Domain.Purchasing;

public sealed class PurchaseReturn : BusinessDocument
{
    public string ReturnNo { get; set; } = string.Empty;

    public Guid SupplierId { get; set; }

    public Supplier? Supplier { get; set; }

    public Guid? ReferenceReceiptId { get; set; }

    public PurchaseReceipt? ReferenceReceipt { get; set; }

    public DateTime ReturnDate { get; set; }

    public string? Notes { get; set; }

    public ICollection<PurchaseReturnLine> Lines { get; set; } = new List<PurchaseReturnLine>();
}
