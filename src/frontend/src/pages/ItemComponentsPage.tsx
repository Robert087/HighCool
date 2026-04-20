import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { ApiError } from "../services/api";
import { RowActions } from "../components/patterns";
import {
  Badge,
  Button,
  DataTable,
  EmptyState,
  Pagination,
  SkeletonLoader,
  useToast,
} from "../components/ui";
import { MasterDataFilterToolbar, MasterDataPageHeader } from "../components/masterData";
import { deleteItemComponent, listItemComponents, type ItemComponent } from "../services/masterDataApi";

const PAGE_SIZE = 10;

export function ItemComponentsPage() {
  const { showToast } = useToast();
  const [rows, setRows] = useState<ItemComponent[]>([]);
  const [search, setSearch] = useState("");
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
        const result = await listItemComponents(search);
        if (active) {
          setRows(result);
        }
      } catch (loadError) {
        if (active) {
          setError(loadError instanceof ApiError ? loadError.message : "Failed to load item components.");
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
  }, [search, reloadKey]);

  useEffect(() => {
    setPage(1);
  }, [search]);

  async function handleDelete(id: string) {
    try {
      await deleteItemComponent(id);
      setRows((current) => current.filter((row) => row.id !== id));
      showToast({ tone: "success", title: "Component row deleted", description: "The item-component relationship was removed." });
    } catch (actionError) {
      setError(actionError instanceof ApiError ? actionError.message : "Failed to delete item component.");
    }
  }

  const totalPages = Math.max(1, Math.ceil(rows.length / PAGE_SIZE));
  const safePage = Math.min(page, totalPages);
  const pageStart = (safePage - 1) * PAGE_SIZE;
  const visibleRows = rows.slice(pageStart, pageStart + PAGE_SIZE);
  const hasFilters = Boolean(search.trim());
  const resultLabel = rows.length === 1 ? "1 component row" : `${rows.length} component rows`;

  return (
    <section className="hc-list-page">
      <MasterDataPageHeader title="Item Components" description="Review parent and component relationships." actionLabel="New component row" actionTo="/item-components/new" />

      <MasterDataFilterToolbar
        hasFilters={hasFilters}
        resultLabel={resultLabel}
        searchLabel="Search"
        searchPlaceholder="Search component rows"
        searchValue={search}
        searchWidth="full"
        statusEnabled={false}
        emptyText="All component relationships"
        filteredText="Filtered component relationships"
        onSearchChange={setSearch}
      />

      {error ? <div className="hc-card hc-card--md"><EmptyState title="Unable to load item components" description={error} action={<Button variant="secondary" onClick={() => setReloadKey((current) => current + 1)}>Retry</Button>} /></div> : null}
      {loading ? <div className="hc-card hc-card--md hc-table-card"><div className="hc-skeleton-stack"><SkeletonLoader height="2.75rem" variant="rect" /><SkeletonLoader height="3.5rem" variant="rect" /><SkeletonLoader height="3.5rem" variant="rect" /></div></div> : null}

      {!loading && !error ? (
        <DataTable
          hasData={rows.length > 0}
          columns={<tr><th scope="col">Parent Item</th><th scope="col">Component Item</th><th scope="col">Quantity</th><th scope="col">Relationship</th><th scope="col" className="hc-table__head-actions" aria-label="Actions" /></tr>}
          rows={visibleRows.map((row) => (
            <tr key={row.id} className="hc-table__row">
              <td><div className="hc-table__cell-strong"><span className="hc-table__title">{row.parentItemName}</span><span className="hc-table__subtitle">{row.parentItemCode}</span></div></td>
              <td><div className="hc-table__cell-strong"><span className="hc-table__title">{row.componentItemName}</span><span className="hc-table__subtitle">{row.componentItemCode}</span></div></td>
              <td><div className="hc-table__cell-strong"><span className="hc-table__title">{row.quantity}</span><span className="hc-table__subtitle">Per parent unit</span></div></td>
              <td><Badge tone="primary">Parent &gt; Component</Badge></td>
              <td className="hc-table__cell-actions"><RowActions><Link className="hc-button hc-button--secondary hc-button--sm hc-table__action-button" to={`/item-components/${row.id}/edit`}>Edit</Link><Button className="hc-table__action-button" size="sm" variant="ghost" onClick={() => void handleDelete(row.id)}>Delete</Button></RowActions></td>
            </tr>
          ))}
          footer={
            <>
              <p className="hc-table__footer-note">Client-side pagination for the current result set.</p>
              <Pagination currentPage={safePage} onPageChange={setPage} pageSize={PAGE_SIZE} totalCount={rows.length} totalPages={totalPages} />
            </>
          }
          emptyState={hasFilters ? <EmptyState title="No component rows match the current search" description="Try a broader search." /> : <EmptyState title="No component rows yet" description="Add your first component relationship." action={<Link className="hc-button hc-button--primary hc-button--md" to="/item-components/new">Create component row</Link>} />}
        />
      ) : null}
    </section>
  );
}
