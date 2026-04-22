import type { SupplierStatementEntry, SupplierStatementSummary } from "./supplierStatementsApi";

export type SupplierBalanceType = "Payable" | "Receivable" | "Settled";

export interface SupplierBalanceMeaning {
  absoluteValue: number;
  explanation: string;
  type: SupplierBalanceType;
}

export interface GroupedSupplierStatementRow {
  id: string;
  entryDate: string;
  sourceDocId: string;
  sourceDocType: SupplierStatementEntry["sourceDocType"];
  sourceDocumentNo: string;
  effectTypeLabel: string;
  debit: number;
  credit: number;
  runningBalance: number;
  balanceMeaning: SupplierBalanceMeaning;
  notes: string;
  details: SupplierStatementEntry[];
}

export interface SupplierStatementSummaryViewModel {
  balanceMeaning: SupplierBalanceMeaning;
  currentBalanceText: string;
  currentBalanceTypeText: string;
  dateRangeText: string;
  supplierText: string;
  totalCredit: number;
  totalDebit: number;
}

export function interpretSupplierBalance(balance: number): SupplierBalanceMeaning {
  const absoluteValue = Math.abs(round(balance));

  if (absoluteValue === 0) {
    return {
      absoluteValue: 0,
      explanation: "No open supplier balance.",
      type: "Settled",
    };
  }

  if (balance > 0) {
    return {
      absoluteValue,
      explanation: "You owe supplier",
      type: "Payable",
    };
  }

  return {
    absoluteValue,
    explanation: "Supplier owes you",
    type: "Receivable",
  };
}

export function groupSupplierStatementEntries(entries: SupplierStatementEntry[]): GroupedSupplierStatementRow[] {
  const groups = new Map<string, GroupedSupplierStatementRow>();
  const order: string[] = [];

  for (const entry of entries) {
    const key = `${entry.sourceDocType}:${entry.sourceDocId}`;
    const existing = groups.get(key);
    if (!existing) {
      groups.set(key, {
        id: key,
        entryDate: entry.entryDate,
        sourceDocId: entry.sourceDocId,
        sourceDocType: entry.sourceDocType,
        sourceDocumentNo: entry.sourceDocumentNo,
        effectTypeLabel: formatEffectType(entry.effectType),
        debit: round(entry.debit),
        credit: round(entry.credit),
        runningBalance: round(entry.runningBalance),
        balanceMeaning: interpretSupplierBalance(entry.runningBalance),
        notes: entry.notes?.trim() || "",
        details: [entry],
      });
      order.push(key);
      continue;
    }

    existing.debit = round(existing.debit + entry.debit);
    existing.credit = round(existing.credit + entry.credit);
    existing.details.push(entry);
    existing.notes = mergeNotes(existing.notes, entry.notes);
  }

  return order.map((key) => {
    const group = groups.get(key)!;
    const sortedDetails = [...group.details].sort(compareStatementEntrySequence);
    const closingEntry = sortedDetails[sortedDetails.length - 1];
    const closingBalance = round(closingEntry.runningBalance);

    return {
      ...group,
      entryDate: sortedDetails[0]?.entryDate ?? group.entryDate,
      runningBalance: closingBalance,
      balanceMeaning: interpretSupplierBalance(closingBalance),
      details: sortedDetails,
    };
  });
}

export function buildSupplierStatementSummaryViewModel(
  summary: SupplierStatementSummary,
  selectedSupplierLabel: string,
): SupplierStatementSummaryViewModel {
  const balanceMeaning = interpretSupplierBalance(summary.currentBalance);

  return {
    balanceMeaning,
    currentBalanceText: balanceMeaning.absoluteValue.toLocaleString(),
    currentBalanceTypeText: `${balanceMeaning.type} (${balanceMeaning.explanation})`,
    dateRangeText: `${summary.fromDate ? new Date(summary.fromDate).toLocaleDateString() : "Any start date"} to ${summary.toDate ? new Date(summary.toDate).toLocaleDateString() : "Any end date"}`,
    supplierText: selectedSupplierLabel,
    totalCredit: summary.totalCredit,
    totalDebit: summary.totalDebit,
  };
}

export function formatEffectType(value: SupplierStatementEntry["effectType"]) {
  if (value === "PurchaseReceipt") {
    return "Purchase receipt";
  }

  if (value === "ShortageFinancialResolution") {
    return "Shortage financial resolution";
  }

  return "Payment";
}

export function formatSourceType(value: SupplierStatementEntry["sourceDocType"]) {
  if (value === "PurchaseReceipt") {
    return "Purchase receipt";
  }

  if (value === "ShortageResolution") {
    return "Shortage resolution";
  }

  return "Payment";
}

function compareStatementEntrySequence(left: SupplierStatementEntry, right: SupplierStatementEntry) {
  const leftSequence = left.sourceSequenceNo ?? Number.MAX_SAFE_INTEGER;
  const rightSequence = right.sourceSequenceNo ?? Number.MAX_SAFE_INTEGER;

  if (leftSequence !== rightSequence) {
    return leftSequence - rightSequence;
  }

  const leftCreatedAt = new Date(left.createdAt).getTime();
  const rightCreatedAt = new Date(right.createdAt).getTime();
  if (leftCreatedAt !== rightCreatedAt) {
    return leftCreatedAt - rightCreatedAt;
  }

  return left.id.localeCompare(right.id);
}

function mergeNotes(current: string, incoming: string | null) {
  const next = incoming?.trim() || "";
  if (!next) {
    return current;
  }

  if (!current) {
    return next;
  }

  return current === next ? current : `${current}; ${next}`;
}

function round(value: number) {
  return Math.round(value * 1_000_000) / 1_000_000;
}
