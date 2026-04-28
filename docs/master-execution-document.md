# Master Execution Document — HighCool ERP

## Execution Principles

1. Build features end-to-end
2. Keep posting logic server-side
3. Preserve traceability and auditability
4. Keep code, schema, APIs, frontend, and docs aligned
5. Treat bilingual Arabic/English support and RTL/LTR support as mandatory platform behavior

## Mandatory Procurement Decisions

These decisions are mandatory for ongoing purchasing work:

### Components

* components are child rows of items only
* expected component requirement comes from item BOM definitions
* purchase receipt component rows capture actual delivered component quantities only

### UOM Conversions

* UOM conversions are global
* no item-specific conversion design is allowed

### Purchase Order and Purchase Receipt

* purchase order is the source of ordered quantities
* purchase receipt is the source of actual delivered quantities
* PO-linked receipt lines must store PO traceability and ordered quantity snapshot
* over-receipt is blocked in the current implementation
* until receipt line pricing exists, purchase receipt supplier payable value is captured explicitly on the receipt header and drives supplier statement and payment allocation
* `po remaining receivable qty` is derived from posted, non-reversed receipts only
* `receipt remaining returnable qty` is derived from posted, non-reversed receipts minus posted, non-reversed returns
* fully received PO rows and fully returned receipt rows must disappear from actionable candidate lists

### Stock and Shortage

* stock is derived from stock ledger entries only
* purchase receipt posting writes stock ledger rows
* purchase receipt posting writes supplier statement rows from the current receipt financial basis
* shortage expectation is expanded from BOM against receipt quantity
* shortage ledger rows are created only for positive shortages
* shortage resolution posting is the only supported way to close shortage rows
* physical shortage resolution writes stock ledger rows
* financial shortage resolution writes supplier statement rows
* physical shortage resolution does not write supplier statement rows
* supplier payment allocation is mandatory
* supplier payments settle open procurement balances only through posted allocation rows
* supplier payment posting writes supplier statement rows
* shortage closure is allocation-driven and must support partial and multi-row settlement
* one shortage row may be resolved repeatedly over time through physical and/or financial settlement
* shortage closes only when the full shortage quantity has been covered
* shortage reasons are informational and must not block resolution posting
* `shortage open qty` and `shortage open amount` are derived from posted resolution allocations and must stay correct across repeated mixed physical and financial settlement
* fully resolved shortage rows must disappear from active resolution lists

### Supplier Financial Targets

* supplier financial target open amount is derived from posted source documents, posted active returns, and posted active payment allocations
* purchase return amounts reduce receipt payment targets before payment allocation eligibility is calculated
* fully settled supplier targets must disappear from actionable payment allocation lists
* duplicate logical payment targets inside one draft payment are blocked

## Delivery Rule

Any future procurement work must continue from this model and must not reintroduce:

* receipt-managed ordered quantities disconnected from PO context
* free-form shortage expectations disconnected from item BOM
* direct stock updates outside ledger flows
* direct shortage closure or supplier balance edits outside posted resolution documents

## Performance Addendum

Mandatory performance rules for current and future ERP modules:

* all procurement, inventory, shortage, payment, return, statement, and reversal lists must be paginated server-side
* list endpoints must expose `page`, `pageSize`, filters, and sort inputs with a consistent response contract
* actionable and reporting grids must read from query services, not from write-oriented aggregate loading
* query services must prefer explicit DTO projection over large `Include` graphs in list flows
* dashboards and header summaries must use dedicated summary queries
* database indexing must be reviewed whenever new document tables, ledger tables, or allocation tables are introduced
* no browser screen may load full operational datasets by default

## Localization Addendum

Mandatory localization rules for current and future modules:

* new modules must not introduce hardcoded UI strings
* new grids, forms, dialogs, drawers, and navigation flows must be verified in both Arabic/RTL and English/LTR
* all financial and operational values must use shared locale-aware formatting helpers
* mixed Arabic labels with document codes and numeric values must remain readable and unambiguous
* form completion includes internal labels, placeholders, helper text, validation messages, selector prompts, and inline workflow messages
* filter completion includes localized controls, applied-filter feedback, working reset/clear actions, and real server-side-compatible binding

## Reversal Addendum

Mandatory correction rules for the current procurement scope:

* posted purchase receipts, supplier payments, and shortage resolutions must be corrected through reversal actions only
* posted documents must not be corrected through edit or delete
* purchase returns are first-class procurement documents and are the controlled stock-and-financial return path for received items
* reversal must create opposite effects while preserving the original document and an auditable reversal record
* reversal must be blocked when active posted downstream documents would make the reversal inconsistent
