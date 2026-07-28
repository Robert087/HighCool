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
  listInventoryTransfers,
  type InventoryTransferListFilters,
  type InventoryTransferListItem,
} from "../services/inventoryTransfersApi";
import { useAuth } from "../features/auth/AuthProvider";
import { getActiveWarehousesCached, type Warehouse } from "../services/masterDataApi";
import { Permissions } from "../services/permissions";

const PAGE_SIZE = 12;

const INITIAL_FILTERS: InventoryTransferListFilters = {
  search: "",
  transferNo: "",
  sourceWarehouseId: "",
  destinationWarehouseId: "",
  status: "",
  fromDate: "",
  toDate: "",
  page: 1,
  pageSize: PAGE_SIZE,
  sortBy: "transferDate",
  sortDirection: "Desc",
};

function statusTone(status: InventoryTransferListItem["status"]) {
  return status === "Posted" ? "success" : status === "Canceled" ? "neutral" : "warning";
}

export function InventoryTransfersPage() {
  const { formatDate, t } = useI18n();
  const { hasPermission } = useAuth();
  const [rows, setRows] = useState<InventoryTransferListItem[]>([]);
  const [warehouses, setWarehouses] = useState<Warehouse[]>([]);
  const [filters, setFilters] = useState<InventoryTransferListFilters>(INITIAL_FILTERS);
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
          setError(t("module.inventoryTransfers.referencesError"));
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
        const result = await listInventoryTransfers(filters);

        if (active) {
          setRows(result.items);
          setTotalCount(result.totalCount);
          setTotalPages(result.totalPages);
        }
      } catch (loadError) {
        if (active) {
          setError(loadError instanceof ApiError ? loadError.message : t("module.inventoryTransfers.loadError"));
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
    () => filters.search.trim() || filters.transferNo.trim() || filters.sourceWarehouseId || filters.destinationWarehouseId || filters.status || filters.fromDate || filters.toDate,
    [filters],
  );
  const activeFilters = useMemo(() => {
    const chips: FilterChip[] = [];
    const sourceWarehouse = warehouses.find((row) => row.id === filters.sourceWarehouseId);
    const destinationWarehouse = warehouses.find((row) => row.id === filters.destinationWarehouseId);

    if (filters.search.trim()) {
      chips.push({
        key: "search",
        label: t("module.inventoryTransfers.filter.searchChip", { value: filters.search.trim() }),
        onRemove: () => setFilter("search", ""),
      });
    }

    if (filters.transferNo.trim()) {
      chips.push({
        key: "transferNo",
        label: t("module.inventoryTransfers.filter.numberChip", { value: filters.transferNo.trim() }),
        onRemove: () => setFilter("transferNo", ""),
      });
    }

    if (sourceWarehouse) {
      chips.push({
        key: "sourceWarehouse",
        label: t("module.inventoryTransfers.filter.sourceWarehouseChip", { value: `${sourceWarehouse.code} - ${sourceWarehouse.name}` }),
        onRemove: () => setFilter("sourceWarehouseId", ""),
      });
    }

    if (destinationWarehouse) {
      chips.push({
        key: "destinationWarehouse",
        label: t("module.inventoryTransfers.filter.destinationWarehouseChip", { value: `${destinationWarehouse.code} - ${destinationWarehouse.name}` }),
        onRemove: () => setFilter("destinationWarehouseId", ""),
      });
    }

    if (filters.status) {
      chips.push({
        key: "status",
        label: t("module.inventoryTransfers.filter.statusChip", { value: t(`document.status.${filters.status}`) }),
        onRemove: () => setFilter("status", ""),
      });
    }

    if (filters.fromDate || filters.toDate) {
      chips.push({
        key: "dateRange",
        label: t("module.inventoryTransfers.filter.dateChip", { from: filters.fromDate || t("common.any"), to: filters.toDate || t("common.any") }),
        onRemove: () => {
          setFilter("fromDate", "");
          setFilter("toDate", "");
        },
      });
    }

    return chips;
  }, [filters, t, warehouses]);
  const resultLabel = totalCount === 1
    ? t("module.inventoryTransfers.resultLabel.one", { count: totalCount })
    : t("module.inventoryTransfers.resultLabel.other", { count: totalCount });
  const canCreateTransfer = hasPermission(Permissions.InventoryTransferCreate);

  function setFilter<K extends keyof InventoryTransferListFilters>(key: K, value: InventoryTransferListFilters[K]) {
    setFilters((current) => ({
      ...current,
      [key]: value,
      page: key === "page" ? Number(value) : 1,
    }));
  }

  return (
    <section className="hc-list-page">
      <PageHeader
        title="module.inventoryTransfers.title"
        description="module.inventoryTransfers.description"
        eyebrow="route.section.inventory"
        actions={canCreateTransfer ? (
          <Link className="hc-button hc-button--primary hc-button--md" to="/inventory-transfers/new">
            {t("module.inventoryTransfers.new")}
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
            <Field label={t("module.inventoryTransfers.transferNo")}>
              <Input value={filters.transferNo} onChange={(event) => setFilter("transferNo", event.target.value)} />
            </Field>
            <Field label={t("module.inventoryTransfers.sourceWarehouse")}>
              <Select value={filters.sourceWarehouseId} onChange={(event) => setFilter("sourceWarehouseId", event.target.value)}>
                <option value="">{t("module.inventoryTransfers.allSourceWarehouses")}</option>
                {warehouses.map((warehouse) => (
                  <option key={warehouse.id} value={warehouse.id}>{warehouse.code} - {warehouse.name}</option>
                ))}
              </Select>
            </Field>
            <Field label={t("module.inventoryTransfers.destinationWarehouse")}>
              <Select value={filters.destinationWarehouseId} onChange={(event) => setFilter("destinationWarehouseId", event.target.value)}>
                <option value="">{t("module.inventoryTransfers.allDestinationWarehouses")}</option>
                {warehouses.map((warehouse) => (
                  <option key={warehouse.id} value={warehouse.id}>{warehouse.code} - {warehouse.name}</option>
                ))}
              </Select>
            </Field>
            <Field label={t("table.status")}>
              <Select value={filters.status} onChange={(event) => setFilter("status", event.target.value)}>
                <option value="">{t("module.inventoryTransfers.allStatuses")}</option>
                <option value="Draft">{t("document.status.Draft")}</option>
                <option value="Posted">{t("document.status.Posted")}</option>
                <option value="Canceled">{t("document.status.Canceled")}</option>
              </Select>
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
            <FilterDropdown aria-label={t("module.inventoryTransfers.filter.sourceWarehouseAria")} value={filters.sourceWarehouseId} onChange={(event) => setFilter("sourceWarehouseId", event.target.value)}>
              <option value="">{t("module.inventoryTransfers.sourceWarehouse")}</option>
              {warehouses.map((warehouse) => (
                <option key={warehouse.id} value={warehouse.id}>{warehouse.code} - {warehouse.name}</option>
              ))}
            </FilterDropdown>
            <FilterDropdown aria-label={t("module.inventoryTransfers.filter.destinationWarehouseAria")} value={filters.destinationWarehouseId} onChange={(event) => setFilter("destinationWarehouseId", event.target.value)}>
              <option value="">{t("module.inventoryTransfers.destinationWarehouse")}</option>
              {warehouses.map((warehouse) => (
                <option key={warehouse.id} value={warehouse.id}>{warehouse.code} - {warehouse.name}</option>
              ))}
            </FilterDropdown>
            <FilterDropdown aria-label={t("module.inventoryTransfers.filter.statusAria")} value={filters.status} onChange={(event) => setFilter("status", event.target.value)}>
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
            aria-label={t("module.inventoryTransfers.searchAria")}
            placeholder={t("module.inventoryTransfers.searchPlaceholder")}
            value={filters.search}
            onChange={(event) => setFilter("search", event.target.value)}
          />
        )}
        secondaryActiveCount={filters.transferNo.trim() ? 1 : 0}
        secondaryFilters={(
          <Field label={t("module.inventoryTransfers.transferNo")}>
            <Input value={filters.transferNo} onChange={(event) => setFilter("transferNo", event.target.value)} />
          </Field>
        )}
      />

      {error ? (
        <Card padding="md">
          <EmptyState
            title="module.inventoryTransfers.loadError"
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
              <th scope="col">{t("module.inventoryTransfers.transferNo")}</th>
              <th scope="col">{t("module.inventoryTransfers.transferDate")}</th>
              <th scope="col">{t("module.inventoryTransfers.sourceWarehouse")}</th>
              <th scope="col">{t("module.inventoryTransfers.destinationWarehouse")}</th>
              <th scope="col">{t("table.status")}</th>
              <th scope="col">{t("table.lines")}</th>
            </tr>
          )}
          rows={rows.map((row) => (
            <tr key={row.id} className="hc-table__row">
              <td>
                <Link className="hc-table__link" to={`/inventory-transfers/${row.id}`}>
                  {row.transferNo}
                </Link>
              </td>
              <td>{formatDate(row.transferDate)}</td>
              <td>
                <div className="hc-table__cell-strong">
                  <span className="hc-table__title">{row.sourceWarehouseName}</span>
                  <span className="hc-table__subtitle">{row.sourceWarehouseCode}</span>
                </div>
              </td>
              <td>
                <div className="hc-table__cell-strong">
                  <span className="hc-table__title">{row.destinationWarehouseName}</span>
                  <span className="hc-table__subtitle">{row.destinationWarehouseCode}</span>
                </div>
              </td>
              <td><Badge tone={statusTone(row.status)}>{t(`document.status.${row.status}`)}</Badge></td>
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
            ? <EmptyState title="module.inventoryTransfers.emptyFiltered" description="module.inventoryTransfers.emptyFilteredDescription" />
            : <EmptyState title="module.inventoryTransfers.empty" description="module.inventoryTransfers.emptyDescription" />}
        />
      ) : null}
    </section>
  );
}
