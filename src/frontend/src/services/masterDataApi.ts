import { requestJson } from "./api";

export interface Supplier {
  id: string;
  code: string;
  name: string;
  statementName: string;
  phone: string | null;
  email: string | null;
  taxNumber: string | null;
  address: string | null;
  city: string | null;
  area: string | null;
  creditLimit: number;
  paymentTerms: string | null;
  notes: string | null;
  isActive: boolean;
  createdAt: string;
  updatedAt: string | null;
}

export interface Customer {
  id: string;
  code: string;
  name: string;
  phone: string | null;
  email: string | null;
  taxNumber: string | null;
  address: string | null;
  city: string | null;
  area: string | null;
  creditLimit: number;
  paymentTerms: string | null;
  notes: string | null;
  isActive: boolean;
  createdAt: string;
  updatedAt: string | null;
}

export interface CustomerListItem {
  id: string;
  code: string;
  name: string;
  phone: string | null;
  email: string | null;
  city: string | null;
  area: string | null;
  creditLimit: number;
  paymentTerms: string | null;
  isActive: boolean;
  createdAt: string;
  updatedAt: string | null;
}

export interface Warehouse {
  id: string;
  code: string;
  name: string;
  location: string | null;
  isActive: boolean;
  createdAt: string;
  updatedAt: string | null;
}

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  appliedFilters: unknown;
  sort: {
    sortBy: string;
    direction: "Asc" | "Desc";
  };
}

export interface Uom {
  id: string;
  code: string;
  name: string;
  precision: number;
  allowsFraction: boolean;
  isActive: boolean;
  createdAt: string;
  updatedAt: string | null;
}

export interface Item {
  id: string;
  code: string;
  name: string;
  categoryId: string | null;
  categoryCode: string | null;
  categoryName: string | null;
  baseUomId: string;
  baseUomCode: string;
  baseUomName: string;
  defaultWarehouseId: string | null;
  defaultWarehouseCode: string | null;
  defaultWarehouseName: string | null;
  minimumStockQuantity: number;
  imageUrl?: string;
  isActive: boolean;
  isSellable: boolean;
  hasComponents: boolean;
  components: ItemComponent[];
  createdAt: string;
  updatedAt: string | null;
}

export interface ItemCategory {
  id: string;
  code: string;
  name: string;
  description: string | null;
  isActive: boolean;
  createdAt: string;
  updatedAt: string | null;
}

export interface ItemComponent {
  id: string;
  itemId: string;
  componentItemId: string;
  componentItemCode: string;
  componentItemName: string;
  componentBaseUomId: string;
  componentBaseUomCode: string;
  uomId: string;
  uomCode: string;
  uomName: string;
  quantity: number;
  createdAt: string;
  updatedAt: string | null;
}

export type RoundingMode = "None" | "Round" | "Floor" | "Ceiling";

export interface UomConversion {
  id: string;
  fromUomId: string;
  fromUomCode: string;
  fromUomName: string;
  toUomId: string;
  toUomCode: string;
  toUomName: string;
  factor: number;
  roundingMode: RoundingMode;
  isActive: boolean;
  createdAt: string;
  updatedAt: string | null;
}

export interface SupplierFormValues {
  code: string;
  name: string;
  statementName: string;
  phone: string;
  email: string;
  taxNumber: string;
  address: string;
  city: string;
  area: string;
  creditLimit: number;
  paymentTerms: string;
  notes: string;
  isActive: boolean;
}

export interface CustomerFormValues {
  code: string;
  name: string;
  phone: string;
  email: string;
  taxNumber: string;
  address: string;
  city: string;
  area: string;
  creditLimit: number;
  paymentTerms: string;
  notes: string;
  isActive: boolean;
}

export interface WarehouseFormValues {
  code: string;
  name: string;
  location: string;
  isActive: boolean;
}

export interface UomFormValues {
  code: string;
  name: string;
  precision: number;
  allowsFraction: boolean;
  isActive: boolean;
}

export interface ItemFormValues {
  code: string;
  name: string;
  categoryId: string | null;
  baseUomId: string;
  defaultWarehouseId: string | null;
  minimumStockQuantity: number;
  isActive: boolean;
  isSellable: boolean;
  hasComponents: boolean;
  components: ItemComponentFormValues[];
}

export interface ItemCategoryFormValues {
  code: string;
  name: string;
  description: string;
  isActive: boolean;
}

export interface ItemComponentFormValues {
  componentItemId: string;
  uomId: string;
  quantity: number;
}

