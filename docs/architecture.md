# Architecture — HighCool ERP

## Principles

* Modular monolith
* ASP.NET Core Web API backend
* EF Core persistence
* React + TypeScript frontend
* Server as source of truth
* Draft-only offline support
* Arabic/English bilingual UI with shared i18n and RTL/LTR support

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
* calculates PO-linked supplier payable amount from linked PO line pricing
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

## Performance Standards

Performance is a mandatory architecture concern for every ERP module.

Required rules:

* all list and report endpoints must use server-side pagination
* list endpoints must support server-side filtering and deterministic sorting
* no unbounded operational list API is allowed
* read-heavy screens must use dedicated query services and lightweight DTO projections
* detail endpoints may load document graphs, but list endpoints must not load full aggregates by default
* read-only queries should use `AsNoTracking`
* summary cards and header metrics must use summary queries instead of full list downloads
* schema work must define indexes for foreign keys, high-frequency filters, and traceability joins during design
* frontend grids must request only the current page and must not paginate large datasets in the browser

## Frontend Form Performance Standards

Form screens are operational entry points and must be optimized for fast first paint and fast repeat-open behavior.

Required rules:

* shared active reference datasets such as suppliers, customers, items, warehouses, UOMs, and global UOM conversions must load through shared cached option loaders rather than refetching identical lists on every form open
* cached option loaders must support mutation invalidation so create, update, activate, and deactivate actions do not leave forms with stale selectors
* form initialization must load only the minimum reference data required for initial interaction
* forms must not eagerly hydrate full document details for every selectable record in a dropdown
* reference document detail must be fetched on demand when the user selects a record or when edit mode requires one specific linked document
* independent form queries should run in parallel, but each query must stay bounded and aligned to the first-screen need
* form selectors must prefer lightweight list DTOs for candidate records and reserve detail DTOs for the active record being edited or linked

## Localization Standards

Frontend architecture must treat localization as platform infrastructure rather than page-level customization.

Required rules:

* the root app shell controls locale and document direction
* shared components must be safe for both Arabic/RTL and English/LTR
* module pages must use shared translation dictionaries and shared locale-aware formatters
* business values remain server-sourced, but display formatting is client-localized
* form internals, dialogs, drawers, grid toolbars, and navigation are part of the localization contract and must not ship with visible hardcoded strings
* list filters must be standardized, localized, RTL-safe, and bound to real server-side query parameters where applicable
