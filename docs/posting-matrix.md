# Posting Matrix

## Purchase Order

### Draft Save

Actions:

* `POST /api/purchase-orders`
* `PUT /api/purchase-orders/{id}`

Effects:

* persists PO header and lines
* line pricing is captured as `unit_price`
* no stock effect
* no shortage effect
* no financial statement effect
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
* calculates PO-linked `supplier_payable_amount` from received quantities and linked PO line unit prices
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
* duplicate component rows inside one receipt line are rejected

Posting effects:

* status changes from `Draft` to `Posted`
* one stock ledger `IN` row is written per receipt line
* one supplier statement row is written per posted receipt header only when `supplier_payable_amount > 0`
* expected component quantities are expanded from the item BOM using `received_qty`
* actual components are compared against expected quantities
* shortage ledger rows are written only for positive shortages

Receipt financial basis:

* receipt statement amount uses `supplier_payable_amount` from the posted receipt header
* for PO-linked receipts, `supplier_payable_amount` is server-calculated from `received_qty x purchase_order_line.unit_price`
* for manual receipts, `supplier_payable_amount` remains the explicit procurement financial basis until manual line pricing is implemented
* if `supplier_payable_amount <= 0`, posting is still allowed but no financial supplier statement row is written

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
* the same shortage row cannot appear more than once inside the same resolution
* any shortage row with open quantity may be posted physically or financially
* fully resolved shortage rows are excluded from active allocation candidates

Posting effects:

* status changes from `Draft` to `Posted`
* physical resolution writes one stock ledger `IN` row per allocation using transaction type `ShortagePhysicalResolution`
* financial resolution writes one supplier statement row per allocation using source type `ShortageFinancialResolution` and effect type `ShortageFinancialResolution`
* shortage ledger updates `resolved_physical_qty`, `resolved_financial_qty_equivalent`, and `open_qty` per allocation
* `final_physical_component_qty` is `initial actual qty + resolved_physical_qty`
* financial resolution closes shortage exposure without increasing physical component quantity
* shortage status changes from `Open` to `PartiallyResolved` to `Resolved` based on remaining open quantity
* the same shortage row may be settled many times across multiple posted resolutions
* mixed physical plus financial settlement is supported until the shortage quantity is fully covered

Idempotency:

* reposting an already posted resolution returns the current posted document
* duplicate stock and supplier statement rows are guarded by source document plus allocation indexing

## Supplier Payment

### Draft Save

Actions:

* `POST /api/payments`
* `PUT /api/payments/{id}`

Effects:

* persists payment header and allocation rows
* no stock ledger effect
* no supplier statement effect
* status remains `Draft`

### Post

Action:

* `POST /api/payments/{id}/post`

Preconditions:

* payment exists
* payment status is `Draft`
* supplier exists and is active
* payment amount is greater than zero
* at least one allocation exists
* allocated total does not exceed payment amount
* allocated total must equal payment amount before posting
* each allocation points to an open target owned by the same supplier
* allocation amount does not exceed current open amount
* the same target document cannot appear more than once inside one payment
* `direction = OutboundToParty` may allocate only to `PurchaseReceipt`
* `direction = InboundFromParty` may allocate only to financial `ShortageResolution`
* purchase receipt target open amount is `supplier_payable_amount - active posted purchase returns - active posted payment allocations`
* fully settled targets are excluded from active allocation candidates

Posting effects:

* status changes from `Draft` to `Posted`
* no stock ledger effect
* supplier statement rows are written using source type `Payment` and effect type `Payment`
* outbound supplier payment writes debit rows and reduces supplier payable balance
* inbound supplier payment writes credit rows and reduces supplier receivable balance created by financial shortage resolutions
* open supplier target amount is reduced only through posted payment allocations

Idempotency:

* reposting an already posted payment returns the current posted document
* duplicate supplier statement rows are guarded by payment source document plus allocation indexing

## Purchase Return

### Post

Action:

* `POST /api/purchase-returns/{id}/post`

Preconditions:

* linked return rows must point to posted, non-reversed receipt lines
* `return_qty` must not exceed remaining returnable quantity
* duplicate logical rows are blocked inside one return
* `remaining returnable quantity` is validated in the referenced receipt line UOM and must remain correct after prior posted returns

Posting effects:

* status changes from `Draft` to `Posted`
* stock ledger `OUT` rows are written
* supplier statement rows are written using source type `PurchaseReturn` and effect type `PurchaseReturn` only when a valid referenced receipt financial basis produces a positive return amount
* if no valid supplier financial basis is available, posting still succeeds but no zero-value supplier statement row is written
* receipt traceability is preserved when provided

## Reversal Actions

### Purchase Receipt Reverse

Action:

* `POST /api/purchase-receipts/{id}/reverse`

Effects:

* creates a reversal audit record
* writes stock ledger `OUT` rows that reverse the original receipt stock `IN`
* writes supplier statement reversal rows using source type `PurchaseReceiptReversal` and effect type `PurchaseReceiptReversal`
* cancels unresolved shortage rows created by the receipt
* blocked when active purchase returns, payment allocations, or shortage resolutions already depend on the receipt

### Supplier Payment Reverse

Action:

* `POST /api/payments/{id}/reverse`

Effects:

* creates a reversal audit record
* writes opposite supplier statement rows per allocation using source type `PaymentReversal` and effect type `PaymentReversal`
* restores supplier open balances
* duplicate reversal is blocked

### Shortage Resolution Reverse

Action:

* `POST /api/shortage-resolutions/{id}/reverse`

Effects:

* creates a reversal audit record
* physical allocations write stock ledger `OUT` rows
* financial allocations write opposite supplier statement rows using source type `ShortageResolutionReversal` and effect type `ShortageResolutionReversal`
* shortage open quantity and status are restored
