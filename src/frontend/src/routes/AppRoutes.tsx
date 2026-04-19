import { Navigate, Route, Routes } from "react-router-dom";
import { DashboardPage } from "../pages/DashboardPage";
import { ItemComponentFormPage } from "../pages/ItemComponentFormPage";
import { ItemComponentsPage } from "../pages/ItemComponentsPage";
import { ItemFormPage } from "../pages/ItemFormPage";
import { ItemsPage } from "../pages/ItemsPage";
import { ItemUomConversionFormPage } from "../pages/ItemUomConversionFormPage";
import { ItemUomConversionsPage } from "../pages/ItemUomConversionsPage";
import { NotFoundPage } from "../pages/NotFoundPage";
import { SupplierFormPage } from "../pages/SupplierFormPage";
import { SuppliersPage } from "../pages/SuppliersPage";
import { UomFormPage } from "../pages/UomFormPage";
import { UomsPage } from "../pages/UomsPage";
import { WarehouseFormPage } from "../pages/WarehouseFormPage";
import { WarehousesPage } from "../pages/WarehousesPage";

export function AppRoutes() {
  return (
    <Routes>
      <Route path="/" element={<DashboardPage />} />
      <Route path="/items" element={<ItemsPage />} />
      <Route path="/items/new" element={<ItemFormPage />} />
      <Route path="/items/:itemId/edit" element={<ItemFormPage />} />
      <Route path="/item-components" element={<ItemComponentsPage />} />
      <Route path="/item-components/new" element={<ItemComponentFormPage />} />
      <Route path="/item-components/:itemComponentId/edit" element={<ItemComponentFormPage />} />
      <Route path="/item-uom-conversions" element={<ItemUomConversionsPage />} />
      <Route path="/item-uom-conversions/new" element={<ItemUomConversionFormPage />} />
      <Route path="/item-uom-conversions/:itemUomConversionId/edit" element={<ItemUomConversionFormPage />} />
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
