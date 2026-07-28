import { requestJson, type PaginatedResult, type PaginationParams } from "./api";
import type { DocumentStatus } from "./purchaseReceiptsApi";

export interface InventoryTransferListFilters extends PaginationParams {
  search: string;
  transferNo: string;
  sourceWarehouseId: string;
  destinationWarehouseId: string;
  status: string;
  fromDate: string;
  toDate: string;
}

export interface InventoryTransferListItem {
  id: string;
  transferNo: string;
  transferDate: string;
  sourceWarehouseId: string;
  sourceWarehouseCode: string;
  sourceWarehouseName: string;
  destinationWarehouseId: string;
  destinationWarehouseCode: string;
  destinationWarehouseName: string;
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

export interface InventoryTransferLine {
  id: string;
  lineNo: number;
  itemId: string;
  itemCode: string;
  itemName: string;
  uomId: string;
  uomCode: string;
  uomName: string;
  quantity: number;
  baseQty: number;
  notes: string | null;
  createdAt: string;
  updatedAt: string | null;
}

export interface InventoryTransfer {
  id: string;
  transferNo: string;
  transferDate: string;
  sourceWarehouseId: string;
  sourceWarehouseCode: string;
  sourceWarehouseName: string;
  destinationWarehouseId: string;
  destinationWarehouseCode: string;
  destinationWarehouseName: string;
  notes: string | null;
  status: DocumentStatus;
  lines: InventoryTransferLine[];
  createdAt: string;
  createdBy: string;
  updatedAt: string | null;
  updatedBy: string | null;
  postedAt: string | null;
  postedBy: string | null;
  canceledAt: string | null;
  canceledBy: string | null;
}

export interface InventoryTransferLineFormValues {
  lineNo: number;
  itemId: string;
  uomId: string;
  quantity: number | "";
  baseQty: number;
  notes: string;
}

export interface InventoryTransferFormValues {
  transferNo: string;
  transferDate: string;
  sourceWarehouseId: string;
  destinationWarehouseId: string;
  notes: string;
  lines: InventoryTransferLineFormValues[];
}

function buildListUrl(filters: InventoryTransferListFilters) {
  const url = new URL("/api/inventory-transfers", window.location.origin);

  if (filters.search.trim()) {
    url.searchParams.set("search", filters.search.trim());
  }

  if (filters.transferNo.trim()) {
    url.searchParams.set("transferNo", filters.transferNo.trim());
  }

  if (filters.sourceWarehouseId) {
    url.searchParams.set("sourceWarehouseId", filters.sourceWarehouseId);
  }

  if (filters.destinationWarehouseId) {
    url.searchParams.set("destinationWarehouseId", filters.destinationWarehouseId);
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
  url.searchParams.set("sortBy", filters.sortBy ?? "transferDate");
  url.searchParams.set("sortDirection", filters.sortDirection ?? "Desc");

  return `${url.pathname}${url.search}`;
}

function normalizePayload(values: InventoryTransferFormValues) {
  return {
    transferNo: values.transferNo.trim() || null,
    transferDate: values.transferDate ? new Date(values.transferDate).toISOString() : null,
    sourceWarehouseId: values.sourceWarehouseId,
    destinationWarehouseId: values.destinationWarehouseId,
    notes: values.notes.trim() || null,
    lines: values.lines.map((line) => ({
      lineNo: Number(line.lineNo),
      itemId: line.itemId,
      uomId: line.uomId,
      quantity: line.quantity === "" ? 0 : Number(line.quantity),
      notes: line.notes.trim() || null,
    })),
  };
}

export function listInventoryTransfers(filters: InventoryTransferListFilters) {
  return requestJson<PaginatedResult<InventoryTransferListItem>>(buildListUrl(filters));
}

export function getInventoryTransfer(id: string) {
  return requestJson<InventoryTransfer>(`/api/inventory-transfers/${id}`);
}

export function createInventoryTransfer(values: InventoryTransferFormValues) {
  return requestJson<InventoryTransfer>("/api/inventory-transfers", {
    method: "POST",
    body: JSON.stringify(normalizePayload(values)),
  });
}

export function updateInventoryTransfer(id: string, values: InventoryTransferFormValues) {
  return requestJson<InventoryTransfer>(`/api/inventory-transfers/${id}`, {
    method: "PUT",
    body: JSON.stringify(normalizePayload(values)),
  });
}

export function deleteInventoryTransferDraft(id: string) {
  return requestJson<void>(`/api/inventory-transfers/${id}`, {
    method: "DELETE",
  });
}

export function postInventoryTransfer(id: string) {
  return requestJson<InventoryTransfer>(`/api/inventory-transfers/${id}/post`, {
    method: "POST",
  });
}

export function cancelInventoryTransfer(id: string) {
  return requestJson<InventoryTransfer>(`/api/inventory-transfers/${id}/cancel`, {
    method: "POST",
  });
}

export function mapInventoryTransferToFormValues(document: InventoryTransfer): InventoryTransferFormValues {
  return {
    transferNo: document.transferNo,
    transferDate: document.transferDate.slice(0, 10),
    sourceWarehouseId: document.sourceWarehouseId,
    destinationWarehouseId: document.destinationWarehouseId,
    notes: document.notes ?? "",
    lines: document.lines.map((line) => ({
      lineNo: line.lineNo,
      itemId: line.itemId,
      uomId: line.uomId,
      quantity: line.quantity,
      baseQty: line.baseQty,
      notes: line.notes ?? "",
    })),
  };
}
