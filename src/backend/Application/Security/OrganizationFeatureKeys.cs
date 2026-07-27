namespace ERP.Application.Security;

public enum OrganizationFeature
{
    Inventory,
    Purchasing,
    Sales,
    Employees,
    Salaries,
    EmployeeAdvances,
    Expenses,
    Reports,
    Notifications,
    PriceLists,
    InventoryTransfers,
    InventoryAdjustments,
    InventoryCounts,
    InventoryIssues,
    LowStockAlerts,
    PurchaseOrders,
    PurchaseReceipts,
    Warehouses,
    SupplierManagement,
    SupplierFinancials,
    ShortageManagement,
    Uom,
    UomConversion,
    Reversals
}

public static class OrganizationFeatureKeys
{
    public const string Inventory = "inventory";
    public const string Procurement = Purchasing;
    public const string Purchasing = "purchasing";
    public const string Sales = "sales";
    public const string Employees = "employees";
    public const string Salaries = "salaries";
    public const string EmployeeAdvances = "employee_advances";
    public const string Expenses = "expenses";
    public const string Reports = "reports";
    public const string Notifications = "notifications";
    public const string PriceLists = "price_lists";
    public const string InventoryTransfers = "inventory_transfers";
    public const string InventoryAdjustments = "inventory_adjustments";
    public const string InventoryCounts = "inventory_counts";
    public const string InventoryIssues = "inventory_issues";
    public const string LowStockAlerts = "low_stock_alerts";
    public const string PurchaseOrders = "purchase_orders";
    public const string PurchaseReceipts = "purchase_receipts";
    public const string Warehouses = "warehouses";
    public const string SupplierManagement = "supplier_management";
    public const string SupplierFinancials = "supplier_financials";
    public const string ShortageManagement = "shortage_management";
    public const string Uom = "uom";
    public const string UomConversion = "uom_conversion";
    public const string Reversals = "reversals";

    public static string ToKey(this OrganizationFeature feature)
    {
        return feature switch
        {
            OrganizationFeature.Inventory => Inventory,
            OrganizationFeature.Purchasing => Purchasing,
            OrganizationFeature.Sales => Sales,
            OrganizationFeature.Employees => Employees,
            OrganizationFeature.Salaries => Salaries,
            OrganizationFeature.EmployeeAdvances => EmployeeAdvances,
            OrganizationFeature.Expenses => Expenses,
            OrganizationFeature.Reports => Reports,
            OrganizationFeature.Notifications => Notifications,
            OrganizationFeature.PriceLists => PriceLists,
            OrganizationFeature.InventoryTransfers => InventoryTransfers,
            OrganizationFeature.InventoryAdjustments => InventoryAdjustments,
            OrganizationFeature.InventoryCounts => InventoryCounts,
            OrganizationFeature.InventoryIssues => InventoryIssues,
            OrganizationFeature.LowStockAlerts => LowStockAlerts,
            OrganizationFeature.PurchaseOrders => PurchaseOrders,
            OrganizationFeature.PurchaseReceipts => PurchaseReceipts,
            OrganizationFeature.Warehouses => Warehouses,
            OrganizationFeature.SupplierManagement => SupplierManagement,
            OrganizationFeature.SupplierFinancials => SupplierFinancials,
            OrganizationFeature.ShortageManagement => ShortageManagement,
            OrganizationFeature.Uom => Uom,
            OrganizationFeature.UomConversion => UomConversion,
            OrganizationFeature.Reversals => Reversals,
            _ => throw new ArgumentOutOfRangeException(nameof(feature), feature, null)
        };
    }

    public static OrganizationFeature Parse(string key)
    {
        return key switch
        {
            Inventory => OrganizationFeature.Inventory,
            Purchasing or "procurement" => OrganizationFeature.Purchasing,
            Sales => OrganizationFeature.Sales,
            Employees => OrganizationFeature.Employees,
            Salaries => OrganizationFeature.Salaries,
            EmployeeAdvances => OrganizationFeature.EmployeeAdvances,
            Expenses => OrganizationFeature.Expenses,
            Reports => OrganizationFeature.Reports,
            Notifications => OrganizationFeature.Notifications,
            PriceLists => OrganizationFeature.PriceLists,
            InventoryTransfers => OrganizationFeature.InventoryTransfers,
            InventoryAdjustments => OrganizationFeature.InventoryAdjustments,
            InventoryCounts => OrganizationFeature.InventoryCounts,
            InventoryIssues => OrganizationFeature.InventoryIssues,
            LowStockAlerts => OrganizationFeature.LowStockAlerts,
            PurchaseOrders => OrganizationFeature.PurchaseOrders,
            PurchaseReceipts => OrganizationFeature.PurchaseReceipts,
            Warehouses => OrganizationFeature.Warehouses,
            SupplierManagement => OrganizationFeature.SupplierManagement,
            SupplierFinancials => OrganizationFeature.SupplierFinancials,
            ShortageManagement => OrganizationFeature.ShortageManagement,
            Uom => OrganizationFeature.Uom,
            UomConversion => OrganizationFeature.UomConversion,
            Reversals => OrganizationFeature.Reversals,
            _ => throw new ArgumentException($"Unknown organization feature '{key}'.", nameof(key))
        };
    }
}
