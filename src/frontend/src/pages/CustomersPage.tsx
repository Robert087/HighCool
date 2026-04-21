import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { ApiError } from "../services/api";
import { RowActions } from "../components/patterns";
import {
  Button,
  DataTable,
  EmptyState,
  Pagination,
  SkeletonLoader,
  useToast,
} from "../components/ui";
import { MasterDataFilterToolbar, MasterDataPageHeader, StatusBadge } from "../components/masterData";
import {
  activateCustomer,
  deactivateCustomer,
  listCustomers,
  type CustomerListItem,
} from "../services/masterDataApi";

const PAGE_SIZE = 10;

export function CustomersPage() {
  const { showToast } = useToast();
  const [customers, setCustomers] = useState<CustomerListItem[]>([]);
  const [search, setSearch] = useState("");
  const [status, setStatus] = useState("all");
  const [error, setError] = useState("");
  const [loading, setLoading] = useState(true);
  const [page, setPage] = useState(1);
  const [reloadKey, setReloadKey] = useState(0);

  useEffect(() => {
    let active = true;

    async function load() {
      try {
        setLoading(true);
        setError("");
        const result = await listCustomers(search, status);

        if (active) {
          setCustomers(result);
        }
      } catch (loadError) {
        if (active) {
          setError(loadError instanceof ApiError ? loadError.message : "Failed to load customers.");
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
  }, [search, status, reloadKey]);

  useEffect(() => {
    setPage(1);
  }, [search, status]);

  async function handleToggleStatus(id: string, nextIsActive: boolean) {
    try {
      if (nextIsActive) {
        await activateCustomer(id);
      } else {
        await deactivateCustomer(id);
      }

      setCustomers((current) =>
        current.map((customer) =>
          customer.id === id ? { ...customer, isActive: nextIsActive } : customer,
        ),
      );

      showToast({
        tone: "success",
        title: nextIsActive ? "Customer activated" : "Customer deactivated",
        description: nextIsActive
          ? "The customer is active again."
          : "The customer is now inactive.",
      });
    } catch (actionError) {
      setError(actionError instanceof ApiError ? actionError.message : "Failed to update customer status.");
    }
  }

  const totalPages = Math.max(1, Math.ceil(customers.length / PAGE_SIZE));
  const safePage = Math.min(page, totalPages);
  const pageStart = (safePage - 1) * PAGE_SIZE;
  const visibleCustomers = customers.slice(pageStart, pageStart + PAGE_SIZE);
  const hasFilters = Boolean(search.trim()) || status !== "all";
  const resultLabel = customers.length === 1 ? "1 customer" : `${customers.length} customers`;

  return (
    <section className="hc-list-page">
      <MasterDataPageHeader
        title="Customers"
        description="Review customer accounts, contact details, and commercial limits."
        actionLabel="New customer"
        actionTo="/customers/new"
      />

      <MasterDataFilterToolbar
        hasFilters={hasFilters}
        resultLabel={resultLabel}
        searchLabel="Search"
        searchPlaceholder="Search by code, name, or phone"
        searchValue={search}
        statusValue={status}
        emptyText="All customer records"
        filteredText="Filtered customer records"
        onSearchChange={setSearch}
        onStatusChange={setStatus}
      />

      {error ? (
        <div className="hc-card hc-card--md">
          <EmptyState
            title="Unable to load customers"
            description={error}
            action={<Button variant="secondary" onClick={() => setReloadKey((current) => current + 1)}>Retry</Button>}
          />
        </div>
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
          hasData={customers.length > 0}
          columns={
            <tr>
              <th scope="col">Customer</th>
              <th scope="col">Contact</th>
              <th scope="col">Location</th>
              <th scope="col">Credit terms</th>
              <th scope="col">Status</th>
              <th scope="col" className="hc-table__head-actions" aria-label="Actions" />
            </tr>
          }
          rows={visibleCustomers.map((customer) => (
            <tr key={customer.id} className="hc-table__row">
              <td>
                <div className="hc-table__cell-strong">
                  <span className="hc-table__title">{customer.name}</span>
                  <span className="hc-table__subtitle">{customer.code}</span>
                </div>
              </td>
              <td>
                <div className="hc-table__meta-list">
                  <span className="hc-table__subtitle">{customer.phone || "No phone"}</span>
                  <span className="hc-table__subtitle">{customer.email || "No email"}</span>
                </div>
              </td>
              <td>
                <div className="hc-table__meta-list">
                  <span className="hc-table__subtitle">{customer.city || "No city"}</span>
                  <span className="hc-table__subtitle">{customer.area || "No area"}</span>
                </div>
              </td>
              <td>
                <div className="hc-table__meta-list">
                  <span className="hc-table__subtitle">{customer.paymentTerms || "No payment terms"}</span>
                  <span className="hc-table__subtitle">{customer.creditLimit.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}</span>
                </div>
              </td>
              <td><StatusBadge isActive={customer.isActive} /></td>
              <td className="hc-table__cell-actions">
                <RowActions>
                  <Link className="hc-button hc-button--secondary hc-button--sm hc-table__action-button" to={`/customers/${customer.id}/edit`}>Edit</Link>
                  {customer.isActive ? (
                    <Button className="hc-table__action-button" size="sm" variant="ghost" onClick={() => void handleToggleStatus(customer.id, false)}>Deactivate</Button>
                  ) : (
                    <Button className="hc-table__action-button" size="sm" variant="ghost" onClick={() => void handleToggleStatus(customer.id, true)}>Activate</Button>
                  )}
                </RowActions>
              </td>
            </tr>
          ))}
          footer={
            <>
              <p className="hc-table__footer-note">Client-side pagination for the current result set.</p>
              <Pagination currentPage={safePage} onPageChange={setPage} pageSize={PAGE_SIZE} totalCount={customers.length} totalPages={totalPages} />
            </>
          }
          emptyState={
            hasFilters ? (
              <EmptyState title="No customers match the current filters" description="Try a broader search or reset the filters." />
            ) : (
              <EmptyState
                title="No customers yet"
                description="Add your first customer to start the directory."
                action={<Link className="hc-button hc-button--primary hc-button--md" to="/customers/new">Create customer</Link>}
              />
            )
          }
        />
      ) : null}
    </section>
  );
}
