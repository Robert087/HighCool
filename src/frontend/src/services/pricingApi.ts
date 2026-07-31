import { requestJson, type PaginatedResult, type PaginationParams } from "./api";

export type PriceListType = "Selling" | "Buying";

export interface PricingOption {
  id: string;
  code: string;
  name: string;
  currency?: string | null;
}

export interface PricingFilterOptions {
  priceLists: PricingOption[];
  items: PricingOption[];
  uoms: PricingOption[];
  categories: PricingOption[];
  currencies: string[];
}

export interface ItemPricingUomOptions {
  itemId: string;
  uoms: PricingOption[];
}

export interface PriceList {
  id: string;
  code: string;
  name: string;
  type: PriceListType;
  currency: string;
  isDefault: boolean;
  isActive: boolean;
  description: string | null;
  itemPriceCount: number;
  version: number;
  createdAt: string;
  updatedAt: string | null;
}

export interface ItemPrice {
  id: string;
  priceListId: string;
  priceListCode: string;
  priceListName: string;
  priceListType: PriceListType;
  itemId: string;
  itemCode: string;
  itemName: string;
  categoryId: string | null;
  categoryCode: string | null;
  categoryName: string | null;
  uomId: string;
  uomCode: string;
  uomName: string;
  currency: string;
  rate: number;
  minimumQuantity: number;
  validFrom: string;
  validTo: string | null;
  isActive: boolean;
  isCurrentlyEffective: boolean;
  notes: string | null;
  version: number;
  createdAt: string;
  updatedAt: string | null;
}

export interface PriceListFilters extends PaginationParams {
  search: string;
  type: string;
  currency: string;
  isActive: string;
  isDefault: string;
}

export interface ItemPriceFilters extends PaginationParams {
  search: string;
  priceListId: string;
  priceListType: string;
  itemId: string;
  categoryId: string;
  uomId: string;
  currency: string;
  isActive: string;
  effectiveOn: string;
}

export interface PriceListFormValues {
  code: string;
  name: string;
  type: PriceListType;
  currency: string;
  isDefault: boolean;
  isActive: boolean;
  description: string;
  version?: number;
}

export interface ItemPriceFormValues {
  priceListId: string;
  itemId: string;
  uomId: string;
  currency: string;
  rate: number | "";
  minimumQuantity: number | "";
  validFrom: string;
  validTo: string;
  isActive: boolean;
  notes: string;
  version?: number;
}

export interface PriceResolution {
  itemPriceId: string;
  priceListId: string;
  itemId: string;
  uomId: string;
  currency: string;
  rate: number;
  minimumQuantity: number;
  validFrom: string;
  validTo: string | null;
}

function appendBaseParams(url: URL, filters: PaginationParams) {
  url.searchParams.set("page", String(filters.page));
  url.searchParams.set("pageSize", String(filters.pageSize));
  url.searchParams.set("sortBy", filters.sortBy ?? "code");
  url.searchParams.set("sortDirection", filters.sortDirection ?? "Asc");
}

function buildPriceListUrl(filters: PriceListFilters) {
  const url = new URL("/api/pricing/price-lists", window.location.origin);
  if (filters.search.trim()) url.searchParams.set("search", filters.search.trim());
  if (filters.type) url.searchParams.set("type", filters.type);
  if (filters.currency) url.searchParams.set("currency", filters.currency);
  if (filters.isActive) url.searchParams.set("isActive", filters.isActive);
  if (filters.isDefault) url.searchParams.set("isDefault", filters.isDefault);
  appendBaseParams(url, filters);
  return `${url.pathname}${url.search}`;
}

function buildItemPriceUrl(filters: ItemPriceFilters) {
  const url = new URL("/api/pricing/item-prices", window.location.origin);
  if (filters.search.trim()) url.searchParams.set("search", filters.search.trim());
  if (filters.priceListId) url.searchParams.set("priceListId", filters.priceListId);
  if (filters.priceListType) url.searchParams.set("priceListType", filters.priceListType);
  if (filters.itemId) url.searchParams.set("itemId", filters.itemId);
  if (filters.categoryId) url.searchParams.set("categoryId", filters.categoryId);
  if (filters.uomId) url.searchParams.set("uomId", filters.uomId);
  if (filters.currency) url.searchParams.set("currency", filters.currency);
  if (filters.isActive) url.searchParams.set("isActive", filters.isActive);
  if (filters.effectiveOn) url.searchParams.set("effectiveOn", filters.effectiveOn);
  appendBaseParams(url, filters);
  return `${url.pathname}${url.search}`;
}

function priceListPayload(values: PriceListFormValues) {
  return {
    code: values.code.trim(),
    name: values.name.trim(),
    type: values.type,
    currency: values.currency.trim().toUpperCase(),
    isDefault: values.isDefault,
    isActive: values.isActive,
    description: values.description.trim() || null,
    ...(values.version !== undefined ? { version: values.version } : {}),
  };
}

