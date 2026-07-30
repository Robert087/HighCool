import { useEffect, useMemo, useState } from "react";
import { Link } from "react-router-dom";
import {
  Badge,
  Button,
  Card,
  Checkbox,
  DataTable,
  EmptyState,
  Field,
  FilterDropdown,
  FiltersToolbar,
  FilterTextInput,
  PageHeader,
  Pagination,
  Select,
  SkeletonLoader,
  type FilterChip,
  useI18n,
} from "../components/ui";
import { ApiError } from "../services/api";
import {
  getInventoryMonitoringDashboard,
  getInventoryMonitoringFilterOptions,
  listInventoryMonitoringItems,
  type InventoryMonitoringDashboard,
  type InventoryMonitoringFilters,
  type InventoryMonitoringFilterOption,
  type InventoryMonitoringItem,
  type InventoryStockStatus,
} from "../services/inventoryMonitoringApi";
import { Permissions } from "../services/permissions";
import { useAuth } from "../features/auth/AuthProvider";

const PAGE_SIZE = 12;

const INITIAL_FILTERS: InventoryMonitoringFilters = {
  search: "",
  warehouseId: "",
  categoryId: "",
  status: "",
  onlyMonitored: true,
  page: 1,
  pageSize: PAGE_SIZE,
  sortBy: "status",
  sortDirection: "Desc",
};

function statusTone(status: InventoryStockStatus) {
  switch (status) {
    case "Healthy":
      return "success" as const;
    case "LowStock":
      return "warning" as const;
    case "OutOfStock":
      return "danger" as const;
    default:
      return "neutral" as const;
  }
}

function StatCard({ label, value, tone = "neutral" }: { label: string; value: string; tone?: "neutral" | "success" | "warning" | "danger" }) {
  const toneClass = tone === "neutral" ? "" : `hc-badge--${tone}`;

  return (
    <div className="hc-statement-summary-metric">
      <span className="hc-statement-summary-metric__label">{label}</span>
      <strong className={`hc-statement-summary-metric__value ${toneClass}`}>{value}</strong>
    </div>
  );
}

