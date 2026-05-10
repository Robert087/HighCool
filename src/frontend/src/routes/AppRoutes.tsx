import type { ReactElement } from "react";
import { Navigate, Route, Routes } from "react-router-dom";
import { useAuth } from "../features/auth/AuthProvider";
import { Permissions } from "../services/permissions";
import { RouteGate } from "./RouteGate";
import { AccessDeniedPage } from "../pages/AccessDeniedPage";
import { CustomerFormPage } from "../pages/CustomerFormPage";
import { CustomersPage } from "../pages/CustomersPage";
import { DashboardPage } from "../pages/DashboardPage";
import { ItemFormPage } from "../pages/ItemFormPage";
import { ItemsPage } from "../pages/ItemsPage";
import { LoginPage } from "../pages/LoginPage";
import { NotFoundPage } from "../pages/NotFoundPage";
import { PurchaseOrderFormPage } from "../pages/PurchaseOrderFormPage";
import { PurchaseOrdersPage } from "../pages/PurchaseOrdersPage";
import { PurchaseReceiptFormPage } from "../pages/PurchaseReceiptFormPage";
import { PurchaseReceiptsPage } from "../pages/PurchaseReceiptsPage";
import { PurchaseReturnFormPage } from "../pages/PurchaseReturnFormPage";
import { PurchaseReturnsPage } from "../pages/PurchaseReturnsPage";
import { OpenShortagesPage } from "../pages/OpenShortagesPage";
import { PaymentFormPage } from "../pages/PaymentFormPage";
import { PaymentsPage } from "../pages/PaymentsPage";
import { ShortageResolutionFormPage } from "../pages/ShortageResolutionFormPage";
import { ShortageResolutionsPage } from "../pages/ShortageResolutionsPage";
import { SupplierFormPage } from "../pages/SupplierFormPage";
import { SupplierStatementPage } from "../pages/SupplierStatementPage";
import { SuppliersPage } from "../pages/SuppliersPage";
import { SignupPage } from "../pages/SignupPage";
import { StockBalancePage } from "../pages/StockBalancePage";
import { StockMovementPage } from "../pages/StockMovementPage";
import { SetupOrganizationPage } from "../pages/SetupOrganizationPage";
import { SettingsRolesPage } from "../pages/settings/SettingsRolesPage";
import { SettingsUsersPage } from "../pages/settings/SettingsUsersPage";
import { UomConversionFormPage } from "../pages/UomConversionFormPage";
import { UomConversionsPage } from "../pages/UomConversionsPage";
import { UomFormPage } from "../pages/UomFormPage";
import { UomsPage } from "../pages/UomsPage";
import { WarehouseFormPage } from "../pages/WarehouseFormPage";
import { WarehousesPage } from "../pages/WarehousesPage";
import { DISABLE_ORG_SETUP_WIZARD } from "../config/temporaryFlags";

