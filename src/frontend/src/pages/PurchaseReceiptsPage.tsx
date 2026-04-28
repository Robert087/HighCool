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
import { listPurchaseReceiptDrafts, type PurchaseReceiptListItem } from "../services/purchaseReceiptsApi";
import { formatDate } from "../i18n";

const PAGE_SIZE = 10;
const INITIAL_FILTERS = {
  status: "",
  source: "",
  fromDate: "",
  toDate: "",
};

export function PurchaseReceiptsPage() {
  const [rows, setRows] = useState<PurchaseReceiptListItem[]>([]);
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

  async function handleDelete(row: PurchaseReceiptListItem) {
    if (row.status === "Posted") {
      showToast({
        tone: "warning",
        title: t("Cannot delete posted receipt"),
        description: t("Posted receipts cannot be deleted from the UI."),
      });
      return;
    }

    const confirmed = await confirm({
      title: t("Delete purchase receipt"),
      description: t("Permanent delete is not available in this version. You can confirm this message and keep working, but no deletion will happen."),
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
        const result = await listPurchaseReceiptDrafts({
          search,
          status: filters.status,
          source: filters.source,
          fromDate: filters.fromDate,
          toDate: filters.toDate,
          page,
          pageSize: PAGE_SIZE,
          sortBy: "receiptDate",
          sortDirection: "Desc",
        });

        if (active) {
          setRows(result.items);
          setTotalCount(result.totalCount);
          setTotalPages(result.totalPages);
        }
      } catch (loadError) {
        if (active) {
          setError(loadError instanceof ApiError ? loadError.message : t("module.purchaseReceipts.error"));
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
        label: t("module.purchaseReceipts.filter.searchChip", { value: search.trim() }),
        onRemove: () => setSearch(""),
      });
    }

    if (filters.status) {
      chips.push({
        key: "status",
        label: t("module.purchaseReceipts.filter.statusChip", { value: t(`status.${filters.status.toLowerCase()}`) }),
        onRemove: () => setFilters((current) => ({ ...current, status: "" })),
      });
    }

    if (filters.source) {
      chips.push({
        key: "source",
        label: t("module.purchaseReceipts.filter.sourceChip", { value: filters.source === "Linked" ? t("module.purchaseReceipts.poLinked") : t("status.manual") }),
        onRemove: () => setFilters((current) => ({ ...current, source: "" })),
      });
    }

    if (filters.fromDate || filters.toDate) {
      chips.push({
        key: "dateRange",
        label: t("module.purchaseReceipts.filter.dateChip", {
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
    ? t("module.purchaseReceipts.resultLabel.one", { count: totalCount })
    : t("module.purchaseReceipts.resultLabel.other", { count: totalCount });

  return (
    <section className="hc-list-page">
      <PageHeader
        title="module.purchaseReceipts"
        actions={
          <Link className="hc-button hc-button--primary hc-button--md" to="/purchase-receipts/new">
            {t("module.purchaseReceipts.new")}
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
            <Field label="Source">
              <Select value={filters.source} onChange={(event) => setFilters((current) => ({ ...current, source: event.target.value }))}>
                <option value="">All sources</option>
                <option value="Linked">PO linked</option>
                <option value="Manual">Manual</option>
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
            <FilterDropdown aria-label="Purchase receipt status filter" value={filters.status} onChange={(event) => setFilters((current) => ({ ...current, status: event.target.value }))}>
              <option value="">Status</option>
              <option value="Draft">Draft</option>
              <option value="Posted">Posted</option>
              <option value="Canceled">Canceled</option>
            </FilterDropdown>
            <FilterDropdown aria-label="Purchase receipt source filter" value={filters.source} onChange={(event) => setFilters((current) => ({ ...current, source: event.target.value }))}>
              <option value="">Source</option>
              <option value="Linked">PO linked</option>
              <option value="Manual">Manual</option>
            </FilterDropdown>
          </>
        )}
        resultLabel={resultLabel}
        search={(
          <FilterTextInput
            aria-label={t("module.purchaseReceipts")}
            placeholder={t("module.purchaseReceipts.searchPlaceholder")}
            value={search}
            onChange={(event) => setSearch(event.target.value)}
          />
        )}
        mobileTriggerOnly
      />

      {error ? (
        <div className="hc-card hc-card--md">
          <EmptyState
            title="module.purchaseReceipts.error"
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
              <th scope="col">Receipt</th>
              <th scope="col">Supplier</th>
              <th scope="col">Receipt Context</th>
              <th scope="col">Status</th>
              <th scope="col" className="hc-table__head-actions" aria-label="Actions" />
            </tr>
          }
          rows={rows.map((row) => (
            <tr key={row.id} className="hc-table__row">
              <td>
                <div className="hc-table__cell-strong hc-table__primary-cell">
                  <span className="hc-table__title">{row.receiptNo}</span>
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
                  <div className="hc-table__cell-strong">
                    <span className="hc-table__title">{row.warehouseName}</span>
                    <span className="hc-table__subtitle">{row.warehouseCode}</span>
                  </div>
                  <div className="hc-table__metric">
                    <span className="hc-table__metric-label">{t("common.source")}</span>
                    <span className="hc-table__subtitle">{row.purchaseOrderNo ?? t("status.manual")}</span>
                  </div>
                  <div className="hc-table__metric">
                    <span className="hc-table__metric-label">{t("common.date")}</span>
                    <span className="hc-table__subtitle">{formatDate(row.receiptDate)}</span>
                  </div>
                </div>
              </td>
              <td>
                <div className="hc-table__status-stack">
                  <Badge tone={row.status === "Posted" ? "success" : row.status === "Canceled" ? "danger" : "warning"}>{row.status}</Badge>
                </div>
              </td>
              <td className="hc-table__cell-actions">
                <RowActions
                  primaryAction={<Link className="hc-button hc-button--secondary hc-button--sm hc-table__action-button" to={`/purchase-receipts/${row.id}/edit`}>{t("common.view")}</Link>}
                  menuItems={[
                    ...(row.status === "Draft" ? [{ label: t("common.edit"), to: `/purchase-receipts/${row.id}/edit` }] : []),
                    ...(row.status === "Draft" ? [{ label: t("common.delete"), onSelect: () => handleDelete(row), tone: "danger" as const }] : []),
                  ]}
                />
              </td>
            </tr>
          ))}
          footer={<Pagination currentPage={safePage} onPageChange={setPage} pageSize={PAGE_SIZE} totalCount={totalCount} totalPages={Math.max(totalPages, 1)} />}
          emptyState={hasFilters
            ? <EmptyState title="module.purchaseReceipts.emptyFiltered" description="module.purchaseReceipts.emptyFilteredDescription" />
            : <EmptyState title="module.purchaseReceipts.empty" description="module.purchaseReceipts.emptyDescription" action={<Link className="hc-button hc-button--primary hc-button--md" to="/purchase-receipts/new">{t("module.purchaseReceipts.new")}</Link>} />}
        />
      ) : null}
      {dialog}
    </section>
  );
}
