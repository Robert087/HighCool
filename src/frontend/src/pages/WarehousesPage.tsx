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
import { deactivateWarehouse, listWarehouses, type Warehouse } from "../services/masterDataApi";

const PAGE_SIZE = 10;

export function WarehousesPage() {
  const { showToast } = useToast();
  const [warehouses, setWarehouses] = useState<Warehouse[]>([]);
  const [search, setSearch] = useState("");
  const [debouncedSearch, setDebouncedSearch] = useState("");
  const [status, setStatus] = useState("all");
  const [error, setError] = useState("");
  const [loading, setLoading] = useState(true);
  const [page, setPage] = useState(1);
  const [totalCount, setTotalCount] = useState(0);
  const [totalPages, setTotalPages] = useState(0);
  const [reloadKey, setReloadKey] = useState(0);

  useEffect(() => {
    const timeout = window.setTimeout(() => setDebouncedSearch(search), 300);
    return () => window.clearTimeout(timeout);
  }, [search]);

  useEffect(() => {
    let active = true;

    async function load() {
      try {
        setLoading(true);
        setError("");
        const result = await listWarehouses(debouncedSearch, status, page, PAGE_SIZE);

        if (active) {
          setWarehouses(result.items);
          setTotalCount(result.totalCount);
          setTotalPages(result.totalPages);
        }
      } catch (loadError) {
        if (active) {
          setError(loadError instanceof ApiError ? loadError.message : "Failed to load warehouses.");
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
  }, [debouncedSearch, status, page, reloadKey]);

  useEffect(() => {
    setPage(1);
  }, [debouncedSearch, status]);

  async function handleDeactivate(id: string) {
    try {
      await deactivateWarehouse(id);
      setWarehouses((current) =>
        current.map((warehouse) =>
          warehouse.id === id ? { ...warehouse, isActive: false } : warehouse,
        ),
      );
      showToast({ tone: "success", title: "Warehouse deactivated", description: "The warehouse is now inactive." });
    } catch (actionError) {
      setError(actionError instanceof ApiError ? actionError.message : "Failed to deactivate warehouse.");
    }
  }

  const safeTotalPages = Math.max(1, totalPages);
  const safePage = Math.min(page, safeTotalPages);
  const hasFilters = Boolean(search.trim()) || status !== "all";
  const resultLabel = totalCount === 1 ? "1 warehouse" : `${totalCount} warehouses`;

  return (
    <section className="hc-list-page">
      <MasterDataPageHeader
        title="Warehouses"
        description="Review warehouse records and locations."
        actionLabel="New warehouse"
        actionTo="/warehouses/new"
      />

      <MasterDataFilterToolbar
        hasFilters={hasFilters}
        resultLabel={resultLabel}
        searchLabel="Search"
        searchPlaceholder="Search warehouses"
        searchValue={search}
        statusValue={status}
        emptyText="All warehouse records"
        filteredText="Filtered warehouse records"
        onSearchChange={setSearch}
        onStatusChange={setStatus}
      />

      {error ? (
        <div className="hc-card hc-card--md">
          <EmptyState title="Unable to load warehouses" description={error} action={<Button variant="secondary" onClick={() => setReloadKey((current) => current + 1)}>Retry</Button>} />
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
          hasData={warehouses.length > 0}
          columns={<tr><th scope="col">Warehouse</th><th scope="col">Location</th><th scope="col">Status</th><th scope="col" className="hc-table__head-actions" aria-label="Actions" /></tr>}
          rows={warehouses.map((warehouse) => (
            <tr key={warehouse.id} className="hc-table__row">
              <td>
                <div className="hc-table__cell-strong hc-table__primary-cell">
                  <span className="hc-table__title">{warehouse.name}</span>
                  <span className="hc-table__subtitle">{warehouse.code}</span>
                </div>
              </td>
              <td>
                <div className="hc-table__cell-strong">
                  <span className="hc-table__title">{warehouse.location || "Not set"}</span>
                  <span className="hc-table__subtitle">Location reference</span>
                </div>
              </td>
              <td><div className="hc-table__status-stack"><StatusBadge isActive={warehouse.isActive} /></div></td>
              <td className="hc-table__cell-actions">
                <RowActions
                  primaryAction={<Link className="hc-button hc-button--secondary hc-button--sm hc-table__action-button" to={`/warehouses/${warehouse.id}/edit`}>View</Link>}
                  menuItems={[
                    { label: "Edit", to: `/warehouses/${warehouse.id}/edit` },
                    ...(warehouse.isActive ? [{ label: "Deactivate", onSelect: () => void handleDeactivate(warehouse.id) }] : []),
                  ]}
                />
              </td>
            </tr>
          ))}
          footer={
            <>
              <Pagination currentPage={safePage} onPageChange={setPage} pageSize={PAGE_SIZE} totalCount={totalCount} totalPages={safeTotalPages} />
            </>
          }
          emptyState={
            hasFilters ? (
              <EmptyState title="No warehouses match the current filters" description="Try a broader search or reset the filters." />
            ) : (
              <EmptyState title="No warehouses yet" description="Add your first warehouse to start the location list." action={<Link className="hc-button hc-button--primary hc-button--md" to="/warehouses/new">Create warehouse</Link>} />
            )
          }
        />
      ) : null}
    </section>
  );
}
