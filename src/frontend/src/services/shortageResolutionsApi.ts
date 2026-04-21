import { buildApiUrl, requestJson } from "./api";
import type { DocumentStatus } from "./purchaseReceiptsApi";

export type ShortageResolutionType = "Physical" | "Financial";
export type ShortageEntryStatus = "Open" | "PartiallyResolved" | "Resolved" | "Canceled";

export interface OpenShortageFilters {
  search: string;
  supplierId: string;
  itemId: string;
  componentItemId: string;
  status: string;
  affectsSupplierBalance: string;
  fromDate: string;
  toDate: string;
}

export interface ShortageResolutionFilters {
  search: string;
  supplierId: string;
  resolutionType: string;
  status: string;
  fromDate: string;
  toDate: string;
}

export interface OpenShortage {
  id: string;
  supplierId: string;
  supplierCode: string;
  supplierName: string;
  purchaseReceiptId: string;
  purchaseReceiptNo: string;
  receiptDate: string;
  purchaseReceiptLineId: string;
  purchaseOrderId: string | null;
  purchaseOrderNo: string | null;
  itemId: string;
  itemCode: string;
  itemName: string;
  componentItemId: string;
  componentItemCode: string;
  componentItemName: string;
  shortageQty: number;
  resolvedQty: number;
  openQty: number;
  shortageValue: number | null;
  resolvedAmount: number;
  openAmount: number | null;
  status: ShortageEntryStatus;
  affectsSupplierBalance: boolean;
  shortageReasonCodeId: string | null;
  shortageReasonCode: string | null;
  shortageReasonName: string | null;
  approvalStatus: string;
  createdAt: string;
  updatedAt: string | null;
}

export interface ShortageResolutionAllocation {
  id: string;
  resolutionId: string;
  shortageLedgerId: string;
  sequenceNo: number;
  allocationMethod: string;
  allocatedQty: number | null;
  allocatedAmount: number | null;
  valuationRate: number | null;
  supplierId: string;
  supplierCode: string;
  supplierName: string;
  purchaseReceiptNo: string;
  receiptDate: string;
  itemId: string;
  itemCode: string;
  itemName: string;
  componentItemId: string;
  componentItemCode: string;
  componentItemName: string;
  shortageQty: number;
  resolvedQty: number;
  openQty: number;
  openAmount: number | null;
  affectsSupplierBalance: boolean;
  status: string;
  createdAt: string;
  createdBy: string;
}

export interface ShortageResolution {
  id: string;
  resolutionNo: string;
  supplierId: string;
  supplierCode: string;
  supplierName: string;
  resolutionType: ShortageResolutionType;
  resolutionDate: string;
  totalQty: number | null;
  totalAmount: number | null;
  currency: string | null;
  notes: string | null;
  status: DocumentStatus;
  approvedBy: string | null;
  createdAt: string;
  createdBy: string;
  updatedAt: string | null;
  updatedBy: string | null;
  allocations: ShortageResolutionAllocation[];
}

export interface ShortageResolutionListItem {
  id: string;
  resolutionNo: string;
  supplierId: string;
  supplierCode: string;
  supplierName: string;
  resolutionType: ShortageResolutionType;
  resolutionDate: string;
  totalQty: number | null;
  totalAmount: number | null;
  currency: string | null;
  status: DocumentStatus;
  allocationCount: number;
  createdAt: string;
  updatedAt: string | null;
}

export interface SuggestedShortageAllocation {
  shortageLedgerId: string;
  sequenceNo: number;
  allocationMethod: string;
  allocatedQty: number | null;
  allocatedAmount: number | null;
  valuationRate: number | null;
  openQty: number;
  openAmount: number | null;
  purchaseReceiptNo: string;
  receiptDate: string;
  itemCode: string;
  componentItemCode: string;
}

export interface ShortageResolutionAllocationFormValues {
  shortageLedgerId: string;
  allocatedQty: number | "";
  allocatedAmount: number | "";
  valuationRate: number | "";
  allocationMethod: string;
  sequenceNo: number;
}

export interface ShortageResolutionFormValues {
  resolutionNo: string;
  supplierId: string;
  resolutionType: ShortageResolutionType;
  resolutionDate: string;
  currency: string;
  notes: string;
  allocations: ShortageResolutionAllocationFormValues[];
}

function applyDateFilters(url: URL, fromDate: string, toDate: string) {
  if (fromDate) {
    url.searchParams.set("fromDate", new Date(fromDate).toISOString());
  }

  if (toDate) {
    const endOfDay = new Date(toDate);
    endOfDay.setHours(23, 59, 59, 999);
    url.searchParams.set("toDate", endOfDay.toISOString());
  }
}

