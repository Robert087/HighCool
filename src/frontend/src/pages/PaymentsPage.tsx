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
} from "../components/ui";
import { ApiError } from "../services/api";
import { listSuppliers, type Supplier } from "../services/masterDataApi";
import { listPayments, type PaymentDirection, type PaymentFilters, type PaymentListItem, type PaymentMethod } from "../services/paymentsApi";

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

function formatDirection(direction: PaymentDirection) {
  return direction === "OutboundToParty" ? "Paid to supplier" : "Received from supplier";
}

function formatMethod(method: PaymentMethod) {
  return method === "BankTransfer" ? "Bank transfer" : method;
}

function formatAmount(amount: number, currency?: string | null) {
  const label = amount.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 });
  return currency ? `${label} ${currency}` : label;
}

function badgeTone(status: PaymentListItem["status"]) {
  return status === "Posted" ? "success" : status === "Canceled" ? "danger" : "neutral";
}

export function PaymentsPage() {
  const [suppliers, setSuppliers] = useState<Supplier[]>([]);
  const [rows, setRows] = useState<PaymentListItem[]>([]);
  const [filters, setFilters] = useState<PaymentFilters>(INITIAL_FILTERS);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [page, setPage] = useState(1);

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
          setError("Failed to load payment filters.");
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
        const result = await listPayments(filters);
        if (active) {
          setRows(result);
        }
      } catch (loadError) {
        if (active) {
          setRows([]);
          setError(loadError instanceof ApiError ? loadError.message : "Unable to load supplier payments.");
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
  }, [filters]);

  useEffect(() => {
    setPage(1);
  }, [filters]);

  const totalPages = Math.max(1, Math.ceil(rows.length / PAGE_SIZE));
  const safePage = Math.min(page, totalPages);
  const pageStart = (safePage - 1) * PAGE_SIZE;
  const visibleRows = rows.slice(pageStart, pageStart + PAGE_SIZE);
  const resultLabel = rows.length === 1 ? "1 payment" : `${rows.length} payments`;

  const activeFilters = useMemo(() => {
    const chips: FilterChip[] = [];
    const selectedSupplier = suppliers.find((supplier) => supplier.id === filters.supplierId);

    if (filters.search.trim()) {
      chips.push({ key: "search", label: `Search: ${filters.search.trim()}`, onRemove: () => setFilter("search", "") });
    }

    if (selectedSupplier) {
      chips.push({
        key: "supplier",
        label: `Supplier: ${selectedSupplier.code} - ${selectedSupplier.name}`,
        onRemove: () => setFilter("supplierId", ""),
      });
    }

    if (filters.direction) {
      chips.push({
        key: "direction",
        label: `Direction: ${formatDirection(filters.direction as PaymentDirection)}`,
        onRemove: () => setFilter("direction", ""),
      });
    }

    if (filters.status) {
      chips.push({
        key: "status",
        label: `Status: ${filters.status}`,
        onRemove: () => setFilter("status", ""),
      });
    }

    if (filters.paymentMethod) {
      chips.push({
        key: "paymentMethod",
        label: `Method: ${formatMethod(filters.paymentMethod as PaymentMethod)}`,
        onRemove: () => setFilter("paymentMethod", ""),
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

  function setFilter<K extends keyof PaymentFilters>(key: K, value: PaymentFilters[K]) {
    setFilters((current) => ({ ...current, [key]: value }));
  }

  return (
    <section className="hc-list-page">
      <PageHeader
        title="Supplier Payments"
        eyebrow="Procurement"
        description="Review draft and posted supplier payments with mandatory allocations against open procurement balances."
        actions={<Link className="hc-button hc-button--primary hc-button--md" to="/payments/new">New payment</Link>}
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
            <Field label="Direction">
              <Select value={filters.direction} onChange={(event) => setFilter("direction", event.target.value)}>
                <option value="">All directions</option>
                <option value="OutboundToParty">Paid to supplier</option>
                <option value="InboundFromParty">Received from supplier</option>
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
                <option value="">All methods</option>
                <option value="Cash">Cash</option>
                <option value="BankTransfer">Bank transfer</option>
                <option value="Cheque">Cheque</option>
                <option value="Other">Other</option>
              </Select>
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
            <FilterDropdown aria-label="Direction filter" value={filters.direction} onChange={(event) => setFilter("direction", event.target.value)}>
              <option value="">Direction</option>
              <option value="OutboundToParty">Paid to supplier</option>
              <option value="InboundFromParty">Received from supplier</option>
            </FilterDropdown>
          </>
        )}
        resultLabel={resultLabel}
        search={(
          <FilterTextInput
            aria-label="Search supplier payments"
            placeholder="Search payment, supplier, or reference"
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
                <option value="">All methods</option>
                <option value="Cash">Cash</option>
                <option value="BankTransfer">Bank transfer</option>
                <option value="Cheque">Cheque</option>
                <option value="Other">Other</option>
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
          <EmptyState title="Unable to load supplier payments" description={error} />
        ) : rows.length === 0 ? (
          <EmptyState
            title="No supplier payments found"
            description="Create the first supplier payment once open purchase receipts or supplier receivables need settlement."
            action={<Link className="hc-button hc-button--primary hc-button--md" to="/payments/new">New payment</Link>}
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
                  {visibleRows.map((row) => (
                    <tr key={row.id}>
                      <td>
                        <Link className="hc-table__link" to={`/payments/${row.id}`}>
                          {row.paymentNo}
                        </Link>
                        {row.referenceNote ? <div className="hc-table__subtext">{row.referenceNote}</div> : null}
                      </td>
                      <td>{row.partyCode} - {row.partyName}</td>
                      <td>{formatDirection(row.direction)}</td>
                      <td>{formatAmount(row.amount, row.currency)}</td>
                      <td>{formatAmount(row.allocatedAmount, row.currency)}</td>
                      <td>{formatAmount(row.unallocatedAmount, row.currency)}</td>
                      <td>{new Date(row.paymentDate).toLocaleDateString()}</td>
                      <td>{formatMethod(row.paymentMethod)}</td>
                      <td><Badge tone={badgeTone(row.status)}>{row.status}</Badge></td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>

            <Pagination currentPage={safePage} totalPages={totalPages} onPageChange={setPage} />
          </>
        )}
      </div>
    </section>
  );
}
