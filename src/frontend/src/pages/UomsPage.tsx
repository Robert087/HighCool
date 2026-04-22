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
        const result = await listUoms(search, status);

        if (active) {
          setUoms(result);
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
  }, [search, status, reloadKey]);

  useEffect(() => {
    setPage(1);
  }, [search, status]);

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

  const totalPages = Math.max(1, Math.ceil(uoms.length / PAGE_SIZE));
  const safePage = Math.min(page, totalPages);
  const pageStart = (safePage - 1) * PAGE_SIZE;
  const visibleUoms = uoms.slice(pageStart, pageStart + PAGE_SIZE);
  const hasFilters = Boolean(search.trim()) || status !== "all";
  const resultLabel = uoms.length === 1 ? "1 UOM" : `${uoms.length} UOMs`;

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
          rows={visibleUoms.map((uom) => (
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
              <p className="hc-table__footer-note">Client-side pagination for the current result set.</p>
              <Pagination currentPage={safePage} onPageChange={setPage} pageSize={PAGE_SIZE} totalCount={uoms.length} totalPages={totalPages} />
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
