# Business Document — HighCool ERP

## System Goal

Build a production-ready ERP that preserves:

* correct stock
* traceable supplier and customer balances
* auditable document posting
* server-side correctness ahead of convenience

## Core Master Data

Current master data includes:

* Suppliers
* Warehouses
* Units of measure
* Global UOM conversions
* Items with embedded component rows
* Shortage reason codes

## Item and UOM Rules

Mandatory decisions:

* Components are modeled only as child rows of `Item`
* Components are never a standalone module
* UOM conversions are global and are never item-specific
* Stock calculations and shortage expectations rely on the item BOM plus global conversions

## Procurement Document Rules

### Purchase Order

The purchase order is the source of expected supplier quantities.

Approved rules:

* PO defines expected items and ordered quantities
* PO has no stock impact
* PO has no financial impact
* PO has no statement impact
* only `Draft` POs can be edited
* only `Draft` POs can be posted
* posted POs are immutable except through cancel flow

### Purchase Receipt

The purchase receipt captures actual delivered items and actual delivered components.

Approved rules:

* receipts may be manual, but PO-linked receipt flow is first-class
* when linked to a PO, receipt supplier must match the PO supplier
* PO-linked receipt lines store:
  * `purchase_order_line_id`
  * `ordered_qty_snapshot`
* `ordered_qty_snapshot` comes from the linked PO line
* partial receipts are allowed
* multiple receipts against one PO are allowed
* over-receipt is not allowed in the current implementation
* only `Draft` receipts can be edited or posted
* posted receipts are immutable
* posting is idempotent

## Shortage Rules

Shortage expectation comes from the item BOM, not from free-form receipt rows.

Approved rules:

* expected item quantity comes from the receipt line and PO context when linked
* expected component quantity = `received_qty x item BOM quantity`
* actual receipt component rows do not define expectation
* if `actual == expected`, no shortage is created
* if `actual < expected`, a shortage ledger row is created
* if `actual > expected`, no shortage row is created
* shortage reason is optional when a positive shortage exists
* shortage reason is informational and may still drive approval or reporting rules, but it does not control shortage resolution eligibility

### Shortage Resolution Lifecycle

Shortages remain open until they are settled through a posted shortage resolution document.

Approved rules:

* shortage resolution supports `Physical` and `Financial` settlement types
* one shortage resolution may settle multiple shortage rows
* one shortage row may be settled across multiple shortage resolutions over time
* allocation rows are mandatory for traceability
* physical resolution adds stock and reduces open shortage quantity
* financial resolution uses resolved quantity plus valuation rate, calculates amount automatically, creates supplier statement impact, and reduces open shortage quantity by the resolved quantity
* shortage rows close only when the open shortage quantity reaches zero
* if a valuation basis exists, shortage open amount is reduced proportionally as physical or financial settlement is posted
* any open shortage row may be settled physically or financially
* no direct shortage closure is allowed outside posted shortage resolution flows

## Inventory Rules

Inventory remains append-only and document-driven.

Mandatory rules:

* stock is derived from stock ledger entries only
* no direct stock edits are allowed
* purchase receipt posting creates stock ledger `IN` entries
* shortage physical resolution posting creates stock ledger `IN` entries
* posted document effects are never overwritten
* reversals must be done through reversing documents or reversing ledger logic

## Supplier Statement Rules

Supplier statement movement is also document-driven.

Mandatory rules:

* supplier statement entries are derived from posted business documents only
* no manual supplier statement entries are allowed
* purchase receipt posting creates supplier statement rows
* shortage financial resolution posting creates supplier statement entries
* physical shortage resolution does not create supplier statement entries
* supplier statement rows remain traceable to shortage resolution allocations
* purchase receipt statement amount follows the current receipt financial basis available in the system
* in the current implementation, posted purchase receipts create traceability rows but receipt payable amount remains `0` until receipt pricing or valuation is implemented explicitly

## UI Expectations

Procurement UX must support:

* purchase order list and form workflows
* purchase receipt list and form workflows
* create receipt from posted PO
* clear status display
* responsive, structured ERP-style forms
* line grids with traceable document context