export function AppRoutes() {
  const { isAuthenticated, isLoading } = useAuth();

  if (isLoading) {
    return null;
  }

  const guard = (element: ReactElement, permission?: string, feature?: "workspaceEnabled" | "procurementEnabled" | "inventoryEnabled" | "suppliersEnabled" | "supplierFinancialsEnabled" | "settingsEnabled") => (
    <RouteGate permission={permission} feature={feature}>
      {element}
    </RouteGate>
  );

  return (
    <Routes>
      <Route path="/login" element={isAuthenticated ? <Navigate to="/workspace" replace /> : <LoginPage />} />
      <Route path="/signup" element={isAuthenticated ? <Navigate to="/workspace" replace /> : <SignupPage />} />
      <Route path="/" element={isAuthenticated ? <Navigate to="/workspace" replace /> : <Navigate to="/login" replace />} />
      <Route
        path="/setup/organization"
        element={DISABLE_ORG_SETUP_WIZARD ? <Navigate to="/workspace" replace /> : <RouteGate allowDuringSetup><SetupOrganizationPage /></RouteGate>}
      />
      <Route path="/workspace" element={guard(<DashboardPage />, undefined, "workspaceEnabled")} />
      <Route path="/dashboard" element={guard(<DashboardPage />, undefined, "workspaceEnabled")} />
      <Route path="/customers" element={guard(<CustomersPage />, Permissions.CustomersView, "suppliersEnabled")} />
      <Route path="/customers/new" element={guard(<CustomerFormPage />, Permissions.CustomersView, "suppliersEnabled")} />
      <Route path="/customers/:customerId/edit" element={guard(<CustomerFormPage />, Permissions.CustomersView, "suppliersEnabled")} />
      <Route path="/purchase-orders" element={guard(<PurchaseOrdersPage />, Permissions.ProcurementPurchaseOrderView, "procurementEnabled")} />
      <Route path="/procurement/purchase-orders" element={guard(<PurchaseOrdersPage />, Permissions.ProcurementPurchaseOrderView, "procurementEnabled")} />
      <Route path="/purchase-orders/new" element={guard(<PurchaseOrderFormPage />, Permissions.ProcurementPurchaseOrderView, "procurementEnabled")} />
      <Route path="/purchase-orders/:purchaseOrderId/edit" element={guard(<PurchaseOrderFormPage />, Permissions.ProcurementPurchaseOrderView, "procurementEnabled")} />
      <Route path="/purchase-receipts" element={guard(<PurchaseReceiptsPage />, Permissions.ProcurementPurchaseReceiptView, "procurementEnabled")} />
      <Route path="/procurement/purchase-receipts" element={guard(<PurchaseReceiptsPage />, Permissions.ProcurementPurchaseReceiptView, "procurementEnabled")} />
      <Route path="/purchase-receipts/new" element={guard(<PurchaseReceiptFormPage />, Permissions.ProcurementPurchaseReceiptView, "procurementEnabled")} />
      <Route path="/purchase-receipts/:purchaseReceiptId/edit" element={guard(<PurchaseReceiptFormPage />, Permissions.ProcurementPurchaseReceiptView, "procurementEnabled")} />
      <Route path="/purchase-returns" element={guard(<PurchaseReturnsPage />, Permissions.ProcurementPurchaseReturnView, "procurementEnabled")} />
      <Route path="/procurement/purchase-returns" element={guard(<PurchaseReturnsPage />, Permissions.ProcurementPurchaseReturnView, "procurementEnabled")} />
      <Route path="/purchase-returns/new" element={guard(<PurchaseReturnFormPage />, Permissions.ProcurementPurchaseReturnView, "procurementEnabled")} />
      <Route path="/purchase-returns/:purchaseReturnId" element={guard(<PurchaseReturnFormPage />, Permissions.ProcurementPurchaseReturnView, "procurementEnabled")} />
      <Route path="/purchase-returns/:purchaseReturnId/edit" element={guard(<PurchaseReturnFormPage />, Permissions.ProcurementPurchaseReturnView, "procurementEnabled")} />
      <Route path="/open-shortages" element={guard(<OpenShortagesPage />, Permissions.ShortageView, "inventoryEnabled")} />
      <Route path="/shortage-resolutions" element={guard(<ShortageResolutionsPage />, Permissions.ShortageView, "inventoryEnabled")} />
      <Route path="/shortage-resolutions/new" element={guard(<ShortageResolutionFormPage />, Permissions.ShortageView, "inventoryEnabled")} />
      <Route path="/shortage-resolutions/:shortageResolutionId/edit" element={guard(<ShortageResolutionFormPage />, Permissions.ShortageView, "inventoryEnabled")} />
      <Route path="/payments" element={guard(<PaymentsPage />, Permissions.SupplierFinancialsPayablesView, "supplierFinancialsEnabled")} />
      <Route path="/supplier-financials/payments" element={guard(<PaymentsPage />, Permissions.SupplierFinancialsPayablesView, "supplierFinancialsEnabled")} />
      <Route path="/payments/new" element={guard(<PaymentFormPage />, Permissions.SupplierFinancialsPayablesView, "supplierFinancialsEnabled")} />
      <Route path="/payments/:paymentId" element={guard(<PaymentFormPage />, Permissions.SupplierFinancialsPayablesView, "supplierFinancialsEnabled")} />
      <Route path="/payments/:paymentId/edit" element={guard(<PaymentFormPage />, Permissions.SupplierFinancialsPayablesView, "supplierFinancialsEnabled")} />
      <Route path="/stock-balances" element={guard(<StockBalancePage />, Permissions.InventoryStockLedgerView, "inventoryEnabled")} />
      <Route path="/stock-movements" element={guard(<StockMovementPage />, Permissions.InventoryStockLedgerView, "inventoryEnabled")} />
      <Route path="/inventory/stock-ledger" element={guard(<StockMovementPage />, Permissions.InventoryStockLedgerView, "inventoryEnabled")} />
      <Route path="/items" element={guard(<ItemsPage />, Permissions.ItemsView, "inventoryEnabled")} />
      <Route path="/items/new" element={guard(<ItemFormPage />, Permissions.ItemsView, "inventoryEnabled")} />
      <Route path="/items/:itemId/edit" element={guard(<ItemFormPage />, Permissions.ItemsView, "inventoryEnabled")} />
      <Route path="/uom-conversions" element={guard(<UomConversionsPage />, Permissions.UomsManage, "inventoryEnabled")} />
      <Route path="/uom-conversions/new" element={guard(<UomConversionFormPage />, Permissions.UomsManage, "inventoryEnabled")} />
      <Route path="/uom-conversions/:uomConversionId/edit" element={guard(<UomConversionFormPage />, Permissions.UomsManage, "inventoryEnabled")} />
      <Route path="/suppliers" element={guard(<SuppliersPage />, Permissions.SuppliersView, "suppliersEnabled")} />
      <Route path="/supplier-statements" element={guard(<SupplierStatementPage />, Permissions.SupplierFinancialsPayablesView, "supplierFinancialsEnabled")} />
      <Route path="/supplier-financials" element={guard(<SupplierStatementPage />, Permissions.SupplierFinancialsPayablesView, "supplierFinancialsEnabled")} />
      <Route path="/suppliers/new" element={guard(<SupplierFormPage />, Permissions.SuppliersView, "suppliersEnabled")} />
      <Route path="/suppliers/:supplierId/edit" element={guard(<SupplierFormPage />, Permissions.SuppliersView, "suppliersEnabled")} />
      <Route path="/warehouses" element={guard(<WarehousesPage />, Permissions.InventoryWarehouseManage, "inventoryEnabled")} />
      <Route path="/inventory/warehouses" element={guard(<WarehousesPage />, Permissions.InventoryWarehouseManage, "inventoryEnabled")} />
      <Route path="/warehouses/new" element={guard(<WarehouseFormPage />, Permissions.InventoryWarehouseManage, "inventoryEnabled")} />
      <Route path="/warehouses/:warehouseId/edit" element={guard(<WarehouseFormPage />, Permissions.InventoryWarehouseManage, "inventoryEnabled")} />
      <Route path="/uoms" element={guard(<UomsPage />, Permissions.UomsManage, "inventoryEnabled")} />
      <Route path="/uoms/new" element={guard(<UomFormPage />, Permissions.UomsManage, "inventoryEnabled")} />
      <Route path="/uoms/:uomId/edit" element={guard(<UomFormPage />, Permissions.UomsManage, "inventoryEnabled")} />
      <Route path="/settings" element={<Navigate to="/settings/users" replace />} />
      <Route path="/settings/organization" element={<Navigate to="/workspace" replace />} />
      <Route path="/settings/users" element={guard(<SettingsUsersPage />, Permissions.SettingsUsersManage, "settingsEnabled")} />
      <Route path="/settings/profiles" element={<Navigate to="/workspace" replace />} />
      <Route path="/settings/roles" element={guard(<SettingsRolesPage />, Permissions.SettingsRolesManage, "settingsEnabled")} />
      <Route path="/settings/invitations" element={<Navigate to="/workspace" replace />} />
      <Route path="/settings/security" element={<Navigate to="/workspace" replace />} />
      <Route path="/settings/sessions" element={<Navigate to="/workspace" replace />} />
      <Route path="/settings/audit-log" element={<Navigate to="/workspace" replace />} />
      <Route path="/settings/features" element={<Navigate to="/workspace" replace />} />
      <Route path="/403" element={guard(<AccessDeniedPage />)} />
      <Route path="*" element={isAuthenticated ? <NotFoundPage /> : <Navigate to="/login" replace />} />
      <Route path="/home" element={<Navigate to="/workspace" replace />} />
    </Routes>
  );
}
