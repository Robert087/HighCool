import { listStockBalances } from "./inventoryApi";
import { listSuppliers } from "./masterDataApi";
import { listPayments } from "./paymentsApi";
import { listPurchaseOrders } from "./purchaseOrdersApi";
import { listPurchaseReceiptDrafts } from "./purchaseReceiptsApi";
import { listPurchaseReturns } from "./purchaseReturnsApi";
import { listOpenShortages, listShortageResolutions } from "./shortageResolutionsApi";
import { listSupplierStatements } from "./supplierStatementsApi";

const DASHBOARD_PAGE_SIZE = 1;
const STOCK_SCAN_PAGE_SIZE = 250;
const DASHBOARD_CACHE_TTL_MS = 60_000;

let snapshotCache: { expiresAt: number; value: DashboardSnapshot } | null = null;
let inflightSnapshotPromise: Promise<DashboardSnapshot> | null = null;

export interface DashboardSnapshot {
  approvalsQueueCount: number;
  financeDraftCount: number;
  negativeStockCount: number;
  oldestApprovalDate: string | null;
  oldestSupplierIssueDate: string | null;
  oldestWarehouseDate: string | null;
  openShortageCount: number;
  openShortageQty: number;
  ordersTodayCount: number;
  pendingReceiptCount: number;
  postedReceiptsTodayCount: number;
  postedReturnsTodayCount: number;
  statementCreditsToday: number;
  statementDebitsToday: number;
  statementEntriesTodayCount: number;
  supplierIssueCount: number;
  supplierIssueNames: string[];
  unpostedTransactionCount: number;
  warehouseQueueCount: number;
}

function roundValue(value: number) {
  return Number(value.toFixed(2));
}

function toDateInputValue(value: Date) {
  const year = value.getFullYear();
  const month = String(value.getMonth() + 1).padStart(2, "0");
  const day = String(value.getDate()).padStart(2, "0");
  return `${year}-${month}-${day}`;
}

function minDate(values: Array<string | null | undefined>) {
  const timestamps = values
    .filter((value): value is string => Boolean(value))
    .map((value) => new Date(value).getTime())
    .filter((value) => Number.isFinite(value));

  if (timestamps.length === 0) {
    return null;
  }

  return new Date(Math.min(...timestamps)).toISOString();
}

export async function loadDashboardSnapshot(): Promise<DashboardSnapshot> {
  const now = Date.now();
  if (snapshotCache && snapshotCache.expiresAt > now) {
    return snapshotCache.value;
  }

  if (inflightSnapshotPromise) {
    return inflightSnapshotPromise;
  }

  inflightSnapshotPromise = loadFreshDashboardSnapshot();

  try {
    const snapshot = await inflightSnapshotPromise;
    snapshotCache = {
      expiresAt: Date.now() + DASHBOARD_CACHE_TTL_MS,
      value: snapshot,
    };
    return snapshot;
  } finally {
    inflightSnapshotPromise = null;
  }
}

