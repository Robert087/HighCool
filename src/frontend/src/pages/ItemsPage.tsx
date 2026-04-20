import { useEffect, useState } from "react";
import { ApiError } from "../services/api";
import {
  Button,
  EmptyState,
  SkeletonLoader,
  useToast,
} from "../components/ui";
import { ItemsFilterToolbar, ItemsPageHeader, ItemsTable } from "../components/items";
import { deactivateItem, listItems, type Item } from "../services/masterDataApi";

const PAGE_SIZE = 10;

export function ItemsPage() {
  const { showToast } = useToast();
  const [items, setItems] = useState<Item[]>([]);
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
        const result = await listItems(search, status);

        if (active) {
          setItems(result);
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
  }, [search, status, reloadKey]);

  useEffect(() => {
    setPage(1);
  }, [search, status]);

  async function handleDeactivate(id: string) {
    try {
      await deactivateItem(id);
      setItems((current) => current.map((item) => (item.id === id ? { ...item, isActive: false } : item)));
      showToast({ tone: "success", title: "Item deactivated", description: "The item is now inactive." });
    } catch (actionError) {
      setError(actionError instanceof ApiError ? actionError.message : "Failed to deactivate item.");
    }
  }

  const totalPages = Math.max(1, Math.ceil(items.length / PAGE_SIZE));
  const safePage = Math.min(page, totalPages);
  const pageStart = (safePage - 1) * PAGE_SIZE;
  const visibleItems = items.slice(pageStart, pageStart + PAGE_SIZE);
  const hasFilters = Boolean(search.trim()) || status !== "all";
  const resultLabel =
    items.length === 1 ? "1 item" : `${items.length} items`;

  return (
    <section className="hc-list-page">
      <ItemsPageHeader />

      <ItemsFilterToolbar
        hasFilters={hasFilters}
        resultLabel={resultLabel}
        search={search}
        status={status}
        onSearchChange={setSearch}
        onStatusChange={setStatus}
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
          items={visibleItems}
          onDeactivate={handleDeactivate}
          onPageChange={setPage}
          safePage={safePage}
          totalPages={totalPages}
        />
      ) : null}
    </section>
  );
}
