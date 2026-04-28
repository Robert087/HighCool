import { requestJson, type PaginatedResult, type PaginationParams } from "./api";
import type { DocumentStatus } from "./purchaseReceiptsApi";

export type PurchaseOrderReceiptProgressStatus = "NotReceived" | "PartiallyReceived" | "FullyReceived";

export interface PurchaseOrderListItem {
  id: string;
  poNo: string;
  supplierId: string;
  supplierCode: string;
  supplierName: string;
  orderDate: string;
  expectedDate: string | null;
  status: DocumentStatus;
  receiptProgressStatus: PurchaseOrderReceiptProgressStatus;
  lineCount: number;
  createdAt: string;
  updatedAt: string | null;
}

export interface PurchaseOrderLine {
  id: string;
  lineNo: number;
  itemId: string;
  itemCode: string;
  itemName: string;
  orderedQty: number;
  uomId: string;
  uomCode: string;
  uomName: string;
  receivedQty: number;
  remainingQty: number;
  notes: string | null;
  createdAt: string;
  updatedAt: string | null;
}

export interface PurchaseOrder {
  id: string;
  poNo: string;
  supplierId: string;
  supplierCode: string;
  supplierName: string;
  orderDate: string;
  expectedDate: string | null;
  notes: string | null;
  status: DocumentStatus;
  receiptProgressStatus: PurchaseOrderReceiptProgressStatus;
  lines: PurchaseOrderLine[];
  createdAt: string;
  updatedAt: string | null;
}

export interface PurchaseOrderAvailableLine {
  purchaseOrderLineId: string;
  lineNo: number;
  itemId: string;
  itemCode: string;
  itemName: string;
  orderedQty: number;
  receivedQty: number;
  remainingQty: number;
  uomId: string;
  uomCode: string;
  uomName: string;
  notes: string | null;
}

export interface PurchaseOrderLineFormValues {
  lineNo: number;
  itemId: string;
  orderedQty: number | "";
  uomId: string;
  notes: string;
}

export interface PurchaseOrderFormValues {
  poNo: string;
  supplierId: string;
  orderDate: string;
  expectedDate: string;
  notes: string;
  lines: PurchaseOrderLineFormValues[];
}

export interface PurchaseOrderListFilters extends PaginationParams {
  search: string;
  status: string;
  receiptProgress: string;
  fromDate: string;
  toDate: string;
}

function buildListUrl(filters: PurchaseOrderListFilters): string {
  const url = new URL("/api/purchase-orders", window.location.origin);

  if (filters.search.trim()) {
    url.searchParams.set("search", filters.search.trim());
  }

  if (filters.status) {
    url.searchParams.set("status", filters.status);
  }

  if (filters.receiptProgress) {
    url.searchParams.set("receiptProgressStatus", filters.receiptProgress);
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
  url.searchParams.set("sortBy", filters.sortBy ?? "orderDate");
  url.searchParams.set("sortDirection", filters.sortDirection ?? "Desc");

  return `${url.pathname}${url.search}`;
}

function normalizePayload(values: PurchaseOrderFormValues) {
  return {
    poNo: values.poNo.trim() || null,
    supplierId: values.supplierId,
    orderDate: values.orderDate ? new Date(values.orderDate).toISOString() : null,
    expectedDate: values.expectedDate ? new Date(values.expectedDate).toISOString() : null,
    notes: values.notes.trim() || null,
    lines: values.lines.map((line) => ({
      lineNo: Number(line.lineNo),
      itemId: line.itemId,
      orderedQty: Number(line.orderedQty),
      uomId: line.uomId,
      notes: line.notes.trim() || null,
    })),
  };
}

export function listPurchaseOrders(filters: PurchaseOrderListFilters) {
  return requestJson<PaginatedResult<PurchaseOrderListItem>>(buildListUrl(filters));
}

export function getPurchaseOrder(id: string) {
  return requestJson<PurchaseOrder>(`/api/purchase-orders/${id}`);
}

export function createPurchaseOrder(values: PurchaseOrderFormValues) {
  return requestJson<PurchaseOrder>("/api/purchase-orders", {
    method: "POST",
    body: JSON.stringify(normalizePayload(values)),
  });
}

export function updatePurchaseOrder(id: string, values: PurchaseOrderFormValues) {
  return requestJson<PurchaseOrder>(`/api/purchase-orders/${id}`, {
    method: "PUT",
    body: JSON.stringify(normalizePayload(values)),
  });
}

export function postPurchaseOrder(id: string) {
  return requestJson<PurchaseOrder>(`/api/purchase-orders/${id}/post`, {
    method: "POST",
  });
}

export function cancelPurchaseOrder(id: string) {
  return requestJson<PurchaseOrder>(`/api/purchase-orders/${id}/cancel`, {
    method: "POST",
  });
}

export function listAvailablePurchaseOrderLines(id: string) {
  return requestJson<PurchaseOrderAvailableLine[]>(`/api/purchase-orders/${id}/available-lines-for-receipt`);
}
