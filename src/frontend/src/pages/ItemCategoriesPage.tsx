import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { RowActions } from "../components/patterns";
import { Button, DataTable, EmptyState, Pagination, SkeletonLoader, useToast } from "../components/ui";
import { MasterDataFilterToolbar, MasterDataPageHeader, StatusBadge } from "../components/masterData";
import { useI18n } from "../i18n";
import { ApiError } from "../services/api";
import { deactivateItemCategory, listItemCategories, type ItemCategory } from "../services/masterDataApi";

const PAGE_SIZE = 10;

export function ItemCategoriesPage() {
  const { showToast } = useToast();
  const { t } = useI18n();
  const [categories, setCategories] = useState<ItemCategory[]>([]);
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
        const result = await listItemCategories(debouncedSearch, status, page, PAGE_SIZE);

        if (active) {
          setCategories(result.items);
          setTotalCount(result.totalCount);
          setTotalPages(result.totalPages);
        }
      } catch (loadError) {
        if (active) {
          setError(loadError instanceof ApiError ? loadError.message : "module.itemCategories.loadError");
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
      await deactivateItemCategory(id);
      setCategories((current) => current.map((category) => category.id === id ? { ...category, isActive: false } : category));
      showToast({ tone: "success", title: "module.itemCategories.deactivated", description: "module.itemCategories.deactivatedDescription" });
    } catch (actionError) {
      setError(actionError instanceof ApiError ? actionError.message : "module.itemCategories.deactivateError");
    }
  }

  const safeTotalPages = Math.max(1, totalPages);
  const safePage = Math.min(page, safeTotalPages);
  const hasFilters = Boolean(search.trim()) || status !== "all";
  const resultLabel = t(totalCount === 1 ? "module.itemCategories.resultOne" : "module.itemCategories.resultMany", { count: totalCount });

  return (
    <section className="hc-list-page">
      <MasterDataPageHeader
        title="module.itemCategories.title"
        description="module.itemCategories.description"
        actionLabel="module.itemCategories.new"
        actionTo="/item-categories/new"
      />

      <MasterDataFilterToolbar
        hasFilters={hasFilters}
        resultLabel={resultLabel}
        searchLabel="common.search"
        searchPlaceholder="module.itemCategories.searchPlaceholder"
        searchValue={search}
        statusValue={status}
        emptyText="module.itemCategories.allRecords"
        filteredText="module.itemCategories.filteredRecords"
        onSearchChange={setSearch}
        onStatusChange={setStatus}
      />

      {error ? (
        <div className="hc-card hc-card--md">
          <EmptyState title="module.itemCategories.loadErrorTitle" description={error} action={<Button variant="secondary" onClick={() => setReloadKey((current) => current + 1)}>common.retry</Button>} />
        </div>
      ) : null}

      {loading ? (
        <div className="hc-card hc-card--md hc-table-card">
          <div className="hc-skeleton-stack">
            <SkeletonLoader height="2.75rem" variant="rect" />
            <SkeletonLoader height="3.5rem" variant="rect" />
            <SkeletonLoader height="3.5rem" variant="rect" />
          </div>
        </div>
      ) : null}

      {!loading && !error ? (
        <DataTable
          hasData={categories.length > 0}
          columns={<tr><th scope="col">module.itemCategories.table.category</th><th scope="col">module.itemCategories.table.description</th><th scope="col">table.status</th><th scope="col" className="hc-table__head-actions" aria-label="common.actions" /></tr>}
          rows={categories.map((category) => (
            <tr key={category.id} className="hc-table__row">
              <td><div className="hc-table__cell-strong hc-table__primary-cell"><span className="hc-table__title">{category.name}</span><span className="hc-table__subtitle">{category.code}</span></div></td>
              <td><span className="hc-table__subtitle">{category.description || "common.notSet"}</span></td>
              <td><div className="hc-table__status-stack"><StatusBadge isActive={category.isActive} /></div></td>
              <td className="hc-table__cell-actions">
                <RowActions
                  primaryAction={<Link className="hc-button hc-button--secondary hc-button--sm hc-table__action-button" to={`/item-categories/${category.id}/edit`}>common.view</Link>}
                  menuItems={[
                    { label: "common.edit", to: `/item-categories/${category.id}/edit` },
                    ...(category.isActive ? [{ label: "common.deactivate", onSelect: () => void handleDeactivate(category.id) }] : []),
                  ]}
                />
              </td>
            </tr>
          ))}
          footer={<Pagination currentPage={safePage} onPageChange={setPage} pageSize={PAGE_SIZE} totalCount={totalCount} totalPages={safeTotalPages} />}
          emptyState={hasFilters ? <EmptyState title="module.itemCategories.emptyFiltered" description="module.itemCategories.emptyFilteredDescription" /> : <EmptyState title="module.itemCategories.empty" description="module.itemCategories.emptyDescription" action={<Link className="hc-button hc-button--primary hc-button--md" to="/item-categories/new">module.itemCategories.new</Link>} />}
        />
      ) : null}
    </section>
  );
}
