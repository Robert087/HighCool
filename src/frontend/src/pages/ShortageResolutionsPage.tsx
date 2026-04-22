import { useEffect, useMemo, useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import { RowActions } from "../components/patterns";
import {
  Badge,
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
  useToast,
} from "../components/ui";
import { ApiError } from "../services/api";
import { listSuppliers, type Supplier } from "../services/masterDataApi";
import {
  listShortageResolutions,
  type ShortageResolutionFilters,
  type ShortageResolutionListItem,
} from "../services/shortageResolutionsApi";

const PAGE_SIZE = 12;

const INITIAL_FILTERS: ShortageResolutionFilters = {
  search: "",
  supplierId: "",
  resolutionType: "",
  status: "",
  fromDate: "",
  toDate: "",
};

export function ShortageResolutionsPage() {
  const [rows, setRows] = useState<ShortageResolutionListItem[]>([]);
  const [suppliers, setSuppliers] = useState<Supplier[]>([]);
  const [filters, setFilters] = useState<ShortageResolutionFilters>(INITIAL_FILTERS);
  const [error, setError] = useState("");
  const [loading, setLoading] = useState(true);
  const [page, setPage] = useState(1);
  const [reloadKey, setReloadKey] = useState(0);
  const navigate = useNavigate();
  const { showToast } = useToast();

  useEffect(() => {
    let active = true;

    async function loadReferences() {
      try {
        const result = await listSuppliers("", "active");

        if (active) {
          setSuppliers(result);
        }
      } catch {
        if (active) {
          setError("Failed to load shortage resolution filters.");
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
        const result = await listShortageResolutions(filters);

        if (active) {
          setRows(result);
        }
      } catch (loadError) {
        if (active) {
          setError(loadError instanceof ApiError ? loadError.message : "Failed to load shortage resolutions.");
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
  const resultLabel = rows.length === 1 ? "1 shortage resolution" : `${rows.length} shortage resolutions`;
  const hasFilters = useMemo(
    () => Object.values(filters).some((value) => value.trim().length > 0),
    [filters],
  );
  const activeFilters = useMemo(() => {
    const selectedSupplier = suppliers.find((supplier) => supplier.id === filters.supplierId);
    const chips: FilterChip[] = [];

    if (filters.search.trim()) {
      chips.push({
        key: "search",
        label: `Search: ${filters.search.trim()}`,
        onRemove: () => setFilter("search", ""),
      });
    }

    if (selectedSupplier) {
      chips.push({
        key: "supplier",
        label: `Supplier: ${selectedSupplier.code} - ${selectedSupplier.name}`,
        onRemove: () => setFilter("supplierId", ""),
      });
    }

    if (filters.resolutionType) {
      chips.push({
        key: "resolutionType",
        label: `Type: ${filters.resolutionType}`,
        onRemove: () => setFilter("resolutionType", ""),
      });
    }

    if (filters.status) {
      chips.push({
        key: "status",
        label: `Status: ${filters.status}`,
        onRemove: () => setFilter("status", ""),
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
  }, [filters, suppliers]);

  function setFilter<K extends keyof ShortageResolutionFilters>(key: K, value: ShortageResolutionFilters[K]) {
    setFilters((current) => ({ ...current, [key]: value }));
  }

  function handleEdit(row: ShortageResolutionListItem) {
    if (row.status === "Posted") {
      showToast({
        tone: "warning",
        title: "Posted resolution is read-only",
        description: "Posted shortage resolutions cannot be edited directly.",
      });
      return;
    }

    navigate(`/shortage-resolutions/${row.id}/edit`);
  }

  return (
    <section className="hc-list-page">
      <PageHeader
        title="Shortage Resolutions"
        description="Manage physical replacements and financial settlements that close receipt shortage rows over time."
        eyebrow="Inventory"
        actions={
          <Link className="hc-button hc-button--primary hc-button--md" to="/shortage-resolutions/new">
            New shortage resolution
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

            <Field label="Resolution type">
              <Select value={filters.resolutionType} onChange={(event) => setFilter("resolutionType", event.target.value)}>
                <option value="">All types</option>
                <option value="Physical">Physical</option>
                <option value="Financial">Financial</option>
              </Select>
            </Field>

            <Field label="Status">
              <Select value={filters.status} onChange={(event) => setFilter("status", event.target.value)}>
                <option value="">All statuses</option>
                <option value="Draft">Draft</option>
                <option value="Posted">Posted</option>
                <option value="Canceled">Canceled</option>
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
            <FilterDropdown aria-label="Supplier filter" value={filters.supplierId} onChange={(event) => setFilter("supplierId", event.target.value)}>
              <option value="">Supplier</option>
              {suppliers.map((supplier) => (
                <option key={supplier.id} value={supplier.id}>
                  {supplier.code} - {supplier.name}
                </option>
              ))}
            </FilterDropdown>

            <FilterDropdown aria-label="Resolution type filter" value={filters.resolutionType} onChange={(event) => setFilter("resolutionType", event.target.value)}>
              <option value="">Type</option>
              <option value="Physical">Physical</option>
              <option value="Financial">Financial</option>
            </FilterDropdown>
          </>
        )}
        resetLabel="Reset"
        resultLabel={resultLabel}
        search={(
          <FilterTextInput
            aria-label="Search shortage resolutions"
            placeholder="Search resolution no, supplier, notes"
            value={filters.search}
            onChange={(event) => setFilter("search", event.target.value)}
          />
        )}
        secondaryActiveCount={filters.status ? 1 : 0}
        secondaryFilters={(
          <Field label="Status">
            <Select value={filters.status} onChange={(event) => setFilter("status", event.target.value)}>
              <option value="">All statuses</option>
              <option value="Draft">Draft</option>
              <option value="Posted">Posted</option>
              <option value="Canceled">Canceled</option>
            </Select>
          </Field>
        )}
      />

      {error ? (
        <Card padding="md">
          <EmptyState
            title="Unable to load shortage resolutions"
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
          </div>
        </div>
      ) : null}

      {!loading && !error ? (
        <DataTable
          hasData={rows.length > 0}
          columns={
            <tr>
              <th scope="col">Resolution</th>
              <th scope="col">Supplier</th>
              <th scope="col">Resolution Context</th>
              <th scope="col">Status</th>
              <th scope="col" className="hc-table__head-actions" aria-label="Actions" />
            </tr>
          }
          rows={visibleRows.map((row) => (
            <tr key={row.id} className="hc-table__row">
              <td>
                <div className="hc-table__cell-strong hc-table__primary-cell">
                  <span className="hc-table__title">{row.resolutionNo}</span>
                  <span className="hc-table__subtitle">{row.allocationCount} allocations</span>
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
                  <div className="hc-table__status-stack">
                    <Badge tone={row.resolutionType === "Physical" ? "primary" : "warning"}>{row.resolutionType}</Badge>
                  </div>
                  <div className="hc-table__metric">
                    <span className="hc-table__metric-label">{row.resolutionType === "Physical" ? "Total qty" : "Total amount"}</span>
                    <span className="hc-table__subtitle">{row.resolutionType === "Physical" ? (row.totalQty?.toLocaleString() ?? "0") : (row.totalAmount?.toLocaleString() ?? "0")}</span>
                  </div>
                  <div className="hc-table__metric">
                    <span className="hc-table__metric-label">Date</span>
                    <span className="hc-table__subtitle">{new Date(row.resolutionDate).toLocaleDateString()}</span>
                  </div>
                  {row.resolutionType === "Financial" ? (
                    <div className="hc-table__metric">
                      <span className="hc-table__metric-label">Currency</span>
                      <span className="hc-table__subtitle">{row.currency ?? "Not set"}</span>
                    </div>
                  ) : null}
                </div>
              </td>
              <td>
                <div className="hc-table__status-stack">
                  <Badge tone={row.status === "Posted" ? "success" : row.status === "Canceled" ? "danger" : "warning"}>{row.status}</Badge>
                </div>
              </td>
              <td className="hc-table__cell-actions">
                <RowActions
                  primaryAction={<Button size="sm" variant="secondary" className="hc-table__action-button" onClick={() => handleEdit(row)}>View</Button>}
                />
              </td>
            </tr>
          ))}
          footer={<Pagination currentPage={safePage} onPageChange={setPage} pageSize={PAGE_SIZE} totalCount={rows.length} totalPages={totalPages} />}
          emptyState={
            hasFilters
              ? <EmptyState title="No resolutions match the current filters" description="Try broadening the supplier, type, or date range." />
              : <EmptyState title="No shortage resolutions yet" description="Create the first physical or financial settlement document." action={<Link className="hc-button hc-button--primary hc-button--md" to="/shortage-resolutions/new">Create shortage resolution</Link>} />
          }
        />
      ) : null}
    </section>
  );
}
