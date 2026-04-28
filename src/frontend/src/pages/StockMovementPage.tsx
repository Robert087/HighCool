import { useEffect, useMemo, useState } from "react";
import { Link } from "react-router-dom";
import {
  Button,
  Card,
  DataTable,
  EmptyState,
  Field,
  FilterDateRangeInline,
  FilterDropdown,
  FiltersToolbar,
  FilterTextInput,
  Input,
  PageHeader,
  Pagination,
  Select,
  SkeletonLoader,
  type FilterChip,
  useI18n,
} from "../components/ui";
import { ApiError } from "../services/api";
import { listStockLedger, type InventoryFilters, type StockLedgerEntry } from "../services/inventoryApi";
import { listItems, listWarehouses, type Item, type Warehouse } from "../services/masterDataApi";
import { formatDate, formatQuantity } from "../i18n";

const PAGE_SIZE = 15;

const INITIAL_FILTERS: InventoryFilters = {
  search: "",
  itemId: "",
  warehouseId: "",
  transactionType: "",
  fromDate: "",
  toDate: "",
};

function formatTransactionType(value: StockLedgerEntry["transactionType"], t: (key: string) => string) {
  if (value === "PurchaseReceipt") {
    return t("module.stockBalance.purchaseReceipt");
  }

  if (value === "PurchaseReceiptReversal") {
    return t("module.stockBalance.purchaseReceiptReversal");
  }

  return t("module.stockBalance.shortagePhysicalResolution");
}