export interface UomConversionFormValues {
  fromUomId: string;
  toUomId: string;
  factor: number;
  roundingMode: RoundingMode;
  isActive: boolean;
}

interface MasterDataListParams {
  search?: string;
  status?: string;
  page?: number;
  pageSize?: number;
  sortBy?: string;
  sortDirection?: "Asc" | "Desc";
  isSellable?: string;
  categoryId?: string;
  baseUomId?: string;
  fromUomId?: string;
  toUomId?: string;
}

function buildUrl(path: string, search?: string, isActive?: string): string {
  return buildPagedUrl(path, { search, status: isActive });
}

function buildPagedUrl(path: string, params: MasterDataListParams = {}): string {
  const url = new URL(path, window.location.origin);

  if (params.search) {
    url.searchParams.set("search", params.search);
  }

  if (params.status && params.status !== "all") {
    url.searchParams.set("isActive", String(params.status === "active"));
  }

  if (params.page) {
    url.searchParams.set("page", String(params.page));
  }

  if (params.pageSize) {
    url.searchParams.set("pageSize", String(params.pageSize));
  }

  if (params.sortBy) {
    url.searchParams.set("sortBy", params.sortBy);
  }

  if (params.sortDirection) {
    url.searchParams.set("sortDirection", params.sortDirection);
  }

  if (params.isSellable && params.isSellable !== "all") {
    url.searchParams.set("isSellable", String(params.isSellable === "sellable"));
  }

  if (params.categoryId) {
    url.searchParams.set("categoryId", params.categoryId);
  }

  if (params.baseUomId) {
    url.searchParams.set("baseUomId", params.baseUomId);
  }

  if (params.fromUomId) {
    url.searchParams.set("fromUomId", params.fromUomId);
  }

  if (params.toUomId) {
    url.searchParams.set("toUomId", params.toUomId);
  }

  return `${url.pathname}${url.search}`;
}

const FORM_OPTIONS_CACHE_TTL_MS = 5 * 60 * 1000;

type CacheNamespace =
  | "customers"
  | "item-categories"
  | "items"
  | "suppliers"
  | "uom-conversions"
  | "uoms"
  | "warehouses";

type CachedOptionsEntry<T> = {
  expiresAt: number;
  inflight: Promise<T[]> | null;
  value: T[] | null;
};

const cachedActiveOptions = new Map<CacheNamespace, CachedOptionsEntry<unknown>>();

function invalidateCachedOptions(namespace: CacheNamespace) {
  cachedActiveOptions.delete(namespace);
}

async function getCachedActiveOptions<T>(namespace: CacheNamespace, loader: () => Promise<T[]>): Promise<T[]> {
  const now = Date.now();
  const cached = cachedActiveOptions.get(namespace) as CachedOptionsEntry<T> | undefined;

  if (cached?.value && cached.expiresAt > now) {
    return cached.value;
  }

  if (cached?.inflight) {
    return cached.inflight;
  }

  const inflight = loader();
  cachedActiveOptions.set(namespace, {
    expiresAt: now + FORM_OPTIONS_CACHE_TTL_MS,
    inflight,
    value: cached?.value ?? null,
  });

  try {
    const value = await inflight;
    cachedActiveOptions.set(namespace, {
      expiresAt: Date.now() + FORM_OPTIONS_CACHE_TTL_MS,
      inflight: null,
      value,
    });
    return value;
  } catch (error) {
    cachedActiveOptions.delete(namespace);
    throw error;
  }
}

async function listAllPagedOptions<T>(loader: (page: number, pageSize: number) => Promise<PagedResult<T>>): Promise<T[]> {
  const pageSize = 100;
  const firstPage = await loader(1, pageSize);
  const rows = [...firstPage.items];

  for (let page = 2; page <= firstPage.totalPages; page += 1) {
    const result = await loader(page, pageSize);
    rows.push(...result.items);
  }

  return rows;
}

export function listSuppliers(search: string, status: string) {
  return requestJson<Supplier[]>(buildUrl("/api/suppliers", search, status));
}

export function getActiveSuppliersCached() {
  return getCachedActiveOptions("suppliers", () => listSuppliers("", "active"));
}

export function listCustomers(search: string, status: string) {
  return requestJson<CustomerListItem[]>(buildUrl("/api/customers", search, status));
}

export function getActiveCustomersCached() {
  return getCachedActiveOptions("customers", () => listCustomers("", "active"));
}

export function getCustomer(id: string) {
  return requestJson<Customer>(`/api/customers/${id}`);
}

export function createCustomer(values: CustomerFormValues) {
  return requestJson<Customer>("/api/customers", {
    method: "POST",
    body: JSON.stringify(values),
  }).then((result) => {
    invalidateCachedOptions("customers");
    return result;
  });
}

