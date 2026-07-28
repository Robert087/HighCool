import { requestJson, type PaginatedResult, type PaginationParams } from "./api";
import type { DocumentStatus } from "./purchaseReceiptsApi";

export interface InventoryCountListFilters extends PaginationParams {
  search: string;
  countNo: string;
  warehouseId: string;
  status: string;
  fromDate: string;
  toDate: string;
}

export interface InventoryCountListItem {
  id: string;
  countNo: string;
  countDate: string;
  snapshotAt: string | null;
  warehouseId: string;
  warehouseCode: string;
  warehouseName: string;
  status: DocumentStatus;
  lineCount: number;
  createdBy: string;
  createdAt: string;
  updatedAt: string | null;
  postedAt: string | null;
  postedBy: string | null;
  canceledAt: string | null;
  canceledBy: string | null;
}

export interface InventoryCountLine {
  id: string;
  lineNo: number;
  itemId: string;
  itemCode: string;
  itemName: string;
  uomId: string;
  uomCode: string;
  uomName: string;
  systemQty: number;
  countedQty: number;
  varianceQty: number;
  baseSystemQty: number;
  baseCountedQty: number;
  baseVarianceQty: number;
  notes: string | null;
  createdAt: string;
  updatedAt: string | null;
}

export interface InventoryCount {
  id: string;
  countNo: string;
  countDate: string;
  snapshotAt: string | null;
  warehouseId: string;
  warehouseCode: string;
  warehouseName: string;
  notes: string | null;
  status: DocumentStatus;
  lines: InventoryCountLine[];
  createdAt: string;
  createdBy: string;
  updatedAt: string | null;
  updatedBy: string | null;
  postedAt: string | null;
  postedBy: string | null;
  canceledAt: string | null;
  canceledBy: string | null;
}

export interface InventoryCountLineFormValues {
  lineNo: number;
  itemId: string;
  uomId: string;
  systemQty: number;
  countedQty: number | "";
  varianceQty: number;
  baseSystemQty: number;
  baseCountedQty: number;
  baseVarianceQty: number;
  notes: string;
}

export interface InventoryCountFormValues {
  countNo: string;
  countDate: string;
  snapshotAt: string | null;
  warehouseId: string;
  notes: string;
  lines: InventoryCountLineFormValues[];
}

function buildListUrl(filters: InventoryCountListFilters) {
  const url = new URL("/api/inventory-counts", window.location.origin);

  if (filters.search.trim()) {
    url.searchParams.set("search", filters.search.trim());
  }

  if (filters.countNo.trim()) {
    url.searchParams.set("countNo", filters.countNo.trim());
  }

  if (filters.warehouseId) {
    url.searchParams.set("warehouseId", filters.warehouseId);
  }

  if (filters.status) {
    url.searchParams.set("status", filters.status);
  }

  if (filters.fromDate) {
    url.searchParams.set("fromDate", new Date(filters.fromDate).toISOString());
  }

  if (filters.toDate) {
    const endOfDay = new Date(filters.toDate);
    endOfDay.setHours(23, 59, 59, 999);
    url.searchParams.set("toDate", endOfDay.toISOString());
  }

  url.searchParams.set("page", String(filters.page));
  url.searchParams.set("pageSize", String(filters.pageSize));
  url.searchParams.set("sortBy", filters.sortBy ?? "countDate");
  url.searchParams.set("sortDirection", filters.sortDirection ?? "Desc");

  return `${url.pathname}${url.search}`;
}

function normalizePayload(values: InventoryCountFormValues) {
  return {
    countNo: values.countNo.trim() || null,
    countDate: values.countDate ? new Date(values.countDate).toISOString() : null,
    warehouseId: values.warehouseId,
    notes: values.notes.trim() || null,
    lines: values.lines.map((line) => ({
      lineNo: Number(line.lineNo),
      itemId: line.itemId,
      uomId: line.uomId,
      countedQty: line.countedQty === "" ? 0 : Number(line.countedQty),
      notes: line.notes.trim() || null,
    })),
  };
}

export function listInventoryCounts(filters: InventoryCountListFilters) {
  return requestJson<PaginatedResult<InventoryCountListItem>>(buildListUrl(filters));
}

export function getInventoryCount(id: string) {
  return requestJson<InventoryCount>(`/api/inventory-counts/${id}`);
}

export function createInventoryCount(values: InventoryCountFormValues) {
  return requestJson<InventoryCount>("/api/inventory-counts", {
    method: "POST",
    body: JSON.stringify(normalizePayload(values)),
  });
}

export function updateInventoryCount(id: string, values: InventoryCountFormValues) {
  return requestJson<InventoryCount>(`/api/inventory-counts/${id}`, {
    method: "PUT",
    body: JSON.stringify(normalizePayload(values)),
  });
}

export function refreshInventoryCountSystemQuantities(id: string) {
  return requestJson<InventoryCount>(`/api/inventory-counts/${id}/refresh-system-quantities`, {
    method: "POST",
  });
}

export function deleteInventoryCountDraft(id: string) {
  return requestJson<void>(`/api/inventory-counts/${id}`, {
    method: "DELETE",
  });
}

export function postInventoryCount(id: string) {
  return requestJson<InventoryCount>(`/api/inventory-counts/${id}/post`, {
    method: "POST",
  });
}

export function cancelInventoryCount(id: string) {
  return requestJson<InventoryCount>(`/api/inventory-counts/${id}/cancel`, {
    method: "POST",
  });
}

export function mapInventoryCountToFormValues(document: InventoryCount): InventoryCountFormValues {
  return {
    countNo: document.countNo,
    countDate: document.countDate.slice(0, 10),
    snapshotAt: document.snapshotAt,
    warehouseId: document.warehouseId,
    notes: document.notes ?? "",
    lines: document.lines.map((line) => ({
      lineNo: line.lineNo,
      itemId: line.itemId,
      uomId: line.uomId,
      systemQty: line.systemQty,
      countedQty: line.countedQty,
      varianceQty: line.varianceQty,
      baseSystemQty: line.baseSystemQty,
      baseCountedQty: line.baseCountedQty,
      baseVarianceQty: line.baseVarianceQty,
      notes: line.notes ?? "",
    })),
  };
}
