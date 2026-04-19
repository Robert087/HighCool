import { requestJson } from "./api";

export interface Supplier {
  id: string;
  code: string;
  name: string;
  statementName: string;
  phone: string | null;
  email: string | null;
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
  isActive: boolean;
  isSellable: boolean;
  isComponent: boolean;
  createdAt: string;
  updatedAt: string | null;
}

export interface ItemComponent {
  id: string;
  parentItemId: string;
  parentItemCode: string;
  parentItemName: string;
  componentItemId: string;
  componentItemCode: string;
  componentItemName: string;
  quantity: number;
  createdAt: string;
  updatedAt: string | null;
}

export type RoundingMode = "None" | "Round" | "Floor" | "Ceiling";

export interface ItemUomConversion {
  id: string;
  itemId: string;
  itemCode: string;
  itemName: string;
  fromUomId: string;
  fromUomCode: string;
  toUomId: string;
  toUomCode: string;
  factor: number;
  roundingMode: RoundingMode;
  minFraction: number;
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
  isComponent: boolean;
}

export interface ItemComponentFormValues {
  parentItemId: string;
  componentItemId: string;
  quantity: number;
}

export interface ItemUomConversionFormValues {
  itemId: string;
  fromUomId: string;
  toUomId: string;
  factor: number;
  roundingMode: RoundingMode;
  minFraction: number;
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

export function listSuppliers(search: string, status: string) {
  return requestJson<Supplier[]>(buildUrl("/api/suppliers", search, status));
}

export function getSupplier(id: string) {
  return requestJson<Supplier>(`/api/suppliers/${id}`);
}

export function createSupplier(values: SupplierFormValues) {
  return requestJson<Supplier>("/api/suppliers", {
    method: "POST",
    body: JSON.stringify(values),
  });
}

export function updateSupplier(id: string, values: SupplierFormValues) {
  return requestJson<Supplier>(`/api/suppliers/${id}`, {
    method: "PUT",
    body: JSON.stringify(values),
  });
}

export function deactivateSupplier(id: string) {
  return requestJson<void>(`/api/suppliers/${id}/deactivate`, { method: "POST" });
}

export function listWarehouses(search: string, status: string) {
  return requestJson<Warehouse[]>(buildUrl("/api/warehouses", search, status));
}

export function getWarehouse(id: string) {
  return requestJson<Warehouse>(`/api/warehouses/${id}`);
}

export function createWarehouse(values: WarehouseFormValues) {
  return requestJson<Warehouse>("/api/warehouses", {
    method: "POST",
    body: JSON.stringify(values),
  });
}

export function updateWarehouse(id: string, values: WarehouseFormValues) {
  return requestJson<Warehouse>(`/api/warehouses/${id}`, {
    method: "PUT",
    body: JSON.stringify(values),
  });
}

export function deactivateWarehouse(id: string) {
  return requestJson<void>(`/api/warehouses/${id}/deactivate`, { method: "POST" });
}

export function listUoms(search: string, status: string) {
  return requestJson<Uom[]>(buildUrl("/api/uoms", search, status));
}

export function getUom(id: string) {
  return requestJson<Uom>(`/api/uoms/${id}`);
}

export function createUom(values: UomFormValues) {
  return requestJson<Uom>("/api/uoms", {
    method: "POST",
    body: JSON.stringify(values),
  });
}

export function updateUom(id: string, values: UomFormValues) {
  return requestJson<Uom>(`/api/uoms/${id}`, {
    method: "PUT",
    body: JSON.stringify(values),
  });
}

export function deactivateUom(id: string) {
  return requestJson<void>(`/api/uoms/${id}/deactivate`, { method: "POST" });
}

export function listItems(search: string, status: string) {
  return requestJson<Item[]>(buildUrl("/api/items", search, status));
}

export function getItem(id: string) {
  return requestJson<Item>(`/api/items/${id}`);
}

export function createItem(values: ItemFormValues) {
  return requestJson<Item>("/api/items", {
    method: "POST",
    body: JSON.stringify(values),
  });
}

export function updateItem(id: string, values: ItemFormValues) {
  return requestJson<Item>(`/api/items/${id}`, {
    method: "PUT",
    body: JSON.stringify(values),
  });
}

export function deactivateItem(id: string) {
  return requestJson<void>(`/api/items/${id}/deactivate`, { method: "POST" });
}

function buildItemComponentUrl(search?: string): string {
  const url = new URL("/api/item-components", window.location.origin);
  if (search) {
    url.searchParams.set("search", search);
  }

  return `${url.pathname}${url.search}`;
}

export function listItemComponents(search: string) {
  return requestJson<ItemComponent[]>(buildItemComponentUrl(search));
}

export function getItemComponent(id: string) {
  return requestJson<ItemComponent>(`/api/item-components/${id}`);
}

export function createItemComponent(values: ItemComponentFormValues) {
  return requestJson<ItemComponent>("/api/item-components", {
    method: "POST",
    body: JSON.stringify({
      ...values,
      quantity: Number(values.quantity),
    }),
  });
}

export function updateItemComponent(id: string, values: ItemComponentFormValues) {
  return requestJson<ItemComponent>(`/api/item-components/${id}`, {
    method: "PUT",
    body: JSON.stringify({
      ...values,
      quantity: Number(values.quantity),
    }),
  });
}

export function deleteItemComponent(id: string) {
  return requestJson<void>(`/api/item-components/${id}`, { method: "DELETE" });
}

export function listItemUomConversions(search: string, status: string) {
  return requestJson<ItemUomConversion[]>(buildUrl("/api/item-uom-conversions", search, status));
}

export function getItemUomConversion(id: string) {
  return requestJson<ItemUomConversion>(`/api/item-uom-conversions/${id}`);
}

export function createItemUomConversion(values: ItemUomConversionFormValues) {
  return requestJson<ItemUomConversion>("/api/item-uom-conversions", {
    method: "POST",
    body: JSON.stringify(values),
  });
}

export function updateItemUomConversion(id: string, values: ItemUomConversionFormValues) {
  return requestJson<ItemUomConversion>(`/api/item-uom-conversions/${id}`, {
    method: "PUT",
    body: JSON.stringify(values),
  });
}

export function deactivateItemUomConversion(id: string) {
  return requestJson<void>(`/api/item-uom-conversions/${id}/deactivate`, { method: "POST" });
}
