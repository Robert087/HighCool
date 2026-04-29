using ERP.Domain.Common;

namespace ERP.Domain.Identity;

public sealed class Organization : AuditableEntity
{
    public string Name { get; set; } = string.Empty;

    public string? Logo { get; set; }

    public string? Address { get; set; }

    public string? Phone { get; set; }

    public string? TaxId { get; set; }

    public string? CommercialRegistry { get; set; }

    public string DefaultCurrency { get; set; } = "EGP";

    public string Timezone { get; set; } = "Africa/Cairo";

    public string DefaultLanguage { get; set; } = "en";

    public bool RtlEnabled { get; set; }

    public int FiscalYearStartMonth { get; set; } = 1;

    public string PurchaseOrderPrefix { get; set; } = "PO";

    public string PurchaseReceiptPrefix { get; set; } = "PR";

    public string PurchaseReturnPrefix { get; set; } = "RTN";

    public string PaymentPrefix { get; set; } = "PAY";

    public Guid? DefaultWarehouseId { get; set; }

    public bool AutoPostDrafts { get; set; }

    public bool SetupCompleted { get; set; }

    public DateTime? SetupCompletedAt { get; set; }

    public string? SetupCompletedBy { get; set; }

    public string? SetupStep { get; set; }

    public string? SetupVersion { get; set; }

    public bool EnableProcurement { get; set; } = true;

    public bool EnablePurchaseOrders { get; set; } = true;

    public bool EnablePurchaseReceipts { get; set; } = true;

    public bool EnableInventory { get; set; } = true;

    public bool EnableWarehouses { get; set; } = true;

    public bool EnableMultipleWarehouses { get; set; }

    public bool EnableSupplierManagement { get; set; } = true;

    public bool EnableSupplierFinancials { get; set; } = true;

    public bool EnableShortageManagement { get; set; } = true;

    public bool EnableComponentsBom { get; set; } = true;

    public bool EnableUom { get; set; } = true;

    public bool EnableUomConversion { get; set; } = true;

    public bool RequirePoBeforeReceipt { get; set; }

    public bool AllowDirectPurchaseReceipt { get; set; } = true;

    public bool AllowPartialReceipt { get; set; } = true;

    public bool AllowOverReceipt { get; set; }

    public decimal OverReceiptTolerancePercent { get; set; }

    public bool EnablePostingWorkflow { get; set; } = true;

    public bool LockPostedDocuments { get; set; } = true;

    public bool RequireApprovalBeforePosting { get; set; }

    public bool EnableReversals { get; set; } = true;

    public bool RequireReasonForCancelOrReversal { get; set; } = true;

    public bool AllowNegativeStock { get; set; }

    public bool EnableBatchTracking { get; set; }

    public bool EnableSerialTracking { get; set; }

    public bool EnableExpiryTracking { get; set; }

    public bool EnableStockTransfers { get; set; }

    public bool EnableStockAdjustments { get; set; }

    public ICollection<OrganizationMembership> Memberships { get; set; } = new List<OrganizationMembership>();
}
