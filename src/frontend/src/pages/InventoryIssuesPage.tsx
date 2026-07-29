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
  INVENTORY_ISSUE_REASONS,
  listInventoryIssues,
  type InventoryIssueListFilters,
  type InventoryIssueListItem,
} from "../services/inventoryIssuesApi";
import { getActiveWarehousesCached, type Warehouse } from "../services/masterDataApi";
import { Permissions } from "../services/permissions";

const PAGE_SIZE = 12;

const INITIAL_FILTERS: InventoryIssueListFilters = {
  search: "",
  issueNo: "",
  warehouseId: "",
  reason: "",
  status: "",
  fromDate: "",
  toDate: "",
  page: 1,
  pageSize: PAGE_SIZE,
  sortBy: "issueDate",
  sortDirection: "Desc",
};

function statusTone(status: InventoryIssueListItem["status"]) {
  return status === "Posted" ? "success" : status === "Canceled" ? "neutral" : "warning";
}

export function InventoryIssuesPage() {
  const { formatDate, t } = useI18n();
  const { hasPermission } = useAuth();
  const [rows, setRows] = useState<InventoryIssueListItem[]>([]);
  const [warehouses, setWarehouses] = useState<Warehouse[]>([]);
  const [filters, setFilters] = useState<InventoryIssueListFilters>(INITIAL_FILTERS);
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
          setError(t("module.inventoryIssues.referencesError"));
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
        const result = await listInventoryIssues(filters);

        if (active) {
          setRows(result.items);
          setTotalCount(result.totalCount);
          setTotalPages(result.totalPages);
        }
      } catch (loadError) {
        if (active) {
          setError(loadError instanceof ApiError ? loadError.message : t("module.inventoryIssues.loadError"));
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
    () => filters.search.trim() || filters.issueNo.trim() || filters.warehouseId || filters.reason || filters.status || filters.fromDate || filters.toDate,
    [filters],
  );
  const activeFilters = useMemo(() => {
    const chips: FilterChip[] = [];
    const warehouse = warehouses.find((row) => row.id === filters.warehouseId);

    if (filters.search.trim()) {
      chips.push({
        key: "search",
        label: t("module.inventoryIssues.filter.searchChip", { value: filters.search.trim() }),
        onRemove: () => setFilter("search", ""),
      });
    }

    if (filters.issueNo.trim()) {
      chips.push({
        key: "issueNo",
        label: t("module.inventoryIssues.filter.numberChip", { value: filters.issueNo.trim() }),
        onRemove: () => setFilter("issueNo", ""),
      });
    }

    if (warehouse) {
      chips.push({
        key: "warehouse",
        label: t("module.inventoryIssues.filter.warehouseChip", { value: `${warehouse.code} - ${warehouse.name}` }),
        onRemove: () => setFilter("warehouseId", ""),
      });
    }

    if (filters.reason) {
      chips.push({
        key: "reason",
        label: t("module.inventoryIssues.filter.reasonChip", { value: t(`inventory.issueReason.${filters.reason}`) }),
        onRemove: () => setFilter("reason", ""),
      });
    }

    if (filters.status) {
      chips.push({
        key: "status",
        label: t("module.inventoryIssues.filter.statusChip", { value: t(`document.status.${filters.status}`) }),
        onRemove: () => setFilter("status", ""),
      });
    }

    if (filters.fromDate || filters.toDate) {
      chips.push({
        key: "dateRange",
        label: t("module.inventoryIssues.filter.dateChip", { from: filters.fromDate || t("common.any"), to: filters.toDate || t("common.any") }),
        onRemove: () => {
          setFilter("fromDate", "");
          setFilter("toDate", "");
        },
      });
    }

    return chips;
  }, [filters, t, warehouses]);
  const resultLabel = totalCount === 1
    ? t("module.inventoryIssues.resultLabel.one", { count: totalCount })
    : t("module.inventoryIssues.resultLabel.other", { count: totalCount });
  const canCreateIssue = hasPermission(Permissions.InventoryIssueCreate);

  function setFilter<K extends keyof InventoryIssueListFilters>(key: K, value: InventoryIssueListFilters[K]) {
    setFilters((current) => ({
      ...current,
      [key]: value,
      page: key === "page" ? Number(value) : 1,
    }));
  }

  return (
    <section className="hc-list-page">
      <PageHeader
        title="module.inventoryIssues.title"
        description="module.inventoryIssues.description"
        eyebrow="route.section.inventory"
        actions={canCreateIssue ? (
          <Link className="hc-button hc-button--primary hc-button--md" to="/inventory-issues/new">
            {t("module.inventoryIssues.new")}
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
            <Field label={t("module.inventoryIssues.issueNo")}>
              <Input value={filters.issueNo} onChange={(event) => setFilter("issueNo", event.target.value)} />
            </Field>
            <Field label={t("module.inventoryIssues.warehouse")}>
              <Select value={filters.warehouseId} onChange={(event) => setFilter("warehouseId", event.target.value)}>
                <option value="">{t("module.inventoryIssues.allWarehouses")}</option>
                {warehouses.map((warehouse) => (
                  <option key={warehouse.id} value={warehouse.id}>{warehouse.code} - {warehouse.name}</option>
                ))}
              </Select>
            </Field>
            <Field label={t("module.inventoryIssues.reason")}>
              <Select value={filters.reason} onChange={(event) => setFilter("reason", event.target.value)}>
                <option value="">{t("module.inventoryIssues.allReasons")}</option>
                {INVENTORY_ISSUE_REASONS.map((reason) => (
                  <option key={reason} value={reason}>{t(`inventory.issueReason.${reason}`)}</option>
                ))}
              </Select>
            </Field>
            <Field label={t("table.status")}>
              <Select value={filters.status} onChange={(event) => setFilter("status", event.target.value)}>
                <option value="">{t("module.inventoryIssues.allStatuses")}</option>
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
            <FilterDropdown aria-label={t("module.inventoryIssues.filter.warehouseAria")} value={filters.warehouseId} onChange={(event) => setFilter("warehouseId", event.target.value)}>
              <option value="">{t("module.inventoryIssues.warehouse")}</option>
              {warehouses.map((warehouse) => (
                <option key={warehouse.id} value={warehouse.id}>{warehouse.code} - {warehouse.name}</option>
              ))}
            </FilterDropdown>
            <FilterDropdown aria-label={t("module.inventoryIssues.filter.reasonAria")} value={filters.reason} onChange={(event) => setFilter("reason", event.target.value)}>
              <option value="">{t("module.inventoryIssues.reason")}</option>
              {INVENTORY_ISSUE_REASONS.map((reason) => (
                <option key={reason} value={reason}>{t(`inventory.issueReason.${reason}`)}</option>
              ))}
            </FilterDropdown>
            <FilterDropdown aria-label={t("module.inventoryIssues.filter.statusAria")} value={filters.status} onChange={(event) => setFilter("status", event.target.value)}>
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
            aria-label={t("module.inventoryIssues.searchAria")}
            placeholder={t("module.inventoryIssues.searchPlaceholder")}
            value={filters.search}
            onChange={(event) => setFilter("search", event.target.value)}
          />
        )}
        secondaryActiveCount={filters.issueNo.trim() ? 1 : 0}
        secondaryFilters={(
          <Field label={t("module.inventoryIssues.issueNo")}>
            <Input value={filters.issueNo} onChange={(event) => setFilter("issueNo", event.target.value)} />
          </Field>
        )}
      />

      {error ? (
        <Card padding="md">
          <EmptyState
            title="module.inventoryIssues.loadError"
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
              <th scope="col">{t("module.inventoryIssues.issueNo")}</th>
              <th scope="col">{t("module.inventoryIssues.issueDate")}</th>
              <th scope="col">{t("module.inventoryIssues.warehouse")}</th>
              <th scope="col">{t("module.inventoryIssues.reason")}</th>
              <th scope="col">{t("table.status")}</th>
              <th scope="col">{t("module.inventoryIssues.requestedBy")}</th>
              <th scope="col">{t("table.createdBy")}</th>
              <th scope="col">{t("module.inventoryIssues.postedAt")}</th>
            </tr>
          )}
          rows={rows.map((row) => (
            <tr key={row.id} className="hc-table__row">
              <td>
                <Link className="hc-table__link" to={`/inventory-issues/${row.id}`}>
                  {row.issueNo}
                </Link>
              </td>
              <td>{formatDate(row.issueDate)}</td>
              <td>
                <div className="hc-table__cell-strong">
                  <span className="hc-table__title">{row.warehouseName}</span>
                  <span className="hc-table__subtitle">{row.warehouseCode}</span>
                </div>
              </td>
              <td>{t(`inventory.issueReason.${row.reason}`)}</td>
              <td><Badge tone={statusTone(row.status)}>{t(`document.status.${row.status}`)}</Badge></td>
              <td>{row.requestedBy || t("common.notAvailable")}</td>
              <td>{row.createdBy}</td>
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
            ? <EmptyState title="module.inventoryIssues.emptyFiltered" description="module.inventoryIssues.emptyFilteredDescription" />
            : <EmptyState title="module.inventoryIssues.empty" description="module.inventoryIssues.emptyDescription" />}
        />
      ) : null}
    </section>
  );
}
