import { requestJson, type PaginatedResult, type PaginationParams } from "./api";
import type { DocumentStatus, PurchaseReceipt } from "./purchaseReceiptsApi";

export interface PurchaseReturnListItem {
  id: string;
  returnNo: string;
  supplierId: string;
  supplierCode: string;
  supplierName: string;
  referenceReceiptId: string | null;
  referenceReceiptNo: string | null;
  returnDate: string;
  status: DocumentStatus;
  lineCount: number;
  reversalDocumentId: string | null;
  reversedAt: string | null;
  createdAt: string;
  updatedAt: string | null;
}

export interface PurchaseReturnLine {
  id: string;
  lineNo: number;
  itemId: string;
  itemCode: string;
  itemName: string;
  componentId: string | null;
  componentCode: string | null;
  componentName: string | null;
  warehouseId: string;
  warehouseCode: string;
  warehouseName: string;
  returnQty: number;
  remainingReturnableQty: number;
  uomId: string;
  uomCode: string;
  uomName: string;
  baseQty: number;
  referenceReceiptLineId: string | null;
  referenceReceiptLineNo: number | null;
  createdAt: string;
  updatedAt: string | null;
}

export interface PurchaseReturn {
  id: string;
  returnNo: string;
  supplierId: string;
  supplierCode: string;
  supplierName: string;
  referenceReceiptId: string | null;
  referenceReceiptNo: string | null;
  returnDate: string;
  notes: string | null;
  status: DocumentStatus;
  reversalDocumentId: string | null;
  reversedAt: string | null;
  lines: PurchaseReturnLine[];
  createdAt: string;
  updatedAt: string | null;
}

export interface PurchaseReturnLineFormValues {
  lineNo: number;
  itemId: string;
  componentId: string;
  warehouseId: string;
  returnQty: number | "";
  remainingReturnableQty: number;
  uomId: string;
  referenceReceiptLineId: string;
}

export interface PurchaseReturnFormValues {
  returnNo: string;
  supplierId: string;
  referenceReceiptId: string;
  returnDate: string;
  notes: string;
  lines: PurchaseReturnLineFormValues[];
}

export interface PurchaseReturnListFilters extends PaginationParams {
  search: string;
  status: string;
  fromDate: string;
  toDate: string;
}

function buildListUrl(filters: PurchaseReturnListFilters) {
  const url = new URL("/api/purchase-returns", window.location.origin);

  if (filters.search.trim()) {
    url.searchParams.set("search", filters.search.trim());
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
  url.searchParams.set("sortBy", filters.sortBy ?? "returnDate");
  url.searchParams.set("sortDirection", filters.sortDirection ?? "Desc");

  return `${url.pathname}${url.search}`;
}

function normalizePayload(values: PurchaseReturnFormValues) {
  return {
    returnNo: values.returnNo.trim() || null,
    supplierId: values.supplierId,
    referenceReceiptId: values.referenceReceiptId || null,
    returnDate: values.returnDate ? new Date(values.returnDate).toISOString() : null,
    notes: values.notes.trim() || null,
    lines: values.lines.map((line) => ({
      lineNo: Number(line.lineNo),
      itemId: line.itemId,
      componentId: line.componentId || null,
      warehouseId: line.warehouseId,
      returnQty: line.returnQty === "" ? 0 : Number(line.returnQty),
      uomId: line.uomId,
      referenceReceiptLineId: line.referenceReceiptLineId || null,
    })),
  };
}

export function listPurchaseReturns(filters: PurchaseReturnListFilters) {
  return requestJson<PaginatedResult<PurchaseReturnListItem>>(buildListUrl(filters));
}

export function getPurchaseReturn(id: string) {
  return requestJson<PurchaseReturn>(`/api/purchase-returns/${id}`);
}

export function createPurchaseReturn(values: PurchaseReturnFormValues) {
  return requestJson<PurchaseReturn>("/api/purchase-returns", {
    method: "POST",
    body: JSON.stringify(normalizePayload(values)),
  });
}

export function updatePurchaseReturn(id: string, values: PurchaseReturnFormValues) {
  return requestJson<PurchaseReturn>(`/api/purchase-returns/${id}`, {
    method: "PUT",
    body: JSON.stringify(normalizePayload(values)),
  });
}

export function postPurchaseReturn(id: string) {
  return requestJson<PurchaseReturn>(`/api/purchase-returns/${id}/post`, {
    method: "POST",
  });
}

export function mapPurchaseReturnToFormValues(document: PurchaseReturn): PurchaseReturnFormValues {
  return {
    returnNo: document.returnNo,
    supplierId: document.supplierId,
    referenceReceiptId: document.referenceReceiptId ?? "",
    returnDate: document.returnDate.slice(0, 10),
    notes: document.notes ?? "",
    lines: document.lines.map((line) => ({
      lineNo: line.lineNo,
      itemId: line.itemId,
      componentId: line.componentId ?? "",
      warehouseId: line.warehouseId,
      returnQty: line.returnQty,
      remainingReturnableQty: typeof line.remainingReturnableQty === "number" && Number.isFinite(line.remainingReturnableQty)
        ? line.remainingReturnableQty
        : 0,
      uomId: line.uomId,
      referenceReceiptLineId: line.referenceReceiptLineId ?? "",
    })),
  };
}

export function buildReceiptLineLookup(receipt: PurchaseReceipt | null) {
  return new Map((receipt?.lines ?? []).map((line) => [line.id, line]));
}