async function loadFreshDashboardSnapshot(): Promise<DashboardSnapshot> {
  const today = toDateInputValue(new Date());

  const [
    openShortages,
    draftPurchaseOrders,
    todayPurchaseOrders,
    draftReceipts,
    postedReceiptsToday,
    draftReturns,
    postedReturnsToday,
    draftPayments,
    todaySupplierStatements,
    draftShortageResolutions,
    stockBalances,
    suppliers,
  ] = await Promise.all([
    listOpenShortages({
      filters: {
        search: "",
        supplierId: "",
        itemId: "",
        componentItemId: "",
        status: "",
        affectsSupplierBalance: "",
        fromDate: "",
        toDate: "",
      },
      page: 1,
      pageSize: DASHBOARD_PAGE_SIZE,
      sortBy: "receiptDate",
      sortDirection: "Asc",
    }),
    listPurchaseOrders({
      search: "",
      status: "Draft",
      receiptProgress: "",
      fromDate: "",
      toDate: "",
      page: 1,
      pageSize: DASHBOARD_PAGE_SIZE,
      sortBy: "orderDate",
      sortDirection: "Asc",
    }),
    listPurchaseOrders({
      search: "",
      status: "",
      receiptProgress: "",
      fromDate: today,
      toDate: today,
      page: 1,
      pageSize: DASHBOARD_PAGE_SIZE,
      sortBy: "orderDate",
      sortDirection: "Desc",
    }),
    listPurchaseReceiptDrafts({
      search: "",
      status: "Draft",
      source: "",
      fromDate: "",
      toDate: "",
      page: 1,
      pageSize: DASHBOARD_PAGE_SIZE,
      sortBy: "receiptDate",
      sortDirection: "Asc",
    }),
    listPurchaseReceiptDrafts({
      search: "",
      status: "Posted",
      source: "",
      fromDate: today,
      toDate: today,
      page: 1,
      pageSize: DASHBOARD_PAGE_SIZE,
      sortBy: "receiptDate",
      sortDirection: "Desc",
    }),
    listPurchaseReturns({
      search: "",
      status: "Draft",
      fromDate: "",
      toDate: "",
      page: 1,
      pageSize: DASHBOARD_PAGE_SIZE,
      sortBy: "returnDate",
      sortDirection: "Asc",
    }),
    listPurchaseReturns({
      search: "",
      status: "Posted",
      fromDate: today,
      toDate: today,
      page: 1,
      pageSize: DASHBOARD_PAGE_SIZE,
      sortBy: "returnDate",
      sortDirection: "Desc",
    }),
    listPayments({
      filters: {
        search: "",
        supplierId: "",
        direction: "",
        status: "Draft",
        paymentMethod: "",
        fromDate: "",
        toDate: "",
      },
      page: 1,
      pageSize: DASHBOARD_PAGE_SIZE,
      sortBy: "paymentDate",
      sortDirection: "Asc",
    }),
    listSupplierStatements({
      filters: {
        search: "",
        supplierId: "",
        effectType: "",
        sourceDocType: "",
        fromDate: today,
        toDate: today,
      },
      page: 1,
      pageSize: DASHBOARD_PAGE_SIZE,
      sortBy: "entryDate",
      sortDirection: "Desc",
    }),
    listShortageResolutions({
      filters: {
        search: "",
        supplierId: "",
        resolutionType: "",
        status: "Draft",
        fromDate: "",
        toDate: "",
      },
      page: 1,
      pageSize: DASHBOARD_PAGE_SIZE,
      sortBy: "resolutionDate",
      sortDirection: "Asc",
    }),
    listStockBalances({
      filters: {
        search: "",
        itemId: "",
        warehouseId: "",
        transactionType: "",
        fromDate: "",
        toDate: "",
      },
      page: 1,
      pageSize: STOCK_SCAN_PAGE_SIZE,
      sortBy: "balanceQty",
      sortDirection: "Asc",
    }),
    listSuppliers("", "all"),
  ]);

  const negativeStockCount = stockBalances.items.filter((row) => row.balanceQty < 0).length;
  const supplierIssues = suppliers.filter((supplier) => {
    const missingStatementName = supplier.statementName.trim().length === 0;
    const missingTaxNumber = (supplier.taxNumber ?? "").trim().length === 0;
    const missingPaymentTerms = (supplier.paymentTerms ?? "").trim().length === 0;
    return missingStatementName || missingTaxNumber || missingPaymentTerms;
  });

  const statementDebitsToday = roundValue(
    todaySupplierStatements.items.reduce((sum, entry) => sum + entry.debit, 0),
  );
  const statementCreditsToday = roundValue(
    todaySupplierStatements.items.reduce((sum, entry) => sum + entry.credit, 0),
  );
  const openShortageQty = roundValue(
    openShortages.items.reduce((sum, shortage) => sum + shortage.openQty, 0),
  );

  return {
    approvalsQueueCount: draftPurchaseOrders.totalCount + draftShortageResolutions.totalCount,
    financeDraftCount: draftPayments.totalCount,
    negativeStockCount,
    oldestApprovalDate: minDate([
      draftPurchaseOrders.items[0]?.orderDate,
      draftShortageResolutions.items[0]?.resolutionDate,
    ]),
    oldestSupplierIssueDate: minDate(supplierIssues.map((supplier) => supplier.createdAt)),
    oldestWarehouseDate: minDate([
      draftReceipts.items[0]?.receiptDate,
      draftReturns.items[0]?.returnDate,
    ]),
    openShortageCount: openShortages.totalCount,
    openShortageQty,
    ordersTodayCount: todayPurchaseOrders.totalCount,
    pendingReceiptCount: draftReceipts.totalCount,
    postedReceiptsTodayCount: postedReceiptsToday.totalCount,
    postedReturnsTodayCount: postedReturnsToday.totalCount,
    statementCreditsToday,
    statementDebitsToday,
    statementEntriesTodayCount: todaySupplierStatements.totalCount,
    supplierIssueCount: supplierIssues.length,
    supplierIssueNames: supplierIssues.slice(0, 3).map((supplier) => supplier.statementName || supplier.name),
    unpostedTransactionCount:
      draftPurchaseOrders.totalCount +
      draftReceipts.totalCount +
      draftReturns.totalCount +
      draftPayments.totalCount +
      draftShortageResolutions.totalCount,
    warehouseQueueCount: draftReceipts.totalCount + draftReturns.totalCount,
  };
}
