import { buildApiUrl, requestJson } from "./api";
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

function buildListUrl(search?: string): string {
  const url = new URL(buildApiUrl("/api/purchase-orders"));
  if (search) {
    url.searchParams.set("search", search);
  }

  return url.toString();
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

export function listPurchaseOrders(search: string) {
  return requestJson<PurchaseOrderListItem[]>(buildListUrl(search));
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
