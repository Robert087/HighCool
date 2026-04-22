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
} from "../components/ui";
import { ApiError } from "../services/api";
import { listStockLedger, type InventoryFilters, type StockLedgerEntry } from "../services/inventoryApi";
import { listItems, listWarehouses, type Item, type Warehouse } from "../services/masterDataApi";

const PAGE_SIZE = 15;

const INITIAL_FILTERS: InventoryFilters = {
  search: "",
  itemId: "",
  warehouseId: "",
  transactionType: "",
  fromDate: "",
  toDate: "",
};

function formatTransactionType(value: StockLedgerEntry["transactionType"]) {
  if (value === "PurchaseReceipt") {
    return "Purchase receipt";
  }

  if (value === "PurchaseReceiptReversal") {
    return "Purchase receipt reversal";
  }

  return "Shortage physical resolution";
}

export function StockMovementPage() {
  const [rows, setRows] = useState<StockLedgerEntry[]>([]);
  const [items, setItems] = useState<Item[]>([]);
  const [warehouses, setWarehouses] = useState<Warehouse[]>([]);
  const [filters, setFilters] = useState<InventoryFilters>(INITIAL_FILTERS);
  const [error, setError] = useState("");
  const [loading, setLoading] = useState(true);
  const [page, setPage] = useState(1);
  const [reloadKey, setReloadKey] = useState(0);

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
          setError("Failed to load stock movement filters.");
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
        const result = await listStockLedger(filters);

        if (active) {
          setRows(result);
        }
      } catch (loadError) {
        if (active) {
          setError(loadError instanceof ApiError ? loadError.message : "Failed to load stock movements.");
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
  }, [filters, reloadKey]);

  useEffect(() => {
    setPage(1);
  }, [filters]);

  const totalPages = Math.max(1, Math.ceil(rows.length / PAGE_SIZE));
  const safePage = Math.min(page, totalPages);
  const pageStart = (safePage - 1) * PAGE_SIZE;
  const visibleRows = rows.slice(pageStart, pageStart + PAGE_SIZE);
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
        label: `Search: ${filters.search.trim()}`,
        onRemove: () => setFilter("search", ""),
      });
    }

    if (selectedItem) {
      chips.push({
        key: "item",
        label: `Item: ${selectedItem.code} - ${selectedItem.name}`,
        onRemove: () => setFilter("itemId", ""),
      });
    }

    if (selectedWarehouse) {
      chips.push({
        key: "warehouse",
        label: `Warehouse: ${selectedWarehouse.code} - ${selectedWarehouse.name}`,
        onRemove: () => setFilter("warehouseId", ""),
      });
    }

    if (filters.transactionType) {
      chips.push({
        key: "transactionType",
        label: `Type: ${formatTransactionType(filters.transactionType as StockLedgerEntry["transactionType"])}`,
        onRemove: () => setFilter("transactionType", ""),
      });
    }

    if (filters.fromDate || filters.toDate) {
      chips.push({
        key: "dateRange",
        label: `Date: ${filters.fromDate || "Any"} to ${filters.toDate || "Any"}`,
        onRemove: () => {
          setFilter("fromDate", "");
          setFilter("toDate", "");
        },
      });
    }

    return chips;
  }, [filters, items, warehouses]);
  const resultLabel = rows.length === 1 ? "1 movement row" : `${rows.length} movement rows`;

  function setFilter<K extends keyof InventoryFilters>(key: K, value: InventoryFilters[K]) {
    setFilters((current) => ({ ...current, [key]: value }));
  }

  return (
    <section className="hc-list-page">
      <PageHeader
        title="Stock Card"
        description="Trace stock movement history and running balances from append-only ledger entries."
        eyebrow="Inventory"
        actions={
          <Link className="hc-button hc-button--secondary hc-button--md" to="/stock-balances">
            View stock balance
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
            placeholder="Search item code, item name, warehouse"
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
            title="Unable to load stock movements"
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
          rows={visibleRows.map((row) => (
            <tr key={row.id} className="hc-table__row">
              <td>
                <div className="hc-table__cell-strong hc-table__primary-cell">
                  <span className="hc-table__title">{new Date(row.transactionDate).toLocaleDateString()}</span>
                  <span className="hc-table__subtitle">{row.uomCode}</span>
                </div>
              </td>
              <td>
                <div className="hc-table__cell-strong">
                  <span className="hc-table__title">{formatTransactionType(row.transactionType)}</span>
                  <span className="hc-table__subtitle">{row.sourceDocType}</span>
                </div>
              </td>
              <td>
                <div className="hc-table__cell-strong">
                  <span className="hc-table__title">{row.sourceDocumentNo}</span>
                  <span className="hc-table__subtitle">{row.itemCode} / {row.warehouseCode}</span>
                </div>
              </td>
              <td><span className="hc-table__subtitle">{row.qtyIn.toLocaleString()}</span></td>
              <td><span className="hc-table__subtitle">{row.qtyOut.toLocaleString()}</span></td>
              <td>
                <div className="hc-table__cell-strong">
                  <span className="hc-table__title">{row.runningBalanceQty.toLocaleString()}</span>
                  <span className="hc-table__subtitle">{row.itemName} in {row.warehouseName}</span>
                </div>
              </td>
            </tr>
          ))}
          footer={<Pagination currentPage={safePage} onPageChange={setPage} pageSize={PAGE_SIZE} totalCount={rows.length} totalPages={totalPages} />}
          emptyState={
            hasFilters ? (
              <EmptyState title="No stock movements match the current filters" description="Try a broader search or clear one of the filters." />
            ) : (
              <EmptyState title="No stock movements yet" description="Movement history appears after posted inventory documents write ledger rows." />
            )
          }
        />
      ) : null}
    </section>
  );
}
