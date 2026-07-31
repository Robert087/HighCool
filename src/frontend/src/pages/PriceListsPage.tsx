import { useEffect, useMemo, useState } from "react";
import { Link } from "react-router-dom";
import { RowActions } from "../components/patterns";
import {
  Badge,
  Button,
  DataTable,
  EmptyState,
  Field,
  FilterDropdown,
  FiltersToolbar,
  FilterTextInput,
  PageHeader,
  Pagination,
  Select,
  SkeletonLoader,
  type FilterChip,
  useConfirmationDialog,
  useI18n,
  useToast,
} from "../components/ui";
import { useAuth } from "../features/auth/AuthProvider";
import { ApiError } from "../services/api";
import { Permissions } from "../services/permissions";
import {
  activatePriceList,
  deactivatePriceList,
  deletePriceList,
  listPriceLists,
  type PriceList,
  type PriceListFilters,
} from "../services/pricingApi";

const PAGE_SIZE = 12;

const INITIAL_FILTERS: PriceListFilters = {
  search: "",
  type: "",
  currency: "",
  isActive: "true",
  isDefault: "",
  page: 1,
  pageSize: PAGE_SIZE,
  sortBy: "code",
  sortDirection: "Asc",
};

export function PriceListsPage() {
  const { t } = useI18n();
  const { showToast } = useToast();
  const { confirm, dialog } = useConfirmationDialog();
  const { hasPermission } = useAuth();
  const [rows, setRows] = useState<PriceList[]>([]);
  const [filters, setFilters] = useState<PriceListFilters>(INITIAL_FILTERS);
  const [totalCount, setTotalCount] = useState(0);
  const [totalPages, setTotalPages] = useState(0);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [reloadKey, setReloadKey] = useState(0);
  const canManage = hasPermission(Permissions.PricingPriceListManage);

  useEffect(() => {
    let active = true;

    async function load() {
      try {
        setLoading(true);
        setError("");
        const result = await listPriceLists(filters);

        if (active) {
          setRows(result.items);
          setTotalCount(result.totalCount);
          setTotalPages(result.totalPages);
        }
      } catch (loadError) {
        if (active) {
          setError(loadError instanceof ApiError ? loadError.message : t("module.pricing.priceLists.loadError"));
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
  }, [filters, reloadKey, t]);

  const currencies = useMemo(() => [...new Set(rows.map((row) => row.currency))].sort(), [rows]);
  const activeFilters = useMemo(() => {
    const chips: FilterChip[] = [];
    if (filters.search.trim()) {
      chips.push({ key: "search", label: t("module.pricing.filter.searchChip", { value: filters.search.trim() }), onRemove: () => setFilter("search", "") });
    }
    if (filters.type) {
      chips.push({ key: "type", label: t("module.pricing.filter.typeChip", { value: t(`module.pricing.type.${filters.type}`) }), onRemove: () => setFilter("type", "") });
    }
    if (filters.currency) {
      chips.push({ key: "currency", label: t("module.pricing.filter.currencyChip", { value: filters.currency }), onRemove: () => setFilter("currency", "") });
    }
    if (filters.isActive) {
      chips.push({ key: "status", label: t("module.pricing.filter.statusChip", { value: t(filters.isActive === "true" ? "status.active" : "status.inactive") }), onRemove: () => setFilter("isActive", "") });
    }
    if (filters.isDefault) {
      chips.push({ key: "default", label: t("module.pricing.filter.defaultChip"), onRemove: () => setFilter("isDefault", "") });
    }
    return chips;
  }, [filters, t]);

  const safeTotalPages = Math.max(1, totalPages);
  const resultLabel = totalCount === 1
    ? t("module.pricing.priceLists.resultLabel.one", { count: totalCount })
    : t("module.pricing.priceLists.resultLabel.other", { count: totalCount });

  function setFilter<K extends keyof PriceListFilters>(key: K, value: PriceListFilters[K]) {
    setFilters((current) => ({ ...current, [key]: value, page: 1 }));
  }

  function resetFilters() {
    setFilters(INITIAL_FILTERS);
  }

  async function runAction(action: () => Promise<unknown>, successKey: string) {
    try {
      await action();
      showToast({ tone: "success", title: t(successKey), description: t("module.pricing.priceLists.actionSaved") });
      setReloadKey((current) => current + 1);
    } catch (actionError) {
      setError(actionError instanceof ApiError ? actionError.message : t("module.pricing.priceLists.actionError"));
    }
  }

  async function handleDelete(row: PriceList) {
    const accepted = await confirm({
      title: "module.pricing.priceLists.deleteTitle",
      description: "module.pricing.priceLists.deleteDescription",
      confirmLabel: "common.delete",
      tone: "danger",
    });
    if (!accepted) return;
    await runAction(() => deletePriceList(row.id, row.version), "module.pricing.priceLists.deletedTitle");
  }

  return (
    <section className="hc-list-page">
      {dialog}
      <PageHeader
        title="module.pricing.priceLists.title"
        description="module.pricing.priceLists.description"
        eyebrow="route.section.inventory"
        actions={canManage ? <Link className="hc-button hc-button--primary hc-button--md" to="/price-lists/new">{t("module.pricing.priceLists.new")}</Link> : null}
      />

      <FiltersToolbar
        activeFilters={activeFilters}
        mobileFilters={(
          <>
            <Field label={t("module.pricing.type")}>
              <Select value={filters.type} onChange={(event) => setFilter("type", event.target.value)}>
                <option value="">{t("module.pricing.allTypes")}</option>
                <option value="Selling">{t("module.pricing.type.Selling")}</option>
                <option value="Buying">{t("module.pricing.type.Buying")}</option>
              </Select>
            </Field>
            <Field label={t("module.pricing.currency")}>
              <Select value={filters.currency} onChange={(event) => setFilter("currency", event.target.value)}>
                <option value="">{t("module.pricing.allCurrencies")}</option>
                {currencies.map((currency) => <option key={currency} value={currency}>{currency}</option>)}
              </Select>
            </Field>
          </>
        )}
        onReset={resetFilters}
        primaryFilters={(
          <>
            <FilterDropdown aria-label="module.pricing.type" value={filters.type} onChange={(event) => setFilter("type", event.target.value)}>
              <option value="">{t("module.pricing.allTypes")}</option>
              <option value="Selling">{t("module.pricing.type.Selling")}</option>
              <option value="Buying">{t("module.pricing.type.Buying")}</option>
            </FilterDropdown>
            <FilterDropdown aria-label="table.status" value={filters.isActive} onChange={(event) => setFilter("isActive", event.target.value)}>
              <option value="">{t("module.pricing.allStatuses")}</option>
              <option value="true">{t("status.active")}</option>
              <option value="false">{t("status.inactive")}</option>
            </FilterDropdown>
          </>
        )}
        resultLabel={resultLabel}
        search={<FilterTextInput aria-label="module.pricing.priceLists.searchAria" placeholder={t("module.pricing.priceLists.searchPlaceholder")} value={filters.search} onChange={(event) => setFilter("search", event.target.value)} />}
      />

      {error ? <div className="hc-card hc-card--md"><EmptyState title="module.pricing.priceLists.errorTitle" description={error} action={<Button variant="secondary" onClick={() => setReloadKey((current) => current + 1)}>common.retry</Button>} /></div> : null}

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
          hasData={rows.length > 0}
          columns={<tr><th scope="col">{t("module.pricing.priceList")}</th><th scope="col">{t("module.pricing.type")}</th><th scope="col">{t("module.pricing.currency")}</th><th scope="col">{t("module.pricing.itemPrices")}</th><th scope="col">{t("table.status")}</th><th scope="col" className="hc-table__head-actions" aria-label={t("common.actions")} /></tr>}
          rows={rows.map((row) => (
            <tr key={row.id} className="hc-table__row">
              <td><div className="hc-table__cell-strong hc-table__primary-cell"><span className="hc-table__title">{row.name}</span><span className="hc-table__subtitle">{row.code}</span></div></td>
              <td>{t(`module.pricing.type.${row.type}`)}</td>
              <td>{row.currency}</td>
              <td>{row.itemPriceCount}</td>
              <td><div className="hc-table__status-stack"><Badge tone={row.isActive ? "success" : "neutral"}>{row.isActive ? t("status.active") : t("status.inactive")}</Badge>{row.isDefault ? <Badge tone="primary">{t("module.pricing.default")}</Badge> : null}</div></td>
              <td className="hc-table__cell-actions">
                <RowActions
                  primaryAction={<Link className="hc-button hc-button--secondary hc-button--sm hc-table__action-button" to={`/price-lists/${row.id}`}>{t("common.view")}</Link>}
                  menuItems={[
                    ...(canManage ? [{ label: t("common.edit"), to: `/price-lists/${row.id}/edit` }] : []),
                    ...(canManage && row.isActive ? [{ label: t("module.pricing.deactivate"), onSelect: () => void runAction(() => deactivatePriceList(row.id, row.version), "module.pricing.priceLists.deactivatedTitle") }] : []),
                    ...(canManage && !row.isActive ? [{ label: t("module.pricing.activate"), onSelect: () => void runAction(() => activatePriceList(row.id, row.version), "module.pricing.priceLists.activatedTitle") }] : []),
                    ...(canManage ? [{ label: t("common.delete"), onSelect: () => void handleDelete(row) }] : []),
                  ]}
                />
              </td>
            </tr>
          ))}
          footer={<Pagination currentPage={filters.page} onPageChange={(page) => setFilter("page", page)} pageSize={PAGE_SIZE} totalCount={totalCount} totalPages={safeTotalPages} />}
          emptyState={<EmptyState title="module.pricing.priceLists.empty" description="module.pricing.priceLists.emptyDescription" action={canManage ? <Link className="hc-button hc-button--primary hc-button--md" to="/price-lists/new">{t("module.pricing.priceLists.new")}</Link> : undefined} />}
        />
      ) : null}
    </section>
  );
}
