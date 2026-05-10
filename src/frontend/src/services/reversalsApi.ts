import { requestJson } from "./api";

export interface DocumentReversal {
  id: string;
  reversalNo: string;
  reversedDocumentType: string;
  reversedDocumentId: string;
  reversalDate: string;
  reversalReason: string;
  createdAt: string;
  createdBy: string;
}

interface ReverseDocumentPayload {
  reversalDate: string;
  reversalReason: string;
}

function buildPayload(reason: string): ReverseDocumentPayload {
  return {
    reversalDate: new Date().toISOString(),
    reversalReason: reason.trim(),
  };
}

export function reversePurchaseReceipt(id: string, reason: string) {
  return requestJson<DocumentReversal>(`/api/purchase-receipts/${id}/reverse`, {
    method: "POST",
    body: JSON.stringify(buildPayload(reason)),
  });
}

export function reversePayment(id: string, reason: string) {
  return requestJson<DocumentReversal>(`/api/payments/${id}/reverse`, {
    method: "POST",
    body: JSON.stringify(buildPayload(reason)),
  });
}

export function reverseShortageResolution(id: string, reason: string) {
  return requestJson<DocumentReversal>(`/api/shortage-resolutions/${id}/reverse`, {
    method: "POST",
    body: JSON.stringify(buildPayload(reason)),
  });
}