export function InventoryMonitoringPage() {
  const { formatNumber, formatQuantity, t } = useI18n();
  const { hasPermission } = useAuth();
  const [dashboard, setDashboard] = useState<InventoryMonitoringDashboard | null>(null);
  const [rows, setRows] = useState<InventoryMonitoringItem[]>([]);
  const [warehouses, setWarehouses] = useState<InventoryMonitoringFilterOption[]>([]);
  const [categories, setCategories] = useState<InventoryMonitoringFilterOption[]>([]);
  const [filters, setFilters] = useState<InventoryMonitoringFilters>(INITIAL_FILTERS);
  const [totalCount, setTotalCount] = useState(0);
  const [totalPages, setTotalPages] = useState(0);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [reloadKey, setReloadKey] = useState(0);
  const canManage = hasPermission(Permissions.InventoryMonitorManage);

  useEffect(() => {
    let active = true;

    async function loadReferences() {
      try {
        const options = await getInventoryMonitoringFilterOptions();

        if (active) {
          setWarehouses(options.warehouses);
          setCategories(options.categories);
        }
      } catch {
        if (active) {
          setError(t("module.inventoryMonitoring.referencesError"));
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
        const [dashboardResult, listResult] = await Promise.all([
          getInventoryMonitoringDashboard(),
          listInventoryMonitoringItems(filters),
        ]);

        if (active) {
          setDashboard(dashboardResult);
          setRows(listResult.items);
          setTotalCount(listResult.totalCount);
          setTotalPages(listResult.totalPages);
        }
      } catch (loadError) {
        if (active) {
          setError(loadError instanceof ApiError ? loadError.message : t("module.inventoryMonitoring.loadError"));
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

  const activeFilters = useMemo(() => {
    const selectedWarehouse = warehouses.find((warehouse) => warehouse.id === filters.warehouseId);
    const selectedCategory = categories.find((category) => category.id === filters.categoryId);
    const chips: FilterChip[] = [];

    if (filters.search.trim()) {
      chips.push({
        key: "search",
        label: t("module.inventoryMonitoring.filter.searchChip", { value: filters.search.trim() }),
        onRemove: () => setFilter("search", ""),
      });
    }

    if (selectedWarehouse) {
      chips.push({
        key: "warehouse",
        label: t("module.inventoryMonitoring.filter.warehouseChip", { value: `${selectedWarehouse.code} - ${selectedWarehouse.name}` }),
        onRemove: () => setFilter("warehouseId", ""),
      });
    }

    if (selectedCategory) {
      chips.push({
        key: "category",
        label: t("module.inventoryMonitoring.filter.categoryChip", { value: `${selectedCategory.code} - ${selectedCategory.name}` }),
        onRemove: () => setFilter("categoryId", ""),
      });
    }

    if (filters.status) {
      chips.push({
        key: "status",
        label: t("module.inventoryMonitoring.filter.statusChip", { value: t(`inventory.stockStatus.${filters.status}`) }),
        onRemove: () => setFilter("status", ""),
      });
    }

    if (filters.onlyMonitored) {
      chips.push({
        key: "onlyMonitored",
        label: t("module.inventoryMonitoring.filter.onlyMonitoredChip"),
        onRemove: () => setFilter("onlyMonitored", false),
      });
    }

    return chips;
  }, [categories, filters, t, warehouses]);

  const resultLabel = totalCount === 1
    ? t("module.inventoryMonitoring.resultLabel.one", { count: totalCount })
    : t("module.inventoryMonitoring.resultLabel.other", { count: totalCount });

  function setFilter<K extends keyof InventoryMonitoringFilters>(key: K, value: InventoryMonitoringFilters[K]) {
    setFilters((current) => ({ ...current, [key]: value, page: 1 }));
  }

  function setPage(page: number) {
    setFilters((current) => ({ ...current, page }));
  }

  function resetFilters() {
    setFilters(INITIAL_FILTERS);
  }

  function formatRowQuantity(row: InventoryMonitoringItem, value: number) {
    return t("module.inventoryMonitoring.quantityWithUom", {
      quantity: formatQuantity(value),
      uom: row.baseUomCode,
    });
  }

  function formatOptionalRowQuantity(row: InventoryMonitoringItem, value: number | null) {
    return value === null ? t("common.notSet") : formatRowQuantity(row, value);
  }

  return (
    <section className="hc-list-page">
      <PageHeader
        title="module.inventoryMonitoring.title"
        description="module.inventoryMonitoring.description"
        eyebrow="route.section.inventory"
      />

      <Card className="hc-statement-summary-panel" padding="md">
        <div className="hc-statement-summary-grid">
        <StatCard label={t("module.inventoryMonitoring.totalMonitored")} value={formatNumber(dashboard?.totalMonitoredItems ?? 0)} />
        <StatCard label={t("inventory.stockStatus.Healthy")} value={formatNumber(dashboard?.healthyItems ?? 0)} tone="success" />
        <StatCard label={t("inventory.stockStatus.LowStock")} value={formatNumber(dashboard?.lowStockItems ?? 0)} tone="warning" />
        <StatCard label={t("inventory.stockStatus.OutOfStock")} value={formatNumber(dashboard?.outOfStockItems ?? 0)} tone="danger" />
        </div>
      </Card>

      <FiltersToolbar
        activeFilters={activeFilters}
        mobileFilters={(
          <>
            <Field label={t("table.warehouse")}>
              <Select value={filters.warehouseId} onChange={(event) => setFilter("warehouseId", event.target.value)}>
                <option value="">{t("module.inventoryMonitoring.allWarehouses")}</option>
                {warehouses.map((warehouse) => (
                  <option key={warehouse.id} value={warehouse.id}>{warehouse.code} - {warehouse.name}</option>
                ))}
              </Select>
            </Field>
            <Field label={t("module.inventoryMonitoring.category")}>
              <Select value={filters.categoryId} onChange={(event) => setFilter("categoryId", event.target.value)}>
                <option value="">{t("module.inventoryMonitoring.allCategories")}</option>
                {categories.map((category) => (
                  <option key={category.id} value={category.id}>{category.code} - {category.name}</option>
                ))}
              </Select>
            </Field>
            <Field label={t("module.inventoryMonitoring.status")}>
              <Select value={filters.status} onChange={(event) => setFilter("status", event.target.value)}>
                <option value="">{t("module.inventoryMonitoring.allStatuses")}</option>
                <option value="Healthy">{t("inventory.stockStatus.Healthy")}</option>
                <option value="LowStock">{t("inventory.stockStatus.LowStock")}</option>
                <option value="OutOfStock">{t("inventory.stockStatus.OutOfStock")}</option>
                <option value="NotMonitored">{t("inventory.stockStatus.NotMonitored")}</option>
              </Select>
            </Field>
            <Checkbox
              checked={filters.onlyMonitored}
              label={t("module.inventoryMonitoring.onlyMonitored")}
              onChange={(event) => setFilter("onlyMonitored", event.target.checked)}
            />
          </>
        )}
        onReset={resetFilters}
        primaryFilters={(
          <>
            <FilterDropdown aria-label={t("module.inventoryMonitoring.filter.warehouseAria")} value={filters.warehouseId} onChange={(event) => setFilter("warehouseId", event.target.value)}>
              <option value="">{t("table.warehouse")}</option>
              {warehouses.map((warehouse) => (
                <option key={warehouse.id} value={warehouse.id}>{warehouse.code} - {warehouse.name}</option>
              ))}
            </FilterDropdown>
            <FilterDropdown aria-label={t("module.inventoryMonitoring.filter.statusAria")} value={filters.status} onChange={(event) => setFilter("status", event.target.value)}>
              <option value="">{t("module.inventoryMonitoring.status")}</option>
              <option value="Healthy">{t("inventory.stockStatus.Healthy")}</option>
              <option value="LowStock">{t("inventory.stockStatus.LowStock")}</option>
              <option value="OutOfStock">{t("inventory.stockStatus.OutOfStock")}</option>
              <option value="NotMonitored">{t("inventory.stockStatus.NotMonitored")}</option>
            </FilterDropdown>
          </>
        )}
        resultLabel={resultLabel}
        search={(
          <FilterTextInput
            aria-label={t("module.inventoryMonitoring.searchAria")}
            placeholder={t("module.inventoryMonitoring.searchPlaceholder")}
            value={filters.search}
            onChange={(event) => setFilter("search", event.target.value)}
          />
        )}
        secondaryActiveCount={(filters.categoryId ? 1 : 0) + (filters.onlyMonitored ? 1 : 0)}
        secondaryFilters={(
          <>
            <Field label={t("module.inventoryMonitoring.category")}>
              <Select value={filters.categoryId} onChange={(event) => setFilter("categoryId", event.target.value)}>
                <option value="">{t("module.inventoryMonitoring.allCategories")}</option>
                {categories.map((category) => (
                  <option key={category.id} value={category.id}>{category.code} - {category.name}</option>
                ))}
              </Select>
            </Field>
            <Checkbox
              checked={filters.onlyMonitored}
              label={t("module.inventoryMonitoring.onlyMonitored")}
              onChange={(event) => setFilter("onlyMonitored", event.target.checked)}
            />
          </>
        )}
      />

      {error ? (
        <Card padding="md">
          <EmptyState
            action={<Button variant="secondary" onClick={() => setReloadKey((current) => current + 1)}>{t("common.retry")}</Button>}
            description={error}
            title="module.inventoryMonitoring.loadError"
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
          emptyState={<EmptyState title="module.inventoryMonitoring.empty" description="module.inventoryMonitoring.emptyDescription" />}
          hasData={rows.length > 0}
          columns={
            <tr>
              <th scope="col">{t("table.item")}</th>
              <th scope="col">{t("table.warehouse")}</th>
              <th scope="col">{t("module.inventoryMonitoring.currentStock")}</th>
              <th scope="col">{t("module.inventoryMonitoring.minimumStock")}</th>
              <th scope="col">{t("module.inventoryMonitoring.reorderPoint")}</th>
              <th scope="col">{t("module.inventoryMonitoring.maximumStock")}</th>
              <th scope="col">{t("module.inventoryMonitoring.suggestedQuantity")}</th>
              <th scope="col">{t("module.inventoryMonitoring.status")}</th>
              {canManage ? <th scope="col">{t("table.actions")}</th> : null}
            </tr>
          }
          rows={rows.map((row) => (
            <tr key={`${row.itemId}-${row.warehouseId}`} className="hc-table__row">
              <td>
                <div className="hc-table__cell-strong hc-table__primary-cell">
                  <span className="hc-table__title">{row.itemName}</span>
                  <span className="hc-table__subtitle">{row.itemCode}</span>
                </div>
              </td>
              <td>
                <div className="hc-table__cell-strong">
                  <span className="hc-table__title">{row.warehouseName}</span>
                  <span className="hc-table__subtitle">{row.warehouseCode}</span>
                </div>
              </td>
              <td>{formatRowQuantity(row, row.currentStock)}</td>
              <td>{formatRowQuantity(row, row.minimumStock)}</td>
              <td>{formatOptionalRowQuantity(row, row.reorderPoint)}</td>
              <td>{formatOptionalRowQuantity(row, row.maximumStock)}</td>
              <td>{formatRowQuantity(row, row.suggestedReorderQuantity)}</td>
              <td><Badge tone={statusTone(row.status)}>{t(`inventory.stockStatus.${row.status}`)}</Badge></td>
              {canManage ? (
                <td className="hc-table__actions-cell">
                  <Link className="hc-button hc-button--ghost hc-button--sm" to={`/inventory-monitoring/items/${row.itemId}/settings`}>
                    {t("module.inventoryMonitoring.manageSettings")}
                  </Link>
                </td>
              ) : null}
            </tr>
          ))}
          footer={<Pagination currentPage={filters.page} pageSize={PAGE_SIZE} totalCount={totalCount} totalPages={totalPages} onPageChange={setPage} />}
        />
      ) : null}
    </section>
  );
}
