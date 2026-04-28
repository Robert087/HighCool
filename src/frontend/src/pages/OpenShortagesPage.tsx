import { useEffect, useMemo, useState } from "react";
import { Link } from "react-router-dom";
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
import { listItems, listSuppliers, type Item, type Supplier } from "../services/masterDataApi";
import {
  listOpenShortages,
  type OpenShortage,
  type OpenShortageFilters,
} from "../services/shortageResolutionsApi";
import { formatCurrency, formatDate, formatQuantity } from "../i18n";

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
  const [totalCount, setTotalCount] = useState(0);
  const [totalPages, setTotalPages] = useState(0);
  const [suppliers, setSuppliers] = useState<Supplier[]>([]);
  const [items, setItems] = useState<Item[]>([]);
  const [filters, setFilters] = useState<OpenShortageFilters>(INITIAL_FILTERS);
  const [error, setError] = useState("");
  const [loading, setLoading] = useState(true);
  const [page, setPage] = useState(1);
  const [reloadKey, setReloadKey] = useState(0);
  const { t } = useI18n();

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
          setError(t("module.shortages.filterError"));
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
        const result = await listOpenShortages({
          filters,
          page,
          pageSize: PAGE_SIZE,
          sortBy: "receiptDate",
          sortDirection: "Asc",
        });

        if (active) {
          setRows(result.items);
          setTotalCount(result.totalCount);
          setTotalPages(result.totalPages);
        }
      } catch (loadError) {
        if (active) {
          setError(loadError instanceof ApiError ? loadError.message : t("module.shortages.error"));
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
  const resultLabel = totalCount === 1 ? t("module.shortages.resultLabel.one", { count: totalCount }) : t("module.shortages.resultLabel.other", { count: totalCount });
  const hasFilters = useMemo(
    () => Object.values(filters).some((value) => value.trim().length > 0),
    [filters],
  );
  const activeFilters = useMemo(() => {
    const selectedSupplier = suppliers.find((supplier) => supplier.id === filters.supplierId);
    const selectedItem = items.find((item) => item.id === filters.itemId);
    const selectedComponent = items.find((item) => item.id === filters.componentItemId);
    const chips: FilterChip[] = [];

    if (filters.search.trim()) {
      chips.push({
        key: "search",
        label: t("module.shortages.filter.searchChip", { value: filters.search.trim() }),
        onRemove: () => setFilter("search", ""),
      });
    }

    if (selectedSupplier) {
      chips.push({
        key: "supplier",
        label: t("module.shortages.filter.supplierChip", { value: `${selectedSupplier.code} - ${selectedSupplier.name}` }),
        onRemove: () => setFilter("supplierId", ""),
      });
    }

    if (selectedItem) {
      chips.push({
        key: "item",
        label: t("module.shortages.filter.parentItemChip", { value: `${selectedItem.code} - ${selectedItem.name}` }),
        onRemove: () => setFilter("itemId", ""),
      });
    }

    if (selectedComponent) {
      chips.push({
        key: "component",
        label: t("module.shortages.filter.componentChip", { value: `${selectedComponent.code} - ${selectedComponent.name}` }),
        onRemove: () => setFilter("componentItemId", ""),
      });
    }

    if (filters.status) {
      chips.push({
        key: "status",
        label: t("module.shortages.filter.statusChip", { value: filters.status === "PartiallyResolved" ? t("module.shortages.partiallyResolved") : t("module.shortages.open") }),
        onRemove: () => setFilter("status", ""),
      });
    }

    if (filters.affectsSupplierBalance) {
      chips.push({
        key: "affectsSupplierBalance",
        label: t("module.shortages.filter.supplierBalanceChip", { value: filters.affectsSupplierBalance === "yes" ? t("module.shortages.affectsSupplierBalance") : t("module.shortages.internalOnly") }),
        onRemove: () => setFilter("affectsSupplierBalance", ""),
      });
    }

    if (filters.fromDate || filters.toDate) {
      chips.push({
        key: "dateRange",
        label: t("module.shortages.filter.dateChip", { from: filters.fromDate || t("common.any"), to: filters.toDate || t("common.any") }),
        onRemove: () => {
          setFilter("fromDate", "");
          setFilter("toDate", "");
        },
      });
    }

    return chips;
  }, [filters, items, suppliers]);

  function setFilter<K extends keyof OpenShortageFilters>(key: K, value: OpenShortageFilters[K]) {
    setFilters((current) => ({ ...current, [key]: value }));
  }

  return (
    <section className="hc-list-page">
      <PageHeader
        title="module.shortages"
        description="module.shortages.description"
        eyebrow="route.section.inventory"
        actions={
          <Link className="hc-button hc-button--primary hc-button--md" to="/shortage-resolutions/new">
            {t("module.shortages.new")}
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

            <FilterDropdown aria-label="Parent item filter" value={filters.itemId} onChange={(event) => setFilter("itemId", event.target.value)}>
              <option value="">Parent item</option>
              {items.map((item) => (
                <option key={item.id} value={item.id}>
                  {item.code} - {item.name}
                </option>
              ))}
            </FilterDropdown>
          </>
        )}
        resultLabel={resultLabel}
        search={(
          <FilterTextInput
            aria-label="Search open shortages"
            placeholder={t("module.shortages.searchPlaceholder")}
            value={filters.search}
            onChange={(event) => setFilter("search", event.target.value)}
          />
        )}
        secondaryActiveCount={[filters.componentItemId, filters.status, filters.affectsSupplierBalance].filter(Boolean).length}
        secondaryFilters={(
          <>
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
          </>
        )}
      />

      {error ? (
        <Card padding="md">
          <EmptyState
            title="module.shortages.error"
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
            <SkeletonLoader height="3.5rem" variant="rect" />
          </div>
        </div>
      ) : null}

      {!loading && !error ? (
        <DataTable
          hasData={rows.length > 0}
          columns={
            <tr>
              <th scope="col">Receipt</th>
              <th scope="col">Item Context</th>
              <th scope="col">Quantities</th>
              <th scope="col">Status</th>
              <th scope="col">Supplier impact</th>
              <th scope="col">Reason</th>
            </tr>
          }
          rows={rows.map((row) => (
            <tr key={row.id} className="hc-table__row">
              <td>
                <div className="hc-table__cell-strong hc-table__primary-cell">
                  <span className="hc-table__title">{row.purchaseReceiptNo}</span>
                  <span className="hc-table__subtitle">{row.supplierName} · {row.supplierCode}</span>
                  <span className="hc-table__subtitle">{formatDate(row.receiptDate)}</span>
                </div>
              </td>
              <td>
                <div className="hc-table__stack">
                  <div className="hc-table__cell-strong">
                    <span className="hc-table__title">{row.itemName}</span>
                    <span className="hc-table__subtitle">{row.itemCode}</span>
                  </div>
                  <div className="hc-table__metric">
                    <span className="hc-table__metric-label">{t("module.shortages.componentLabel")}</span>
                    <span className="hc-table__subtitle">{row.componentItemName} · {row.componentItemCode}</span>
                  </div>
                </div>
              </td>
              <td>
                <div className="hc-table__stack">
                  <div className="hc-table__metric">
                    <span className="hc-table__metric-label">Expected</span>
                    <span className="hc-table__subtitle">{formatQuantity(row.expectedComponentQty)}</span>
                  </div>
                  <div className="hc-table__metric">
                    <span className="hc-table__metric-label">{t("module.shortages.initialActual")}</span>
                    <span className="hc-table__subtitle">{formatQuantity(row.initialActualComponentQty)}</span>
                  </div>
                  <div className="hc-table__metric">
                    <span className="hc-table__metric-label">{t("module.shortages.physicalResolved")}</span>
                    <span className="hc-table__subtitle">{formatQuantity(row.resolvedPhysicalQty)}</span>
                  </div>
                  <div className="hc-table__metric">
                    <span className="hc-table__metric-label">{t("module.shortages.finalPhysical")}</span>
                    <span className="hc-table__subtitle">{formatQuantity(row.finalPhysicalComponentQty)}</span>
                  </div>
                  <div className="hc-table__metric">
                    <span className="hc-table__metric-label">{t("module.shortages.financiallySettled")}</span>
                    <span className="hc-table__subtitle">{formatQuantity(row.resolvedFinancialQtyEquivalent)}</span>
                  </div>
                  <div className="hc-table__metric">
                    <span className="hc-table__metric-label">{t("module.shortages.shortageResolved")}</span>
                    <span className="hc-table__subtitle">{formatQuantity(row.shortageQty)} / {formatQuantity(row.resolvedQtyEquivalent)}</span>
                  </div>
                  <div className="hc-table__metric">
                    <span className="hc-table__metric-label">{t("module.shortages.open")}</span>
                    <span className="hc-table__title">{formatQuantity(row.openQty)}</span>
                  </div>
                  <div className="hc-table__metric">
                    <span className="hc-table__metric-label">{t("module.shortages.openAmount")}</span>
                    <span className="hc-table__subtitle">{row.openAmount != null ? formatCurrency(row.openAmount) : t("common.pendingValue")}</span>
                  </div>
                </div>
              </td>
              <td><div className="hc-table__status-stack"><Badge tone={row.status === "PartiallyResolved" ? "warning" : "neutral"}>{row.status}</Badge></div></td>
              <td><div className="hc-table__status-stack"><Badge tone={row.affectsSupplierBalance ? "primary" : "neutral"}>{row.affectsSupplierBalance ? t("module.shortages.supplierImpactSupplier") : t("module.shortages.supplierImpactInternal")}</Badge></div></td>
              <td>
                <div className="hc-table__cell-strong">
                  <span className="hc-table__title">{row.shortageReasonName ?? t("module.shortages.notSpecified")}</span>
                  <span className="hc-table__subtitle">{row.shortageReasonCode ?? row.approvalStatus}</span>
                </div>
              </td>
            </tr>
          ))}
          footer={<Pagination currentPage={safePage} onPageChange={setPage} pageSize={PAGE_SIZE} totalCount={totalCount} totalPages={Math.max(totalPages, 1)} />}
          emptyState={
            hasFilters
              ? <EmptyState title="module.shortages.emptyFiltered" description="module.shortages.emptyFilteredDescription" />
              : <EmptyState title="module.shortages.empty" description="module.shortages.emptyDescription" />
          }
        />
      ) : null}
    </section>
  );
}
