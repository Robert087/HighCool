import { describe, expect, it } from "vitest";
import type { SupplierStatementEntry, SupplierStatementSummary } from "./supplierStatementsApi";
import {
  buildSupplierStatementSummaryViewModel,
  formatEffectType,
  formatSourceType,
  groupSupplierStatementEntries,
  interpretSupplierBalance,
} from "./supplierStatementPresentation";

function createEntry(overrides: Partial<SupplierStatementEntry>): SupplierStatementEntry {
  return {
    id: crypto.randomUUID(),
    supplierId: "supplier-1",
    supplierCode: "SUP-001",
    supplierName: "Delta Cooling Supplies",
    entryDate: "2026-04-22T00:00:00.000Z",
    sourceDocType: "ShortageResolution",
    sourceDocId: "resolution-1",
    sourceLineId: crypto.randomUUID(),
    sourceSequenceNo: 1,
    sourceDocumentNo: "SR-0001",
    effectType: "ShortageFinancialResolution",
    debit: 100,
    credit: 0,
    runningBalance: -100,
    currency: "EGP",
    notes: "Allocation 1",
    createdAt: "2026-04-22T10:00:00.000Z",
    createdBy: "system",
    ...overrides,
  };
}

describe("interpretSupplierBalance", () => {
  it("labels positive balances as payable", () => {
    expect(interpretSupplierBalance(1100)).toEqual({
      absoluteValue: 1100,
      explanation: "You owe supplier",
      type: "Payable",
    });
  });

  it("labels negative balances as receivable", () => {
    expect(interpretSupplierBalance(-1100)).toEqual({
      absoluteValue: 1100,
      explanation: "Supplier owes you",
      type: "Receivable",
    });
  });
});

describe("groupSupplierStatementEntries", () => {
  it("groups multiple allocations from the same source document into one visible row", () => {
    const rows = groupSupplierStatementEntries([
      createEntry({
        id: "row-2",
        sourceLineId: "alloc-2",
        sourceSequenceNo: 2,
        runningBalance: -1100,
        notes: "Allocation 2",
      }),
      createEntry({
        id: "row-1",
        sourceLineId: "alloc-1",
        sourceSequenceNo: 1,
        runningBalance: -1000,
        notes: "Allocation 1",
      }),
    ]);

    expect(rows).toHaveLength(1);
    expect(rows[0].debit).toBe(200);
    expect(rows[0].credit).toBe(0);
    expect(rows[0].runningBalance).toBe(-1100);
    expect(rows[0].balanceMeaning.type).toBe("Receivable");
    expect(rows[0].details).toHaveLength(2);
    expect(rows[0].details.map((entry) => entry.sourceSequenceNo)).toEqual([1, 2]);
  });

  it("keeps separate documents as separate grouped rows", () => {
    const rows = groupSupplierStatementEntries([
      createEntry({ sourceDocId: "resolution-1", sourceDocumentNo: "SR-0001" }),
      createEntry({
        id: "row-3",
        sourceDocId: "receipt-1",
        sourceDocType: "PurchaseReceipt",
        sourceDocumentNo: "PR-0001",
        effectType: "PurchaseReceipt",
        sourceLineId: "receipt-line",
        sourceSequenceNo: null,
        debit: 0,
        credit: 300,
        runningBalance: 300,
        notes: "Receipt posting",
      }),
    ]);

    expect(rows).toHaveLength(2);
    expect(rows.map((row) => row.sourceDocumentNo)).toEqual(["SR-0001", "PR-0001"]);
  });
});

describe("statement label formatting", () => {
  it("formats purchase return and reversal types without falling back to payment labels", () => {
    expect(formatEffectType("PurchaseReturn")).toBe("Purchase return");
    expect(formatEffectType("PaymentReversal")).toBe("Payment reversal");
    expect(formatSourceType("PurchaseReturn")).toBe("Purchase return");
    expect(formatSourceType("PurchaseReceiptReversal")).toBe("Purchase receipt reversal");
  });
});

describe("buildSupplierStatementSummaryViewModel", () => {
  it("produces payable or receivable meaning from the signed summary balance", () => {
    const summary: SupplierStatementSummary = {
      supplierId: "supplier-1",
      supplierCode: "SUP-001",
      supplierName: "Delta Cooling Supplies",
      fromDate: "2026-04-20T00:00:00.000Z",
      toDate: "2026-04-22T00:00:00.000Z",
      currentBalance: -1100,
      openingBalance: 0,
      closingBalance: -1100,
      totalDebit: 1100,
      totalCredit: 0,
      entryCount: 2,
    };

    const viewModel = buildSupplierStatementSummaryViewModel(summary, "SUP-001 - Delta Cooling Supplies");

    expect(viewModel.currentBalanceText).toBe("1,100");
    expect(viewModel.currentBalanceTypeText).toBe("Receivable (Supplier owes you)");
    expect(viewModel.totalDebit).toBe(1100);
    expect(viewModel.totalCredit).toBe(0);
  });
});
