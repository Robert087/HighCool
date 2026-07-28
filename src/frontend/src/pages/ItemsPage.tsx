import { useEffect, useState } from "react";
import { ApiError } from "../services/api";
import {
  Button,
  EmptyState,
  SkeletonLoader,
  useToast,
} from "../components/ui";
import { ItemsFilterToolbar, ItemsPageHeader, ItemsTable } from "../components/items";
import { deactivateItem, getActiveItemCategoriesCached, listItems, type Item, type ItemCategory } from "../services/masterDataApi";

const PAGE_SIZE = 10;

export function ItemsPage() {
  const { showToast } = useToast();
  const [items, setItems] = useState<Item[]>([]);
  const [categories, setCategories] = useState<ItemCategory[]>([]);
  const [search, setSearch] = useState("");
  const [debouncedSearch, setDebouncedSearch] = useState("");
  const [status, setStatus] = useState("all");
  const [categoryId, setCategoryId] = useState("");
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
        const [result, categoryList] = await Promise.all([
          listItems(debouncedSearch, status, page, PAGE_SIZE, "name", "Asc", { categoryId: categoryId || undefined }),
          getActiveItemCategoriesCached(),
        ]);

        if (active) {
          setItems(result.items);
          setTotalCount(result.totalCount);
          setTotalPages(result.totalPages);
          setCategories(categoryList);
        }
      } catch (loadError) {
        if (active) {
          setError(loadError instanceof ApiError ? loadError.message : "Failed to load items.");
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
  }, [debouncedSearch, status, categoryId, page, reloadKey]);

  useEffect(() => {
    setPage(1);
  }, [debouncedSearch, status, categoryId]);

  async function handleDeactivate(id: string) {
    try {
      await deactivateItem(id);
      setItems((current) => current.map((item) => (item.id === id ? { ...item, isActive: false } : item)));
      showToast({ tone: "success", title: "Item deactivated", description: "The item is now inactive." });
    } catch (actionError) {
      setError(actionError instanceof ApiError ? actionError.message : "Failed to deactivate item.");
    }
  }

  const safeTotalPages = Math.max(1, totalPages);
  const safePage = Math.min(page, safeTotalPages);
  const hasFilters = Boolean(search.trim()) || status !== "all" || Boolean(categoryId);
  const resultLabel =
    totalCount === 1 ? "1 item" : `${totalCount} items`;

  return (
    <section className="hc-list-page">
      <ItemsPageHeader />

      <ItemsFilterToolbar
        hasFilters={hasFilters}
        resultLabel={resultLabel}
        search={search}
        status={status}
        categories={categories}
        categoryId={categoryId}
        onSearchChange={setSearch}
        onStatusChange={setStatus}
        onCategoryChange={setCategoryId}
      />

      {error ? (
        <div className="hc-card hc-card--md">
          <EmptyState
            title="Unable to load items"
            description={error}
            action={
              <Button variant="secondary" onClick={() => setReloadKey((current) => current + 1)}>
                Retry
              </Button>
            }
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
        <ItemsTable
          hasFilters={hasFilters}
          items={items}
          onDeactivate={handleDeactivate}
          onPageChange={setPage}
          safePage={safePage}
          pageSize={PAGE_SIZE}
          totalCount={totalCount}
          totalPages={safeTotalPages}
        />
      ) : null}
    </section>
  );
}
