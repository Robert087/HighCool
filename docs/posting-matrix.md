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

## Inventory Transfer

### Draft Save

Actions:

* `POST /api/inventory-transfers`
* `PUT /api/inventory-transfers/{id}`

Effects:

* persists transfer header and lines
* transfer number is generated server-side using the `TRF-` document sequence
* line `base_qty` is calculated server-side from the line UOM to the item base UOM
* no stock ledger effect
* status remains `Draft`

### Post

Action:

* `POST /api/inventory-transfers/{id}/post`

Preconditions:

* transfer exists
* transfer status is `Draft`
* source and destination warehouses are active and different
* at least one line exists
* all item and UOM references resolve
* required global UOM conversions resolve
* source warehouse stock is sufficient when negative stock is disabled
* duplicate item/source warehouse lines are aggregated in base UOM before stock availability validation

Posting effects:

* status changes from `Draft` to `Posted`
* each line writes one source warehouse `OUT` stock ledger row with transaction type `InventoryTransferOut`
* each line writes one destination warehouse `IN` stock ledger row with transaction type `InventoryTransferIn`
* no financial statement effect

Idempotency:

* reposting an already posted transfer returns the current posted document
* duplicate transfer stock rows are guarded by document status, concurrency, and unique ledger operation keys

### Cancel

Action:

* `POST /api/inventory-transfers/{id}/cancel`

Preconditions:

* transfer exists
* transfer status is `Posted`
* source and destination warehouses are active and different
* destination warehouse stock is sufficient when negative stock is disabled
* duplicate item/destination warehouse lines are aggregated in base UOM before stock availability validation

Effects:

* status changes from `Posted` to `Canceled`
* each line writes one source warehouse `IN` reversal row with transaction type `InventoryTransferCancellationIn`
* each line writes one destination warehouse `OUT` reversal row with transaction type `InventoryTransferCancellationOut`
* original posted transfer ledger rows are never edited or deleted
* duplicate cancellation is idempotent and returns the current canceled document

## Inventory Monitoring

### Reorder Settings Save

Actions:

* `PUT /api/inventory/items/{id}/reorder-settings`

Effects:

* persists only item-level reorder settings
* no stock ledger effect
* no financial statement effect
* no shortage ledger effect
* no purchase order, reservation, approval, or notification effect

### Dashboard and List

Actions:

* `GET /api/inventory/monitor/dashboard`
* `GET /api/inventory/monitor/items`
* `GET /api/inventory/monitor/filter-options`
* `GET /api/inventory/items/{id}/reorder-settings`

Effects:

* reads current stock from append-only stock ledger entries
* calculates health status and suggested reorder quantity dynamically
* does not persist stock balances, stock health status, suggested reorder quantity, or dashboard totals

## Reversal Actions

## Inventory Count

### Draft Save

Actions:

* `POST /api/inventory-counts`
* `PUT /api/inventory-counts/{id}`
* `POST /api/inventory-counts/{id}/refresh-system-quantities`

Effects:

* persists count header and lines
* count number is generated server-side using the `CNT-` document sequence
* one count applies to one active warehouse
* the same item cannot appear more than once in one count document
* counted quantity must be zero or positive
* draft system quantities may be refreshed from current stock ledger balances
* no stock ledger effect
* status remains `Draft`

### Post

Action:

* `POST /api/inventory-counts/{id}/post`

Preconditions:

* count exists
* count status is `Draft`
* warehouse is active
* at least one line exists
* all item and UOM references resolve and are active
* required global UOM conversions resolve
* counted quantities are non-negative
* item references are unique inside the count

Posting effects:

* posting runs in one transaction
* current system stock is re-read from the stock ledger at posting time
* line system, counted, and variance quantities are persisted from the posting-time calculation
* variance is `counted quantity - system quantity`
* positive variance writes one `InventoryCountIncrease` stock ledger `IN` row
* negative variance writes one `InventoryCountDecrease` stock ledger `OUT` row
* zero variance writes no stock ledger row
* final stock after posting equals the counted base quantity for each line
* status changes from `Draft` to `Posted`
* no financial statement effect

Idempotency:

* reposting an already posted count returns the current posted document
* duplicate count stock rows are guarded by document status, concurrency, and unique ledger operation keys
* the posting transaction re-reads the latest committed stock but does not freeze unrelated item or warehouse postings; strict physical-count cutoffs require a future stock-freeze or reservation flow

### Cancel

Action:

* `POST /api/inventory-counts/{id}/cancel`

Preconditions:

* count exists
* count status is `Posted`
* warehouse is active
* cancellation of count increases validates current stock when negative stock is disabled
* cancellation of count decreases adds stock and does not need availability validation

Effects:

* status changes from `Posted` to `Canceled`
* original posted system, counted, and variance quantities are preserved
* `InventoryCountIncrease` rows are reversed with `InventoryCountCancellationOut`
* `InventoryCountDecrease` rows are reversed with `InventoryCountCancellationIn`
* zero-variance lines create no original or reversal ledger rows
* original posted count ledger rows are never edited or deleted
* duplicate cancellation is idempotent and returns the current canceled document

## Inventory Issue

### Draft Save

Actions:

* `POST /api/inventory-issues`
* `PUT /api/inventory-issues/{id}`

Effects:

* persists issue header and lines
* issue number is generated server-side using the `ISS-` document sequence
* one issue removes stock from one active warehouse for a controlled reason
* line `base_qty` is calculated server-side from the line UOM to the item base UOM
* the same item cannot appear more than once in one issue document
* no stock ledger effect
* status remains `Draft`

### Post

Action:

* `POST /api/inventory-issues/{id}/post`

Preconditions:

* issue exists
* issue status is `Draft`
* warehouse is active
* reason is a valid `InventoryIssueReason`
* at least one line exists
* all item and UOM references resolve and are active
* required global UOM conversions resolve
* quantities are positive
* item references are unique inside the issue
* warehouse stock is sufficient when negative stock is disabled
* stock availability validation aggregates issue lines by item and warehouse in base UOM

Posting effects:

* posting runs in one transaction
* line `base_qty` is recalculated server-side at posting time
* each line writes one warehouse `OUT` stock ledger row with transaction type `InventoryIssue`
* source document type is `InventoryIssue`
* status changes from `Draft` to `Posted`
* no financial statement effect

Idempotency:

* reposting an already posted issue returns the current posted document
* duplicate issue stock rows are guarded by document status, concurrency, and unique ledger operation keys

### Cancel

Action:

* `POST /api/inventory-issues/{id}/cancel`

Preconditions:

* issue exists
* issue status is `Posted`
* original warehouse, item, and UOM references still resolve

Effects:

* status changes from `Posted` to `Canceled`
* each original issue line writes one warehouse `IN` reversal row with transaction type `InventoryIssueCancellation`
* source document type is `InventoryIssueCancellation`
* original posted issue ledger rows are never edited or deleted
* cancellation adds stock back and does not require stock availability validation
* duplicate cancellation is idempotent and returns the current canceled document

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

## Pricing

Documents and master data:

* `PriceList`
* `ItemPrice`

Posting effects:

* none

Rules:

* pricing is maintained as organization-scoped master data
* price resolution is read-only and does not create stock ledger, supplier statement, payment, shortage, commission, tax, or accounting rows
* Phase 9 does not automatically copy resolved prices into purchase orders, sales documents, invoices, receipts, or returns
* future transaction pricing must call the server-side resolver and persist the selected price on the transaction document at creation time
