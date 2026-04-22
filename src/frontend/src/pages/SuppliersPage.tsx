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
import { deactivateSupplier, listSuppliers, type Supplier } from "../services/masterDataApi";

const PAGE_SIZE = 10;

export function SuppliersPage() {
  const { showToast } = useToast();
  const [suppliers, setSuppliers] = useState<Supplier[]>([]);
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
        const result = await listSuppliers(search, status);

        if (active) {
          setSuppliers(result);
        }
      } catch (loadError) {
        if (active) {
          setError(loadError instanceof ApiError ? loadError.message : "Failed to load suppliers.");
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

  async function handleDeactivate(id: string) {
    try {
      await deactivateSupplier(id);
      setSuppliers((current) =>
        current.map((supplier) =>
          supplier.id === id ? { ...supplier, isActive: false } : supplier,
        ),
      );
      showToast({ tone: "success", title: "Supplier deactivated", description: "The supplier is now inactive." });
    } catch (actionError) {
      setError(actionError instanceof ApiError ? actionError.message : "Failed to deactivate supplier.");
    }
  }

  const totalPages = Math.max(1, Math.ceil(suppliers.length / PAGE_SIZE));
  const safePage = Math.min(page, totalPages);
  const pageStart = (safePage - 1) * PAGE_SIZE;
  const visibleSuppliers = suppliers.slice(pageStart, pageStart + PAGE_SIZE);
  const hasFilters = Boolean(search.trim()) || status !== "all";
  const resultLabel = suppliers.length === 1 ? "1 supplier" : `${suppliers.length} suppliers`;

  return (
    <section className="hc-list-page">
      <MasterDataPageHeader
        title="Suppliers"
        description="Review supplier records and contacts."
        actionLabel="New supplier"
        actionTo="/suppliers/new"
      />

      <MasterDataFilterToolbar
        hasFilters={hasFilters}
        resultLabel={resultLabel}
        searchLabel="Search"
        searchPlaceholder="Search suppliers"
        searchValue={search}
        statusValue={status}
        emptyText="All supplier records"
        filteredText="Filtered supplier records"
        onSearchChange={setSearch}
        onStatusChange={setStatus}
      />

      {error ? (
        <div className="hc-card hc-card--md">
          <EmptyState
            title="Unable to load suppliers"
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
          hasData={suppliers.length > 0}
          columns={
            <tr>
              <th scope="col">Supplier</th>
              <th scope="col">Statement name</th>
              <th scope="col">Contact</th>
              <th scope="col">Status</th>
              <th scope="col" className="hc-table__head-actions" aria-label="Actions" />
            </tr>
          }
          rows={visibleSuppliers.map((supplier) => (
            <tr key={supplier.id} className="hc-table__row">
              <td>
                <div className="hc-table__cell-strong hc-table__primary-cell">
                  <span className="hc-table__title">{supplier.name}</span>
                  <span className="hc-table__subtitle">{supplier.code}</span>
                </div>
              </td>
              <td>
                <div className="hc-table__cell-strong">
                  <span className="hc-table__title">{supplier.statementName}</span>
                  <span className="hc-table__subtitle">Statement identity</span>
                </div>
              </td>
              <td>
                <div className="hc-table__meta-list">
                  <span className="hc-table__subtitle">{supplier.phone || "No phone"}</span>
                  <span className="hc-table__subtitle">{supplier.email || "No email"}</span>
                </div>
              </td>
              <td><div className="hc-table__status-stack"><StatusBadge isActive={supplier.isActive} /></div></td>
              <td className="hc-table__cell-actions">
                <RowActions
                  primaryAction={<Link className="hc-button hc-button--secondary hc-button--sm hc-table__action-button" to={`/suppliers/${supplier.id}/edit`}>View</Link>}
                  menuItems={[
                    { label: "Edit", to: `/suppliers/${supplier.id}/edit` },
                    ...(supplier.isActive ? [{ label: "Deactivate", onSelect: () => void handleDeactivate(supplier.id) }] : []),
                  ]}
                />
              </td>
            </tr>
          ))}
          footer={
            <>
              <p className="hc-table__footer-note">Client-side pagination for the current result set.</p>
              <Pagination currentPage={safePage} onPageChange={setPage} pageSize={PAGE_SIZE} totalCount={suppliers.length} totalPages={totalPages} />
            </>
          }
          emptyState={
            hasFilters ? (
              <EmptyState title="No suppliers match the current filters" description="Try a broader search or reset the filters." />
            ) : (
              <EmptyState
                title="No suppliers yet"
                description="Add your first supplier to start the directory."
                action={<Link className="hc-button hc-button--primary hc-button--md" to="/suppliers/new">Create supplier</Link>}
              />
            )
          }
        />
      ) : null}
    </section>
  );
}
