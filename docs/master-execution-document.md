# Master Execution Document — HighCool ERP

## Execution Principles

1. Build features end-to-end
2. Keep posting logic server-side
3. Preserve traceability and auditability
4. Keep code, schema, APIs, frontend, and docs aligned

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

### Stock and Shortage

* stock is derived from stock ledger entries only
* purchase receipt posting writes stock ledger rows
* shortage expectation is expanded from BOM against receipt quantity
* shortage ledger rows are created only for positive shortages
* shortage resolution posting is the only supported way to close shortage rows
* physical shortage resolution writes stock ledger rows
* financial shortage resolution writes supplier statement rows
* shortage closure is allocation-driven and must support partial and multi-row settlement
* one shortage row may be resolved repeatedly over time through physical and/or financial settlement
* shortage closes only when the full shortage quantity has been covered
* shortage reasons are informational and must not block resolution posting

## Delivery Rule

Any future procurement work must continue from this model and must not reintroduce:

* receipt-managed ordered quantities disconnected from PO context
* free-form shortage expectations disconnected from item BOM
* direct stock updates outside ledger flows
* direct shortage closure or supplier balance edits outside posted resolution documents
