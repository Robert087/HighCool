import { requestJson } from "./api";

export type SupplierStatementEffectType = "PurchaseReceipt" | "ShortageFinancialResolution" | "Payment";
export type SupplierStatementSourceDocumentType = "PurchaseReceipt" | "ShortageResolution" | "Payment";

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

function buildQuery(url: URL, filters: SupplierStatementFilters) {
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
  return `${url.pathname}${url.search}`;
}

export function listSupplierStatements(filters: SupplierStatementFilters) {
  const url = new URL("/api/supplier-statements", window.location.origin);
  return requestJson<SupplierStatementEntry[]>(buildQuery(url, filters));
}

export function getSupplierStatement(supplierId: string, filters: SupplierStatementFilters) {
  const url = new URL(`/api/suppliers/${supplierId}/statement`, window.location.origin);
  return requestJson<SupplierStatementEntry[]>(buildQuery(url, { ...filters, supplierId }));
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