function itemPricePayload(values: ItemPriceFormValues) {
  return {
    priceListId: values.priceListId,
    itemId: values.itemId,
    uomId: values.uomId,
    currency: values.currency.trim().toUpperCase() || null,
    rate: values.rate === "" ? 0 : Number(values.rate),
    minimumQuantity: values.minimumQuantity === "" ? 0 : Number(values.minimumQuantity),
    validFrom: values.validFrom || null,
    validTo: values.validTo || null,
    isActive: values.isActive,
    notes: values.notes.trim() || null,
    ...(values.version !== undefined ? { version: values.version } : {}),
  };
}

export function getPricingFilterOptions() {
  return requestJson<PricingFilterOptions>("/api/pricing/filter-options");
}

export function getItemPricingUomOptions(itemId: string) {
  return requestJson<ItemPricingUomOptions>(`/api/pricing/items/${itemId}/uoms`);
}

export function listPriceLists(filters: PriceListFilters) {
  return requestJson<PaginatedResult<PriceList>>(buildPriceListUrl(filters));
}

export function getPriceList(id: string) {
  return requestJson<PriceList>(`/api/pricing/price-lists/${id}`);
}

export function createPriceList(values: PriceListFormValues) {
  return requestJson<PriceList>("/api/pricing/price-lists", {
    method: "POST",
    body: JSON.stringify(priceListPayload(values)),
  });
}

export function updatePriceList(id: string, values: PriceListFormValues) {
  return requestJson<PriceList>(`/api/pricing/price-lists/${id}`, {
    method: "PUT",
    body: JSON.stringify(priceListPayload(values)),
  });
}

export function activatePriceList(id: string, version: number) {
  return requestJson<PriceList>(`/api/pricing/price-lists/${id}/activate`, {
    method: "POST",
    body: JSON.stringify({ version }),
  });
}

export function deactivatePriceList(id: string, version: number) {
  return requestJson<PriceList>(`/api/pricing/price-lists/${id}/deactivate`, {
    method: "POST",
    body: JSON.stringify({ version }),
  });
}

export function deletePriceList(id: string, version: number) {
  return requestJson<void>(`/api/pricing/price-lists/${id}?version=${version}`, { method: "DELETE" });
}

export function listItemPrices(filters: ItemPriceFilters) {
  return requestJson<PaginatedResult<ItemPrice>>(buildItemPriceUrl(filters));
}

export function getItemPrice(id: string) {
  return requestJson<ItemPrice>(`/api/pricing/item-prices/${id}`);
}

export function createItemPrice(values: ItemPriceFormValues) {
  return requestJson<ItemPrice>("/api/pricing/item-prices", {
    method: "POST",
    body: JSON.stringify(itemPricePayload(values)),
  });
}

export function updateItemPrice(id: string, values: ItemPriceFormValues) {
  return requestJson<ItemPrice>(`/api/pricing/item-prices/${id}`, {
    method: "PUT",
    body: JSON.stringify(itemPricePayload(values)),
  });
}

export function activateItemPrice(id: string, version: number) {
  return requestJson<ItemPrice>(`/api/pricing/item-prices/${id}/activate`, {
    method: "POST",
    body: JSON.stringify({ version }),
  });
}

export function deactivateItemPrice(id: string, version: number) {
  return requestJson<ItemPrice>(`/api/pricing/item-prices/${id}/deactivate`, {
    method: "POST",
    body: JSON.stringify({ version }),
  });
}

export function deleteItemPrice(id: string, version: number) {
  return requestJson<void>(`/api/pricing/item-prices/${id}?version=${version}`, { method: "DELETE" });
}

export function resolveItemPrice(priceListId: string, itemId: string, uomId: string, quantity: number, effectiveDate: string) {
  const url = new URL("/api/pricing/resolve", window.location.origin);
  url.searchParams.set("priceListId", priceListId);
  url.searchParams.set("itemId", itemId);
  url.searchParams.set("uomId", uomId);
  url.searchParams.set("quantity", String(quantity));
  if (effectiveDate) url.searchParams.set("effectiveDate", effectiveDate);
  return requestJson<PriceResolution>(`${url.pathname}${url.search}`);
}

export function mapPriceListToFormValues(priceList: PriceList): PriceListFormValues {
  return {
    code: priceList.code,
    name: priceList.name,
    type: priceList.type,
    currency: priceList.currency,
    isDefault: priceList.isDefault,
    isActive: priceList.isActive,
    description: priceList.description ?? "",
    version: priceList.version,
  };
}

export function mapItemPriceToFormValues(itemPrice: ItemPrice): ItemPriceFormValues {
  return {
    priceListId: itemPrice.priceListId,
    itemId: itemPrice.itemId,
    uomId: itemPrice.uomId,
    currency: itemPrice.currency,
    rate: itemPrice.rate,
    minimumQuantity: itemPrice.minimumQuantity,
    validFrom: itemPrice.validFrom.slice(0, 10),
    validTo: itemPrice.validTo ? itemPrice.validTo.slice(0, 10) : "",
    isActive: itemPrice.isActive,
    notes: itemPrice.notes ?? "",
    version: itemPrice.version,
  };
}
