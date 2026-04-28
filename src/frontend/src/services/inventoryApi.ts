import { requestJson, type PaginatedResult, type PaginationParams } from "./api";

export type StockTransactionType = "PurchaseReceipt" | "PurchaseReceiptReversal" | "ShortagePhysicalResolution";
export type SourceDocumentType = "PurchaseReceipt" | "PurchaseReceiptReversal" | "ShortageResolution";

export interface InventoryFilters {
  search: string;
  itemId: string;
  warehouseId: string;
  transactionType: string;
  fromDate: string;
  toDate: string;
}

export interface InventoryListRequest extends PaginationParams {
  filters: InventoryFilters;
}

export interface StockLedgerEntry {
  id: string;
  itemId: string;
  itemCode: string;
  itemName: string;
  warehouseId: string;
  warehouseCode: string;
  warehouseName: string;
  transactionType: StockTransactionType;
  sourceDocType: SourceDocumentType;
  sourceDocId: string;
  sourceLineId: string | null;
  sourceDocumentNo: string;
  qtyIn: number;
  qtyOut: number;
  uomId: string;
  uomCode: string;
  uomName: string;
  baseQty: number;
  runningBalanceQty: number;
  transactionDate: string;
  unitCost: number | null;
  totalCost: number | null;
  createdAt: string;
  createdBy: string;
}

export interface StockBalance {
  itemId: string;
  itemCode: string;
  itemName: string;
  warehouseId: string;
  warehouseCode: string;
  warehouseName: string;
  baseUomId: string;
  baseUomCode: string;
  baseUomName: string;
  balanceQty: number;
  lastTransactionDate: string | null;
}

function buildInventoryUrl(path: string, request: InventoryListRequest): string {
  const url = new URL(path, window.location.origin);
  const { filters } = request;

  if (filters.search.trim()) {
    url.searchParams.set("search", filters.search.trim());
  }

  if (filters.itemId) {
    url.searchParams.set("itemId", filters.itemId);
  }

  if (filters.warehouseId) {
    url.searchParams.set("warehouseId", filters.warehouseId);
  }

  if (filters.transactionType) {
    url.searchParams.set("transactionType", filters.transactionType);
  }

  if (filters.fromDate) {
    url.searchParams.set("fromDate", new Date(filters.fromDate).toISOString());
  }

  if (filters.toDate) {
    const endOfDay = new Date(filters.toDate);
    endOfDay.setHours(23, 59, 59, 999);
    url.searchParams.set("toDate", endOfDay.toISOString());
  }

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

export function listStockLedger(request: InventoryListRequest) {
  return requestJson<PaginatedResult<StockLedgerEntry>>(buildInventoryUrl("/api/stock-ledger", request));
}

export function listStockBalances(request: InventoryListRequest) {
  return requestJson<PaginatedResult<StockBalance>>(buildInventoryUrl("/api/stock-balance", request));
}
