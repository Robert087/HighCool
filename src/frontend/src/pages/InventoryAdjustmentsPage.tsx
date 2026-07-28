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
import {
  listInventoryAdjustments,
  type InventoryAdjustmentListFilters,
  type InventoryAdjustmentListItem,
} from "../services/inventoryAdjustmentsApi";
import { useAuth } from "../features/auth/AuthProvider";
import { getActiveWarehousesCached, type Warehouse } from "../services/masterDataApi";
import { Permissions } from "../services/permissions";

const PAGE_SIZE = 12;

const INITIAL_FILTERS: InventoryAdjustmentListFilters = {
  search: "",
  adjustmentNo: "",
  warehouseId: "",
  status: "",
  reason: "",
  fromDate: "",
  toDate: "",
  page: 1,
  pageSize: PAGE_SIZE,
  sortBy: "adjustmentDate",
  sortDirection: "Desc",
};

function statusTone(status: InventoryAdjustmentListItem["status"]) {
  return status === "Posted" ? "success" : status === "Canceled" ? "neutral" : "warning";
}

export function InventoryAdjustmentsPage() {
  const { formatDate, t } = useI18n();
  const { hasPermission } = useAuth();
  const [rows, setRows] = useState<InventoryAdjustmentListItem[]>([]);
  const [warehouses, setWarehouses] = useState<Warehouse[]>([]);
  const [filters, setFilters] = useState<InventoryAdjustmentListFilters>(INITIAL_FILTERS);
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
          setError(t("module.inventoryAdjustments.referencesError"));
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
        const result = await listInventoryAdjustments(filters);

        if (active) {
          setRows(result.items);
          setTotalCount(result.totalCount);
          setTotalPages(result.totalPages);
        }
      } catch (loadError) {
        if (active) {
          setError(loadError instanceof ApiError ? loadError.message : t("module.inventoryAdjustments.loadError"));
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
  }, [filters, reloadKey]);

  const safePage = totalPages > 0 ? Math.min(filters.page, totalPages) : 1;
  const hasFilters = useMemo(
    () => filters.search.trim() || filters.adjustmentNo.trim() || filters.warehouseId || filters.status || filters.reason.trim() || filters.fromDate || filters.toDate,
    [filters],
  );
  const activeFilters = useMemo(() => {
    const chips: FilterChip[] = [];
    const warehouse = warehouses.find((row) => row.id === filters.warehouseId);

    if (filters.search.trim()) {
      chips.push({
        key: "search",
        label: t("module.inventoryAdjustments.filter.searchChip", { value: filters.search.trim() }),
        onRemove: () => setFilter("search", ""),
      });
    }

    if (filters.adjustmentNo.trim()) {
      chips.push({
        key: "adjustmentNo",
        label: t("module.inventoryAdjustments.filter.numberChip", { value: filters.adjustmentNo.trim() }),
        onRemove: () => setFilter("adjustmentNo", ""),
      });
    }

    if (warehouse) {
      chips.push({
        key: "warehouse",
        label: t("module.inventoryAdjustments.filter.warehouseChip", { value: `${warehouse.code} - ${warehouse.name}` }),
        onRemove: () => setFilter("warehouseId", ""),
      });
    }

    if (filters.status) {
      chips.push({
        key: "status",
        label: t("module.inventoryAdjustments.filter.statusChip", { value: t(`document.status.${filters.status}`) }),
        onRemove: () => setFilter("status", ""),
      });
    }

    if (filters.reason.trim()) {
      chips.push({
        key: "reason",
        label: t("module.inventoryAdjustments.filter.reasonChip", { value: filters.reason.trim() }),
        onRemove: () => setFilter("reason", ""),
      });
    }

    if (filters.fromDate || filters.toDate) {
      chips.push({
        key: "dateRange",
        label: t("module.inventoryAdjustments.filter.dateChip", { from: filters.fromDate || t("common.any"), to: filters.toDate || t("common.any") }),
        onRemove: () => {
          setFilter("fromDate", "");
          setFilter("toDate", "");
        },
      });
    }

    return chips;
  }, [filters, t, warehouses]);
  const resultLabel = totalCount === 1
    ? t("module.inventoryAdjustments.resultLabel.one", { count: totalCount })
    : t("module.inventoryAdjustments.resultLabel.other", { count: totalCount });
  const canCreateAdjustment = hasPermission(Permissions.InventoryAdjustmentCreate);

  function setFilter<K extends keyof InventoryAdjustmentListFilters>(key: K, value: InventoryAdjustmentListFilters[K]) {
    setFilters((current) => ({
      ...current,
      [key]: value,
      page: key === "page" ? Number(value) : 1,
    }));
  }

  return (
    <section className="hc-list-page">
      <PageHeader
        title="module.inventoryAdjustments.title"
        description="module.inventoryAdjustments.description"
        eyebrow="route.section.inventory"
        actions={canCreateAdjustment ? (
          <Link className="hc-button hc-button--primary hc-button--md" to="/inventory-adjustments/new">
            {t("module.inventoryAdjustments.new")}
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
            <Field label={t("module.inventoryAdjustments.adjustmentNo")}>
              <Input value={filters.adjustmentNo} onChange={(event) => setFilter("adjustmentNo", event.target.value)} />
            </Field>
            <Field label={t("table.warehouse")}>
              <Select value={filters.warehouseId} onChange={(event) => setFilter("warehouseId", event.target.value)}>
                <option value="">{t("module.inventoryAdjustments.allWarehouses")}</option>
                {warehouses.map((warehouse) => (
                  <option key={warehouse.id} value={warehouse.id}>{warehouse.code} - {warehouse.name}</option>
                ))}
              </Select>
            </Field>
            <Field label={t("table.status")}>
              <Select value={filters.status} onChange={(event) => setFilter("status", event.target.value)}>
                <option value="">{t("module.inventoryAdjustments.allStatuses")}</option>
                <option value="Draft">{t("document.status.Draft")}</option>
                <option value="Posted">{t("document.status.Posted")}</option>
                <option value="Canceled">{t("document.status.Canceled")}</option>
              </Select>
            </Field>
            <Field label={t("module.inventoryAdjustments.reason")}>
              <Input value={filters.reason} onChange={(event) => setFilter("reason", event.target.value)} />
            </Field>
            <Field label={t("common.fromDate")}>
              <Input type="date" value={filters.fromDate} onChange={(event) => setFilter("fromDate", event.target.value)} />
            </Field>
            <Field label={t("common.toDate")}>
              <Input type="date" value={filters.toDate} onChange={(event) => setFilter("toDate", event.target.value)} />
            </Field>
          </>
        )}
        onReset={() => setFilters(INITIAL_FILTERS)}
        primaryFilters={(
          <>
            <FilterDropdown aria-label={t("module.inventoryAdjustments.filter.warehouseAria")} value={filters.warehouseId} onChange={(event) => setFilter("warehouseId", event.target.value)}>
              <option value="">{t("table.warehouse")}</option>
              {warehouses.map((warehouse) => (
                <option key={warehouse.id} value={warehouse.id}>{warehouse.code} - {warehouse.name}</option>
              ))}
            </FilterDropdown>
            <FilterDropdown aria-label={t("module.inventoryAdjustments.filter.statusAria")} value={filters.status} onChange={(event) => setFilter("status", event.target.value)}>
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
            aria-label={t("module.inventoryAdjustments.searchAria")}
            placeholder={t("module.inventoryAdjustments.searchPlaceholder")}
            value={filters.search}
            onChange={(event) => setFilter("search", event.target.value)}
          />
        )}
        secondaryActiveCount={(filters.adjustmentNo.trim() ? 1 : 0) + (filters.reason.trim() ? 1 : 0)}
        secondaryFilters={(
          <>
            <Field label={t("module.inventoryAdjustments.adjustmentNo")}>
              <Input value={filters.adjustmentNo} onChange={(event) => setFilter("adjustmentNo", event.target.value)} />
            </Field>
            <Field label={t("module.inventoryAdjustments.reason")}>
              <Input value={filters.reason} onChange={(event) => setFilter("reason", event.target.value)} />
            </Field>
          </>
        )}
      />

      {error ? (
        <Card padding="md">
          <EmptyState
            title="module.inventoryAdjustments.loadError"
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
              <th scope="col">{t("module.inventoryAdjustments.adjustmentNo")}</th>
              <th scope="col">{t("module.inventoryAdjustments.adjustmentDate")}</th>
              <th scope="col">{t("table.warehouse")}</th>
              <th scope="col">{t("table.status")}</th>
              <th scope="col">{t("module.inventoryAdjustments.reason")}</th>
              <th scope="col">{t("table.lines")}</th>
            </tr>
          )}
          rows={rows.map((row) => (
            <tr key={row.id} className="hc-table__row">
              <td>
                <Link className="hc-table__link" to={`/inventory-adjustments/${row.id}`}>
                  {row.adjustmentNo}
                </Link>
              </td>
              <td>{formatDate(row.adjustmentDate)}</td>
              <td>
                <div className="hc-table__cell-strong">
                  <span className="hc-table__title">{row.warehouseName}</span>
                  <span className="hc-table__subtitle">{row.warehouseCode}</span>
                </div>
              </td>
              <td><Badge tone={statusTone(row.status)}>{t(`document.status.${row.status}`)}</Badge></td>
              <td>{row.reason}</td>
              <td>{row.lineCount}</td>
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
            ? <EmptyState title="module.inventoryAdjustments.emptyFiltered" description="module.inventoryAdjustments.emptyFilteredDescription" />
            : <EmptyState title="module.inventoryAdjustments.empty" description="module.inventoryAdjustments.emptyDescription" />}
        />
      ) : null}
    </section>
  );
}
