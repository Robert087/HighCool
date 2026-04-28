import { useEffect, useMemo, useState } from "react";
import { Link } from "react-router-dom";
import {
  Badge,
  EmptyState,
  Field,
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
import { listPurchaseReturns, type PurchaseReturnListItem } from "../services/purchaseReturnsApi";

const PAGE_SIZE = 12;

export function PurchaseReturnsPage() {
  const [rows, setRows] = useState<PurchaseReturnListItem[]>([]);
  const [totalCount, setTotalCount] = useState(0);
  const [totalPages, setTotalPages] = useState(0);
  const [search, setSearch] = useState("");
  const [status, setStatus] = useState("");
  const [fromDate, setFromDate] = useState("");
  const [toDate, setToDate] = useState("");
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [page, setPage] = useState(1);

  useEffect(() => {
    let active = true;

    async function load() {
      try {
        setLoading(true);
        setError("");
        const result = await listPurchaseReturns({
          search,
          status,
          fromDate,
          toDate,
          page,
          pageSize: PAGE_SIZE,
          sortBy: "returnDate",
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
          setError(loadError instanceof ApiError ? loadError.message : "Failed to load purchase returns.");
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
  }, [fromDate, page, search, status, toDate]);

  const activeFilters = useMemo(() => {
    const chips: FilterChip[] = [];

    if (search.trim()) {
      chips.push({ key: "search", label: `Search: ${search.trim()}`, onRemove: () => setSearch("") });
    }

    if (status) {
      chips.push({ key: "status", label: `Status: ${status}`, onRemove: () => setStatus("") });
    }

    if (fromDate || toDate) {
      chips.push({
        key: "date",
        label: `Return date: ${fromDate || "Any"} to ${toDate || "Any"}`,
        onRemove: () => {
          setFromDate("");
          setToDate("");
        },
      });
    }

    return chips;
  }, [fromDate, search, status, toDate]);

  useEffect(() => {
    setPage(1);
  }, [fromDate, search, status, toDate]);

  const safePage = totalPages > 0 ? Math.min(page, totalPages) : 1;

  return (
    <section className="hc-list-page">
      <PageHeader
        title="Purchase Returns"
        eyebrow="Purchasing"
        description="Reverse received stock through a controlled purchase return document instead of editing posted receipts."
        actions={<Link className="hc-button hc-button--primary hc-button--md" to="/purchase-returns/new">New purchase return</Link>}
      />

      <FiltersToolbar
        activeFilters={activeFilters}
        mobileFilters={(
          <>
            <Field label="Status">
              <Select value={status} onChange={(event) => setStatus(event.target.value)}>
                <option value="">All statuses</option>
                <option value="Draft">Draft</option>
                <option value="Posted">Posted</option>
              </Select>
            </Field>
            <Field label="From date">
              <Input type="date" value={fromDate} onChange={(event) => setFromDate(event.target.value)} />
            </Field>
            <Field label="To date">
              <Input type="date" value={toDate} onChange={(event) => setToDate(event.target.value)} />
            </Field>
          </>
        )}
        onReset={() => {
          setSearch("");
          setStatus("");
          setFromDate("");
          setToDate("");
        }}
        primaryFilters={(
          <FilterDropdown aria-label="Purchase return status filter" value={status} onChange={(event) => setStatus(event.target.value)}>
            <option value="">Status</option>
            <option value="Draft">Draft</option>
            <option value="Posted">Posted</option>
          </FilterDropdown>
        )}
        resultLabel={totalCount === 1 ? "1 purchase return" : `${totalCount} purchase returns`}
        search={<FilterTextInput aria-label="Search purchase returns" placeholder="Search return, supplier, or receipt" value={search} onChange={(event) => setSearch(event.target.value)} />}
      />

      <div className="hc-list-card">
        {loading ? (
          <>
            <SkeletonLoader />
            <SkeletonLoader />
            <SkeletonLoader />
          </>
        ) : error ? (
          <EmptyState title="Unable to load purchase returns" description={error} />
        ) : rows.length === 0 ? (
          <EmptyState
            title="No purchase returns found"
            description="Create a purchase return when received stock needs to be sent back through a controlled return flow."
            action={<Link className="hc-button hc-button--primary hc-button--md" to="/purchase-returns/new">New purchase return</Link>}
          />
        ) : (
          <>
            <div className="hc-table-wrap">
              <table className="hc-table hc-table--compact">
                <thead>
                  <tr>
                    <th>Return</th>
                    <th>Supplier</th>
                    <th>Reference receipt</th>
                    <th>Return date</th>
                    <th>Lines</th>
                    <th>Status</th>
                  </tr>
                </thead>
                <tbody>
                  {rows.map((row) => (
                    <tr key={row.id}>
                      <td>
                        <Link className="hc-inline-link" to={`/purchase-returns/${row.id}`}>{row.returnNo}</Link>
                      </td>
                      <td>{row.supplierCode} - {row.supplierName}</td>
                      <td>{row.referenceReceiptNo ?? "Manual"}</td>
                      <td>{new Date(row.returnDate).toLocaleDateString()}</td>
                      <td>{row.lineCount}</td>
                      <td>
                        <div className="hc-inline-cluster">
                          <Badge tone={row.status === "Posted" ? "success" : "warning"}>{row.status}</Badge>
                          {row.reversalDocumentId ? <Badge tone="neutral">Reversed</Badge> : null}
                        </div>
                      </td>
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