export function updateCustomer(id: string, values: CustomerFormValues) {
  return requestJson<Customer>(`/api/customers/${id}`, {
    method: "PUT",
    body: JSON.stringify(values),
  }).then((result) => {
    invalidateCachedOptions("customers");
    return result;
  });
}

export function activateCustomer(id: string) {
  return requestJson<void>(`/api/customers/${id}/activate`, { method: "POST" }).then((result) => {
    invalidateCachedOptions("customers");
    return result;
  });
}

export function deactivateCustomer(id: string) {
  return requestJson<void>(`/api/customers/${id}/deactivate`, { method: "POST" }).then((result) => {
    invalidateCachedOptions("customers");
    return result;
  });
}

export function getSupplier(id: string) {
  return requestJson<Supplier>(`/api/suppliers/${id}`);
}

export function createSupplier(values: SupplierFormValues) {
  return requestJson<Supplier>("/api/suppliers", {
    method: "POST",
    body: JSON.stringify(values),
  }).then((result) => {
    invalidateCachedOptions("suppliers");
    return result;
  });
}

export function updateSupplier(id: string, values: SupplierFormValues) {
  return requestJson<Supplier>(`/api/suppliers/${id}`, {
    method: "PUT",
    body: JSON.stringify(values),
  }).then((result) => {
    invalidateCachedOptions("suppliers");
    return result;
  });
}

export function deactivateSupplier(id: string) {
  return requestJson<void>(`/api/suppliers/${id}/deactivate`, { method: "POST" }).then((result) => {
    invalidateCachedOptions("suppliers");
    return result;
  });
}

export function listWarehouses(search: string, status: string, page = 1, pageSize = 20, sortBy = "name", sortDirection: "Asc" | "Desc" = "Asc") {
  return requestJson<PagedResult<Warehouse>>(buildPagedUrl("/api/warehouses", { search, status, page, pageSize, sortBy, sortDirection }));
}

export function getActiveWarehousesCached() {
  return getCachedActiveOptions("warehouses", () => listAllPagedOptions((page, pageSize) => listWarehouses("", "active", page, pageSize)));
}

export function getWarehouse(id: string) {
  return requestJson<Warehouse>(`/api/warehouses/${id}`);
}

export function createWarehouse(values: WarehouseFormValues) {
  return requestJson<Warehouse>("/api/warehouses", {
    method: "POST",
    body: JSON.stringify(values),
  }).then((result) => {
    invalidateCachedOptions("warehouses");
    return result;
  });
}

export function updateWarehouse(id: string, values: WarehouseFormValues) {
  return requestJson<Warehouse>(`/api/warehouses/${id}`, {
    method: "PUT",
    body: JSON.stringify(values),
  }).then((result) => {
    invalidateCachedOptions("warehouses");
    return result;
  });
}

export function deactivateWarehouse(id: string) {
  return requestJson<void>(`/api/warehouses/${id}/deactivate`, { method: "POST" }).then((result) => {
    invalidateCachedOptions("warehouses");
    return result;
  });
}

export function listUoms(search: string, status: string, page = 1, pageSize = 20, sortBy = "name", sortDirection: "Asc" | "Desc" = "Asc") {
  return requestJson<PagedResult<Uom>>(buildPagedUrl("/api/uoms", { search, status, page, pageSize, sortBy, sortDirection }));
}

export function getActiveUomsCached() {
  return getCachedActiveOptions("uoms", () => listAllPagedOptions((page, pageSize) => listUoms("", "active", page, pageSize)));
}

export function getUom(id: string) {
  return requestJson<Uom>(`/api/uoms/${id}`);
}

export function createUom(values: UomFormValues) {
  return requestJson<Uom>("/api/uoms", {
    method: "POST",
    body: JSON.stringify(values),
  }).then((result) => {
    invalidateCachedOptions("uoms");
    return result;
  });
}

export function updateUom(id: string, values: UomFormValues) {
  return requestJson<Uom>(`/api/uoms/${id}`, {
    method: "PUT",
    body: JSON.stringify(values),
  }).then((result) => {
    invalidateCachedOptions("uoms");
    return result;
  });
}

export function deactivateUom(id: string) {
  return requestJson<void>(`/api/uoms/${id}/deactivate`, { method: "POST" }).then((result) => {
    invalidateCachedOptions("uoms");
    return result;
  });
}

