import { useEffect, useMemo, useState } from "react";
import { Link } from "react-router-dom";
import {
  Badge,
  Button,
  Card,
  DataTable,
  EmptyState,
  Field,
  Input,
  PageHeader,
  Pagination,
  Select,
  SkeletonLoader,
} from "../components/ui";
import { ApiError } from "../services/api";
import { listItems, listSuppliers, type Item, type Supplier } from "../services/masterDataApi";
import {
  listOpenShortages,
  type OpenShortage,
  type OpenShortageFilters,
} from "../services/shortageResolutionsApi";

const PAGE_SIZE = 15;

const INITIAL_FILTERS: OpenShortageFilters = {
  search: "",
  supplierId: "",
  itemId: "",
  componentItemId: "",
  status: "",
  affectsSupplierBalance: "",
  fromDate: "",
  toDate: "",
};

export function OpenShortagesPage() {
  const [rows, setRows] = useState<OpenShortage[]>([]);
  const [suppliers, setSuppliers] = useState<Supplier[]>([]);
  const [items, setItems] = useState<Item[]>([]);
  const [filters, setFilters] = useState<OpenShortageFilters>(INITIAL_FILTERS);
  const [error, setError] = useState("");
  const [loading, setLoading] = useState(true);
  const [page, setPage] = useState(1);
  const [reloadKey, setReloadKey] = useState(0);

  useEffect(() => {
    let active = true;

    async function loadReferences() {
      try {
        const [supplierRows, itemRows] = await Promise.all([
          listSuppliers("", "active"),
          listItems("", "active"),
        ]);

        if (active) {
          setSuppliers(supplierRows);
          setItems(itemRows);
        }
      } catch {
        if (active) {
          setError("Failed to load shortage filters.");
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
        const result = await listOpenShortages(filters);

        if (active) {
          setRows(result);
        }
      } catch (loadError) {
        if (active) {
          setError(loadError instanceof ApiError ? loadError.message : "Failed to load open shortages.");
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
  const resultLabel = rows.length === 1 ? "1 open shortage" : `${rows.length} open shortages`;
  const hasFilters = useMemo(
    () => Object.values(filters).some((value) => value.trim().length > 0),
    [filters],
  );

  function setFilter<K extends keyof OpenShortageFilters>(key: K, value: OpenShortageFilters[K]) {
    setFilters((current) => ({ ...current, [key]: value }));
  }

  return (
    <section className="hc-list-page">
      <PageHeader
        title="Open Shortages"
        description="Review unresolved receipt shortages before settling them physically or financially through shortage resolutions."
        eyebrow="Inventory"
        actions={
          <Link className="hc-button hc-button--primary hc-button--md" to="/shortage-resolutions/new">
            New shortage resolution
          </Link>
        }
      />

      <Card className="hc-inventory-filter-panel" padding="md">
        <div className="hc-inventory-filter-panel__top">
          <div>
            <h2 className="hc-inventory-filter-panel__title">Filters</h2>
            <p className="hc-inventory-filter-panel__description">Search supplier shortages and narrow the rows that still carry open quantity or open value.</p>
          </div>
          <div className="hc-inventory-filter-panel__meta">
            <span className="hc-inventory-filter-panel__result">{resultLabel}</span>
            {hasFilters ? (
              <Button size="sm" variant="ghost" onClick={() => setFilters(INITIAL_FILTERS)}>
                Reset filters
              </Button>
            ) : null}
          </div>
        </div>

        <div className="hc-form-grid hc-inventory-filter-grid">
          <Field label="Search">
            <Input
              placeholder="Search supplier, receipt, item, component"
              value={filters.search}
              onChange={(event) => setFilter("search", event.target.value)}
            />
          </Field>

          <Field label="Supplier">
            <Select value={filters.supplierId} onChange={(event) => setFilter("supplierId", event.target.value)}>
              <option value="">All suppliers</option>
              {suppliers.map((supplier) => (
                <option key={supplier.id} value={supplier.id}>
                  {supplier.code} - {supplier.name}
                </option>
              ))}
            </Select>
          </Field>

          <Field label="Parent item">
            <Select value={filters.itemId} onChange={(event) => setFilter("itemId", event.target.value)}>
              <option value="">All parent items</option>
              {items.map((item) => (
                <option key={item.id} value={item.id}>
                  {item.code} - {item.name}
                </option>
              ))}
            </Select>
          </Field>

          <Field label="Component">
            <Select value={filters.componentItemId} onChange={(event) => setFilter("componentItemId", event.target.value)}>
              <option value="">All components</option>
              {items.map((item) => (
                <option key={item.id} value={item.id}>
                  {item.code} - {item.name}
                </option>
              ))}
            </Select>
          </Field>

          <Field label="Status">
            <Select value={filters.status} onChange={(event) => setFilter("status", event.target.value)}>
              <option value="">All open statuses</option>
              <option value="Open">Open</option>
              <option value="PartiallyResolved">Partially resolved</option>
            </Select>
          </Field>

          <Field label="Supplier balance">
            <Select value={filters.affectsSupplierBalance} onChange={(event) => setFilter("affectsSupplierBalance", event.target.value)}>
              <option value="">All rows</option>
              <option value="yes">Affects supplier balance</option>
              <option value="no">Internal only</option>
            </Select>
          </Field>

          <Field label="From date">
            <Input type="date" value={filters.fromDate} onChange={(event) => setFilter("fromDate", event.target.value)} />
          </Field>

          <Field label="To date">
            <Input type="date" value={filters.toDate} onChange={(event) => setFilter("toDate", event.target.value)} />
          </Field>
        </div>
      </Card>

      {error ? (
        <Card padding="md">
          <EmptyState
            title="Unable to load open shortages"
            description={error}
            action={<Button variant="secondary" onClick={() => setReloadKey((current) => current + 1)}>Retry</Button>}
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
              <th scope="col">Supplier</th>
              <th scope="col">Receipt</th>
              <th scope="col">Item</th>
              <th scope="col">Component</th>
              <th scope="col">Shortage qty</th>
              <th scope="col">Resolved qty</th>
              <th scope="col">Physical / Financial</th>
              <th scope="col">Open qty</th>
              <th scope="col">Open amount</th>
              <th scope="col">Status</th>
              <th scope="col">Supplier impact</th>
              <th scope="col">Reason</th>
            </tr>
          }
          rows={visibleRows.map((row) => (
            <tr key={row.id} className="hc-table__row">
              <td>
                <div className="hc-table__cell-strong">
                  <span className="hc-table__title">{row.supplierName}</span>
                  <span className="hc-table__subtitle">{row.supplierCode}</span>
                </div>
              </td>
              <td>
                <div className="hc-table__cell-strong">
                  <span className="hc-table__title">{row.purchaseReceiptNo}</span>
                  <span className="hc-table__subtitle">{new Date(row.receiptDate).toLocaleDateString()}</span>
                </div>
              </td>
              <td>
                <div className="hc-table__cell-strong">
                  <span className="hc-table__title">{row.itemName}</span>
                  <span className="hc-table__subtitle">{row.itemCode}</span>
                </div>
              </td>
              <td>
                <div className="hc-table__cell-strong">
                  <span className="hc-table__title">{row.componentItemName}</span>
                  <span className="hc-table__subtitle">{row.componentItemCode}</span>
                </div>
              </td>
              <td><span className="hc-table__subtitle">{row.shortageQty.toLocaleString()}</span></td>
              <td><span className="hc-table__subtitle">{row.resolvedQtyEquivalent.toLocaleString()}</span></td>
              <td><span className="hc-table__subtitle">{row.resolvedPhysicalQty.toLocaleString()} / {row.resolvedFinancialQtyEquivalent.toLocaleString()}</span></td>
              <td><span className="hc-table__title">{row.openQty.toLocaleString()}</span></td>
              <td><span className="hc-table__subtitle">{row.openAmount?.toLocaleString() ?? "Pending value"}</span></td>
              <td><Badge tone={row.status === "PartiallyResolved" ? "warning" : "neutral"}>{row.status}</Badge></td>
              <td><Badge tone={row.affectsSupplierBalance ? "primary" : "neutral"}>{row.affectsSupplierBalance ? "Supplier" : "Internal"}</Badge></td>
              <td>
                <div className="hc-table__cell-strong">
                  <span className="hc-table__title">{row.shortageReasonName ?? "Not specified"}</span>
                  <span className="hc-table__subtitle">{row.shortageReasonCode ?? row.approvalStatus}</span>
                </div>
              </td>
            </tr>
          ))}
          footer={<Pagination currentPage={safePage} onPageChange={setPage} pageSize={PAGE_SIZE} totalCount={rows.length} totalPages={totalPages} />}
          emptyState={
            hasFilters
              ? <EmptyState title="No shortages match the current filters" description="Try broadening the supplier, item, or date filters." />
              : <EmptyState title="No open shortages" description="All current receipt shortages are fully settled or no shortage rows have been generated yet." />
          }
        />
      ) : null}
    </section>
  );
}
