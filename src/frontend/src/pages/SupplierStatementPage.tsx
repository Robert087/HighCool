import { Fragment, useEffect, useMemo, useState } from "react";
import { useSearchParams } from "react-router-dom";
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
} from "../services/supplierStatementPresentation";

const PAGE_SIZE = 15;

const INITIAL_FILTERS: SupplierStatementFilters = {
  search: "",
  supplierId: "",
  effectType: "",
  sourceDocType: "",
  fromDate: "",
  toDate: "",
}

export function SupplierStatementPage() {
  const [searchParams, setSearchParams] = useSearchParams();
  const [suppliers, setSuppliers] = useState<Supplier[]>([]);
  const [rows, setRows] = useState<SupplierStatementEntry[]>([]);
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
          setError("Failed to load supplier statement filters.");
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
          ? await getSupplierStatement(filters.supplierId, filters)
          : await listSupplierStatements(filters);

        if (active) {
          setRows(result);
        }
      } catch (loadError) {
        if (active) {
          setError(loadError instanceof ApiError ? loadError.message : "Failed to load supplier statements.");
          setRows([]);
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
  const totalPages = Math.max(1, Math.ceil(groupedRows.length / PAGE_SIZE));
  const safePage = Math.min(page, Math.max(1, Math.ceil(groupedRows.length / PAGE_SIZE)));
  const pageStart = (safePage - 1) * PAGE_SIZE;
  const visibleRows = groupedRows.slice(pageStart, pageStart + PAGE_SIZE);
  const activeFilters = useMemo(() => {
    const selectedSupplier = suppliers.find((supplier) => supplier.id === filters.supplierId);
    const chips: FilterChip[] = [];

    if (filters.search.trim()) {
      chips.push({
        key: "search",
        label: `Search: ${filters.search.trim()}`,
        onRemove: () => setFilter("search", ""),
      });
    }

    if (selectedSupplier) {
      chips.push({
        key: "supplier",
        label: `Supplier: ${selectedSupplier.code} - ${selectedSupplier.name}`,
        onRemove: () => setFilter("supplierId", ""),
      });
    }

    if (filters.effectType) {
      chips.push({
        key: "effectType",
        label: `Effect: ${formatEffectType(filters.effectType as SupplierStatementEntry["effectType"])}`,
        onRemove: () => setFilter("effectType", ""),
      });
    }

    if (filters.sourceDocType) {
      chips.push({
        key: "sourceDocType",
        label: `Source: ${formatSourceType(filters.sourceDocType as SupplierStatementEntry["sourceDocType"])}`,
        onRemove: () => setFilter("sourceDocType", ""),
      });
    }

    if (filters.fromDate || filters.toDate) {
      chips.push({
        key: "dateRange",
        label: `Date: ${filters.fromDate || "Any"} to ${filters.toDate || "Any"}`,
        onRemove: () => {
          setFilter("fromDate", "");
          setFilter("toDate", "");
        },
      });
    }

    return chips;
  }, [filters, suppliers]);
  const resultLabel = groupedRows.length === 1 ? "1 statement document" : `${groupedRows.length} statement documents`;

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
      ? buildSupplierStatementSummaryViewModel(summary, `${selectedSupplier.code} - ${selectedSupplier.name}`)
      : null),
    [selectedSupplier, summary],
  );

  function toggleExpandedRow(key: string) {
    setExpandedRows((current) => ({ ...current, [key]: !current[key] }));
  }

  return (
    <section className="hc-list-page">
      <PageHeader
        title="Supplier Statement"
        description="Review supplier statement movement generated only from posted purchasing documents and financial shortage resolutions."
        eyebrow="Purchasing"
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

            <Field label="Effect type">
              <Select value={filters.effectType} onChange={(event) => setFilter("effectType", event.target.value)}>
                <option value="">All effect types</option>
                <option value="PurchaseReceipt">Purchase receipt</option>
                <option value="ShortageFinancialResolution">Shortage financial resolution</option>
                <option value="Payment">Payment</option>
              </Select>
            </Field>

            <Field label="Source document">
              <Select value={filters.sourceDocType} onChange={(event) => setFilter("sourceDocType", event.target.value)}>
                <option value="">All source documents</option>
                <option value="PurchaseReceipt">Purchase receipt</option>
                <option value="ShortageResolution">Shortage resolution</option>
                <option value="Payment">Payment</option>
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

            <FilterDropdown aria-label="Effect type filter" value={filters.effectType} onChange={(event) => setFilter("effectType", event.target.value)}>
              <option value="">Effect type</option>
              <option value="PurchaseReceipt">Purchase receipt</option>
              <option value="ShortageFinancialResolution">Shortage financial resolution</option>
              <option value="Payment">Payment</option>
            </FilterDropdown>
          </>
        )}
        resultLabel={resultLabel}
        search={(
          <FilterTextInput
            aria-label="Search supplier statements"
            placeholder="Search supplier or notes"
            value={filters.search}
            onChange={(event) => setFilter("search", event.target.value)}
          />
        )}
        secondaryActiveCount={filters.sourceDocType ? 1 : 0}
        secondaryFilters={(
          <Field label="Source document">
            <Select value={filters.sourceDocType} onChange={(event) => setFilter("sourceDocType", event.target.value)}>
              <option value="">All source documents</option>
              <option value="PurchaseReceipt">Purchase receipt</option>
              <option value="ShortageResolution">Shortage resolution</option>
              <option value="Payment">Payment</option>
            </Select>
          </Field>
        )}
      />

      <Card className="hc-statement-summary-panel" padding="md">
        <div className="hc-statement-summary-panel__header">
          <div>
            <h2 className="hc-statement-summary-panel__title">Summary</h2>
            <p className="hc-statement-summary-panel__description">
              {selectedSupplier
                ? `${selectedSupplier.name} (${selectedSupplier.code})`
                : "Select a supplier to view supplier-specific balance context."}
            </p>
          </div>
          <div className="hc-statement-summary-panel__range">
            <span>{filters.fromDate || "Any start date"}</span>
            <span className="hc-statement-summary-panel__range-separator">to</span>
            <span>{filters.toDate || "Any end date"}</span>
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
              <span className="hc-statement-summary-metric__label">Supplier</span>
              <strong className="hc-statement-summary-metric__value">{summaryViewModel.supplierText}</strong>
            </div>
            <div className="hc-statement-summary-metric">
              <span className="hc-statement-summary-metric__label">Current balance</span>
              <strong className="hc-statement-summary-metric__value">
                {summaryViewModel.currentBalanceText} {statementCurrency ?? "Base currency"}
              </strong>
              <span className="hc-statement-summary-metric__caption">{summaryViewModel.balanceMeaning.explanation}</span>
            </div>
            <div className="hc-statement-summary-metric">
              <span className="hc-statement-summary-metric__label">Balance type</span>
              <strong className="hc-statement-summary-metric__value">{summaryViewModel.balanceMeaning.type}</strong>
            </div>
            <div className="hc-statement-summary-metric">
              <span className="hc-statement-summary-metric__label">Total debit</span>
              <strong className="hc-statement-summary-metric__value">{summaryViewModel.totalDebit.toLocaleString()}</strong>
            </div>
            <div className="hc-statement-summary-metric">
              <span className="hc-statement-summary-metric__label">Total credit</span>
              <strong className="hc-statement-summary-metric__value">{summaryViewModel.totalCredit.toLocaleString()}</strong>
            </div>
            <div className="hc-statement-summary-metric">
              <span className="hc-statement-summary-metric__label">Date range</span>
              <strong className="hc-statement-summary-metric__value">{summaryViewModel.dateRangeText}</strong>
            </div>
          </div>
        ) : (
          <div className="hc-statement-summary-empty">Select a supplier to view a clear payable or receivable balance summary.</div>
        )}
      </Card>

      {error ? (
        <Card padding="md">
          <EmptyState
            title="Unable to load supplier statements"
            description={error}
            action={
              <Button variant="secondary" onClick={() => setReloadKey((current) => current + 1)}>
                Retry
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
              <th scope="col">Source document</th>
              <th scope="col">Effect type</th>
              <th scope="col">Debit</th>
              <th scope="col">Credit</th>
              <th scope="col">Running balance</th>
              <th scope="col">Notes</th>
            </tr>
          }
          rows={visibleRows.map((row) => {
            const isExpanded = Boolean(expandedRows[row.id]);

            return (
              <Fragment key={row.id}>
                <tr className="hc-table__row">
                  <td>
                    <div className="hc-table__cell-strong">
                      <span className="hc-table__title">{new Date(row.entryDate).toLocaleDateString()}</span>
                      <span className="hc-table__subtitle">{row.details.length} posted line{row.details.length === 1 ? "" : "s"}</span>
                    </div>
                  </td>
                  <td>
                    <div className="hc-table__cell-strong">
                      <span className="hc-table__title">{row.sourceDocumentNo}</span>
                      <span className="hc-table__subtitle">
                        {formatSourceType(row.sourceDocType)}
                        {row.details.length > 1 ? ` · ${row.details.length} allocations` : ""}
                      </span>
                      {row.details.length > 1 ? (
                        <Button
                          className="hc-statement-table__expand"
                          size="sm"
                          variant="ghost"
                          onClick={() => toggleExpandedRow(row.id)}
                        >
                          {isExpanded ? "Hide detail" : "View detail"}
                        </Button>
                      ) : null}
                    </div>
                  </td>
                  <td>
                    <div className="hc-table__cell-strong">
                      <span className="hc-table__title">{row.effectTypeLabel}</span>
                      <span className="hc-table__subtitle">Grouped by source document</span>
                    </div>
                  </td>
                  <td><span className="hc-table__subtitle">{row.debit.toLocaleString()}</span></td>
                  <td><span className="hc-table__subtitle">{row.credit.toLocaleString()}</span></td>
                  <td>
                    <div className="hc-table__cell-strong">
                      <span className="hc-table__title">{row.balanceMeaning.absoluteValue.toLocaleString()}</span>
                      <span className="hc-table__subtitle">{row.balanceMeaning.type} · {row.balanceMeaning.explanation}</span>
                    </div>
                  </td>
                  <td><span className="hc-table__subtitle">{row.notes || "No notes"}</span></td>
                </tr>

                {isExpanded ? (
                  <tr className="hc-statement-table__detail-row">
                    <td colSpan={7}>
                      <div className="hc-statement-table__detail-panel">
                        <div className="hc-statement-table__detail-header">
                          <strong>Posted detail lines</strong>
                          <span>{row.sourceDocumentNo}</span>
                        </div>

                        <div className="hc-statement-table__detail-list">
                          {row.details.map((detail) => {
                            const detailBalance = Math.abs(detail.runningBalance).toLocaleString();
                            const detailBalanceType = detail.runningBalance > 0 ? "Payable" : detail.runningBalance < 0 ? "Receivable" : "Settled";

                            return (
                              <div key={detail.id} className="hc-statement-table__detail-item">
                                <div className="hc-statement-table__detail-main">
                                  <span className="hc-table__title">
                                    {detail.sourceSequenceNo ? `Allocation ${detail.sourceSequenceNo}` : formatSourceType(detail.sourceDocType)}
                                  </span>
                                  <span className="hc-table__subtitle">
                                    {formatEffectType(detail.effectType)} · {new Date(detail.entryDate).toLocaleDateString()}
                                  </span>
                                </div>
                                <div className="hc-statement-table__detail-values">
                                  <span className="hc-table__subtitle">Debit {detail.debit.toLocaleString()}</span>
                                  <span className="hc-table__subtitle">Credit {detail.credit.toLocaleString()}</span>
                                  <span className="hc-table__subtitle">Balance {detailBalance} · {detailBalanceType}</span>
                                  <span className="hc-table__subtitle">{detail.notes || "No notes"}</span>
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
          footer={<Pagination currentPage={safePage} onPageChange={setPage} pageSize={PAGE_SIZE} totalCount={groupedRows.length} totalPages={totalPages} />}
          emptyState={
            hasFilters ? (
              <EmptyState title="No supplier statement rows match the current filters" description="Try a broader date range or clear one of the filters." />
            ) : (
              <EmptyState title="No supplier statement rows yet" description="Statement rows appear only after posted purchasing documents generate supplier financial effects." />
            )
          }
        />
      ) : null}
    </section>
  );
}
