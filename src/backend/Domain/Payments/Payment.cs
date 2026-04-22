using ERP.Domain.Common;
using ERP.Domain.MasterData;

namespace ERP.Domain.Payments;

public sealed class Payment : BusinessDocument
{
    public string PaymentNo { get; set; } = string.Empty;

    public PaymentPartyType PartyType { get; set; } = PaymentPartyType.Supplier;

    public Guid PartyId { get; set; }

    public Supplier? Supplier { get; set; }

    public PaymentDirection Direction { get; set; }

    public decimal Amount { get; set; }

    public DateTime PaymentDate { get; set; }

    public string? Currency { get; set; }

    public decimal? ExchangeRate { get; set; }

    public PaymentMethod PaymentMethod { get; set; }

    public string? ReferenceNote { get; set; }

    public string? Notes { get; set; }

    public ICollection<PaymentAllocation> Allocations { get; set; } = new List<PaymentAllocation>();
}
