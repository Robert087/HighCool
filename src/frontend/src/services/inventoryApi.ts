import { buildApiUrl, requestJson } from "./api";

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

function buildInventoryUrl(path: string, filters: InventoryFilters): string {
  const url = new URL(buildApiUrl(path));

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

  return url.toString();
}

export function listStockLedger(filters: InventoryFilters) {
  return requestJson<StockLedgerEntry[]>(buildInventoryUrl("/api/stock-ledger", filters));
}

export function listStockBalances(filters: InventoryFilters) {
  return requestJson<StockBalance[]>(buildInventoryUrl("/api/stock-balance", filters));
}
