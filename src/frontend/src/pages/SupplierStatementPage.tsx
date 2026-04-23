import { Fragment, useEffect, useMemo, useState } from "react";
import { Link, useSearchParams } from "react-router-dom";
import { RowActions } from "../components/patterns";
import {
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
  getSupplierStatement,
  getSupplierStatementSummary,
  listSupplierStatements,
  type SupplierStatementEntry,
  type SupplierStatementFilters,
  type SupplierStatementSummary,
} from "../services/supplierStatementsApi";
import {
  buildSupplierStatementSummaryViewModel,
  formatEffectType,
  formatSourceType,
  groupSupplierStatementEntries,
  type GroupedSupplierStatementRow,
} from "../services/supplierStatementPresentation";
import { formatDate, formatNumber } from "../i18n";

const PAGE_SIZE = 15;
const EFFECT_TYPE_OPTIONS: SupplierStatementEntry["effectType"][] = [
  "PurchaseReceipt",
  "PurchaseReturn",
  "ShortageFinancialResolution",
  "Payment",
  "PurchaseReceiptReversal",
  "PaymentReversal",
  "ShortageResolutionReversal",
];

const SOURCE_TYPE_OPTIONS: SupplierStatementEntry["sourceDocType"][] = [
  "PurchaseReceipt",
  "PurchaseReturn",
  "ShortageFinancialResolution",
  "Payment",
  "PurchaseReceiptReversal",
  "PaymentReversal",
  "ShortageResolutionReversal",
];

const INITIAL_FILTERS: SupplierStatementFilters = {
  search: "",
  supplierId: "",
  effectType: "",
  sourceDocType: "",
  fromDate: "",
  toDate: "",
}

function getStatementSourcePath(sourceDocType: SupplierStatementEntry["sourceDocType"], sourceDocId: string) {
  if (sourceDocType === "PurchaseReceipt") {
    return `/purchase-receipts/${sourceDocId}/edit`;
  }

  if (sourceDocType === "PurchaseReturn") {
    return `/purchase-returns/${sourceDocId}/edit`;
  }

  if (sourceDocType === "ShortageFinancialResolution" || sourceDocType === "ShortageResolution") {
    return `/shortage-resolutions/${sourceDocId}/edit`;
  }

  if (sourceDocType === "Payment") {
    return `/payments/${sourceDocId}/edit`;
  }

  return null;
}

