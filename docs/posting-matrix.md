# Posting Matrix

## Purchase Order

### Draft Save

Actions:

* `POST /api/purchase-orders`
* `PUT /api/purchase-orders/{id}`

Effects:

* persists PO header and lines
* no stock effect
* no shortage effect
* no financial effect
* status remains `Draft`

### Post

Action:

* `POST /api/purchase-orders/{id}/post`

Effects:

* validates draft PO
* status changes to `Posted`
* PO becomes immutable except through cancel
* no stock ledger effect
* no shortage ledger effect

### Cancel

Action:

* `POST /api/purchase-orders/{id}/cancel`

Effects:

* status changes to `Canceled`
* blocked when posted receipts already exist for the PO
* no stock ledger effect

## Purchase Receipt

### Draft Save

Actions:

* `POST /api/purchase-receipts`
* `PUT /api/purchase-receipts/{id}`

Effects:

* persists receipt header, lines, and auto-filled component rows
* stores PO linkage and ordered snapshot when linked
* recalculates `expected_qty` for each component row from `received_qty x item BOM quantity`
* defaults `actual_received_qty` to `expected_qty` when a component row has not been edited yet
* no stock ledger effect
* no shortage ledger effect
* status remains `Draft`

### Post

Action:

* `POST /api/purchase-receipts/{id}/post`

Preconditions:

* receipt exists
* receipt status is `Draft`
* supplier exists and is active
* warehouse exists and is active
* at least one line exists
* all item and UOM references resolve
* required global UOM conversions resolve
* linked PO, when supplied, is `Posted`
* linked PO supplier matches receipt supplier
* linked PO receipt quantities do not exceed remaining posted PO quantity
* actual component rows match the BOM component set for BOM items
* shortage rows are allowed with or without a shortage reason
* shortage rows are based on persisted `expected_qty` vs `actual_received_qty` on the receipt line component rows

Posting effects:

* status changes from `Draft` to `Posted`
* one stock ledger `IN` row is written per receipt line
* expected component quantities are expanded from the item BOM using `received_qty`
* actual components are compared against expected quantities
* shortage ledger rows are written only for positive shortages

Idempotency:

* reposting an already posted receipt returns the current posted document
* duplicate stock rows are guarded by receipt status plus unique stock ledger indexing

## Shortage Resolution

### Draft Save

Actions:

* `POST /api/shortage-resolutions`
* `PUT /api/shortage-resolutions/{id}`

Effects:

* persists shortage resolution header and allocation rows
* no stock effect
* no supplier statement effect
* no shortage state change
* status remains `Draft`

### Post

Action:

* `POST /api/shortage-resolutions/{id}/post`

Preconditions:

* resolution exists
* resolution status is `Draft`
* supplier exists and is active
* resolution type is `Physical` or `Financial`
* at least one allocation exists
* all allocation rows point to shortage rows owned by the same supplier
* no allocation exceeds current open shortage quantity
* physical allocations require `allocated_qty` only
* financial allocations require `allocated_qty` plus `valuation_rate`
* financial `allocated_amount = allocated_qty x valuation_rate`
* financial `financial_qty_equivalent = allocated_qty`
* any shortage row with open quantity may be posted physically or financially

Posting effects:

* status changes from `Draft` to `Posted`
* physical resolution writes one stock ledger `IN` row per allocation using transaction type `ShortagePhysicalResolution`
* financial resolution writes one supplier statement row per allocation using effect type `ShortageFinancialResolution`
* shortage ledger updates `resolved_physical_qty`, `resolved_financial_qty_equivalent`, and `open_qty` per allocation
* shortage status changes from `Open` to `PartiallyResolved` to `Resolved` based on remaining open quantity
* the same shortage row may be settled many times across multiple posted resolutions
* mixed physical plus financial settlement is supported until the shortage quantity is fully covered

Idempotency:

* reposting an already posted resolution returns the current posted document
* duplicate stock and supplier statement rows are guarded by source document plus allocation indexing