export function listItems(
  search: string,
  status: string,
  page = 1,
  pageSize = 20,
  sortBy = "name",
  sortDirection: "Asc" | "Desc" = "Asc",
  filters: Pick<MasterDataListParams, "isSellable" | "categoryId" | "baseUomId"> = {},
) {
  return requestJson<PagedResult<Item>>(buildPagedUrl("/api/items", { search, status, page, pageSize, sortBy, sortDirection, ...filters }));
}

export function getActiveItemsCached() {
  return getCachedActiveOptions("items", () => listAllPagedOptions((page, pageSize) => listItems("", "active", page, pageSize)));
}

export function getItem(id: string) {
  return requestJson<Item>(`/api/items/${id}`);
}

export function createItem(values: ItemFormValues) {
  return requestJson<Item>("/api/items", {
    method: "POST",
    body: JSON.stringify(values),
  }).then((result) => {
    invalidateCachedOptions("items");
    return result;
  });
}

export function updateItem(id: string, values: ItemFormValues) {
  return requestJson<Item>(`/api/items/${id}`, {
    method: "PUT",
    body: JSON.stringify(values),
  }).then((result) => {
    invalidateCachedOptions("items");
    return result;
  });
}

export function deactivateItem(id: string) {
  return requestJson<void>(`/api/items/${id}/deactivate`, { method: "POST" }).then((result) => {
    invalidateCachedOptions("items");
    return result;
  });
}

export function listItemCategories(search: string, status: string, page = 1, pageSize = 20, sortBy = "name", sortDirection: "Asc" | "Desc" = "Asc") {
  return requestJson<PagedResult<ItemCategory>>(buildPagedUrl("/api/item-categories", { search, status, page, pageSize, sortBy, sortDirection }));
}

export function getActiveItemCategoriesCached() {
  return getCachedActiveOptions("item-categories", () => listAllPagedOptions((page, pageSize) => listItemCategories("", "active", page, pageSize)));
}

export function getItemCategory(id: string) {
  return requestJson<ItemCategory>(`/api/item-categories/${id}`);
}

export function createItemCategory(values: ItemCategoryFormValues) {
  return requestJson<ItemCategory>("/api/item-categories", {
    method: "POST",
    body: JSON.stringify(values),
  }).then((result) => {
    invalidateCachedOptions("item-categories");
    return result;
  });
}

export function updateItemCategory(id: string, values: ItemCategoryFormValues) {
  return requestJson<ItemCategory>(`/api/item-categories/${id}`, {
    method: "PUT",
    body: JSON.stringify(values),
  }).then((result) => {
    invalidateCachedOptions("item-categories");
    invalidateCachedOptions("items");
    return result;
  });
}

export function activateItemCategory(id: string) {
  return requestJson<void>(`/api/item-categories/${id}/activate`, { method: "POST" }).then((result) => {
    invalidateCachedOptions("item-categories");
    return result;
  });
}

export function deactivateItemCategory(id: string) {
  return requestJson<void>(`/api/item-categories/${id}/deactivate`, { method: "POST" }).then((result) => {
    invalidateCachedOptions("item-categories");
    return result;
  });
}

export function listUomConversions(search: string, status: string, page = 1, pageSize = 20, sortBy = "fromUom", sortDirection: "Asc" | "Desc" = "Asc", filters: Pick<MasterDataListParams, "fromUomId" | "toUomId"> = {}) {
  return requestJson<PagedResult<UomConversion>>(buildPagedUrl("/api/uom-conversions", { search, status, page, pageSize, sortBy, sortDirection, ...filters }));
}

export function getActiveUomConversionsCached() {
  return getCachedActiveOptions("uom-conversions", () => listAllPagedOptions((page, pageSize) => listUomConversions("", "active", page, pageSize)));
}

export function getUomConversion(id: string) {
  return requestJson<UomConversion>(`/api/uom-conversions/${id}`);
}

export function createUomConversion(values: UomConversionFormValues) {
  return requestJson<UomConversion>("/api/uom-conversions", {
    method: "POST",
    body: JSON.stringify(values),
  }).then((result) => {
    invalidateCachedOptions("uom-conversions");
    return result;
  });
}

export function updateUomConversion(id: string, values: UomConversionFormValues) {
  return requestJson<UomConversion>(`/api/uom-conversions/${id}`, {
    method: "PUT",
    body: JSON.stringify(values),
  }).then((result) => {
    invalidateCachedOptions("uom-conversions");
    return result;
  });
}

export function deactivateUomConversion(id: string) {
  return requestJson<void>(`/api/uom-conversions/${id}/deactivate`, { method: "POST" }).then((result) => {
    invalidateCachedOptions("uom-conversions");
    return result;
  });
}
