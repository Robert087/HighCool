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
  baseUomId: string;
  baseUomCode: string;
  baseUomName: string;
  imageUrl?: string;
  isActive: boolean;
  isSellable: boolean;
  hasComponents: boolean;
  components: ItemComponent[];
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
  baseUomId: string;
  isActive: boolean;
  isSellable: boolean;
  hasComponents: boolean;
  components: ItemComponentFormValues[];
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

function buildUrl(path: string, search?: string, isActive?: string): string {
  const url = new URL(path, window.location.origin);

  if (search) {
    url.searchParams.set("search", search);
  }

  if (isActive && isActive !== "all") {
    url.searchParams.set("isActive", String(isActive === "active"));
  }

  return `${url.pathname}${url.search}`;
}

const FORM_OPTIONS_CACHE_TTL_MS = 5 * 60 * 1000;

type CacheNamespace =
  | "customers"
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

export function listWarehouses(search: string, status: string) {
  return requestJson<Warehouse[]>(buildUrl("/api/warehouses", search, status));
}

export function getActiveWarehousesCached() {
  return getCachedActiveOptions("warehouses", () => listWarehouses("", "active"));
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

export function listUoms(search: string, status: string) {
  return requestJson<Uom[]>(buildUrl("/api/uoms", search, status));
}

export function getActiveUomsCached() {
  return getCachedActiveOptions("uoms", () => listUoms("", "active"));
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

export function listItems(search: string, status: string) {
  return requestJson<Item[]>(buildUrl("/api/items", search, status));
}

export function getActiveItemsCached() {
  return getCachedActiveOptions("items", () => listItems("", "active"));
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

export function listUomConversions(search: string, status: string) {
  return requestJson<UomConversion[]>(buildUrl("/api/uom-conversions", search, status));
}

export function getActiveUomConversionsCached() {
  return getCachedActiveOptions("uom-conversions", () => listUomConversions("", "active"));
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
