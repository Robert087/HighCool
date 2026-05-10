namespace ERP.Application.Security;

public static class Permissions
{
    public const string AuditLogView = "audit_log.view";
    public const string AuditLogExport = "audit_log.export";
    public const string SettingsOrganizationManage = "settings.organization.manage";
    public const string SettingsUsersManage = "settings.users.manage";
    public const string SettingsRolesManage = "settings.roles.manage";
    public const string SettingsPermissionsManage = "settings.permissions.manage";
    public const string SettingsSecurityManage = "settings.security.manage";
    public const string SettingsProfilesManage = "settings.profiles.manage";
    public const string SettingsInvitationsManage = "settings.invitations.manage";
    public const string SettingsSessionsManage = "settings.sessions.manage";
    public const string ProcurementPurchaseOrderView = "procurement.purchase_order.view";
    public const string ProcurementPurchaseOrderCreate = "procurement.purchase_order.create";
    public const string ProcurementPurchaseOrderEdit = "procurement.purchase_order.edit";
    public const string ProcurementPurchaseOrderDelete = "procurement.purchase_order.delete";
    public const string ProcurementPurchaseOrderPost = "procurement.purchase_order.post";
    public const string ProcurementPurchaseOrderCancel = "procurement.purchase_order.cancel";
    public const string ProcurementPurchaseOrderApprove = "procurement.purchase_order.approve";
    public const string ProcurementPurchaseReceiptView = "procurement.purchase_receipt.view";
    public const string ProcurementPurchaseReceiptCreate = "procurement.purchase_receipt.create";
    public const string ProcurementPurchaseReceiptEdit = "procurement.purchase_receipt.edit";
    public const string ProcurementPurchaseReceiptDelete = "procurement.purchase_receipt.delete";
    public const string ProcurementPurchaseReceiptPost = "procurement.purchase_receipt.post";
    public const string ProcurementPurchaseReceiptCancel = "procurement.purchase_receipt.cancel";
    public const string ProcurementPurchaseReturnView = "procurement.purchase_return.view";
    public const string ProcurementPurchaseReturnCreate = "procurement.purchase_return.create";
    public const string ProcurementPurchaseReturnEdit = "procurement.purchase_return.edit";
    public const string ProcurementPurchaseReturnPost = "procurement.purchase_return.post";
    public const string InventoryStockLedgerView = "inventory.stock_ledger.view";
    public const string InventoryAdjustmentCreate = "inventory.adjustment.create";
    public const string InventoryAdjustmentPost = "inventory.adjustment.post";
    public const string InventoryWarehouseManage = "inventory.warehouse.manage";
    public const string ShortageView = "inventory.shortage.view";
    public const string ShortageResolutionCreate = "inventory.shortage_resolution.create";
    public const string ShortageResolutionPost = "inventory.shortage_resolution.post";
    public const string SuppliersView = "suppliers.view";
    public const string SuppliersCreate = "suppliers.create";
    public const string SuppliersEdit = "suppliers.edit";
    public const string SuppliersDelete = "suppliers.delete";
    public const string CustomersView = "customers.view";
    public const string CustomersCreate = "customers.create";
    public const string CustomersEdit = "customers.edit";
    public const string CustomersDelete = "customers.delete";
    public const string ItemsView = "items.view";
    public const string ItemsCreate = "items.create";
    public const string ItemsEdit = "items.edit";
    public const string UomsManage = "settings.uoms.manage";
    public const string SupplierFinancialsPayablesView = "supplier_financials.payables.view";
    public const string SupplierFinancialsPaymentsCreate = "supplier_financials.payments.create";
    public const string SupplierFinancialsPaymentsPost = "supplier_financials.payments.post";
    public const string SupplierFinancialsReversalsCreate = "supplier_financials.reversals.create";

    public static readonly IReadOnlyCollection<string> All = new[]
    {
        AuditLogView,
        AuditLogExport,
        SettingsOrganizationManage,
        SettingsUsersManage,
        SettingsRolesManage,
        SettingsPermissionsManage,
        SettingsSecurityManage,
        SettingsProfilesManage,
        SettingsInvitationsManage,
        SettingsSessionsManage,
        ProcurementPurchaseOrderView,
        ProcurementPurchaseOrderCreate,
        ProcurementPurchaseOrderEdit,
        ProcurementPurchaseOrderDelete,
        ProcurementPurchaseOrderPost,
        ProcurementPurchaseOrderCancel,
        ProcurementPurchaseOrderApprove,
        ProcurementPurchaseReceiptView,
        ProcurementPurchaseReceiptCreate,
        ProcurementPurchaseReceiptEdit,
        ProcurementPurchaseReceiptDelete,
        ProcurementPurchaseReceiptPost,
        ProcurementPurchaseReceiptCancel,
        ProcurementPurchaseReturnView,
        ProcurementPurchaseReturnCreate,
        ProcurementPurchaseReturnEdit,
        ProcurementPurchaseReturnPost,
        InventoryStockLedgerView,
        InventoryAdjustmentCreate,
        InventoryAdjustmentPost,
        InventoryWarehouseManage,
        ShortageView,
        ShortageResolutionCreate,
        ShortageResolutionPost,
        SuppliersView,
        SuppliersCreate,
        SuppliersEdit,
        SuppliersDelete,
        CustomersView,
        CustomersCreate,
        CustomersEdit,
        CustomersDelete,
        ItemsView,
        ItemsCreate,
        ItemsEdit,
        UomsManage,
        SupplierFinancialsPayablesView,
        SupplierFinancialsPaymentsCreate,
        SupplierFinancialsPaymentsPost,
        SupplierFinancialsReversalsCreate
    };

    public static IReadOnlySet<string> Expand(IEnumerable<string> permissions)
    {
        var set = new HashSet<string>(permissions, StringComparer.OrdinalIgnoreCase);
        var additions = new List<string>();

        foreach (var permission in set)
        {
            additions.AddRange(GetDependencies(permission));
        }

        foreach (var permission in additions)
        {
            set.Add(permission);
        }

        return set;
    }

    public static IReadOnlyCollection<string> GetDependencies(string permission)
    {
        if (permission.EndsWith(".manage", StringComparison.OrdinalIgnoreCase))
        {
            var prefix = permission[..^"manage".Length];
            return new[]
            {
                $"{prefix}view",
                $"{prefix}create",
                $"{prefix}edit",
                $"{prefix}delete"
            };
        }

        if (permission.EndsWith(".create", StringComparison.OrdinalIgnoreCase) ||
            permission.EndsWith(".edit", StringComparison.OrdinalIgnoreCase) ||
            permission.EndsWith(".delete", StringComparison.OrdinalIgnoreCase) ||
            permission.EndsWith(".post", StringComparison.OrdinalIgnoreCase) ||
            permission.EndsWith(".approve", StringComparison.OrdinalIgnoreCase) ||
            permission.EndsWith(".cancel", StringComparison.OrdinalIgnoreCase) ||
            permission.EndsWith(".reverse", StringComparison.OrdinalIgnoreCase) ||
            permission.EndsWith(".export", StringComparison.OrdinalIgnoreCase))
        {
            return [ReplaceLastSegment(permission, "view")];
        }

        return Array.Empty<string>();
    }

    private static string ReplaceLastSegment(string permission, string replacement)
    {
        var lastDot = permission.LastIndexOf('.');
        return lastDot < 0 ? permission : $"{permission[..(lastDot + 1)]}{replacement}";
    }
}
