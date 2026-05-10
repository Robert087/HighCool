import { useEffect, useMemo, useState } from "react";
import { Link } from "react-router-dom";
import {
  Badge,
  Button,
  EmptyState,
  Field,
  FilterDateRangeInline,
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
import { listSuppliers, type Supplier } from "../services/masterDataApi";
import { listPayments, type PaymentDirection, type PaymentFilters, type PaymentListItem, type PaymentMethod } from "../services/paymentsApi";
import { formatCurrency, formatDate } from "../i18n";

const PAGE_SIZE = 15;

const INITIAL_FILTERS: PaymentFilters = {
  search: "",
  supplierId: "",
  direction: "",
  status: "",
  paymentMethod: "",
  fromDate: "",
  toDate: "",
};

function formatDirection(direction: PaymentDirection, t: (key: string) => string) {
  return direction === "OutboundToParty" ? t("module.payments.paidToSupplier") : t("module.payments.receivedFromSupplier");
}

function formatMethod(method: PaymentMethod, t: (key: string) => string) {
  if (method === "BankTransfer") return t("module.payments.bankTransfer");
  if (method === "Cash") return t("module.payments.cash");
  if (method === "Cheque") return t("module.payments.cheque");
  return t("module.payments.other");
}

function badgeTone(status: PaymentListItem["status"]) {
  return status === "Posted" ? "success" : status === "Canceled" ? "danger" : "neutral";
}

export function PaymentsPage() {
  const [suppliers, setSuppliers] = useState<Supplier[]>([]);
  const [rows, setRows] = useState<PaymentListItem[]>([]);
  const [totalCount, setTotalCount] = useState(0);
  const [totalPages, setTotalPages] = useState(0);
  const [filters, setFilters] = useState<PaymentFilters>(INITIAL_FILTERS);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [page, setPage] = useState(1);
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
          setError(t("module.payments.filterError"));
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
        const result = await listPayments({
          filters,
          page,
          pageSize: PAGE_SIZE,
          sortBy: "paymentDate",
          sortDirection: "Desc",
        });
        if (active) {
          setRows(result.items);
          setTotalCount(result.totalCount);
          setTotalPages(result.totalPages);
        }
      } catch (loadError) {
        if (active) {
          setRows([]);
          setTotalCount(0);
          setTotalPages(0);
          setError(loadError instanceof ApiError ? loadError.message : t("module.payments.error"));
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
  }, [filters, page]);

  useEffect(() => {
    setPage(1);
  }, [filters]);

  const safePage = totalPages > 0 ? Math.min(page, totalPages) : 1;
  const resultLabel = totalCount === 1
    ? t("module.payments.resultLabel.one", { count: totalCount })
    : t("module.payments.resultLabel.other", { count: totalCount });

  const activeFilters = useMemo(() => {
    const chips: FilterChip[] = [];
    const selectedSupplier = suppliers.find((supplier) => supplier.id === filters.supplierId);

    if (filters.search.trim()) {
      chips.push({ key: "search", label: t("module.payments.searchChip", { value: filters.search.trim() }), onRemove: () => setFilter("search", "") });
    }

    if (selectedSupplier) {
      chips.push({
        key: "supplier",
        label: t("module.payments.supplierChip", { value: `${selectedSupplier.code} - ${selectedSupplier.name}` }),
        onRemove: () => setFilter("supplierId", ""),
      });
    }

    if (filters.direction) {
      chips.push({
        key: "direction",
        label: t("module.payments.directionChip", { value: formatDirection(filters.direction as PaymentDirection, t) }),
        onRemove: () => setFilter("direction", ""),
      });
    }

    if (filters.status) {
      chips.push({
        key: "status",
        label: t("module.payments.statusChip", { value: t(`status.${filters.status.toLowerCase()}`) }),
        onRemove: () => setFilter("status", ""),
      });
    }

    if (filters.paymentMethod) {
      chips.push({
        key: "paymentMethod",
        label: t("module.payments.methodChip", { value: formatMethod(filters.paymentMethod as PaymentMethod, t) }),
        onRemove: () => setFilter("paymentMethod", ""),
      });
    }

    if (filters.fromDate || filters.toDate) {
      chips.push({
        key: "dateRange",
        label: t("module.payments.dateChip", { from: filters.fromDate || t("common.any"), to: filters.toDate || t("common.any") }),
        onRemove: () => {
          setFilter("fromDate", "");
          setFilter("toDate", "");
        },
      });
    }

    return chips;
  }, [filters, suppliers]);

  function setFilter<K extends keyof PaymentFilters>(key: K, value: PaymentFilters[K]) {
    setFilters((current) => ({ ...current, [key]: value }));
  }

  return (
    <section className="hc-list-page">
      <PageHeader
        title="module.payments"
        eyebrow="route.section.purchasing"
        description="module.payments.description"
        actions={<Link className="hc-button hc-button--primary hc-button--md" to="/payments/new">{t("module.payments.new")}</Link>}
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
                <option value="">{t("module.payments.allSuppliers")}</option>
                {suppliers.map((supplier) => (
                  <option key={supplier.id} value={supplier.id}>
                    {supplier.code} - {supplier.name}
                  </option>
                ))}
              </Select>
            </Field>
            <Field label="Direction">
              <Select value={filters.direction} onChange={(event) => setFilter("direction", event.target.value)}>
                <option value="">{t("module.payments.allDirections")}</option>
                <option value="OutboundToParty">{t("module.payments.paidToSupplierShort")}</option>
                <option value="InboundFromParty">{t("module.payments.receivedFromSupplierShort")}</option>
              </Select>
            </Field>
            <Field label="Status">
              <Select value={filters.status} onChange={(event) => setFilter("status", event.target.value)}>
                <option value="">All statuses</option>
                <option value="Draft">Draft</option>
                <option value="Posted">Posted</option>
                <option value="Canceled">Canceled</option>
              </Select>
            </Field>
            <Field label="Payment method">
              <Select value={filters.paymentMethod} onChange={(event) => setFilter("paymentMethod", event.target.value)}>
                <option value="">{t("module.payments.allMethods")}</option>
                <option value="Cash">{t("module.payments.cash")}</option>
                <option value="BankTransfer">{t("module.payments.bankTransfer")}</option>
                <option value="Cheque">{t("module.payments.cheque")}</option>
                <option value="Other">{t("module.payments.other")}</option>
              </Select>
            </Field>
          </>
        )}
        onReset={() => setFilters(INITIAL_FILTERS)}
        primaryFilters={(
          <>
            <FilterDropdown aria-label="Supplier filter" value={filters.supplierId} onChange={(event) => setFilter("supplierId", event.target.value)}>
              <option value="">{t("common.supplier")}</option>
              {suppliers.map((supplier) => (
                <option key={supplier.id} value={supplier.id}>
                  {supplier.code} - {supplier.name}
                </option>
              ))}
            </FilterDropdown>
            <FilterDropdown aria-label="Direction filter" value={filters.direction} onChange={(event) => setFilter("direction", event.target.value)}>
              <option value="">{t("common.direction")}</option>
              <option value="OutboundToParty">{t("module.payments.paidToSupplierShort")}</option>
              <option value="InboundFromParty">{t("module.payments.receivedFromSupplierShort")}</option>
            </FilterDropdown>
          </>
        )}
        resultLabel={resultLabel}
        search={(
          <FilterTextInput
            aria-label={t("module.payments")}
            placeholder={t("module.payments.searchPlaceholder")}
            value={filters.search}
            onChange={(event) => setFilter("search", event.target.value)}
          />
        )}
        secondaryActiveCount={Number(Boolean(filters.status)) + Number(Boolean(filters.paymentMethod))}
        secondaryFilters={(
          <>
            <Field label="Status">
              <Select value={filters.status} onChange={(event) => setFilter("status", event.target.value)}>
                <option value="">All statuses</option>
                <option value="Draft">Draft</option>
                <option value="Posted">Posted</option>
                <option value="Canceled">Canceled</option>
              </Select>
            </Field>
            <Field label="Payment method">
              <Select value={filters.paymentMethod} onChange={(event) => setFilter("paymentMethod", event.target.value)}>
                <option value="">{t("module.payments.allMethods")}</option>
                <option value="Cash">{t("module.payments.cash")}</option>
                <option value="BankTransfer">{t("module.payments.bankTransfer")}</option>
                <option value="Cheque">{t("module.payments.cheque")}</option>
                <option value="Other">{t("module.payments.other")}</option>
              </Select>
            </Field>
          </>
        )}
      />

      <div className="hc-list-card">
        {loading ? (
          <>
            <SkeletonLoader />
            <SkeletonLoader />
            <SkeletonLoader />
          </>
        ) : error ? (
          <EmptyState title="module.payments.error" description={error} />
        ) : rows.length === 0 ? (
          <EmptyState
            title="module.payments.empty"
            description="module.payments.emptyDescription"
            action={<Link className="hc-button hc-button--primary hc-button--md" to="/payments/new">{t("module.payments.new")}</Link>}
          />
        ) : (
          <>
            <div className="hc-table-wrap">
              <table className="hc-table hc-table--compact">
                <thead>
                  <tr>
                    <th>Payment</th>
                    <th>Supplier</th>
                    <th>Direction</th>
                    <th>Amount</th>
                    <th>Allocated</th>
                    <th>Remainder</th>
                    <th>Payment date</th>
                    <th>Method</th>
                    <th>Status</th>
                  </tr>
                </thead>
                <tbody>
                  {rows.map((row) => (
                    <tr key={row.id}>
                      <td>
                        <Link className="hc-table__link" to={`/payments/${row.id}`}>
                          {row.paymentNo}
                        </Link>
                        {row.referenceNote ? <div className="hc-table__subtext">{row.referenceNote}</div> : null}
                      </td>
                      <td>{row.partyCode} - {row.partyName}</td>
                      <td>{formatDirection(row.direction, t)}</td>
                      <td>{formatCurrency(row.amount, { currency: row.currency })}</td>
                      <td>{formatCurrency(row.allocatedAmount, { currency: row.currency })}</td>
                      <td>{formatCurrency(row.unallocatedAmount, { currency: row.currency })}</td>
                      <td>{formatDate(row.paymentDate)}</td>
                      <td>{formatMethod(row.paymentMethod, t)}</td>
                      <td><Badge tone={badgeTone(row.status)}>{row.status}</Badge></td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>

            <Pagination currentPage={safePage} totalPages={Math.max(totalPages, 1)} onPageChange={setPage} pageSize={PAGE_SIZE} totalCount={totalCount} />
          </>
        )}
      </div>
    </section>
  );
}
