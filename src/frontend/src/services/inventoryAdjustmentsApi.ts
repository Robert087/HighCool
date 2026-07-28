import { requestJson, type PaginatedResult, type PaginationParams } from "./api";
import type { DocumentStatus } from "./purchaseReceiptsApi";

export type InventoryAdjustmentType = "Increase" | "Decrease";

export interface InventoryAdjustmentListFilters extends PaginationParams {
  search: string;
  adjustmentNo: string;
  warehouseId: string;
  status: string;
  reason: string;
  fromDate: string;
  toDate: string;
}

export interface InventoryAdjustmentListItem {
  id: string;
  adjustmentNo: string;
  adjustmentDate: string;
  warehouseId: string;
  warehouseCode: string;
  warehouseName: string;
  status: DocumentStatus;
  reason: string;
  lineCount: number;
  createdBy: string;
  createdAt: string;
  updatedAt: string | null;
  postedAt: string | null;
  postedBy: string | null;
  canceledAt: string | null;
  canceledBy: string | null;
}

export interface InventoryAdjustmentLine {
  id: string;
  lineNo: number;
  itemId: string;
  itemCode: string;
  itemName: string;
  uomId: string;
  uomCode: string;
  uomName: string;
  quantity: number;
  adjustmentType: InventoryAdjustmentType;
  baseQty: number;
  notes: string | null;
  createdAt: string;
  updatedAt: string | null;
}

export interface InventoryAdjustment {
  id: string;
  adjustmentNo: string;
  adjustmentDate: string;
  warehouseId: string;
  warehouseCode: string;
  warehouseName: string;
  reason: string;
  notes: string | null;
  status: DocumentStatus;
  lines: InventoryAdjustmentLine[];
  createdAt: string;
  createdBy: string;
  updatedAt: string | null;
  updatedBy: string | null;
  postedAt: string | null;
  postedBy: string | null;
  canceledAt: string | null;
  canceledBy: string | null;
}

export interface InventoryAdjustmentLineFormValues {
  lineNo: number;
  itemId: string;
  uomId: string;
  quantity: number | "";
  adjustmentType: InventoryAdjustmentType;
  baseQty: number;
  notes: string;
}

export interface InventoryAdjustmentFormValues {
  adjustmentNo: string;
  adjustmentDate: string;
  warehouseId: string;
  reason: string;
  notes: string;
  lines: InventoryAdjustmentLineFormValues[];
}

function buildListUrl(filters: InventoryAdjustmentListFilters) {
  const url = new URL("/api/inventory-adjustments", window.location.origin);

  if (filters.search.trim()) {
    url.searchParams.set("search", filters.search.trim());
  }

  if (filters.adjustmentNo.trim()) {
    url.searchParams.set("adjustmentNo", filters.adjustmentNo.trim());
  }

  if (filters.warehouseId) {
    url.searchParams.set("warehouseId", filters.warehouseId);
  }

  if (filters.status) {
    url.searchParams.set("status", filters.status);
  }

  if (filters.reason.trim()) {
    url.searchParams.set("reason", filters.reason.trim());
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
  url.searchParams.set("sortBy", filters.sortBy ?? "adjustmentDate");
  url.searchParams.set("sortDirection", filters.sortDirection ?? "Desc");

  return `${url.pathname}${url.search}`;
}

function normalizePayload(values: InventoryAdjustmentFormValues) {
  return {
    adjustmentNo: values.adjustmentNo.trim() || null,
    adjustmentDate: values.adjustmentDate ? new Date(values.adjustmentDate).toISOString() : null,
    warehouseId: values.warehouseId,
    reason: values.reason.trim(),
    notes: values.notes.trim() || null,
    lines: values.lines.map((line) => ({
      lineNo: Number(line.lineNo),
      itemId: line.itemId,
      uomId: line.uomId,
      quantity: line.quantity === "" ? 0 : Number(line.quantity),
      adjustmentType: line.adjustmentType,
      notes: line.notes.trim() || null,
    })),
  };
}

export function listInventoryAdjustments(filters: InventoryAdjustmentListFilters) {
  return requestJson<PaginatedResult<InventoryAdjustmentListItem>>(buildListUrl(filters));
}

export function getInventoryAdjustment(id: string) {
  return requestJson<InventoryAdjustment>(`/api/inventory-adjustments/${id}`);
}

export function createInventoryAdjustment(values: InventoryAdjustmentFormValues) {
  return requestJson<InventoryAdjustment>("/api/inventory-adjustments", {
    method: "POST",
    body: JSON.stringify(normalizePayload(values)),
  });
}

export function updateInventoryAdjustment(id: string, values: InventoryAdjustmentFormValues) {
  return requestJson<InventoryAdjustment>(`/api/inventory-adjustments/${id}`, {
    method: "PUT",
    body: JSON.stringify(normalizePayload(values)),
  });
}

export function deleteInventoryAdjustmentDraft(id: string) {
  return requestJson<void>(`/api/inventory-adjustments/${id}`, {
    method: "DELETE",
  });
}

export function postInventoryAdjustment(id: string) {
  return requestJson<InventoryAdjustment>(`/api/inventory-adjustments/${id}/post`, {
    method: "POST",
  });
}

export function cancelInventoryAdjustment(id: string) {
  return requestJson<InventoryAdjustment>(`/api/inventory-adjustments/${id}/cancel`, {
    method: "POST",
  });
}

export function mapInventoryAdjustmentToFormValues(document: InventoryAdjustment): InventoryAdjustmentFormValues {
  return {
    adjustmentNo: document.adjustmentNo,
    adjustmentDate: document.adjustmentDate.slice(0, 10),
    warehouseId: document.warehouseId,
    reason: document.reason,
    notes: document.notes ?? "",
    lines: document.lines.map((line) => ({
      lineNo: line.lineNo,
      itemId: line.itemId,
      uomId: line.uomId,
      quantity: line.quantity,
      adjustmentType: line.adjustmentType,
      baseQty: line.baseQty,
      notes: line.notes ?? "",
    })),
  };
}
