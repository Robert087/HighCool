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
  useI18n,
  useConfirmationDialog,
  useToast,
} from "../components/ui";
import { ApiError } from "../services/api";
import { listPurchaseOrders, type PurchaseOrderListItem } from "../services/purchaseOrdersApi";
import { formatDate } from "../i18n";

const PAGE_SIZE = 10;
const INITIAL_FILTERS = {
  status: "",
  receiptProgress: "",
  fromDate: "",
  toDate: "",
};

function progressTone(status: PurchaseOrderListItem["receiptProgressStatus"]) {
  switch (status) {
    case "FullyReceived":
      return "success";
    case "PartiallyReceived":
      return "warning";
    default:
      return "neutral";
  }
}

export function PurchaseOrdersPage() {
  const [rows, setRows] = useState<PurchaseOrderListItem[]>([]);
  const [totalCount, setTotalCount] = useState(0);
  const [totalPages, setTotalPages] = useState(0);
  const [search, setSearch] = useState("");
  const [filters, setFilters] = useState(INITIAL_FILTERS);
  const [error, setError] = useState("");
  const [loading, setLoading] = useState(true);
  const [page, setPage] = useState(1);
  const [reloadKey, setReloadKey] = useState(0);
  const { showToast } = useToast();
  const { confirm, dialog } = useConfirmationDialog();
  const { t } = useI18n();

  async function handleDelete(row: PurchaseOrderListItem) {
    if (row.status === "Posted") {
      showToast({
        tone: "warning",
        title: t("module.purchaseOrders.deletePostedTitle"),
        description: t("module.purchaseOrders.deletePostedDescription"),
      });
      return;
    }

    const confirmed = await confirm({
      title: t("module.purchaseOrders.deleteTitle"),
      description: t("module.purchaseOrders.deleteDescription"),
      confirmLabel: t("module.purchaseOrders.deleteConfirm"),
      cancelLabel: t("app.close"),
      tone: "warning",
    });

    if (confirmed) {
      showToast({
        tone: "info",
        title: t("module.purchaseOrders.deleteUnavailableTitle"),
        description: t("module.purchaseOrders.deleteUnavailableDescription"),
      });
    }
  }

  useEffect(() => {
    let active = true;

    async function load() {
      try {
        setLoading(true);
        setError("");
        const result = await listPurchaseOrders({
          search,
          status: filters.status,
          receiptProgress: filters.receiptProgress,
          fromDate: filters.fromDate,
          toDate: filters.toDate,
          page,
          pageSize: PAGE_SIZE,
          sortBy: "orderDate",
          sortDirection: "Desc",
        });
        if (active) {
          setRows(result.items);
          setTotalCount(result.totalCount);
          setTotalPages(result.totalPages);
        }
      } catch (loadError) {
        if (active) {
          setError(loadError instanceof ApiError ? loadError.message : t("module.purchaseOrders.error"));
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
  }, [filters, page, reloadKey, search]);

  useEffect(() => {
    setPage(1);
  }, [search, filters]);

  const activeFilters = useMemo(() => {
    const chips: FilterChip[] = [];

    if (search.trim()) {
      chips.push({
        key: "search",
        label: t("module.purchaseOrders.filter.searchChip", { value: search.trim() }),
        onRemove: () => setSearch(""),
      });
    }

    if (filters.status) {
      chips.push({
        key: "status",
        label: t("module.purchaseOrders.filter.statusChip", { value: t(`status.${filters.status.toLowerCase()}`) }),
        onRemove: () => setFilters((current) => ({ ...current, status: "" })),
      });
    }

    if (filters.receiptProgress) {
      chips.push({
        key: "receiptProgress",
        label: t("module.purchaseOrders.filter.progressChip", {
          value: t(`status.${filters.receiptProgress.charAt(0).toLowerCase()}${filters.receiptProgress.slice(1)}`),
        }),
        onRemove: () => setFilters((current) => ({ ...current, receiptProgress: "" })),
      });
    }

    if (filters.fromDate || filters.toDate) {
      chips.push({
        key: "dateRange",
        label: t("module.purchaseOrders.filter.dateChip", {
          from: filters.fromDate || t("common.any"),
          to: filters.toDate || t("common.any"),
        }),
        onRemove: () => setFilters((current) => ({ ...current, fromDate: "", toDate: "" })),
      });
    }

    return chips;
  }, [filters, search]);
  const hasFilters = activeFilters.length > 0;
  const safePage = totalPages > 0 ? Math.min(page, totalPages) : 1;
  const resultLabel = totalCount === 1
    ? t("module.purchaseOrders.resultLabel.one", { count: totalCount })
    : t("module.purchaseOrders.resultLabel.other", { count: totalCount });

  return (
    <section className="hc-list-page">
      <PageHeader
        title="module.purchaseOrders"
        actions={
          <Link className="hc-button hc-button--primary hc-button--md" to="/purchase-orders/new">
            {t("module.purchaseOrders.new")}
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
            <Field label="Receipt progress">
              <Select value={filters.receiptProgress} onChange={(event) => setFilters((current) => ({ ...current, receiptProgress: event.target.value }))}>
                <option value="">All progress states</option>
                <option value="NotReceived">Not received</option>
                <option value="PartiallyReceived">Partially received</option>
                <option value="FullyReceived">Fully received</option>
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
            <FilterDropdown aria-label="Purchase order status filter" value={filters.status} onChange={(event) => setFilters((current) => ({ ...current, status: event.target.value }))}>
              <option value="">Status</option>
              <option value="Draft">Draft</option>
              <option value="Posted">Posted</option>
              <option value="Canceled">Canceled</option>
            </FilterDropdown>
            <FilterDropdown aria-label="Purchase order progress filter" value={filters.receiptProgress} onChange={(event) => setFilters((current) => ({ ...current, receiptProgress: event.target.value }))}>
              <option value="">Receipt progress</option>
              <option value="NotReceived">Not received</option>
              <option value="PartiallyReceived">Partially received</option>
              <option value="FullyReceived">Fully received</option>
            </FilterDropdown>
          </>
        )}
        resultLabel={resultLabel}
        search={(
          <FilterTextInput
            aria-label={t("module.purchaseOrders")}
            placeholder={t("module.purchaseOrders.searchPlaceholder")}
            value={search}
            onChange={(event) => setSearch(event.target.value)}
          />
        )}
        mobileTriggerOnly
      />

      {error ? (
        <div className="hc-card hc-card--md">
          <EmptyState
            title="module.purchaseOrders.error"
            description={error}
            action={<Button variant="secondary" onClick={() => setReloadKey((current) => current + 1)}>{t("common.retry")}</Button>}
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
          hasData={rows.length > 0}
          columns={
            <tr>
              <th scope="col">PO</th>
              <th scope="col">Supplier</th>
              <th scope="col">Dates</th>
              <th scope="col">Status</th>
              <th scope="col">Receipt Progress</th>
              <th scope="col" className="hc-table__head-actions" aria-label="Actions" />
            </tr>
          }
          rows={rows.map((row) => (
            <tr key={row.id} className="hc-table__row">
              <td>
                <div className="hc-table__cell-strong hc-table__primary-cell">
                  <span className="hc-table__title">{row.poNo}</span>
                  <span className="hc-table__subtitle">
                    {row.lineCount === 1
                      ? t("module.purchaseReceipts.lineCount.one", { count: row.lineCount })
                      : t("module.purchaseReceipts.lineCount.other", { count: row.lineCount })}
                  </span>
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
                  <div className="hc-table__metric">
                    <span className="hc-table__metric-label">{t("common.orderDate")}</span>
                    <span className="hc-table__subtitle">{formatDate(row.orderDate)}</span>
                  </div>
                  <div className="hc-table__metric">
                    <span className="hc-table__metric-label">{t("common.date")}</span>
                    <span className="hc-table__subtitle">{row.expectedDate ? formatDate(row.expectedDate) : t("status.notSet")}</span>
                  </div>
                </div>
              </td>
              <td>
                <div className="hc-table__status-stack">
                  <Badge tone={row.status === "Posted" ? "success" : row.status === "Canceled" ? "danger" : "warning"}>{row.status}</Badge>
                </div>
              </td>
              <td>
                <div className="hc-table__status-stack">
                  <Badge tone={progressTone(row.receiptProgressStatus)}>{row.receiptProgressStatus}</Badge>
                </div>
              </td>
              <td className="hc-table__cell-actions">
                <RowActions
                  primaryAction={<Link className="hc-button hc-button--secondary hc-button--sm hc-table__action-button" to={`/purchase-orders/${row.id}/edit`}>{t("common.view")}</Link>}
                  menuItems={[
                    ...(row.status === "Draft" ? [{ label: t("common.edit"), to: `/purchase-orders/${row.id}/edit` }] : []),
                    ...(row.status === "Draft" ? [{ label: t("common.delete"), onSelect: () => handleDelete(row), tone: "danger" as const }] : []),
                    ...(row.status === "Posted" && row.receiptProgressStatus !== "FullyReceived"
                      ? [{ label: t("module.purchaseReceipts.new"), to: `/purchase-receipts/new?purchaseOrderId=${row.id}` }]
                      : []),
                  ]}
                />
              </td>
            </tr>
          ))}
          footer={<Pagination currentPage={safePage} onPageChange={setPage} pageSize={PAGE_SIZE} totalCount={totalCount} totalPages={Math.max(totalPages, 1)} />}
          emptyState={hasFilters
            ? <EmptyState title="module.purchaseOrders.emptyFiltered" description="module.purchaseOrders.emptyFilteredDescription" />
            : <EmptyState title="module.purchaseOrders.empty" description="module.purchaseOrders.emptyDescription" action={<Link className="hc-button hc-button--primary hc-button--md" to="/purchase-orders/new">{t("module.purchaseOrders.new")}</Link>} />}
        />
      ) : null}
      {dialog}
    </section>
  );
}
