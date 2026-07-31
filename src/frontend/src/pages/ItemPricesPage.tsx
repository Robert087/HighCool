import { useEffect, useMemo, useState } from "react";
import { Link } from "react-router-dom";
import { RowActions } from "../components/patterns";
import {
  Badge,
  Button,
  Card,
  DataTable,
  EmptyState,
  Field,
  FilterDropdown,
  FiltersToolbar,
  FilterTextInput,
  Input,
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
  activateItemPrice,
  deactivateItemPrice,
  deleteItemPrice,
  getPricingFilterOptions,
  listItemPrices,
  resolveItemPrice,
  type ItemPrice,
  type ItemPriceFilters,
  type PriceResolution,
  type PricingFilterOptions,
} from "../services/pricingApi";

const PAGE_SIZE = 12;

const INITIAL_FILTERS: ItemPriceFilters = {
  search: "",
  priceListId: "",
  priceListType: "",
  itemId: "",
  categoryId: "",
  uomId: "",
  currency: "",
  isActive: "true",
  effectiveOn: "",
  page: 1,
  pageSize: PAGE_SIZE,
  sortBy: "itemName",
  sortDirection: "Asc",
};

export function ItemPricesPage() {
  const { formatCurrency, formatDate, formatQuantity, t } = useI18n();
  const { showToast } = useToast();
  const { confirm, dialog } = useConfirmationDialog();
  const { hasPermission } = useAuth();
  const [rows, setRows] = useState<ItemPrice[]>([]);
  const [filters, setFilters] = useState<ItemPriceFilters>(INITIAL_FILTERS);
  const [options, setOptions] = useState<PricingFilterOptions>({ priceLists: [], items: [], uoms: [], categories: [], currencies: [] });
  const [resolver, setResolver] = useState({ priceListId: "", itemId: "", uomId: "", quantity: 1, effectiveDate: "" });
  const [resolution, setResolution] = useState<PriceResolution | null>(null);
  const [resolverError, setResolverError] = useState("");
  const [totalCount, setTotalCount] = useState(0);
  const [totalPages, setTotalPages] = useState(0);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [reloadKey, setReloadKey] = useState(0);
  const canManage = hasPermission(Permissions.PricingItemPriceManage);

  useEffect(() => {
    let active = true;
    async function loadOptions() {
      try {
        const result = await getPricingFilterOptions();
        if (active) setOptions(result);
      } catch {
        if (active) setError(t("module.pricing.referencesError"));
      }
    }
    void loadOptions();
    return () => {
      active = false;
    };
  }, [t]);

  useEffect(() => {
    let active = true;
    async function load() {
      try {
        setLoading(true);
        setError("");
        const result = await listItemPrices(filters);
        if (active) {
          setRows(result.items);
          setTotalCount(result.totalCount);
          setTotalPages(result.totalPages);
        }
      } catch (loadError) {
        if (active) setError(loadError instanceof ApiError ? loadError.message : t("module.pricing.itemPrices.loadError"));
      } finally {
        if (active) setLoading(false);
      }
    }
    void load();
    return () => {
      active = false;
    };
  }, [filters, reloadKey, t]);

  const activeFilters = useMemo(() => {
    const chips: FilterChip[] = [];
    const priceList = options.priceLists.find((option) => option.id === filters.priceListId);
    const category = options.categories.find((option) => option.id === filters.categoryId);
    const uom = options.uoms.find((option) => option.id === filters.uomId);
    if (filters.search.trim()) chips.push({ key: "search", label: t("module.pricing.filter.searchChip", { value: filters.search.trim() }), onRemove: () => setFilter("search", "") });
    if (priceList) chips.push({ key: "priceList", label: t("module.pricing.filter.priceListChip", { value: `${priceList.code} - ${priceList.name}` }), onRemove: () => setFilter("priceListId", "") });
    if (filters.priceListType) chips.push({ key: "type", label: t("module.pricing.filter.typeChip", { value: t(`module.pricing.type.${filters.priceListType}`) }), onRemove: () => setFilter("priceListType", "") });
    if (category) chips.push({ key: "category", label: t("module.pricing.filter.categoryChip", { value: `${category.code} - ${category.name}` }), onRemove: () => setFilter("categoryId", "") });
    if (uom) chips.push({ key: "uom", label: t("module.pricing.filter.uomChip", { value: uom.code }), onRemove: () => setFilter("uomId", "") });
    if (filters.currency) chips.push({ key: "currency", label: t("module.pricing.filter.currencyChip", { value: filters.currency }), onRemove: () => setFilter("currency", "") });
    if (filters.isActive) chips.push({ key: "status", label: t("module.pricing.filter.statusChip", { value: t(filters.isActive === "true" ? "status.active" : "status.inactive") }), onRemove: () => setFilter("isActive", "") });
    return chips;
  }, [filters, options, t]);

  const resultLabel = totalCount === 1
    ? t("module.pricing.itemPrices.resultLabel.one", { count: totalCount })
    : t("module.pricing.itemPrices.resultLabel.other", { count: totalCount });
  const safeTotalPages = Math.max(1, totalPages);

  function setFilter<K extends keyof ItemPriceFilters>(key: K, value: ItemPriceFilters[K]) {
    setFilters((current) => ({ ...current, [key]: value, page: 1 }));
  }

  function resetFilters() {
    setFilters(INITIAL_FILTERS);
  }

  async function runAction(action: () => Promise<unknown>, successKey: string) {
    try {
      await action();
      showToast({ tone: "success", title: t(successKey), description: t("module.pricing.itemPrices.actionSaved") });
      setReloadKey((current) => current + 1);
    } catch (actionError) {
      setError(actionError instanceof ApiError ? actionError.message : t("module.pricing.itemPrices.actionError"));
    }
  }

  async function handleDelete(row: ItemPrice) {
    const accepted = await confirm({
      title: "module.pricing.itemPrices.deleteTitle",
      description: "module.pricing.itemPrices.deleteDescription",
      confirmLabel: "common.delete",
      tone: "danger",
    });
    if (!accepted) return;
    await runAction(() => deleteItemPrice(row.id, row.version), "module.pricing.itemPrices.deletedTitle");
  }

  async function handleResolve() {
    try {
      setResolverError("");
      setResolution(null);
      const result = await resolveItemPrice(resolver.priceListId, resolver.itemId, resolver.uomId, Number(resolver.quantity), resolver.effectiveDate);
      setResolution(result);
    } catch (resolveError) {
      setResolverError(resolveError instanceof ApiError ? resolveError.message : t("module.pricing.resolver.error"));
    }
  }

  return (
    <section className="hc-list-page">
      {dialog}
      <PageHeader
        title="module.pricing.itemPrices.title"
        description="module.pricing.itemPrices.description"
        eyebrow="route.section.inventory"
        actions={canManage ? <Link className="hc-button hc-button--primary hc-button--md" to="/item-prices/new">{t("module.pricing.itemPrices.new")}</Link> : null}
      />

      <Card className="hc-statement-summary-panel" padding="md">
        <div className="hc-document-form-grid">
          <Field label={t("module.pricing.priceList")}>
            <Select value={resolver.priceListId} onChange={(event) => setResolver((current) => ({ ...current, priceListId: event.target.value }))}>
              <option value="">{t("module.pricing.selectPriceList")}</option>
              {options.priceLists.map((option) => <option key={option.id} value={option.id}>{option.code} - {option.name}</option>)}
            </Select>
          </Field>
          <Field label={t("table.item")}>
            <Select value={resolver.itemId} onChange={(event) => setResolver((current) => ({ ...current, itemId: event.target.value }))}>
              <option value="">{t("module.pricing.selectItem")}</option>
              {options.items.map((option) => <option key={option.id} value={option.id}>{option.code} - {option.name}</option>)}
            </Select>
          </Field>
          <Field label={t("table.uom")}>
            <Select value={resolver.uomId} onChange={(event) => setResolver((current) => ({ ...current, uomId: event.target.value }))}>
              <option value="">{t("module.pricing.selectUom")}</option>
              {options.uoms.map((option) => <option key={option.id} value={option.id}>{option.code} - {option.name}</option>)}
            </Select>
          </Field>
          <Field label={t("module.pricing.quantity")}>
            <Input min="0.000001" step="0.000001" type="number" value={resolver.quantity} onChange={(event) => setResolver((current) => ({ ...current, quantity: Number(event.target.value) }))} />
          </Field>
          <Field label={t("module.pricing.effectiveDate")}>
            <Input type="date" value={resolver.effectiveDate} onChange={(event) => setResolver((current) => ({ ...current, effectiveDate: event.target.value }))} />
          </Field>
          <div className="hc-document-field--span-full">
            <Button disabled={!resolver.priceListId || !resolver.itemId || !resolver.uomId || resolver.quantity <= 0} onClick={handleResolve}>module.pricing.resolver.resolve</Button>
            {resolution ? <span className="hc-table__subtitle"> {t("module.pricing.resolver.result", { value: formatCurrency(resolution.rate, { currency: resolution.currency }) })}</span> : null}
            {resolverError ? <div className="hc-inline-error">{resolverError}</div> : null}
          </div>
        </div>
      </Card>

      <FiltersToolbar
        activeFilters={activeFilters}
        mobileFilters={(
          <>
            <Field label={t("module.pricing.priceList")}><Select value={filters.priceListId} onChange={(event) => setFilter("priceListId", event.target.value)}><option value="">{t("module.pricing.allPriceLists")}</option>{options.priceLists.map((option) => <option key={option.id} value={option.id}>{option.code} - {option.name}</option>)}</Select></Field>
            <Field label={t("module.pricing.category")}><Select value={filters.categoryId} onChange={(event) => setFilter("categoryId", event.target.value)}><option value="">{t("module.pricing.allCategories")}</option>{options.categories.map((option) => <option key={option.id} value={option.id}>{option.code} - {option.name}</option>)}</Select></Field>
            <Field label={t("table.uom")}><Select value={filters.uomId} onChange={(event) => setFilter("uomId", event.target.value)}><option value="">{t("module.pricing.allUoms")}</option>{options.uoms.map((option) => <option key={option.id} value={option.id}>{option.code} - {option.name}</option>)}</Select></Field>
          </>
        )}
        onReset={resetFilters}
        primaryFilters={(
          <>
            <FilterDropdown aria-label="module.pricing.priceList" value={filters.priceListId} onChange={(event) => setFilter("priceListId", event.target.value)}>
              <option value="">{t("module.pricing.allPriceLists")}</option>
              {options.priceLists.map((option) => <option key={option.id} value={option.id}>{option.code} - {option.name}</option>)}
            </FilterDropdown>
            <FilterDropdown aria-label="module.pricing.type" value={filters.priceListType} onChange={(event) => setFilter("priceListType", event.target.value)}>
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
        search={<FilterTextInput aria-label="module.pricing.itemPrices.searchAria" placeholder={t("module.pricing.itemPrices.searchPlaceholder")} value={filters.search} onChange={(event) => setFilter("search", event.target.value)} />}
      />

      {error ? <div className="hc-card hc-card--md"><EmptyState title="module.pricing.itemPrices.errorTitle" description={error} action={<Button variant="secondary" onClick={() => setReloadKey((current) => current + 1)}>common.retry</Button>} /></div> : null}
      {loading ? <div className="hc-card hc-card--md hc-table-card"><div className="hc-skeleton-stack"><SkeletonLoader height="2.75rem" variant="rect" /><SkeletonLoader height="3.5rem" variant="rect" /><SkeletonLoader height="3.5rem" variant="rect" /></div></div> : null}
      {!loading && !error ? (
        <DataTable
          hasData={rows.length > 0}
          columns={<tr><th scope="col">{t("table.item")}</th><th scope="col">{t("module.pricing.priceList")}</th><th scope="col">{t("module.pricing.rate")}</th><th scope="col">{t("module.pricing.validity")}</th><th scope="col">{t("table.status")}</th><th scope="col" className="hc-table__head-actions" aria-label={t("common.actions")} /></tr>}
          rows={rows.map((row) => (
            <tr key={row.id} className="hc-table__row">
              <td><div className="hc-table__cell-strong hc-table__primary-cell"><span className="hc-table__title">{row.itemName}</span><span className="hc-table__subtitle">{row.itemCode} / {row.uomCode}</span></div></td>
              <td><div className="hc-table__cell-strong"><span className="hc-table__title">{row.priceListName}</span><span className="hc-table__subtitle">{row.priceListCode} / {t(`module.pricing.type.${row.priceListType}`)}</span></div></td>
              <td><div className="hc-table__cell-strong"><span className="hc-table__title">{formatCurrency(row.rate, { currency: row.currency })}</span><span className="hc-table__subtitle">{t("module.pricing.minQtyValue", { value: formatQuantity(row.minimumQuantity) })}</span></div></td>
              <td><span className="hc-table__subtitle">{formatDate(row.validFrom)} - {row.validTo ? formatDate(row.validTo) : t("module.pricing.openEnded")}</span></td>
              <td><div className="hc-table__status-stack"><Badge tone={row.isActive ? "success" : "neutral"}>{row.isActive ? t("status.active") : t("status.inactive")}</Badge>{row.isCurrentlyEffective ? <Badge tone="primary">{t("module.pricing.current")}</Badge> : null}</div></td>
              <td className="hc-table__cell-actions">
                <RowActions
                  primaryAction={<Link className="hc-button hc-button--secondary hc-button--sm hc-table__action-button" to={`/item-prices/${row.id}`}>{t("common.view")}</Link>}
                  menuItems={[
                    ...(canManage ? [{ label: t("common.edit"), to: `/item-prices/${row.id}/edit` }] : []),
                    ...(canManage && row.isActive ? [{ label: t("module.pricing.deactivate"), onSelect: () => void runAction(() => deactivateItemPrice(row.id, row.version), "module.pricing.itemPrices.deactivatedTitle") }] : []),
                    ...(canManage && !row.isActive ? [{ label: t("module.pricing.activate"), onSelect: () => void runAction(() => activateItemPrice(row.id, row.version), "module.pricing.itemPrices.activatedTitle") }] : []),
                    ...(canManage ? [{ label: t("common.delete"), onSelect: () => void handleDelete(row) }] : []),
                  ]}
                />
              </td>
            </tr>
          ))}
          footer={<Pagination currentPage={filters.page} onPageChange={(page) => setFilter("page", page)} pageSize={PAGE_SIZE} totalCount={totalCount} totalPages={safeTotalPages} />}
          emptyState={<EmptyState title="module.pricing.itemPrices.empty" description="module.pricing.itemPrices.emptyDescription" action={canManage ? <Link className="hc-button hc-button--primary hc-button--md" to="/item-prices/new">{t("module.pricing.itemPrices.new")}</Link> : undefined} />}
        />
      ) : null}
    </section>
  );
}