export function SupplierStatementPage() {
  const [searchParams, setSearchParams] = useSearchParams();
  const [suppliers, setSuppliers] = useState<Supplier[]>([]);
  const [rows, setRows] = useState<SupplierStatementEntry[]>([]);
  const [totalCount, setTotalCount] = useState(0);
  const [totalPages, setTotalPages] = useState(0);
  const [summary, setSummary] = useState<SupplierStatementSummary | null>(null);
  const [filters, setFilters] = useState<SupplierStatementFilters>({
    ...INITIAL_FILTERS,
    supplierId: searchParams.get("supplierId") ?? "",
  });
  const [error, setError] = useState("");
  const [loading, setLoading] = useState(true);
  const [loadingSummary, setLoadingSummary] = useState(false);
  const [page, setPage] = useState(1);
  const [reloadKey, setReloadKey] = useState(0);
  const [expandedRows, setExpandedRows] = useState<Record<string, boolean>>({});
  const { t } = useI18n();

  useEffect(() => {
    let active = true;

    async function loadSuppliers() {
      try {
        const result = await listSuppliers("", "active");
        if (active) {
          setSuppliers(result);
        }
      } catch {
        if (active) {
          setError(t("module.supplierStatement.filterError"));
        }
      }
    }

    void loadSuppliers();

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
        const result = filters.supplierId
          ? await getSupplierStatement(filters.supplierId, {
            filters,
            page,
            pageSize: PAGE_SIZE,
            sortBy: "entryDate",
            sortDirection: "Desc",
          })
          : await listSupplierStatements({
            filters,
            page,
            pageSize: PAGE_SIZE,
            sortBy: "entryDate",
            sortDirection: "Desc",
          });

        if (active) {
          setRows(result.items);
          setTotalCount(result.totalCount);
          setTotalPages(result.totalPages);
        }
      } catch (loadError) {
        if (active) {
          setError(loadError instanceof ApiError ? loadError.message : t("module.supplierStatement.error"));
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
    let active = true;

    async function loadSummary() {
      if (!filters.supplierId) {
        setSummary(null);
        return;
      }

      try {
        setLoadingSummary(true);
        const result = await getSupplierStatementSummary(filters.supplierId, filters);
        if (active) {
          setSummary(result);
        }
      } catch {
        if (active) {
          setSummary(null);
        }
      } finally {
        if (active) {
          setLoadingSummary(false);
        }
      }
    }

    void loadSummary();

    return () => {
      active = false;
    };
  }, [filters]);

  useEffect(() => {
    setPage(1);
    const next = new URLSearchParams();
    if (filters.supplierId) {
      next.set("supplierId", filters.supplierId);
    }

    setSearchParams(next, { replace: true });
  }, [filters, setSearchParams]);

  const hasFilters = useMemo(
    () => Object.values(filters).some((value) => value.trim().length > 0),
    [filters],
  );
  const groupedRows = useMemo(() => groupSupplierStatementEntries(rows), [rows]);
  const safePage = totalPages > 0 ? Math.min(page, totalPages) : 1;
  const activeFilters = useMemo(() => {
    const selectedSupplier = suppliers.find((supplier) => supplier.id === filters.supplierId);
    const chips: FilterChip[] = [];

    if (filters.search.trim()) {
      chips.push({
        key: "search",
        label: t("module.supplierStatement.filter.searchChip", { value: filters.search.trim() }),
        onRemove: () => setFilter("search", ""),
      });
    }

    if (selectedSupplier) {
      chips.push({
        key: "supplier",
        label: t("module.supplierStatement.filter.supplierChip", { value: `${selectedSupplier.code} - ${selectedSupplier.name}` }),
        onRemove: () => setFilter("supplierId", ""),
      });
    }

    if (filters.effectType) {
      chips.push({
        key: "effectType",
        label: t("module.supplierStatement.filter.effectChip", { value: formatEffectType(filters.effectType as SupplierStatementEntry["effectType"]) }),
        onRemove: () => setFilter("effectType", ""),
      });
    }

    if (filters.sourceDocType) {
      chips.push({
        key: "sourceDocType",
        label: t("module.supplierStatement.filter.sourceChip", { value: formatSourceType(filters.sourceDocType as SupplierStatementEntry["sourceDocType"]) }),
        onRemove: () => setFilter("sourceDocType", ""),
      });
    }

    if (filters.fromDate || filters.toDate) {
      chips.push({
        key: "dateRange",
        label: t("module.supplierStatement.filter.dateChip", { from: filters.fromDate || t("common.any"), to: filters.toDate || t("common.any") }),
        onRemove: () => {
          setFilter("fromDate", "");
          setFilter("toDate", "");
        },
      });
    }

    return chips;
  }, [filters, suppliers]);
  const resultLabel = totalCount === 1 ? t("module.supplierStatement.resultLabel.one", { count: totalCount }) : t("module.supplierStatement.resultLabel.other", { count: totalCount });

  function setFilter<K extends keyof SupplierStatementFilters>(key: K, value: SupplierStatementFilters[K]) {
    setFilters((current) => ({ ...current, [key]: value }));
  }

  const selectedSupplier = suppliers.find((supplier) => supplier.id === filters.supplierId) ?? null;
  const statementCurrency = useMemo(
    () => rows.find((row) => row.currency)?.currency ?? null,
    [rows],
  );
  const summaryViewModel = useMemo(
    () => (summary && selectedSupplier
      ? buildSupplierStatementSummaryViewModel(summary, t("module.supplierStatement.supplierSpecificLabel", { code: selectedSupplier.code, name: selectedSupplier.name }))
      : null),
    [selectedSupplier, summary, t],
  );

  function toggleExpandedRow(key: string) {
    setExpandedRows((current) => ({ ...current, [key]: !current[key] }));
  }

  function renderViewAction(row: Pick<GroupedSupplierStatementRow, "sourceDocId" | "sourceDocType"> | Pick<SupplierStatementEntry, "sourceDocId" | "sourceDocType">) {
    const path = getStatementSourcePath(row.sourceDocType, row.sourceDocId);
    if (!path) {
      return null;
    }

    return (
      <Link
        className="hc-button hc-button--secondary hc-button--sm hc-table__action-button"
        to={path}
      >
        {t("common.view")}
      </Link>
    );
  }

  return (
    <section className="hc-list-page">
      <PageHeader
        title="module.supplierStatement"
        description="module.supplierStatement.description"
        eyebrow="route.section.purchasing"
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
            <Field label={t("common.supplier")}>
              <Select value={filters.supplierId} onChange={(event) => setFilter("supplierId", event.target.value)}>
                <option value="">All suppliers</option>
                {suppliers.map((supplier) => (
                  <option key={supplier.id} value={supplier.id}>
                    {supplier.code} - {supplier.name}
                  </option>
                ))}
              </Select>
            </Field>

            <Field label={t("module.supplierStatement.effectType")}>
              <Select value={filters.effectType} onChange={(event) => setFilter("effectType", event.target.value)}>
                <option value="">{t("module.supplierStatement.allEffectTypes")}</option>
                {EFFECT_TYPE_OPTIONS.map((value) => (
                  <option key={value} value={value}>
                    {formatEffectType(value)}
                  </option>
                ))}
              </Select>
            </Field>

            <Field label={t("common.source")}>
              <Select value={filters.sourceDocType} onChange={(event) => setFilter("sourceDocType", event.target.value)}>
                <option value="">{t("module.supplierStatement.allSourceDocuments")}</option>
                {SOURCE_TYPE_OPTIONS.map((value) => (
                  <option key={value} value={value}>
                    {formatSourceType(value)}
                  </option>
                ))}
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
            <FilterDropdown aria-label="Supplier filter" value={filters.supplierId} onChange={(event) => setFilter("supplierId", event.target.value)}>
              <option value="">Supplier</option>
              {suppliers.map((supplier) => (
                <option key={supplier.id} value={supplier.id}>
                  {supplier.code} - {supplier.name}
                </option>
              ))}
            </FilterDropdown>

            <FilterDropdown aria-label="Effect type filter" value={filters.effectType} onChange={(event) => setFilter("effectType", event.target.value)}>
              <option value="">{t("module.supplierStatement.allEffectTypes")}</option>
              {EFFECT_TYPE_OPTIONS.map((value) => (
                <option key={value} value={value}>
                  {formatEffectType(value)}
                </option>
              ))}
            </FilterDropdown>
          </>
        )}
        resultLabel={resultLabel}
        search={(
          <FilterTextInput
            aria-label={t("module.supplierStatement")}
            placeholder={t("module.supplierStatement.searchPlaceholder")}
            value={filters.search}
            onChange={(event) => setFilter("search", event.target.value)}
          />
        )}
        secondaryActiveCount={filters.sourceDocType ? 1 : 0}
        secondaryFilters={(
          <Field label={t("common.source")}>
            <Select value={filters.sourceDocType} onChange={(event) => setFilter("sourceDocType", event.target.value)}>
              <option value="">{t("module.supplierStatement.allSourceDocuments")}</option>
              {SOURCE_TYPE_OPTIONS.map((value) => (
                <option key={value} value={value}>
                  {formatSourceType(value)}
                </option>
              ))}
            </Select>
          </Field>
        )}
      />

      <Card className="hc-statement-summary-panel" padding="md">
        <div className="hc-statement-summary-panel__header">
          <div>
            <h2 className="hc-statement-summary-panel__title">{t("module.supplierStatement.summary")}</h2>
            <p className="hc-statement-summary-panel__description">
              {selectedSupplier
                ? t("module.supplierStatement.supplierSpecificLabel", { name: selectedSupplier.name, code: selectedSupplier.code })
                : t("module.supplierStatement.selectSupplierSummary")}
            </p>
          </div>
          <div className="hc-statement-summary-panel__range">
            <span>{filters.fromDate || t("common.anyStartDate")}</span>
            <span className="hc-statement-summary-panel__range-separator">{t("common.to")}</span>
            <span>{filters.toDate || t("common.anyEndDate")}</span>
          </div>
        </div>

        {loadingSummary ? (
          <div className="hc-statement-summary-grid">
            <SkeletonLoader height="4.5rem" variant="rect" />
            <SkeletonLoader height="4.5rem" variant="rect" />
            <SkeletonLoader height="4.5rem" variant="rect" />
            <SkeletonLoader height="4.5rem" variant="rect" />
          </div>
        ) : summaryViewModel ? (
          <div className="hc-statement-summary-grid">
            <div className="hc-statement-summary-metric">
              <span className="hc-statement-summary-metric__label">{t("common.supplier")}</span>
              <strong className="hc-statement-summary-metric__value">{summaryViewModel.supplierText}</strong>
            </div>
            <div className="hc-statement-summary-metric">
              <span className="hc-statement-summary-metric__label">{t("module.supplierStatement.currentBalance")}</span>
              <strong className="hc-statement-summary-metric__value">
                {summaryViewModel.currentBalanceText} {statementCurrency ?? t("common.baseCurrency")}
              </strong>
              <span className="hc-statement-summary-metric__caption">{summaryViewModel.balanceMeaning.explanation}</span>
            </div>
            <div className="hc-statement-summary-metric">
              <span className="hc-statement-summary-metric__label">{t("module.supplierStatement.balanceType")}</span>
              <strong className="hc-statement-summary-metric__value">{summaryViewModel.balanceMeaning.type}</strong>
            </div>
            <div className="hc-statement-summary-metric">
              <span className="hc-statement-summary-metric__label">{t("module.supplierStatement.totalDebit")}</span>
              <strong className="hc-statement-summary-metric__value">{formatNumber(summaryViewModel.totalDebit, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}</strong>
            </div>
            <div className="hc-statement-summary-metric">
              <span className="hc-statement-summary-metric__label">{t("module.supplierStatement.totalCredit")}</span>
              <strong className="hc-statement-summary-metric__value">{formatNumber(summaryViewModel.totalCredit, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}</strong>
            </div>
            <div className="hc-statement-summary-metric">
              <span className="hc-statement-summary-metric__label">{t("module.supplierStatement.dateRange")}</span>
              <strong className="hc-statement-summary-metric__value">{summaryViewModel.dateRangeText}</strong>
            </div>
          </div>
        ) : (
          <div className="hc-statement-summary-empty">{t("module.supplierStatement.summaryEmpty")}</div>
        )}
      </Card>

      {error ? (
        <Card padding="md">
          <EmptyState
            title="module.supplierStatement.error"
            description={error}
            action={
              <Button variant="secondary" onClick={() => setReloadKey((current) => current + 1)}>
                {t("common.retry")}
              </Button>
            }
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
          hasData={groupedRows.length > 0}
          columns={
            <tr>
              <th scope="col">Entry date</th>
              <th scope="col">{t("common.source")}</th>
              <th scope="col">{t("module.supplierStatement.effectType")}</th>
              <th scope="col" className="hc-table__numeric">{t("table.debit")}</th>
              <th scope="col" className="hc-table__numeric">{t("table.credit")}</th>
              <th scope="col" className="hc-table__numeric">{t("module.supplierStatement.runningBalance")}</th>
              <th scope="col">{t("module.supplierStatement.notes")}</th>
              <th scope="col" className="hc-table__head-actions">{t("common.actions")}</th>
            </tr>
          }
          rows={groupedRows.map((row) => {
            const isExpanded = Boolean(expandedRows[row.id]);

            return (
              <Fragment key={row.id}>
                <tr className="hc-table__row">
                  <td>
                    <div className="hc-table__cell-strong">
                      <span className="hc-table__title">{formatDate(row.entryDate)}</span>
                      <span className="hc-table__subtitle">{row.details.length === 1 ? t("module.supplierStatement.postedLine.one", { count: row.details.length }) : t("module.supplierStatement.postedLine.other", { count: row.details.length })}</span>
                    </div>
                  </td>
                  <td>
                    <div className="hc-table__cell-strong">
                      <span className="hc-table__title">{row.sourceDocumentNo}</span>
                      <span className="hc-table__subtitle">
                        {formatSourceType(row.sourceDocType)}
                        {row.details.length > 1 ? ` · ${t("module.supplierStatement.allocations", { count: row.details.length })}` : ""}
                      </span>
                      {row.details.length > 1 ? (
                        <Button
                          className="hc-statement-table__expand"
                          size="sm"
                          variant="ghost"
                          onClick={() => toggleExpandedRow(row.id)}
                        >
                          {isExpanded ? t("module.supplierStatement.hideDetail") : t("module.supplierStatement.viewDetail")}
                        </Button>
                      ) : null}
                    </div>
                  </td>
                  <td>
                    <div className="hc-table__cell-strong">
                      <span className="hc-table__title">{row.effectTypeLabel}</span>
                      <span className="hc-table__subtitle">{t("module.supplierStatement.groupedBySource")}</span>
                    </div>
                  </td>
                  <td className="hc-table__numeric"><span className="hc-table__subtitle">{formatNumber(row.debit, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}</span></td>
                  <td className="hc-table__numeric"><span className="hc-table__subtitle">{formatNumber(row.credit, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}</span></td>
                  <td className="hc-table__numeric">
                    <div className="hc-table__cell-strong hc-table__cell-strong--numeric">
                      <span className="hc-table__title">{formatNumber(row.balanceMeaning.absoluteValue, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}</span>
                      <span className="hc-table__subtitle">{row.balanceMeaning.type} · {row.balanceMeaning.explanation}</span>
                    </div>
                  </td>
                  <td><span className="hc-table__subtitle">{row.notes || t("module.supplierStatement.noNotes")}</span></td>
                  <td className="hc-table__cell-actions">
                    <RowActions primaryAction={renderViewAction(row)} />
                  </td>
                </tr>

                {isExpanded ? (
                  <tr className="hc-statement-table__detail-row">
                    <td colSpan={8}>
                      <div className="hc-statement-table__detail-panel">
                        <div className="hc-statement-table__detail-header">
                          <strong>{t("module.supplierStatement.postedDetailLines")}</strong>
                          <span>{row.sourceDocumentNo}</span>
                        </div>

                        <div className="hc-statement-table__detail-list">
                          {row.details.map((detail) => {
                            const detailBalance = formatNumber(Math.abs(detail.runningBalance), { minimumFractionDigits: 2, maximumFractionDigits: 2 });
                            const detailBalanceType = detail.runningBalance > 0 ? t("payment.balance.payableType") : detail.runningBalance < 0 ? t("payment.balance.receivableType") : t("payment.balance.settledType");

                            return (
                              <div key={detail.id} className="hc-statement-table__detail-item">
                                <div className="hc-statement-table__detail-main">
                                  <span className="hc-table__title">
                                    {detail.sourceSequenceNo ? t("module.supplierStatement.allocation", { count: detail.sourceSequenceNo }) : formatSourceType(detail.sourceDocType)}
                                  </span>
                                  <span className="hc-table__subtitle">
                                    {formatEffectType(detail.effectType)} · {formatDate(detail.entryDate)}
                                  </span>
                                </div>
                                <div className="hc-statement-table__detail-values">
                                  <span className="hc-table__subtitle">{t("module.supplierStatement.detailDebit", { value: formatNumber(detail.debit, { minimumFractionDigits: 2, maximumFractionDigits: 2 }) })}</span>
                                  <span className="hc-table__subtitle">{t("module.supplierStatement.detailCredit", { value: formatNumber(detail.credit, { minimumFractionDigits: 2, maximumFractionDigits: 2 }) })}</span>
                                  <span className="hc-table__subtitle">{t("module.supplierStatement.detailBalance", { value: detailBalance, type: detailBalanceType })}</span>
                                  <span className="hc-table__subtitle">{detail.notes || t("module.supplierStatement.noNotes")}</span>
                                </div>
                                <div className="hc-statement-table__detail-actions">
                                  {renderViewAction(detail)}
                                </div>
                              </div>
                            );
                          })}
                        </div>
                      </div>
                    </td>
                  </tr>
                ) : null}
              </Fragment>
            );
          })}
          footer={<Pagination currentPage={safePage} onPageChange={setPage} pageSize={PAGE_SIZE} totalCount={totalCount} totalPages={Math.max(totalPages, 1)} />}
          emptyState={
            hasFilters ? (
              <EmptyState title="module.supplierStatement.emptyFiltered" description="module.supplierStatement.emptyFilteredDescription" />
            ) : (
              <EmptyState title="module.supplierStatement.empty" description="module.supplierStatement.emptyDescription" />
            )
          }
        />
      ) : null}
    </section>
  );
}
