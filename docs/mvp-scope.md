# MVP Scope

## Active Scope

Current MVP scope includes:

* suppliers
* warehouses
* UOMs
* global UOM conversions
* items with embedded BOM component rows
* purchase orders
* purchase receipts with optional PO linkage
* stock ledger entries from receipt posting
* shortage ledger entries from receipt posting
* inventory monitoring with item reorder settings and ledger-derived stock health

## Out of Scope For This Slice

Not implemented in this slice:

* supplier statement posting
* PO financial effects
* receipt cancellation and stock reversal documents
* full shortage resolution workflow
* offline posting

## Procurement Scope Boundary

This slice is approved only if:

* PO is the source of ordered quantities
* receipt captures actual delivered quantities
* shortage expectation comes from the item BOM
* stock changes happen through stock ledger entries only