export function StockMovementPage() {
  const [rows, setRows] = useState<StockLedgerEntry[]>([]);
  const [totalCount, setTotalCount] = useState(0);
  const [totalPages, setTotalPages] = useState(0);
  const [items, setItems] = useState<Item[]>([]);
  const [warehouses, setWarehouses] = useState<Warehouse[]>([]);
  const [filters, setFilters] = useState<InventoryFilters>(INITIAL_FILTERS);
  const [error, setError] = useState("");
  const [loading, setLoading] = useState(true);
  const [page, setPage] = useState(1);
  const [reloadKey, setReloadKey] = useState(0);
  const { t } = useI18n();

  useEffect(() => {
    let active = true;

    async function loadReferences() {
      try {
        const [itemsResult, warehousesResult] = await Promise.all([
          listItems("", "active"),
          listWarehouses("", "active"),
        ]);

        if (active) {
          setItems(itemsResult);
          setWarehouses(warehousesResult);
        }
      } catch {
        if (active) {
          setError(t("module.stockCard.filterError"));
        }
      }
    }

    void loadReferences();

    return () => {
      active = false;
    };
  }, []);

  useEffect(() => {
    let active = true;

    async function load() {
      try {
        setLoading(true);
        setError("");
        const result = await listStockLedger({
          filters,
          page,
          pageSize: PAGE_SIZE,
          sortBy: "transactionDate",
          sortDirection: "Desc",
        });

        if (active) {
          setRows(result.items);
          setTotalCount(result.totalCount);
          setTotalPages(result.totalPages);
        }
      } catch (loadError) {
        if (active) {
          setError(loadError instanceof ApiError ? loadError.message : t("module.stockCard.error"));
          setRows([]);
          setTotalCount(0);
          setTotalPages(0);
        }
      } finally {
        if (active) {
          setLoading(false);
        }
      }
    }

    void load();

    return () => {
      active = false;
    };
  }, [filters, page, reloadKey]);

  useEffect(() => {
    setPage(1);
  }, [filters]);

  const safePage = totalPages > 0 ? Math.min(page, totalPages) : 1;
  const hasFilters = useMemo(
    () => Object.values(filters).some((value) => value.trim().length > 0),
    [filters],
  );
  const activeFilters = useMemo(() => {
    const selectedItem = items.find((item) => item.id === filters.itemId);
    const selectedWarehouse = warehouses.find((warehouse) => warehouse.id === filters.warehouseId);
    const chips: FilterChip[] = [];

    if (filters.search.trim()) {
      chips.push({
        key: "search",
        label: t("module.stockCard.filter.searchChip", { value: filters.search.trim() }),
        onRemove: () => setFilter("search", ""),
      });
    }

    if (selectedItem) {
      chips.push({
        key: "item",
        label: t("module.stockCard.filter.itemChip", { value: `${selectedItem.code} - ${selectedItem.name}` }),
        onRemove: () => setFilter("itemId", ""),
      });
    }

    if (selectedWarehouse) {
      chips.push({
        key: "warehouse",
        label: t("module.stockCard.filter.warehouseChip", { value: `${selectedWarehouse.code} - ${selectedWarehouse.name}` }),
        onRemove: () => setFilter("warehouseId", ""),
      });
    }

    if (filters.transactionType) {
      chips.push({
        key: "transactionType",
        label: t("module.stockCard.filter.typeChip", { value: formatTransactionType(filters.transactionType as StockLedgerEntry["transactionType"], t) }),
        onRemove: () => setFilter("transactionType", ""),
      });
    }

    if (filters.fromDate || filters.toDate) {
      chips.push({
        key: "dateRange",
        label: t("module.stockCard.filter.dateChip", { from: filters.fromDate || t("common.any"), to: filters.toDate || t("common.any") }),
        onRemove: () => {
          setFilter("fromDate", "");
          setFilter("toDate", "");
        },
      });
    }

    return chips;
  }, [filters, items, warehouses]);
  const resultLabel = totalCount === 1 ? t("module.stockCard.resultLabel.one", { count: totalCount }) : t("module.stockCard.resultLabel.other", { count: totalCount });

  function setFilter<K extends keyof InventoryFilters>(key: K, value: InventoryFilters[K]) {
    setFilters((current) => ({ ...current, [key]: value }));
  }

  return (
    <section className="hc-list-page">
      <PageHeader
        title="module.stockCard"
        description="module.stockCard.description"
        eyebrow="route.section.inventory"
        actions={
          <Link className="hc-button hc-button--secondary hc-button--md" to="/stock-balances">
            {t("module.stockCard.viewStockBalance")}
          </Link>
        }
      />

      <FiltersToolbar
        activeFilters={activeFilters}
        dateRange={(
          <FilterDateRangeInline
            fromValue={filters.fromDate}
            toValue={filters.toDate}
            onFromChange={(value) => setFilter("fromDate", value)}
            onToChange={(value) => setFilter("toDate", value)}
          />
        )}
        mobileFilters={(
          <>
            <Field label="Item">
              <Select value={filters.itemId} onChange={(event) => setFilter("itemId", event.target.value)}>
                <option value="">All items</option>
                {items.map((item) => (
                  <option key={item.id} value={item.id}>
                    {item.code} - {item.name}
                  </option>
                ))}
              </Select>
            </Field>

            <Field label="Warehouse">
              <Select value={filters.warehouseId} onChange={(event) => setFilter("warehouseId", event.target.value)}>
                <option value="">All warehouses</option>
                {warehouses.map((warehouse) => (
                  <option key={warehouse.id} value={warehouse.id}>
                    {warehouse.code} - {warehouse.name}
                  </option>
                ))}
              </Select>
            </Field>

            <Field label="Transaction type">
              <Select value={filters.transactionType} onChange={(event) => setFilter("transactionType", event.target.value)}>
                <option value="">All transaction types</option>
                <option value="PurchaseReceipt">Purchase receipt</option>
                <option value="PurchaseReceiptReversal">Purchase receipt reversal</option>
                <option value="ShortagePhysicalResolution">Shortage physical resolution</option>
              </Select>
            </Field>

            <Field label="From date">
              <Input type="date" value={filters.fromDate} onChange={(event) => setFilter("fromDate", event.target.value)} />
            </Field>

            <Field label="To date">
              <Input type="date" value={filters.toDate} onChange={(event) => setFilter("toDate", event.target.value)} />
            </Field>
          </>
        )}
        onReset={() => setFilters(INITIAL_FILTERS)}
        primaryFilters={(
          <>
            <FilterDropdown aria-label="Item filter" value={filters.itemId} onChange={(event) => setFilter("itemId", event.target.value)}>
              <option value="">Item</option>
              {items.map((item) => (
                <option key={item.id} value={item.id}>
                  {item.code} - {item.name}
                </option>
              ))}
            </FilterDropdown>

            <FilterDropdown aria-label="Warehouse filter" value={filters.warehouseId} onChange={(event) => setFilter("warehouseId", event.target.value)}>
              <option value="">Warehouse</option>
              {warehouses.map((warehouse) => (
                <option key={warehouse.id} value={warehouse.id}>
                  {warehouse.code} - {warehouse.name}
                </option>
              ))}
            </FilterDropdown>
          </>
        )}
        resultLabel={resultLabel}
        search={(
          <FilterTextInput
            aria-label="Search stock movements"
            placeholder={t("module.stockCard.searchPlaceholder")}
            value={filters.search}
            onChange={(event) => setFilter("search", event.target.value)}
          />
        )}
        secondaryActiveCount={filters.transactionType ? 1 : 0}
        secondaryFilters={(
          <Field label="Transaction type">
            <Select value={filters.transactionType} onChange={(event) => setFilter("transactionType", event.target.value)}>
              <option value="">All transaction types</option>
              <option value="PurchaseReceipt">Purchase receipt</option>
              <option value="PurchaseReceiptReversal">Purchase receipt reversal</option>
              <option value="ShortagePhysicalResolution">Shortage physical resolution</option>
            </Select>
          </Field>
        )}
      />

      {error ? (
        <Card padding="md">
          <EmptyState
            title="module.stockCard.error"
            description={error}
            action={
              <Button variant="secondary" onClick={() => setReloadKey((current) => current + 1)}>
                Retry
              </Button>
            }
          />
        </Card>
      ) : null}

      {loading ? (
        <div className="hc-card hc-card--md hc-table-card">
          <div className="hc-skeleton-stack">
            <SkeletonLoader height="2.75rem" variant="rect" />
            <SkeletonLoader height="3.5rem" variant="rect" />
            <SkeletonLoader height="3.5rem" variant="rect" />
            <SkeletonLoader height="3.5rem" variant="rect" />
          </div>
        </div>
      ) : null}

      {!loading && !error ? (
        <DataTable
          hasData={rows.length > 0}
          columns={
            <tr>
              <th scope="col">Transaction date</th>
              <th scope="col">Transaction type</th>
              <th scope="col">Source document</th>
              <th scope="col">Qty in</th>
              <th scope="col">Qty out</th>
              <th scope="col">Running balance</th>
            </tr>
          }
          rows={rows.map((row) => (
            <tr key={row.id} className="hc-table__row">
              <td>
                <div className="hc-table__cell-strong hc-table__primary-cell">
                  <span className="hc-table__title">{formatDate(row.transactionDate)}</span>
                  <span className="hc-table__subtitle">{row.uomCode}</span>
                </div>
              </td>
              <td>
                <div className="hc-table__cell-strong">
                  <span className="hc-table__title">{formatTransactionType(row.transactionType, t)}</span>
                  <span className="hc-table__subtitle">{row.sourceDocType}</span>
                </div>
              </td>
              <td>
                <div className="hc-table__cell-strong">
                  <span className="hc-table__title">{row.sourceDocumentNo}</span>
                  <span className="hc-table__subtitle">{row.itemCode} / {row.warehouseCode}</span>
                </div>
              </td>
              <td><span className="hc-table__subtitle">{formatQuantity(row.qtyIn)}</span></td>
              <td><span className="hc-table__subtitle">{formatQuantity(row.qtyOut)}</span></td>
              <td>
                <div className="hc-table__cell-strong">
                  <span className="hc-table__title">{formatQuantity(row.runningBalanceQty)}</span>
                  <span className="hc-table__subtitle">{t("module.stockCard.runningBalanceLocation", { item: row.itemName, warehouse: row.warehouseName })}</span>
                </div>
              </td>
            </tr>
          ))}
          footer={<Pagination currentPage={safePage} onPageChange={setPage} pageSize={PAGE_SIZE} totalCount={totalCount} totalPages={Math.max(totalPages, 1)} />}
          emptyState={
            hasFilters ? (
              <EmptyState title="module.stockCard.emptyFiltered" description="module.stockCard.emptyFilteredDescription" />
            ) : (
              <EmptyState title="module.stockCard.empty" description="module.stockCard.emptyDescription" />
            )
          }
        />
      ) : null}
    </section>
  );
}
