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
  useI18n,
} from "../components/ui";
import { ApiError } from "../services/api";
import { listSuppliers, type Supplier } from "../services/masterDataApi";
import {
  listShortageResolutions,
  type ShortageResolutionFilters,
  type ShortageResolutionListItem,
} from "../services/shortageResolutionsApi";
import { formatCurrency, formatDate, formatQuantity } from "../i18n";

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
  const [totalCount, setTotalCount] = useState(0);
  const [totalPages, setTotalPages] = useState(0);
  const [suppliers, setSuppliers] = useState<Supplier[]>([]);
  const [filters, setFilters] = useState<ShortageResolutionFilters>(INITIAL_FILTERS);
  const [error, setError] = useState("");
  const [loading, setLoading] = useState(true);
  const [page, setPage] = useState(1);
  const [reloadKey, setReloadKey] = useState(0);
  const navigate = useNavigate();
  const { t } = useI18n();

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
          setError(t("module.shortageResolutions.filterError"));
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
        const result = await listShortageResolutions({
          filters,
          page,
          pageSize: PAGE_SIZE,
          sortBy: "resolutionDate",
          sortDirection: "Desc",
        });

        if (active) {
          setRows(result.items);
          setTotalCount(result.totalCount);
          setTotalPages(result.totalPages);
        }
      } catch (loadError) {
        if (active) {
          setError(loadError instanceof ApiError ? loadError.message : t("module.shortageResolutions.error"));
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
  const resultLabel = totalCount === 1
    ? t("module.shortageResolutions.resultLabel.one", { count: totalCount })
    : t("module.shortageResolutions.resultLabel.other", { count: totalCount });
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
        label: t("module.shortageResolutions.filter.searchChip", { value: filters.search.trim() }),
        onRemove: () => setFilter("search", ""),
      });
    }

    if (selectedSupplier) {
      chips.push({
        key: "supplier",
        label: t("module.shortageResolutions.filter.supplierChip", { value: `${selectedSupplier.code} - ${selectedSupplier.name}` }),
        onRemove: () => setFilter("supplierId", ""),
      });
    }

    if (filters.resolutionType) {
      chips.push({
        key: "resolutionType",
        label: t("module.shortageResolutions.filter.typeChip", { value: filters.resolutionType }),
        onRemove: () => setFilter("resolutionType", ""),
      });
    }

    if (filters.status) {
      chips.push({
        key: "status",
        label: t("module.shortageResolutions.filter.statusChip", { value: t(`status.${filters.status.toLowerCase()}`) }),
        onRemove: () => setFilter("status", ""),
      });
    }

    if (filters.fromDate || filters.toDate) {
      chips.push({
        key: "dateRange",
        label: t("module.shortageResolutions.filter.dateChip", { from: filters.fromDate || t("common.any"), to: filters.toDate || t("common.any") }),
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
    navigate(`/shortage-resolutions/${row.id}/edit`);
  }

  return (
    <section className="hc-list-page">
      <PageHeader
        title="module.shortageResolutions"
        description="module.shortageResolutions.description"
        eyebrow="route.section.inventory"
        actions={
          <Link className="hc-button hc-button--primary hc-button--md" to="/shortage-resolutions/new">
            {t("module.shortageResolutions.new")}
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
        resetLabel={t("common.reset")}
        resultLabel={resultLabel}
        search={(
          <FilterTextInput
            aria-label="Search shortage resolutions"
            placeholder={t("module.shortageResolutions.searchPlaceholder")}
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
            title="module.shortageResolutions.error"
            description={error}
            action={<Button variant="secondary" onClick={() => setReloadKey((current) => current + 1)}>{t("common.retry")}</Button>}
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
          rows={rows.map((row) => (
            <tr key={row.id} className="hc-table__row">
              <td>
                <div className="hc-table__cell-strong hc-table__primary-cell">
                  <span className="hc-table__title">{row.resolutionNo}</span>
                  <span className="hc-table__subtitle">{t("module.shortageResolutions.allocations", { count: row.allocationCount })}</span>
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
                    <span className="hc-table__metric-label">{row.resolutionType === "Physical" ? t("module.shortageResolutions.totalQty") : t("module.shortageResolutions.totalAmount")}</span>
                    <span className="hc-table__subtitle">{row.resolutionType === "Physical" ? formatQuantity(row.totalQty ?? 0) : formatCurrency(row.totalAmount ?? 0, { currency: row.currency })}</span>
                  </div>
                  <div className="hc-table__metric">
                    <span className="hc-table__metric-label">{t("common.date")}</span>
                    <span className="hc-table__subtitle">{formatDate(row.resolutionDate)}</span>
                  </div>
                  {row.resolutionType === "Financial" ? (
                    <div className="hc-table__metric">
                      <span className="hc-table__metric-label">{t("Currency")}</span>
                      <span className="hc-table__subtitle">{row.currency ?? t("status.notSet")}</span>
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
                  primaryAction={<Button size="sm" variant="secondary" className="hc-table__action-button" onClick={() => handleEdit(row)}>{t("common.view")}</Button>}
                />
              </td>
            </tr>
          ))}
          footer={<Pagination currentPage={safePage} onPageChange={setPage} pageSize={PAGE_SIZE} totalCount={totalCount} totalPages={Math.max(totalPages, 1)} />}
          emptyState={
            hasFilters
              ? <EmptyState title="module.shortageResolutions.emptyFiltered" description="module.shortageResolutions.emptyFilteredDescription" />
              : <EmptyState title="module.shortageResolutions.empty" description="module.shortageResolutions.emptyDescription" action={<Link className="hc-button hc-button--primary hc-button--md" to="/shortage-resolutions/new">{t("module.shortageResolutions.new")}</Link>} />
          }
        />
      ) : null}
    </section>
  );
}