function buildOpenShortageUrl(filters: OpenShortageFilters) {
  const url = new URL(buildApiUrl("/api/shortages/open"));

  if (filters.search.trim()) {
    url.searchParams.set("search", filters.search.trim());
  }

  if (filters.supplierId) {
    url.searchParams.set("supplierId", filters.supplierId);
  }

  if (filters.itemId) {
    url.searchParams.set("itemId", filters.itemId);
  }

  if (filters.componentItemId) {
    url.searchParams.set("componentItemId", filters.componentItemId);
  }

  if (filters.status) {
    url.searchParams.set("status", filters.status);
  }

  if (filters.affectsSupplierBalance) {
    url.searchParams.set("affectsSupplierBalance", String(filters.affectsSupplierBalance === "yes"));
  }

  applyDateFilters(url, filters.fromDate, filters.toDate);
  return url.toString();
}

function buildResolutionListUrl(filters: ShortageResolutionFilters) {
  const url = new URL(buildApiUrl("/api/shortage-resolutions"));

  if (filters.search.trim()) {
    url.searchParams.set("search", filters.search.trim());
  }

  if (filters.supplierId) {
    url.searchParams.set("supplierId", filters.supplierId);
  }

  if (filters.resolutionType) {
    url.searchParams.set("resolutionType", filters.resolutionType);
  }

  if (filters.status) {
    url.searchParams.set("status", filters.status);
  }

  applyDateFilters(url, filters.fromDate, filters.toDate);
  return url.toString();
}

function normalizePayload(values: ShortageResolutionFormValues) {
  return {
    resolutionNo: values.resolutionNo.trim() || null,
    supplierId: values.supplierId,
    resolutionType: values.resolutionType,
    resolutionDate: values.resolutionDate ? new Date(values.resolutionDate).toISOString() : null,
    currency: values.currency.trim() || null,
    notes: values.notes.trim() || null,
    allocations: values.allocations.map((allocation) => ({
      shortageLedgerId: allocation.shortageLedgerId,
      allocatedQty: allocation.allocatedQty === "" ? null : Number(allocation.allocatedQty),
      allocatedAmount: allocation.allocatedAmount === "" ? null : Number(allocation.allocatedAmount),
      valuationRate: allocation.valuationRate === "" ? null : Number(allocation.valuationRate),
      allocationMethod: allocation.allocationMethod.trim() || "Manual",
      sequenceNo: Number(allocation.sequenceNo),
    })),
  };
}

export function listOpenShortages(filters: OpenShortageFilters) {
  return requestJson<OpenShortage[]>(buildOpenShortageUrl(filters));
}

export function getShortage(id: string) {
  return requestJson<OpenShortage>(`/api/shortages/${id}`);
}

export function listShortageResolutions(filters: ShortageResolutionFilters) {
  return requestJson<ShortageResolutionListItem[]>(buildResolutionListUrl(filters));
}

export function getShortageResolution(id: string) {
  return requestJson<ShortageResolution>(`/api/shortage-resolutions/${id}`);
}

export function getShortageResolutionAllocations(id: string) {
  return requestJson<ShortageResolutionAllocation[]>(`/api/shortage-resolutions/${id}/allocations`);
}

export function createShortageResolution(values: ShortageResolutionFormValues) {
  return requestJson<ShortageResolution>("/api/shortage-resolutions", {
    method: "POST",
    body: JSON.stringify(normalizePayload(values)),
  });
}

export function updateShortageResolution(id: string, values: ShortageResolutionFormValues) {
  return requestJson<ShortageResolution>(`/api/shortage-resolutions/${id}`, {
    method: "PUT",
    body: JSON.stringify(normalizePayload(values)),
  });
}

export function postShortageResolution(id: string) {
  return requestJson<ShortageResolution>(`/api/shortage-resolutions/${id}/post`, {
    method: "POST",
  });
}

export function suggestShortageAllocations(
  supplierId: string,
  resolutionType: ShortageResolutionType,
  totalQty?: number | null,
  totalAmount?: number | null,
) {
  return requestJson<SuggestedShortageAllocation[]>("/api/shortage-resolutions/suggest-allocations", {
    method: "POST",
    body: JSON.stringify({
      supplierId,
      resolutionType,
      totalQty: totalQty ?? null,
      totalAmount: totalAmount ?? null,
    }),
  });
}
