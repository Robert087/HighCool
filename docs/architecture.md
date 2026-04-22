# Architecture — HighCool ERP

## Principles

* Modular monolith
* ASP.NET Core Web API backend
* EF Core persistence
* React + TypeScript frontend
* Server as source of truth
* Draft-only offline support

## Backend Shape

Business logic lives in services, not endpoint handlers.

Current aggregate direction:

* `Item` owns child `ItemComponent` rows
* `UomConversion` is global master data
* `PurchaseOrder` owns `PurchaseOrderLine`
* `PurchaseReceipt` owns `PurchaseReceiptLine`
* `PurchaseReceiptLine` owns actual `PurchaseReceiptLineComponent` rows

## Document Model

### Purchase Order

Document status:

* `Draft`
* `Posted`
* `Canceled`

Receipt progress is computed from posted receipts:

* `NotReceived`
* `PartiallyReceived`
* `FullyReceived`

The computed progress is separate from document status so the system can keep immutable posting semantics while still exposing open receipt progress.

### Purchase Receipt

Document status:

* `Draft`
* `Posted`
* `Canceled`

Current implementation uses:

* optional `purchase_order_id` on header
* optional `purchase_order_line_id` on line
* optional manual receipts without PO linkage

## Service Responsibilities

### PurchaseOrderService

* draft create, update, get, list
* validates supplier, line items, line UOMs
* computes receipt progress from posted receipts
* exposes available remaining PO lines for receipt creation

### PurchaseOrderPostingService

* validates draft PO
* marks PO as `Posted`

### PurchaseOrderCancellationService

* cancels posted PO
* blocks cancel when posted receipts already exist

### PurchaseReceiptService

* draft create, update, get, list
* validates PO linkage when supplied
* enforces `ordered_qty_snapshot` from PO line
* blocks linked receipt quantities beyond remaining posted PO quantity

### PurchaseReceiptPostingService

* validates draft receipt
* keeps posting idempotent
* writes stock ledger rows
* writes supplier statement rows from the current receipt financial basis
* runs shortage detection
* marks receipt as `Posted`

### StockLedgerService

* converts document quantity to base UOM
* writes append-only stock ledger entries
* carries traceability through source document and line references

### ShortageDetectionService

* loads expected components from item BOM
* computes expected component quantity from posted receipt quantity
* compares expected against actual component rows
* creates shortage ledger rows only for positive shortages

### ShortageResolutionService

* draft create, update, get, list
* validates supplier, resolution type, and allocation references
* exposes open shortage query data for the resolution UI
* supports FIFO allocation suggestion for shortage settlement

### ShortageResolutionPostingService

* validates draft resolution against current open shortage state
* keeps posting idempotent
* applies allocation rows to shortage ledger state
* writes stock ledger rows for physical resolutions
* writes supplier statement rows for financial resolutions
* marks the resolution as `Posted`

### SupplierStatementPostingService

* writes append-only supplier statement rows from posted purchase receipts
* writes append-only supplier statement rows from posted financial shortage resolutions
* blocks duplicate supplier statement effects for the same posted source document

### SupplierStatementQueryService

* lists supplier statement rows with supplier, effect, source document, and date filters
* resolves source document numbers for receipt and shortage resolution traceability

### SupplierBalanceService

* returns supplier statement summaries with opening, closing, and current balances
* keeps summary calculation aligned with append-only statement rows

### ShortageResolutionValidationService

* enforces quantity correctness against current open shortage quantity
* requires valuation rate only for financial settlement
* keeps physical vs financial allocation rules explicit before posting

### ShortageResolutionAllocationService

* applies physical or financial settlements row-by-row
* updates shortage resolved and open balances
* preserves per-allocation traceability into stock and supplier statement ledgers

## Persistence Design

Key tables for this slice:

* `purchase_orders`
* `purchase_order_lines`
* `purchase_receipts`
* `purchase_receipt_lines`
* `purchase_receipt_line_components`
* `stock_ledger_entries`
* `shortage_ledger_entries`
* `shortage_resolutions`
* `shortage_resolution_allocations`
* `supplier_statement_entries`

Important constraints:

* stock and supplier statement ledgers are append-only
* shortage resolution allocations are append-only after posting because posted resolutions are immutable
* posted document data is never mutated into a different business effect
* PO-to-receipt traceability is stored directly in receipt header and line rows
* shortage rows keep lifecycle state while every settlement remains traceable through allocation rows
