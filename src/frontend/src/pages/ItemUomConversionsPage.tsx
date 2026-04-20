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
  deactivateItemUomConversion,
  listItemUomConversions,
  type ItemUomConversion,
} from "../services/masterDataApi";

const PAGE_SIZE = 10;

export function ItemUomConversionsPage() {
  const { showToast } = useToast();
  const [rows, setRows] = useState<ItemUomConversion[]>([]);
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
        const result = await listItemUomConversions(search, status);
        if (active) {
          setRows(result);
        }
      } catch (loadError) {
        if (active) {
          setError(loadError instanceof ApiError ? loadError.message : "Failed to load item UOM conversions.");
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
      await deactivateItemUomConversion(id);
      setRows((current) => current.map((row) => (row.id === id ? { ...row, isActive: false } : row)));
      showToast({ tone: "success", title: "Conversion deactivated", description: "The item UOM rule is now inactive." });
    } catch (actionError) {
      setError(actionError instanceof ApiError ? actionError.message : "Failed to deactivate conversion.");
    }
  }

  const totalPages = Math.max(1, Math.ceil(rows.length / PAGE_SIZE));
  const safePage = Math.min(page, totalPages);
  const pageStart = (safePage - 1) * PAGE_SIZE;
  const visibleRows = rows.slice(pageStart, pageStart + PAGE_SIZE);
  const hasFilters = Boolean(search.trim()) || status !== "all";
  const resultLabel = rows.length === 1 ? "1 conversion" : `${rows.length} conversions`;

  return (
    <section className="hc-list-page">
      <MasterDataPageHeader title="Item UOM Rules" description="Review item-specific conversion rules." actionLabel="New conversion" actionTo="/item-uom-conversions/new" />
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
      {loading ? <div className="hc-card hc-card--md hc-table-card"><div className="hc-skeleton-stack"><SkeletonLoader height="2.75rem" variant="rect" /><SkeletonLoader height="3.5rem" variant="rect" /><SkeletonLoader height="3.5rem" variant="rect" /><SkeletonLoader height="3.5rem" variant="rect" /></div></div> : null}
      {!loading && !error ? (
        <DataTable
          hasData={rows.length > 0}
          columns={<tr><th scope="col">Item</th><th scope="col">Pair</th><th scope="col">Factor</th><th scope="col">Rounding</th><th scope="col">Status</th><th scope="col" className="hc-table__head-actions" aria-label="Actions" /></tr>}
          rows={visibleRows.map((row) => (
            <tr key={row.id} className="hc-table__row">
              <td><div className="hc-table__cell-strong"><span className="hc-table__title">{row.itemName}</span><span className="hc-table__subtitle">{row.itemCode}</span></div></td>
              <td><div className="hc-table__cell-strong"><span className="hc-table__title">{row.fromUomCode} → {row.toUomCode}</span><span className="hc-table__subtitle">Min fraction: {row.minFraction}</span></div></td>
              <td><div className="hc-table__cell-strong"><span className="hc-table__title">{row.factor}</span><span className="hc-table__subtitle">Conversion factor</span></div></td>
              <td><span className="hc-table__subtitle">{row.roundingMode}</span></td>
              <td><StatusBadge isActive={row.isActive} /></td>
              <td className="hc-table__cell-actions"><RowActions><Link className="hc-button hc-button--secondary hc-button--sm hc-table__action-button" to={`/item-uom-conversions/${row.id}/edit`}>Edit</Link>{row.isActive ? <Button className="hc-table__action-button" size="sm" variant="ghost" onClick={() => void handleDeactivate(row.id)}>Deactivate</Button> : null}</RowActions></td>
            </tr>
          ))}
          footer={<><p className="hc-table__footer-note">Client-side pagination for the current result set.</p><Pagination currentPage={safePage} onPageChange={setPage} pageSize={PAGE_SIZE} totalCount={rows.length} totalPages={totalPages} /></>}
          emptyState={hasFilters ? <EmptyState title="No conversions match the current filters" description="Try a broader search or reset the filters." /> : <EmptyState title="No conversions yet" description="Add your first item-specific conversion rule." action={<Link className="hc-button hc-button--primary hc-button--md" to="/item-uom-conversions/new">Create conversion</Link>} />}
        />
      ) : null}
    </section>
  );
}
