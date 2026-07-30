import { requestJson, type PaginatedResult, type PaginationParams } from "./api";

export type InventoryStockStatus = "NotMonitored" | "Healthy" | "LowStock" | "OutOfStock";

export interface InventoryMonitoringDashboard {
  totalMonitoredItems: number;
  healthyItems: number;
  lowStockItems: number;
  outOfStockItems: number;
}

export interface InventoryMonitoringFilterOption {
  id: string;
  code: string;
  name: string;
}

export interface InventoryMonitoringFilterOptions {
  warehouses: InventoryMonitoringFilterOption[];
  categories: InventoryMonitoringFilterOption[];
}

export interface InventoryMonitoringFilters extends PaginationParams {
  search: string;
  warehouseId: string;
  categoryId: string;
  status: string;
  onlyMonitored: boolean;
}

export interface InventoryMonitoringItem {
  itemId: string;
  itemCode: string;
  itemName: string;
  categoryId: string | null;
  categoryCode: string | null;
  categoryName: string | null;
  warehouseId: string;
  warehouseCode: string;
  warehouseName: string;
  baseUomId: string;
  baseUomCode: string;
  currentStock: number;
  enableMonitoring: boolean;
  minimumStock: number;
  reorderPoint: number | null;
  maximumStock: number | null;
  reorderQuantity: number | null;
  safetyStock: number | null;
  leadTimeDays: number | null;
  suggestedReorderQuantity: number;
  status: InventoryStockStatus;
}

export interface ReorderSettings {
  itemId: string;
  itemCode: string;
  itemName: string;
  baseUomId: string;
  baseUomCode: string;
  enableMonitoring: boolean;
  minimumStock: number;
  reorderPoint: number | null;
  maximumStock: number | null;
  reorderQuantity: number | null;
  safetyStock: number | null;
  leadTimeDays: number | null;
  createdAt: string;
  updatedAt: string | null;
}

export interface ReorderSettingsFormValues {
  enableMonitoring: boolean;
  minimumStock: number | "";
  reorderPoint: number | "";
  maximumStock: number | "";
  reorderQuantity: number | "";
  safetyStock: number | "";
  leadTimeDays: number | "";
}

function buildListUrl(filters: InventoryMonitoringFilters) {
  const url = new URL("/api/inventory/monitor/items", window.location.origin);

  if (filters.search.trim()) {
    url.searchParams.set("search", filters.search.trim());
  }

  if (filters.warehouseId) {
    url.searchParams.set("warehouseId", filters.warehouseId);
  }

  if (filters.categoryId) {
    url.searchParams.set("categoryId", filters.categoryId);
  }

  if (filters.status) {
    url.searchParams.set("status", filters.status);
  }

  url.searchParams.set("onlyMonitored", String(filters.onlyMonitored));
  url.searchParams.set("page", String(filters.page));
  url.searchParams.set("pageSize", String(filters.pageSize));
  url.searchParams.set("sortBy", filters.sortBy ?? "itemName");
  url.searchParams.set("sortDirection", filters.sortDirection ?? "Asc");

  return `${url.pathname}${url.search}`;
}

function normalizeSettingsPayload(values: ReorderSettingsFormValues) {
  return {
    enableMonitoring: values.enableMonitoring,
    minimumStock: values.minimumStock === "" ? 0 : Number(values.minimumStock),
    reorderPoint: values.reorderPoint === "" ? 0 : Number(values.reorderPoint),
    maximumStock: values.maximumStock === "" ? 0 : Number(values.maximumStock),
    reorderQuantity: values.reorderQuantity === "" ? 0 : Number(values.reorderQuantity),
    safetyStock: values.safetyStock === "" ? null : Number(values.safetyStock),
    leadTimeDays: values.leadTimeDays === "" ? null : Number(values.leadTimeDays),
  };
}

export function getInventoryMonitoringDashboard() {
  return requestJson<InventoryMonitoringDashboard>("/api/inventory/monitor/dashboard");
}

export function getInventoryMonitoringFilterOptions() {
  return requestJson<InventoryMonitoringFilterOptions>("/api/inventory/monitor/filter-options");
}

export function listInventoryMonitoringItems(filters: InventoryMonitoringFilters) {
  return requestJson<PaginatedResult<InventoryMonitoringItem>>(buildListUrl(filters));
}

export function getReorderSettings(itemId: string) {
  return requestJson<ReorderSettings>(`/api/inventory/items/${itemId}/reorder-settings`);
}

export function updateReorderSettings(itemId: string, values: ReorderSettingsFormValues) {
  return requestJson<ReorderSettings>(`/api/inventory/items/${itemId}/reorder-settings`, {
    method: "PUT",
    body: JSON.stringify(normalizeSettingsPayload(values)),
  });
}

export function mapReorderSettingsToFormValues(settings: ReorderSettings): ReorderSettingsFormValues {
  return {
    enableMonitoring: settings.enableMonitoring,
    minimumStock: settings.minimumStock,
    reorderPoint: settings.reorderPoint ?? "",
    maximumStock: settings.maximumStock ?? "",
    reorderQuantity: settings.reorderQuantity ?? "",
    safetyStock: settings.safetyStock ?? "",
    leadTimeDays: settings.leadTimeDays ?? "",
  };
}
