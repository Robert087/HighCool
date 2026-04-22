import { useEffect, useMemo, useState } from "react";
import { Link } from "react-router-dom";
import { RowActions } from "../components/patterns";
import {
  Badge,
  Button,
  DataTable,
  EmptyState,
  Field,
  FilterDateRangeInline,
  FilterDropdown,
  FiltersToolbar,
  FilterTextInput,
  Input,
  Pagination,
  SkeletonLoader,
  PageHeader,
  Select,
  type FilterChip,
  useToast,
} from "../components/ui";
import { ApiError } from "../services/api";
import { listPurchaseReceiptDrafts, type PurchaseReceiptListItem } from "../services/purchaseReceiptsApi";

const PAGE_SIZE = 10;
const INITIAL_FILTERS = {
  status: "",
  source: "",
  fromDate: "",
  toDate: "",
};

export function PurchaseReceiptsPage() {
  const [rows, setRows] = useState<PurchaseReceiptListItem[]>([]);
  const [search, setSearch] = useState("");
  const [filters, setFilters] = useState(INITIAL_FILTERS);
  const [error, setError] = useState("");
  const [loading, setLoading] = useState(true);
  const [page, setPage] = useState(1);
  const [reloadKey, setReloadKey] = useState(0);
  const { showToast } = useToast();

  function handleDelete(row: PurchaseReceiptListItem) {
    if (row.status === "Posted") {
      showToast({
        tone: "warning",
        title: "Cannot delete posted receipt",
        description: "Posted receipts cannot be deleted from the UI.",
      });
      return;
    }

    if (window.confirm("Delete this purchase receipt? Deletion is not available in this version.")) {
      showToast({
        tone: "info",
        title: "Delete action unavailable",
        description: "Permanent delete is not supported in this UI yet.",
      });
    }
  }

  useEffect(() => {
    let active = true;

    async function load() {
      try {
        setLoading(true);
        setError("");
        const result = await listPurchaseReceiptDrafts(search);

        if (active) {
          setRows(result);
        }
      } catch (loadError) {
        if (active) {
          setError(loadError instanceof ApiError ? loadError.message : "Failed to load purchase receipts.");
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
  }, [reloadKey, search]);

  useEffect(() => {
    setPage(1);
  }, [search, filters]);

  const filteredRows = useMemo(() => rows.filter((row) => {
    if (filters.status && row.status !== filters.status) {
      return false;
    }

    if (filters.source === "Manual" && row.purchaseOrderId) {
      return false;
    }

    if (filters.source === "Linked" && !row.purchaseOrderId) {
      return false;
    }

    if (filters.fromDate) {
      const rowDate = new Date(row.receiptDate);
      const fromDate = new Date(filters.fromDate);
      if (rowDate < fromDate) {
        return false;
      }
    }

    if (filters.toDate) {
      const rowDate = new Date(row.receiptDate);
      const toDate = new Date(filters.toDate);
      toDate.setHours(23, 59, 59, 999);
      if (rowDate > toDate) {
        return false;
      }
    }

    return true;
  }), [filters, rows]);
  const activeFilters = useMemo(() => {
    const chips: FilterChip[] = [];

    if (search.trim()) {
      chips.push({
        key: "search",
        label: `Search: ${search.trim()}`,
        onRemove: () => setSearch(""),
      });
    }

    if (filters.status) {
      chips.push({
        key: "status",
        label: `Status: ${filters.status}`,
        onRemove: () => setFilters((current) => ({ ...current, status: "" })),
      });
    }

    if (filters.source) {
      chips.push({
        key: "source",
        label: `Source: ${filters.source === "Linked" ? "PO linked" : "Manual"}`,
        onRemove: () => setFilters((current) => ({ ...current, source: "" })),
      });
    }

    if (filters.fromDate || filters.toDate) {
      chips.push({
        key: "dateRange",
        label: `Receipt date: ${filters.fromDate || "Any"} to ${filters.toDate || "Any"}`,
        onRemove: () => setFilters((current) => ({ ...current, fromDate: "", toDate: "" })),
      });
    }

    return chips;
  }, [filters, search]);
  const hasFilters = activeFilters.length > 0;
  const totalPages = Math.max(1, Math.ceil(filteredRows.length / PAGE_SIZE));
  const safePage = Math.min(page, totalPages);
  const pageStart = (safePage - 1) * PAGE_SIZE;
  const visibleRows = filteredRows.slice(pageStart, pageStart + PAGE_SIZE);
  const resultLabel = filteredRows.length === 1 ? "1 purchase receipt" : `${filteredRows.length} purchase receipts`;

  return (
    <section className="hc-list-page">
      <PageHeader
        title="Purchase Receipts"
        actions={
          <Link className="hc-button hc-button--primary hc-button--md" to="/purchase-receipts/new">
            New purchase receipt
          </Link>
        }
      />

      <FiltersToolbar
        activeFilters={activeFilters}
        dateRange={(
          <FilterDateRangeInline
            fromValue={filters.fromDate}
            toValue={filters.toDate}
            onFromChange={(value) => setFilters((current) => ({ ...current, fromDate: value }))}
            onToChange={(value) => setFilters((current) => ({ ...current, toDate: value }))}
          />
        )}
        mobileFilters={(
          <>
            <Field label="Status">
              <Select value={filters.status} onChange={(event) => setFilters((current) => ({ ...current, status: event.target.value }))}>
                <option value="">All statuses</option>
                <option value="Draft">Draft</option>
                <option value="Posted">Posted</option>
                <option value="Canceled">Canceled</option>
              </Select>
            </Field>
            <Field label="Source">
              <Select value={filters.source} onChange={(event) => setFilters((current) => ({ ...current, source: event.target.value }))}>
                <option value="">All sources</option>
                <option value="Linked">PO linked</option>
                <option value="Manual">Manual</option>
              </Select>
            </Field>
            <Field label="From date">
              <Input type="date" value={filters.fromDate} onChange={(event) => setFilters((current) => ({ ...current, fromDate: event.target.value }))} />
            </Field>
            <Field label="To date">
              <Input type="date" value={filters.toDate} onChange={(event) => setFilters((current) => ({ ...current, toDate: event.target.value }))} />
            </Field>
          </>
        )}
        onReset={() => {
          setSearch("");
          setFilters(INITIAL_FILTERS);
        }}
        primaryFilters={(
          <>
            <FilterDropdown aria-label="Purchase receipt status filter" value={filters.status} onChange={(event) => setFilters((current) => ({ ...current, status: event.target.value }))}>
              <option value="">Status</option>
              <option value="Draft">Draft</option>
              <option value="Posted">Posted</option>
              <option value="Canceled">Canceled</option>
            </FilterDropdown>
            <FilterDropdown aria-label="Purchase receipt source filter" value={filters.source} onChange={(event) => setFilters((current) => ({ ...current, source: event.target.value }))}>
              <option value="">Source</option>
              <option value="Linked">PO linked</option>
              <option value="Manual">Manual</option>
            </FilterDropdown>
          </>
        )}
        resultLabel={resultLabel}
        search={(
          <FilterTextInput
            aria-label="Search purchase receipts"
            placeholder="Search receipt no, supplier, warehouse, PO, or notes"
            value={search}
            onChange={(event) => setSearch(event.target.value)}
          />
        )}
        mobileTriggerOnly
      />

      {error ? (
        <div className="hc-card hc-card--md">
          <EmptyState
            title="Unable to load purchase receipts"
            description={error}
            action={<Button variant="secondary" onClick={() => setReloadKey((current) => current + 1)}>Retry</Button>}
          />
        </div>
      ) : null}

      {loading ? (
        <div className="hc-card hc-card--md hc-table-card">
          <div className="hc-skeleton-stack">
            <SkeletonLoader height="2.75rem" variant="rect" />
            <SkeletonLoader height="3.5rem" variant="rect" />
            <SkeletonLoader height="3.5rem" variant="rect" />
          </div>
        </div>
      ) : null}

      {!loading && !error ? (
        <DataTable
          hasData={filteredRows.length > 0}
          columns={
            <tr>
              <th scope="col">Receipt</th>
              <th scope="col">Supplier</th>
              <th scope="col">Receipt Context</th>
              <th scope="col">Status</th>
              <th scope="col" className="hc-table__head-actions" aria-label="Actions" />
            </tr>
          }
          rows={visibleRows.map((row) => (
            <tr key={row.id} className="hc-table__row">
              <td>
                <div className="hc-table__cell-strong hc-table__primary-cell">
                  <span className="hc-table__title">{row.receiptNo}</span>
                  <span className="hc-table__subtitle">{row.lineCount} {row.lineCount === 1 ? "line" : "lines"}</span>
                </div>
              </td>
              <td>
                <div className="hc-table__cell-strong">
                  <span className="hc-table__title">{row.supplierName}</span>
                  <span className="hc-table__subtitle">{row.supplierCode}</span>
                </div>
              </td>
              <td>
                <div className="hc-table__stack">
                  <div className="hc-table__cell-strong">
                    <span className="hc-table__title">{row.warehouseName}</span>
                    <span className="hc-table__subtitle">{row.warehouseCode}</span>
                  </div>
                  <div className="hc-table__metric">
                    <span className="hc-table__metric-label">Source</span>
                    <span className="hc-table__subtitle">{row.purchaseOrderNo ?? "Manual"}</span>
                  </div>
                  <div className="hc-table__metric">
                    <span className="hc-table__metric-label">Date</span>
                    <span className="hc-table__subtitle">{new Date(row.receiptDate).toLocaleDateString()}</span>
                  </div>
                </div>
              </td>
              <td>
                <div className="hc-table__status-stack">
                  <Badge tone={row.status === "Posted" ? "success" : row.status === "Canceled" ? "danger" : "warning"}>{row.status}</Badge>
                </div>
              </td>
              <td className="hc-table__cell-actions">
                <RowActions
                  primaryAction={<Link className="hc-button hc-button--secondary hc-button--sm hc-table__action-button" to={`/purchase-receipts/${row.id}/edit`}>View</Link>}
                  menuItems={[
                    ...(row.status === "Draft" ? [{ label: "Edit", to: `/purchase-receipts/${row.id}/edit` }] : []),
                    ...(row.status === "Draft" ? [{ label: "Delete", onSelect: () => handleDelete(row), tone: "danger" as const }] : []),
                  ]}
                />
              </td>
            </tr>
          ))}
          footer={<Pagination currentPage={safePage} onPageChange={setPage} pageSize={PAGE_SIZE} totalCount={filteredRows.length} totalPages={totalPages} />}
          emptyState={hasFilters
            ? <EmptyState title="No purchase receipts match the current filters" description="Try a broader search term or clear one of the filters." />
            : <EmptyState title="No purchase receipts yet" description="Create your first purchase receipt." action={<Link className="hc-button hc-button--primary hc-button--md" to="/purchase-receipts/new">Create purchase receipt</Link>} />}
        />
      ) : null}
    </section>
  );
}
