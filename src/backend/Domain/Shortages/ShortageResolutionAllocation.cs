using ERP.Domain.Common;

namespace ERP.Domain.Shortages;

public sealed class ShortageResolutionAllocation : AuditableEntity
{
    public Guid ResolutionId { get; set; }

    public ShortageResolution? Resolution { get; set; }

    public Guid ShortageLedgerId { get; set; }

    public ShortageLedgerEntry? ShortageLedgerEntry { get; set; }

    public ShortageAllocationType AllocationType { get; set; }

    public decimal? AllocatedQty { get; set; }

    public decimal? AllocatedAmount { get; set; }

    public decimal? ValuationRate { get; set; }

    public decimal? FinancialQtyEquivalent { get; set; }

    public string AllocationMethod { get; set; } = "Manual";

    public int SequenceNo { get; set; }
}
