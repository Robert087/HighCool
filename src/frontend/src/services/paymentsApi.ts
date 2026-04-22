import { requestJson } from "./api";

export type DocumentStatus = "Draft" | "Posted" | "Canceled";
export type PaymentPartyType = "Supplier";
export type PaymentDirection = "OutboundToParty" | "InboundFromParty";
export type PaymentMethod = "Cash" | "BankTransfer" | "Cheque" | "Other";
export type PaymentTargetDocumentType = "PurchaseReceipt" | "ShortageResolution";

export interface PaymentFilters {
  search: string;
  supplierId: string;
  direction: string;
  status: string;
  paymentMethod: string;
  fromDate: string;
  toDate: string;
}

export interface PaymentListItem {
  id: string;
  paymentNo: string;
  partyType: PaymentPartyType;
  partyId: string;
  partyCode: string;
  partyName: string;
  direction: PaymentDirection;
  amount: number;
  allocatedAmount: number;
  unallocatedAmount: number;
  paymentDate: string;
  currency: string | null;
  paymentMethod: PaymentMethod;
  referenceNote: string | null;
  status: DocumentStatus;
  allocationCount: number;
  createdAt: string;
  updatedAt: string | null;
}

export interface PaymentAllocation {
  id: string;
  targetDocType: PaymentTargetDocumentType;
  targetDocId: string;
  targetLineId: string | null;
  targetDocumentNo: string;
  targetDocumentDate: string;
  originalAmount: number;
  alreadyAllocatedAmount: number;
  openAmount: number;
  allocatedAmount: number;
  allocationOrder: number;
  createdAt: string;
  createdBy: string;
}

export interface Payment {
  id: string;
  paymentNo: string;
  partyType: PaymentPartyType;
  partyId: string;
  partyCode: string;
  partyName: string;
  direction: PaymentDirection;
  amount: number;
  allocatedAmount: number;
  unallocatedAmount: number;
  paymentDate: string;
  currency: string | null;
  exchangeRate: number | null;
  paymentMethod: PaymentMethod;
  referenceNote: string | null;
  notes: string | null;
  status: DocumentStatus;
  allocations: PaymentAllocation[];
  createdAt: string;
  updatedAt: string | null;
}

export interface SupplierOpenBalance {
  targetDocType: PaymentTargetDocumentType;
  targetDocId: string;
  supplierId: string;
  supplierCode: string;
  supplierName: string;
  targetDocumentNo: string;
  targetDocumentDate: string;
  originalAmount: number;
  allocatedAmount: number;
  openAmount: number;
  currency: string | null;
  notes: string | null;
}

export interface PaymentAllocationFormValues {
  targetDocType: PaymentTargetDocumentType;
  targetDocId: string;
  targetLineId: string;
  allocatedAmount: number | "";
  allocationOrder: number;
}

export interface PaymentFormValues {
  paymentNo: string;
  partyType: PaymentPartyType;
  partyId: string;
  direction: PaymentDirection;
  amount: number | "";
  paymentDate: string;
  currency: string;
  exchangeRate: number | "";
  paymentMethod: PaymentMethod;
  referenceNote: string;
  notes: string;
  allocations: PaymentAllocationFormValues[];
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

function buildListUrl(filters: PaymentFilters) {
  const url = new URL("/api/payments", window.location.origin);

  if (filters.search.trim()) {
    url.searchParams.set("search", filters.search.trim());
  }

  if (filters.supplierId) {
    url.searchParams.set("supplierId", filters.supplierId);
  }

  if (filters.direction) {
    url.searchParams.set("direction", filters.direction);
  }

  if (filters.status) {
    url.searchParams.set("status", filters.status);
  }

  if (filters.paymentMethod) {
    url.searchParams.set("paymentMethod", filters.paymentMethod);
  }

  applyDateFilters(url, filters.fromDate, filters.toDate);
  return `${url.pathname}${url.search}`;
}

function normalizePayload(values: PaymentFormValues) {
  return {
    paymentNo: values.paymentNo.trim() || null,
    partyType: values.partyType,
    partyId: values.partyId,
    direction: values.direction,
    amount: values.amount === "" ? 0 : Number(values.amount),
    paymentDate: values.paymentDate ? new Date(values.paymentDate).toISOString() : null,
    currency: values.currency.trim() || null,
    exchangeRate: values.exchangeRate === "" ? null : Number(values.exchangeRate),
    paymentMethod: values.paymentMethod,
    referenceNote: values.referenceNote.trim() || null,
    notes: values.notes.trim() || null,
    allocations: values.allocations.map((allocation) => ({
      targetDocType: allocation.targetDocType,
      targetDocId: allocation.targetDocId,
      targetLineId: allocation.targetLineId || null,
      allocatedAmount: allocation.allocatedAmount === "" ? 0 : Number(allocation.allocatedAmount),
      allocationOrder: allocation.allocationOrder,
    })),
  };
}

export function listPayments(filters: PaymentFilters) {
  return requestJson<PaymentListItem[]>(buildListUrl(filters));
}

export function getPayment(id: string) {
  return requestJson<Payment>(`/api/payments/${id}`);
}

export function getPaymentAllocations(id: string) {
  return requestJson<PaymentAllocation[]>(`/api/payments/${id}/allocations`);
}

export function createPayment(values: PaymentFormValues) {
  return requestJson<Payment>("/api/payments", {
    method: "POST",
    body: JSON.stringify(normalizePayload(values)),
  });
}

export function updatePayment(id: string, values: PaymentFormValues) {
  return requestJson<Payment>(`/api/payments/${id}`, {
    method: "PUT",
    body: JSON.stringify(normalizePayload(values)),
  });
}

export function postPayment(id: string) {
  return requestJson<Payment>(`/api/payments/${id}/post`, {
    method: "POST",
  });
}

export function listSupplierOpenBalances(
  supplierId: string,
  direction: PaymentDirection,
  search = "",
  fromDate = "",
  toDate = "",
) {
  const url = new URL(`/api/suppliers/${supplierId}/open-balances`, window.location.origin);
  url.searchParams.set("direction", direction);

  if (search.trim()) {
    url.searchParams.set("search", search.trim());
  }

  applyDateFilters(url, fromDate, toDate);
  return requestJson<SupplierOpenBalance[]>(`${url.pathname}${url.search}`);
}
