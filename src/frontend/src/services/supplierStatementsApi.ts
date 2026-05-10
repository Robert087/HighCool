import { requestJson, type PaginatedResult, type PaginationParams } from "./api";

export type SupplierStatementEffectType =
  | "PurchaseReceipt"
  | "PurchaseReturn"
  | "ShortageFinancialResolution"
  | "Payment"
  | "PurchaseReceiptReversal"
  | "PaymentReversal"
  | "ShortageResolutionReversal";

export type SupplierStatementSourceDocumentType =
  | "PurchaseReceipt"
  | "PurchaseReturn"
  | "ShortageFinancialResolution"
  | "Payment"
  | "PurchaseReceiptReversal"
  | "PaymentReversal"
  | "ShortageResolutionReversal"
  | "ShortageResolution"
  | "DocumentReversal";

export interface SupplierStatementFilters {
  search: string;
  supplierId: string;
  effectType: string;
  sourceDocType: string;
  fromDate: string;
  toDate: string;
}

export interface SupplierStatementEntry {
  id: string;
  supplierId: string;
  supplierCode: string;
  supplierName: string;
  entryDate: string;
  sourceDocType: SupplierStatementSourceDocumentType;
  sourceDocId: string;
  sourceLineId: string | null;
  sourceSequenceNo: number | null;
  sourceDocumentNo: string;
  effectType: SupplierStatementEffectType;
  debit: number;
  credit: number;
  runningBalance: number;
  currency: string | null;
  notes: string | null;
  createdAt: string;
  createdBy: string;
}

export interface SupplierStatementSummary {
  supplierId: string;
  supplierCode: string;
  supplierName: string;
  fromDate: string | null;
  toDate: string | null;
  currentBalance: number;
  openingBalance: number;
  closingBalance: number;
  totalDebit: number;
  totalCredit: number;
  entryCount: number;
}

export interface SupplierStatementListRequest extends PaginationParams {
  filters: SupplierStatementFilters;
}

function applyDateFilters(url: URL, fromDate: string, toDate: string) {
  if (fromDate) {
    url.searchParams.set("fromDate", new Date(fromDate).toISOString());
  }

  if (toDate) {
    const endOfDay = new Date(toDate);
    endOfDay.setHours(23, 59, 59, 999);
    url.searchParams.set("toDate", endOfDay.toISOString());
  }
}

function buildQuery(url: URL, request: SupplierStatementListRequest) {
  const { filters } = request;
  if (filters.search.trim()) {
    url.searchParams.set("search", filters.search.trim());
  }

  if (filters.supplierId) {
    url.searchParams.set("supplierId", filters.supplierId);
  }

  if (filters.effectType) {
    url.searchParams.set("effectType", filters.effectType);
  }

  if (filters.sourceDocType) {
    url.searchParams.set("sourceDocType", filters.sourceDocType);
  }

  applyDateFilters(url, filters.fromDate, filters.toDate);
  url.searchParams.set("page", String(request.page));
  url.searchParams.set("pageSize", String(request.pageSize));
  if (request.sortBy) {
    url.searchParams.set("sortBy", request.sortBy);
  }

  if (request.sortDirection) {
    url.searchParams.set("sortDirection", request.sortDirection);
  }

  return `${url.pathname}${url.search}`;
}

export function listSupplierStatements(request: SupplierStatementListRequest) {
  const url = new URL("/api/supplier-statements", window.location.origin);
  return requestJson<PaginatedResult<SupplierStatementEntry>>(buildQuery(url, request));
}

export function getSupplierStatement(supplierId: string, request: SupplierStatementListRequest) {
  const url = new URL(`/api/suppliers/${supplierId}/statement`, window.location.origin);
  return requestJson<PaginatedResult<SupplierStatementEntry>>(buildQuery(url, { ...request, filters: { ...request.filters, supplierId } }));
}

export function getSupplierStatementSummary(supplierId: string, filters: SupplierStatementFilters) {
  const url = new URL(`/api/suppliers/${supplierId}/statement/summary`, window.location.origin);
  if (filters.effectType) {
    url.searchParams.set("effectType", filters.effectType);
  }

  if (filters.sourceDocType) {
    url.searchParams.set("sourceDocType", filters.sourceDocType);
  }

  applyDateFilters(url, filters.fromDate, filters.toDate);
  return requestJson<SupplierStatementSummary>(`${url.pathname}${url.search}`);
}
