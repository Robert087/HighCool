import { requestJson, type PaginatedResult, type PaginationParams } from "./api";

export type DocumentStatus = "Draft" | "Posted" | "Canceled";

export interface PurchaseReceiptListItem {
  id: string;
  receiptNo: string;
  supplierId: string;
  supplierCode: string;
  supplierName: string;
  warehouseId: string;
  warehouseCode: string;
  warehouseName: string;
  purchaseOrderId: string | null;
  purchaseOrderNo: string | null;
  receiptDate: string;
  status: DocumentStatus;
  lineCount: number;
  createdAt: string;
  updatedAt: string | null;
}

export interface PurchaseReceiptLineComponent {
  id: string;
  componentItemId: string;
  componentItemCode: string;
  componentItemName: string;
  expectedQty: number;
  actualReceivedQty: number;
  uomId: string;
  uomCode: string;
  uomName: string;
  shortageReasonCodeId: string | null;
  shortageReasonCodeCode: string | null;
  shortageReasonCodeName: string | null;
  notes: string | null;
  createdAt: string;
  updatedAt: string | null;
}

export interface ShortageReasonCode {
  id: string;
  code: string;
  name: string;
  description: string | null;
  affectsSupplierBalance: boolean;
  affectsStock: boolean;
  requiresApproval: boolean;
}

export interface PurchaseReceiptLine {
  id: string;
  lineNo: number;
  purchaseOrderLineId: string | null;
  itemId: string;
  itemCode: string;
  itemName: string;
  orderedQtySnapshot: number | null;
  receivedQty: number;
  returnedQty: number;
  remainingReturnableQty: number;
  uomId: string;
  uomCode: string;
  uomName: string;
  notes: string | null;
  components: PurchaseReceiptLineComponent[];
  createdAt: string;
  updatedAt: string | null;
}

export interface PurchaseReceipt {
  id: string;
  receiptNo: string;
  supplierId: string;
  supplierCode: string;
  supplierName: string;
  warehouseId: string;
  warehouseCode: string;
  warehouseName: string;
  purchaseOrderId: string | null;
  purchaseOrderNo: string | null;
  receiptDate: string;
  supplierPayableAmount: number;
  notes: string | null;
  status: DocumentStatus;
  reversalDocumentId: string | null;
  reversedAt: string | null;
  lines: PurchaseReceiptLine[];
  createdAt: string;
  updatedAt: string | null;
}

export interface PurchaseReceiptLineComponentFormValues {
  componentItemId: string;
  expectedQty: number;
  actualReceivedQty: number;
  uomId: string;
  shortageReasonCodeId: string;
  notes: string;
}

export interface PurchaseReceiptLineFormValues {
  lineNo: number;
  purchaseOrderLineId: string;
  itemId: string;
  orderedQtySnapshot: number | "";
  receivedQty: number | "";
  uomId: string;
  notes: string;
  components: PurchaseReceiptLineComponentFormValues[];
}

export interface PurchaseReceiptFormValues {
  receiptNo: string;
  supplierId: string;
  warehouseId: string;
  purchaseOrderId: string;
  receiptDate: string;
  supplierPayableAmount: number | "";
  notes: string;
  reversalDocumentId?: string | null;
  lines: PurchaseReceiptLineFormValues[];
}

export interface PurchaseReceiptListFilters extends PaginationParams {
  search: string;
  status: string;
  source: string;
  fromDate: string;
  toDate: string;
}

function buildListUrl(filters: PurchaseReceiptListFilters): string {
  const url = new URL("/api/purchase-receipts", window.location.origin);

  if (filters.search.trim()) {
    url.searchParams.set("search", filters.search.trim());
  }

  if (filters.status) {
    url.searchParams.set("status", filters.status);
  }

  if (filters.source) {
    url.searchParams.set("linkedToPurchaseOrder", String(filters.source === "Linked"));
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
  url.searchParams.set("sortBy", filters.sortBy ?? "receiptDate");
  url.searchParams.set("sortDirection", filters.sortDirection ?? "Desc");

  return `${url.pathname}${url.search}`;
}

function normalizeDraftPayload(values: PurchaseReceiptFormValues) {
  return {
    receiptNo: values.receiptNo.trim() || null,
    supplierId: values.supplierId,
    warehouseId: values.warehouseId,
    purchaseOrderId: values.purchaseOrderId || null,
    receiptDate: values.receiptDate ? new Date(values.receiptDate).toISOString() : null,
    supplierPayableAmount: values.supplierPayableAmount === "" ? 0 : Number(values.supplierPayableAmount),
    notes: values.notes.trim() || null,
    lines: values.lines.map((line) => ({
      lineNo: Number(line.lineNo),
      purchaseOrderLineId: line.purchaseOrderLineId || null,
      itemId: line.itemId,
      orderedQtySnapshot: line.orderedQtySnapshot === "" ? null : Number(line.orderedQtySnapshot),
      receivedQty: Number(line.receivedQty),
      uomId: line.uomId,
      notes: line.notes.trim() || null,
      components: line.components.map((component) => ({
        componentItemId: component.componentItemId,
        actualReceivedQty: Number(component.actualReceivedQty),
        uomId: component.uomId,
        shortageReasonCodeId: component.shortageReasonCodeId || null,
        notes: component.notes.trim() || null,
      })),
    })),
  };
}

export function listPurchaseReceiptDrafts(filters: PurchaseReceiptListFilters) {
  return requestJson<PaginatedResult<PurchaseReceiptListItem>>(buildListUrl(filters));
}

export function getPurchaseReceiptDraft(id: string) {
  return requestJson<PurchaseReceipt>(`/api/purchase-receipts/${id}`);
}

export function listShortageReasonCodes() {
  return requestJson<ShortageReasonCode[]>("/api/shortage-reason-codes");
}

export function createPurchaseReceiptDraft(values: PurchaseReceiptFormValues) {
  return requestJson<PurchaseReceipt>("/api/purchase-receipts", {
    method: "POST",
    body: JSON.stringify(normalizeDraftPayload(values)),
  });
}

export function updatePurchaseReceiptDraft(id: string, values: PurchaseReceiptFormValues) {
  return requestJson<PurchaseReceipt>(`/api/purchase-receipts/${id}`, {
    method: "PUT",
    body: JSON.stringify(normalizeDraftPayload(values)),
  });
}

export function postPurchaseReceipt(id: string) {
  return requestJson<PurchaseReceipt>(`/api/purchase-receipts/${id}/post`, {
    method: "POST",
  });
}
