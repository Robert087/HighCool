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
import { useAuth } from "../features/auth/AuthProvider";
import { ApiError } from "../services/api";
import {
  listInventoryCounts,
  type InventoryCountListFilters,
  type InventoryCountListItem,
} from "../services/inventoryCountsApi";
import { getActiveWarehousesCached, type Warehouse } from "../services/masterDataApi";
import { Permissions } from "../services/permissions";

const PAGE_SIZE = 12;

const INITIAL_FILTERS: InventoryCountListFilters = {
  search: "",
  countNo: "",
  warehouseId: "",
  status: "",
  fromDate: "",
  toDate: "",
  page: 1,
  pageSize: PAGE_SIZE,
  sortBy: "countDate",
  sortDirection: "Desc",
};

function statusTone(status: InventoryCountListItem["status"]) {
  return status === "Posted" ? "success" : status === "Canceled" ? "neutral" : "warning";
}

export function InventoryCountsPage() {
  const { formatDate, t } = useI18n();
  const { hasPermission } = useAuth();
  const [rows, setRows] = useState<InventoryCountListItem[]>([]);
  const [warehouses, setWarehouses] = useState<Warehouse[]>([]);
  const [filters, setFilters] = useState<InventoryCountListFilters>(INITIAL_FILTERS);
  const [totalCount, setTotalCount] = useState(0);
  const [totalPages, setTotalPages] = useState(0);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [reloadKey, setReloadKey] = useState(0);

  useEffect(() => {
    let active = true;

    async function loadReferences() {
      try {
        const warehouseRows = await getActiveWarehousesCached();
        if (active) {
          setWarehouses(warehouseRows);
        }
      } catch {
        if (active) {
          setError(t("module.inventoryCounts.referencesError"));
        }
      }
    }

    void loadReferences();

    return () => {
      active = false;
    };
  }, [t]);

  useEffect(() => {
    let active = true;

    async function load() {
      try {
        setLoading(true);
        setError("");
        const result = await listInventoryCounts(filters);

        if (active) {
          setRows(result.items);
          setTotalCount(result.totalCount);
          setTotalPages(result.totalPages);
        }
      } catch (loadError) {
        if (active) {
          setError(loadError instanceof ApiError ? loadError.message : t("module.inventoryCounts.loadError"));
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
  }, [filters, reloadKey, t]);

  const safePage = totalPages > 0 ? Math.min(filters.page, totalPages) : 1;
  const hasFilters = useMemo(
    () => filters.search.trim() || filters.countNo.trim() || filters.warehouseId || filters.status || filters.fromDate || filters.toDate,
    [filters],
  );
  const activeFilters = useMemo(() => {
    const chips: FilterChip[] = [];
    const warehouse = warehouses.find((row) => row.id === filters.warehouseId);

    if (filters.search.trim()) {
      chips.push({
        key: "search",
        label: t("module.inventoryCounts.filter.searchChip", { value: filters.search.trim() }),
        onRemove: () => setFilter("search", ""),
      });
    }

    if (filters.countNo.trim()) {
      chips.push({
        key: "countNo",
        label: t("module.inventoryCounts.filter.numberChip", { value: filters.countNo.trim() }),
        onRemove: () => setFilter("countNo", ""),
      });
    }

    if (warehouse) {
      chips.push({
        key: "warehouse",
        label: t("module.inventoryCounts.filter.warehouseChip", { value: `${warehouse.code} - ${warehouse.name}` }),
        onRemove: () => setFilter("warehouseId", ""),
      });
    }

    if (filters.status) {
      chips.push({
        key: "status",
        label: t("module.inventoryCounts.filter.statusChip", { value: t(`document.status.${filters.status}`) }),
        onRemove: () => setFilter("status", ""),
      });
    }

    if (filters.fromDate || filters.toDate) {
      chips.push({
        key: "dateRange",
        label: t("module.inventoryCounts.filter.dateChip", { from: filters.fromDate || t("common.any"), to: filters.toDate || t("common.any") }),
        onRemove: () => {
          setFilter("fromDate", "");
          setFilter("toDate", "");
        },
      });
    }

    return chips;
  }, [filters, t, warehouses]);
  const resultLabel = totalCount === 1
    ? t("module.inventoryCounts.resultLabel.one", { count: totalCount })
    : t("module.inventoryCounts.resultLabel.other", { count: totalCount });
  const canCreateCount = hasPermission(Permissions.InventoryCountCreate);

  function setFilter<K extends keyof InventoryCountListFilters>(key: K, value: InventoryCountListFilters[K]) {
    setFilters((current) => ({
      ...current,
      [key]: value,
      page: key === "page" ? Number(value) : 1,
    }));
  }

  return (
    <section className="hc-list-page">
      <PageHeader
        title="module.inventoryCounts.title"
        description="module.inventoryCounts.description"
        eyebrow="route.section.inventory"
        actions={canCreateCount ? (
          <Link className="hc-button hc-button--primary hc-button--md" to="/inventory-counts/new">
            {t("module.inventoryCounts.new")}
          </Link>
        ) : null}
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
            <Field label={t("module.inventoryCounts.countNo")}>
              <Input value={filters.countNo} onChange={(event) => setFilter("countNo", event.target.value)} />
            </Field>
            <Field label={t("module.inventoryCounts.warehouse")}>
              <Select value={filters.warehouseId} onChange={(event) => setFilter("warehouseId", event.target.value)}>
                <option value="">{t("module.inventoryCounts.allWarehouses")}</option>
                {warehouses.map((warehouse) => (
                  <option key={warehouse.id} value={warehouse.id}>{warehouse.code} - {warehouse.name}</option>
                ))}
              </Select>
            </Field>
            <Field label={t("table.status")}>
              <Select value={filters.status} onChange={(event) => setFilter("status", event.target.value)}>
                <option value="">{t("module.inventoryCounts.allStatuses")}</option>
                <option value="Draft">{t("document.status.Draft")}</option>
                <option value="Posted">{t("document.status.Posted")}</option>
                <option value="Canceled">{t("document.status.Canceled")}</option>
              </Select>
            </Field>
          </>
        )}
        onReset={() => setFilters(INITIAL_FILTERS)}
        primaryFilters={(
          <>
            <FilterDropdown aria-label={t("module.inventoryCounts.filter.warehouseAria")} value={filters.warehouseId} onChange={(event) => setFilter("warehouseId", event.target.value)}>
              <option value="">{t("module.inventoryCounts.warehouse")}</option>
              {warehouses.map((warehouse) => (
                <option key={warehouse.id} value={warehouse.id}>{warehouse.code} - {warehouse.name}</option>
              ))}
            </FilterDropdown>
            <FilterDropdown aria-label={t("module.inventoryCounts.filter.statusAria")} value={filters.status} onChange={(event) => setFilter("status", event.target.value)}>
              <option value="">{t("table.status")}</option>
              <option value="Draft">{t("document.status.Draft")}</option>
              <option value="Posted">{t("document.status.Posted")}</option>
              <option value="Canceled">{t("document.status.Canceled")}</option>
            </FilterDropdown>
          </>
        )}
        resultLabel={resultLabel}
        search={(
          <FilterTextInput
            aria-label={t("module.inventoryCounts.searchAria")}
            placeholder={t("module.inventoryCounts.searchPlaceholder")}
            value={filters.search}
            onChange={(event) => setFilter("search", event.target.value)}
          />
        )}
        secondaryActiveCount={filters.countNo.trim() ? 1 : 0}
        secondaryFilters={(
          <Field label={t("module.inventoryCounts.countNo")}>
            <Input value={filters.countNo} onChange={(event) => setFilter("countNo", event.target.value)} />
          </Field>
        )}
      />

      {error ? (
        <Card padding="md">
          <EmptyState
            title="module.inventoryCounts.loadError"
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
          columns={(
            <tr>
              <th scope="col">{t("module.inventoryCounts.countNo")}</th>
              <th scope="col">{t("module.inventoryCounts.countDate")}</th>
              <th scope="col">{t("module.inventoryCounts.warehouse")}</th>
              <th scope="col">{t("table.status")}</th>
              <th scope="col">{t("table.lines")}</th>
              <th scope="col">{t("module.inventoryCounts.postedAt")}</th>
            </tr>
          )}
          rows={rows.map((row) => (
            <tr key={row.id} className="hc-table__row">
              <td>
                <Link className="hc-table__link" to={`/inventory-counts/${row.id}`}>
                  {row.countNo}
                </Link>
              </td>
              <td>{formatDate(row.countDate)}</td>
              <td>
                <div className="hc-table__cell-strong">
                  <span className="hc-table__title">{row.warehouseName}</span>
                  <span className="hc-table__subtitle">{row.warehouseCode}</span>
                </div>
              </td>
              <td><Badge tone={statusTone(row.status)}>{t(`document.status.${row.status}`)}</Badge></td>
              <td>{row.lineCount}</td>
              <td>{row.postedAt ? formatDate(row.postedAt) : t("common.notAvailable")}</td>
            </tr>
          ))}
          footer={(
            <Pagination
              currentPage={safePage}
              onPageChange={(nextPage) => setFilter("page", nextPage)}
              pageSize={PAGE_SIZE}
              totalCount={totalCount}
              totalPages={Math.max(totalPages, 1)}
            />
          )}
          emptyState={hasFilters
            ? <EmptyState title="module.inventoryCounts.emptyFiltered" description="module.inventoryCounts.emptyFilteredDescription" />
            : <EmptyState title="module.inventoryCounts.empty" description="module.inventoryCounts.emptyDescription" />}
        />
      ) : null}
    </section>
  );
}
