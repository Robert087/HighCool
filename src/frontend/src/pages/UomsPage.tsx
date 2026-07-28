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
import { deactivateUom, listUoms, type Uom } from "../services/masterDataApi";

const PAGE_SIZE = 10;

export function UomsPage() {
  const { showToast } = useToast();
  const [uoms, setUoms] = useState<Uom[]>([]);
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
        const result = await listUoms(debouncedSearch, status, page, PAGE_SIZE);

        if (active) {
          setUoms(result.items);
          setTotalCount(result.totalCount);
          setTotalPages(result.totalPages);
        }
      } catch (loadError) {
        if (active) {
          setError(loadError instanceof ApiError ? loadError.message : "Failed to load UOMs.");
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
      await deactivateUom(id);
      setUoms((current) =>
        current.map((uom) => (uom.id === id ? { ...uom, isActive: false } : uom)),
      );
      showToast({ tone: "success", title: "UOM deactivated", description: "The UOM is now inactive." });
    } catch (actionError) {
      setError(actionError instanceof ApiError ? actionError.message : "Failed to deactivate UOM.");
    }
  }

  const safeTotalPages = Math.max(1, totalPages);
  const safePage = Math.min(page, safeTotalPages);
  const hasFilters = Boolean(search.trim()) || status !== "all";
  const resultLabel = totalCount === 1 ? "1 UOM" : `${totalCount} UOMs`;

  return (
    <section className="hc-list-page">
      <MasterDataPageHeader title="Units of Measure" description="Review the shared measurement catalog." actionLabel="New UOM" actionTo="/uoms/new" />

      <MasterDataFilterToolbar
        hasFilters={hasFilters}
        resultLabel={resultLabel}
        searchLabel="Search"
        searchPlaceholder="Search UOMs"
        searchValue={search}
        statusValue={status}
        emptyText="All UOM records"
        filteredText="Filtered UOM records"
        onSearchChange={setSearch}
        onStatusChange={setStatus}
      />

      {error ? <div className="hc-card hc-card--md"><EmptyState title="Unable to load UOMs" description={error} action={<Button variant="secondary" onClick={() => setReloadKey((current) => current + 1)}>Retry</Button>} /></div> : null}

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
          hasData={uoms.length > 0}
          columns={<tr><th scope="col">UOM</th><th scope="col">Precision</th><th scope="col">Fractions</th><th scope="col">Status</th><th scope="col" className="hc-table__head-actions" aria-label="Actions" /></tr>}
          rows={uoms.map((uom) => (
            <tr key={uom.id} className="hc-table__row">
              <td><div className="hc-table__cell-strong hc-table__primary-cell"><span className="hc-table__title">{uom.name}</span><span className="hc-table__subtitle">{uom.code}</span></div></td>
              <td><div className="hc-table__cell-strong"><span className="hc-table__title">{uom.precision}</span><span className="hc-table__subtitle">Decimal places</span></div></td>
              <td><span className="hc-table__subtitle">{uom.allowsFraction ? "Allowed" : "Whole numbers only"}</span></td>
              <td><div className="hc-table__status-stack"><StatusBadge isActive={uom.isActive} /></div></td>
              <td className="hc-table__cell-actions">
                <RowActions
                  primaryAction={<Link className="hc-button hc-button--secondary hc-button--sm hc-table__action-button" to={`/uoms/${uom.id}/edit`}>View</Link>}
                  menuItems={[
                    { label: "Edit", to: `/uoms/${uom.id}/edit` },
                    ...(uom.isActive ? [{ label: "Deactivate", onSelect: () => void handleDeactivate(uom.id) }] : []),
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
              <EmptyState title="No UOMs match the current filters" description="Try a broader search or reset the filters." />
            ) : (
              <EmptyState title="No UOMs yet" description="Add your first unit of measure." action={<Link className="hc-button hc-button--primary hc-button--md" to="/uoms/new">Create UOM</Link>} />
            )
          }
        />
      ) : null}
    </section>
  );
}
