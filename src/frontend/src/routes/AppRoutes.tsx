import { Navigate, Route, Routes } from "react-router-dom";
import { CustomerFormPage } from "../pages/CustomerFormPage";
import { CustomersPage } from "../pages/CustomersPage";
import { DashboardPage } from "../pages/DashboardPage";
import { ItemFormPage } from "../pages/ItemFormPage";
import { ItemsPage } from "../pages/ItemsPage";
import { NotFoundPage } from "../pages/NotFoundPage";
import { PurchaseOrderFormPage } from "../pages/PurchaseOrderFormPage";
import { PurchaseOrdersPage } from "../pages/PurchaseOrdersPage";
import { PurchaseReceiptFormPage } from "../pages/PurchaseReceiptFormPage";
import { PurchaseReceiptsPage } from "../pages/PurchaseReceiptsPage";
import { SupplierFormPage } from "../pages/SupplierFormPage";
import { SuppliersPage } from "../pages/SuppliersPage";
import { StockBalancePage } from "../pages/StockBalancePage";
import { StockMovementPage } from "../pages/StockMovementPage";
import { UomConversionFormPage } from "../pages/UomConversionFormPage";
import { UomConversionsPage } from "../pages/UomConversionsPage";
import { UomFormPage } from "../pages/UomFormPage";
import { UomsPage } from "../pages/UomsPage";
import { WarehouseFormPage } from "../pages/WarehouseFormPage";
import { WarehousesPage } from "../pages/WarehousesPage";

export function AppRoutes() {
  return (
    <Routes>
      <Route path="/" element={<DashboardPage />} />
      <Route path="/customers" element={<CustomersPage />} />
      <Route path="/customers/new" element={<CustomerFormPage />} />
      <Route path="/customers/:customerId/edit" element={<CustomerFormPage />} />
      <Route path="/purchase-orders" element={<PurchaseOrdersPage />} />
      <Route path="/purchase-orders/new" element={<PurchaseOrderFormPage />} />
      <Route path="/purchase-orders/:purchaseOrderId/edit" element={<PurchaseOrderFormPage />} />
      <Route path="/purchase-receipts" element={<PurchaseReceiptsPage />} />
      <Route path="/purchase-receipts/new" element={<PurchaseReceiptFormPage />} />
      <Route path="/purchase-receipts/:purchaseReceiptId/edit" element={<PurchaseReceiptFormPage />} />
      <Route path="/stock-balances" element={<StockBalancePage />} />
      <Route path="/stock-movements" element={<StockMovementPage />} />
      <Route path="/items" element={<ItemsPage />} />
      <Route path="/items/new" element={<ItemFormPage />} />
      <Route path="/items/:itemId/edit" element={<ItemFormPage />} />
      <Route path="/uom-conversions" element={<UomConversionsPage />} />
      <Route path="/uom-conversions/new" element={<UomConversionFormPage />} />
      <Route path="/uom-conversions/:uomConversionId/edit" element={<UomConversionFormPage />} />
      <Route path="/suppliers" element={<SuppliersPage />} />
      <Route path="/suppliers/new" element={<SupplierFormPage />} />
      <Route path="/suppliers/:supplierId/edit" element={<SupplierFormPage />} />
      <Route path="/warehouses" element={<WarehousesPage />} />
      <Route path="/warehouses/new" element={<WarehouseFormPage />} />
      <Route path="/warehouses/:warehouseId/edit" element={<WarehouseFormPage />} />
      <Route path="/uoms" element={<UomsPage />} />
      <Route path="/uoms/new" element={<UomFormPage />} />
      <Route path="/uoms/:uomId/edit" element={<UomFormPage />} />
      <Route path="*" element={<NotFoundPage />} />
      <Route path="/home" element={<Navigate to="/" replace />} />
    </Routes>
  );
}
