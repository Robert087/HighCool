import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { ApiError } from "../services/api";
import { RowActions } from "../components/patterns";
import { Button, DataTable, EmptyState, Pagination, SkeletonLoader, useToast } from "../components/ui";
import { MasterDataFilterToolbar, MasterDataPageHeader, StatusBadge } from "../components/masterData";
import { deactivateUomConversion, listUomConversions, type UomConversion } from "../services/masterDataApi";

const PAGE_SIZE = 10;

export function UomConversionsPage() {
  const { showToast } = useToast();
  const [rows, setRows] = useState<UomConversion[]>([]);
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
        const result = await listUomConversions(debouncedSearch, status, page, PAGE_SIZE);
        if (active) {
          setRows(result.items);
          setTotalCount(result.totalCount);
          setTotalPages(result.totalPages);
        }
      } catch (loadError) {
        if (active) {
          setError(loadError instanceof ApiError ? loadError.message : "Failed to load UOM conversions.");
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
  }, [reloadKey, debouncedSearch, status, page]);

  useEffect(() => {
    setPage(1);
  }, [debouncedSearch, status]);

  async function handleDeactivate(id: string) {
    try {
      await deactivateUomConversion(id);
      setRows((current) => current.map((row) => (row.id === id ? { ...row, isActive: false } : row)));
      showToast({ tone: "success", title: "Conversion deactivated", description: "The global UOM conversion is now inactive." });
    } catch (actionError) {
      setError(actionError instanceof ApiError ? actionError.message : "Failed to deactivate conversion.");
    }
  }

  const safeTotalPages = Math.max(1, totalPages);
  const safePage = Math.min(page, safeTotalPages);
  const hasFilters = Boolean(search.trim()) || status !== "all";
  const resultLabel = totalCount === 1 ? "1 conversion" : `${totalCount} conversions`;

  return (
    <section className="hc-list-page">
      <MasterDataPageHeader title="UOM Conversions" description="Review global conversion rules shared across items." actionLabel="New conversion" actionTo="/uom-conversions/new" />
      <MasterDataFilterToolbar
        hasFilters={hasFilters}
        resultLabel={resultLabel}
        searchLabel="Search"
        searchPlaceholder="Search conversions"
        searchValue={search}
        statusValue={status}
        emptyText="All conversion rules"
        filteredText="Filtered conversion rules"
        onSearchChange={setSearch}
        onStatusChange={setStatus}
      />
      {error ? <div className="hc-card hc-card--md"><EmptyState title="Unable to load conversions" description={error} action={<Button variant="secondary" onClick={() => setReloadKey((current) => current + 1)}>Retry</Button>} /></div> : null}
      {loading ? <div className="hc-card hc-card--md hc-table-card"><div className="hc-skeleton-stack"><SkeletonLoader height="2.75rem" variant="rect" /><SkeletonLoader height="3.5rem" variant="rect" /><SkeletonLoader height="3.5rem" variant="rect" /></div></div> : null}
      {!loading && !error ? (
        <DataTable
          hasData={rows.length > 0}
          columns={<tr><th scope="col">From</th><th scope="col">To</th><th scope="col">Factor</th><th scope="col">Rounding</th><th scope="col">Status</th><th scope="col" className="hc-table__head-actions" aria-label="Actions" /></tr>}
          rows={rows.map((row) => (
            <tr key={row.id} className="hc-table__row">
              <td><div className="hc-table__cell-strong hc-table__primary-cell"><span className="hc-table__title">{row.fromUomCode}</span><span className="hc-table__subtitle">{row.fromUomName}</span></div></td>
              <td><div className="hc-table__cell-strong"><span className="hc-table__title">{row.toUomCode}</span><span className="hc-table__subtitle">{row.toUomName}</span></div></td>
              <td><span className="hc-table__subtitle">{row.factor}</span></td>
              <td><span className="hc-table__subtitle">{row.roundingMode}</span></td>
              <td><div className="hc-table__status-stack"><StatusBadge isActive={row.isActive} /></div></td>
              <td className="hc-table__cell-actions"><RowActions primaryAction={<Link className="hc-button hc-button--secondary hc-button--sm hc-table__action-button" to={`/uom-conversions/${row.id}/edit`}>View</Link>} menuItems={[{ label: "Edit", to: `/uom-conversions/${row.id}/edit` }, ...(row.isActive ? [{ label: "Deactivate", onSelect: () => void handleDeactivate(row.id) }] : [])]} /></td>
            </tr>
          ))}
          footer={<Pagination currentPage={safePage} onPageChange={setPage} pageSize={PAGE_SIZE} totalCount={totalCount} totalPages={safeTotalPages} />}
          emptyState={hasFilters ? <EmptyState title="No conversions match the current filters" description="Try a broader search or reset the filters." /> : <EmptyState title="No conversions yet" description="Add your first global UOM conversion rule." action={<Link className="hc-button hc-button--primary hc-button--md" to="/uom-conversions/new">Create conversion</Link>} />}
        />
      ) : null}
    </section>
  );
}
