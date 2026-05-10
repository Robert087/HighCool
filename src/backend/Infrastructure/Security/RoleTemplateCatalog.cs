using ERP.Application.Security;

namespace ERP.Infrastructure.Security;

internal static class RoleTemplateCatalog
{
    public static IReadOnlyList<RoleTemplate> DefaultTemplates { get; } =
    [
        new(
            "Owner",
            "owner",
            true,
            Permissions.All),
        new(
            "Viewer",
            "viewer",
            true,
            [
                Permissions.ProcurementPurchaseOrderView,
                Permissions.ProcurementPurchaseReceiptView,
                Permissions.ProcurementPurchaseReturnView,
                Permissions.InventoryStockLedgerView,
                Permissions.ShortageView,
                Permissions.SuppliersView,
                Permissions.CustomersView,
                Permissions.ItemsView,
                Permissions.SupplierFinancialsPayablesView
            ]),
        new(
            "Purchaser",
            "purchaser",
            true,
            [
                Permissions.ProcurementPurchaseOrderView,
                Permissions.ProcurementPurchaseOrderCreate,
                Permissions.ProcurementPurchaseReceiptView,
                Permissions.ProcurementPurchaseReceiptCreate,
                Permissions.SuppliersView
            ]),
        new(
            "Accountant",
            "accountant",
            true,
            [
                Permissions.SuppliersView,
                Permissions.SupplierFinancialsPayablesView,
                Permissions.SupplierFinancialsPaymentsCreate,
                Permissions.SupplierFinancialsPaymentsPost
            ])
    ];
}

internal sealed record RoleTemplate(
    string Name,
    string Key,
    bool IsProtected,
    IReadOnlyCollection<string> Permissions);
