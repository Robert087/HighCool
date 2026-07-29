import { requestJson, type PaginatedResult, type PaginationParams } from "./api";
import type { DocumentStatus } from "./purchaseReceiptsApi";

export type InventoryIssueReason = "InternalConsumption" | "Damage" | "Scrap" | "Sample" | "Maintenance" | "BranchUse" | "Other";

export const INVENTORY_ISSUE_REASONS: InventoryIssueReason[] = [
  "InternalConsumption",
  "Damage",
  "Scrap",
  "Sample",
  "Maintenance",
  "BranchUse",
  "Other",
];

export interface InventoryIssueListFilters extends PaginationParams {
  search: string;
  issueNo: string;
  warehouseId: string;
  reason: string;
  status: string;
  fromDate: string;
  toDate: string;
}

export interface InventoryIssueListItem {
  id: string;
  issueNo: string;
  issueDate: string;
  warehouseId: string;
  warehouseCode: string;
  warehouseName: string;
  reason: InventoryIssueReason;
  referenceNo: string | null;
  requestedBy: string | null;
  status: DocumentStatus;
  lineCount: number;
  createdBy: string;
  createdAt: string;
  updatedAt: string | null;
  postedAt: string | null;
  postedBy: string | null;
  canceledAt: string | null;
  canceledBy: string | null;
}

export interface InventoryIssueLine {
  id: string;
  lineNo: number;
  itemId: string;
  itemCode: string;
  itemName: string;
  uomId: string;
  uomCode: string;
  uomName: string;
  quantity: number;
  baseQty: number;
  notes: string | null;
  createdAt: string;
  updatedAt: string | null;
}

export interface InventoryIssue {
  id: string;
  issueNo: string;
  issueDate: string;
  warehouseId: string;
  warehouseCode: string;
  warehouseName: string;
  reason: InventoryIssueReason;
  referenceNo: string | null;
  requestedBy: string | null;
  notes: string | null;
  status: DocumentStatus;
  lines: InventoryIssueLine[];
  createdAt: string;
  createdBy: string;
  updatedAt: string | null;
  updatedBy: string | null;
  postedAt: string | null;
  postedBy: string | null;
  canceledAt: string | null;
  canceledBy: string | null;
}

export interface InventoryIssueLineFormValues {
  lineNo: number;
  itemId: string;
  uomId: string;
  quantity: number | "";
  baseQty: number;
  notes: string;
}

export interface InventoryIssueFormValues {
  issueNo: string;
  issueDate: string;
  warehouseId: string;
  reason: InventoryIssueReason | "";
  referenceNo: string;
  requestedBy: string;
  notes: string;
  lines: InventoryIssueLineFormValues[];
}

function buildListUrl(filters: InventoryIssueListFilters) {
  const url = new URL("/api/inventory-issues", window.location.origin);

  if (filters.search.trim()) {
    url.searchParams.set("search", filters.search.trim());
  }

  if (filters.issueNo.trim()) {
    url.searchParams.set("issueNo", filters.issueNo.trim());
  }

  if (filters.warehouseId) {
    url.searchParams.set("warehouseId", filters.warehouseId);
  }

  if (filters.reason) {
    url.searchParams.set("reason", filters.reason);
  }

  if (filters.status) {
    url.searchParams.set("status", filters.status);
  }

  if (filters.fromDate) {
    url.searchParams.set("fromDate", new Date(filters.fromDate).toISOString());
  }

  if (filters.toDate) {
    const endOfDay = new Date(filters.toDate);
    endOfDay.setHours(23, 59, 59, 999);
    url.searchParams.set("toDate", endOfDay.toISOString());
  }

  url.searchParams.set("page", String(filters.page));
  url.searchParams.set("pageSize", String(filters.pageSize));
  url.searchParams.set("sortBy", filters.sortBy ?? "issueDate");
  url.searchParams.set("sortDirection", filters.sortDirection ?? "Desc");

  return `${url.pathname}${url.search}`;
}

function normalizePayload(values: InventoryIssueFormValues) {
  return {
    issueNo: values.issueNo.trim() || null,
    issueDate: values.issueDate ? new Date(values.issueDate).toISOString() : null,
    warehouseId: values.warehouseId,
    reason: values.reason || null,
    referenceNo: values.referenceNo.trim() || null,
    requestedBy: values.requestedBy.trim() || null,
    notes: values.notes.trim() || null,
    lines: values.lines.map((line) => ({
      lineNo: Number(line.lineNo),
      itemId: line.itemId,
      uomId: line.uomId,
      quantity: line.quantity === "" ? 0 : Number(line.quantity),
      notes: line.notes.trim() || null,
    })),
  };
}

export function listInventoryIssues(filters: InventoryIssueListFilters) {
  return requestJson<PaginatedResult<InventoryIssueListItem>>(buildListUrl(filters));
}

export function getInventoryIssue(id: string) {
  return requestJson<InventoryIssue>(`/api/inventory-issues/${id}`);
}

export function createInventoryIssue(values: InventoryIssueFormValues) {
  return requestJson<InventoryIssue>("/api/inventory-issues", {
    method: "POST",
    body: JSON.stringify(normalizePayload(values)),
  });
}

export function updateInventoryIssue(id: string, values: InventoryIssueFormValues) {
  return requestJson<InventoryIssue>(`/api/inventory-issues/${id}`, {
    method: "PUT",
    body: JSON.stringify(normalizePayload(values)),
  });
}

export function deleteInventoryIssueDraft(id: string) {
  return requestJson<void>(`/api/inventory-issues/${id}`, {
    method: "DELETE",
  });
}

export function postInventoryIssue(id: string) {
  return requestJson<InventoryIssue>(`/api/inventory-issues/${id}/post`, {
    method: "POST",
  });
}

export function cancelInventoryIssue(id: string) {
  return requestJson<InventoryIssue>(`/api/inventory-issues/${id}/cancel`, {
    method: "POST",
  });
}

export function mapInventoryIssueToFormValues(document: InventoryIssue): InventoryIssueFormValues {
  return {
    issueNo: document.issueNo,
    issueDate: document.issueDate.slice(0, 10),
    warehouseId: document.warehouseId,
    reason: document.reason,
    referenceNo: document.referenceNo ?? "",
    requestedBy: document.requestedBy ?? "",
    notes: document.notes ?? "",
    lines: document.lines.map((line) => ({
      lineNo: line.lineNo,
      itemId: line.itemId,
      uomId: line.uomId,
      quantity: line.quantity,
      baseQty: line.baseQty,
      notes: line.notes ?? "",
    })),
  };
}
