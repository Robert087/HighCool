using ERP.Domain.Common;

namespace ERP.Domain.MasterData;

public sealed class Customer : AuditableEntity
{
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Phone { get; set; }

    public string? Email { get; set; }

    public string? TaxNumber { get; set; }

    public string? Address { get; set; }

    public string? City { get; set; }

    public string? Area { get; set; }

    public decimal CreditLimit { get; set; }

    public string? PaymentTerms { get; set; }

    public string? Notes { get; set; }

    public bool IsActive { get; set; } = true;
}
