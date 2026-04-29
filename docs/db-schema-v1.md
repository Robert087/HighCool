# Database Schema v1 — Procurement and Inventory

## Indexing Standard

Indexing is mandatory design work for every new ERP table.

Minimum rules:

* every foreign key must have an index unless covered by an existing composite index
* document tables must index document date, status, and high-frequency party references
* ledger and allocation tables must index source document traceability columns
* high-volume list filters must use sargable indexed columns
* composite indexes should match the real filter and sort patterns used by list endpoints
* avoid redundant overlapping indexes that add write cost without read benefit

## `customers`

Columns:

* `id`
* `code`
* `name`
* `phone`
* `email`
* `tax_number`
* `address`
* `city`
* `area`
* `credit_limit`
* `payment_terms`
* `notes`
* `is_active`
* audit fields

Constraints:

* unique index on `code`
* index on `name`

## `purchase_orders`

Columns:

* `id`
* `po_no`
* `supplier_id`
* `order_date`
* `expected_date`
* `notes`
* `status`
* audit fields

Constraints:

* unique index on `po_no`
* foreign key to `suppliers(id)` on `supplier_id`
* index on `(supplier_id, status, order_date)`
* index on `(supplier_id, order_date)`

## `purchase_order_lines`

Columns:

* `id`
* `purchase_order_id`
* `line_no`
* `item_id`
* `ordered_qty`
* `unit_price`
* `uom_id`
* `notes`
* audit fields

Constraints:

* unique index on `(purchase_order_id, line_no)`
* foreign key to `purchase_orders(id)` on `purchase_order_id`
* foreign key to `items(id)` on `item_id`
* foreign key to `uoms(id)` on `uom_id`
* index on `item_id`
* index on `uom_id`
* index on `(purchase_order_id, item_id)`

## `purchase_receipts`

Columns:

* `id`
* `receipt_no`
* `supplier_id`
* `warehouse_id`
* `purchase_order_id`
* `receipt_date`
* `supplier_payable_amount`
* `notes`
* `status`
* audit fields

Constraints:

* unique index on `receipt_no`
* foreign key to `suppliers(id)` on `supplier_id`
* foreign key to `warehouses(id)` on `warehouse_id`
* foreign key to `purchase_orders(id)` on `purchase_order_id`
* index on `(supplier_id, status, receipt_date)`
* index on `(warehouse_id, receipt_date)`
* index on `(purchase_order_id, receipt_date)`

## `purchase_receipt_lines`

Columns:

* `id`
* `purchase_receipt_id`
* `line_no`
* `purchase_order_line_id`
* `item_id`
* `ordered_qty_snapshot`
* `received_qty`
* `uom_id`
* `notes`
* audit fields

Constraints:

* unique index on `(purchase_receipt_id, line_no)`
* foreign key to `purchase_receipts(id)` on `purchase_receipt_id`
* foreign key to `purchase_order_lines(id)` on `purchase_order_line_id`
* foreign key to `items(id)` on `item_id`
* foreign key to `uoms(id)` on `uom_id`
* index on `item_id`
* index on `uom_id`
* index on `(purchase_receipt_id, item_id)`

## `purchase_receipt_line_components`

Columns:

* `id`
* `purchase_receipt_line_id`
* `component_item_id`
* `expected_qty`
* `actual_received_qty`
* `uom_id`
* `shortage_reason_code_id`
* `notes`
* audit fields

Constraints:

* unique index on `(purchase_receipt_line_id, component_item_id)`
* foreign key to `purchase_receipt_lines(id)` on `purchase_receipt_line_id`
* foreign key to `items(id)` on `component_item_id`
* foreign key to `uoms(id)` on `uom_id`
* foreign key to `shortage_reason_codes(id)` on `shortage_reason_code_id`
* index on `component_item_id`
* index on `shortage_reason_code_id`

## `stock_ledger_entries`

Columns:

* `id`
* `item_id`
* `warehouse_id`
* `transaction_type`
* `source_doc_type`
* `source_doc_id`
* `source_line_id`
* `qty_in`
* `qty_out`
* `uom_id`
* `base_qty`
* `running_balance_qty`
* `transaction_date`
* `unit_cost`
* `total_cost`
* audit fields

Constraints:

* unique index on `(source_doc_id, source_line_id, transaction_type)`
* index on `(item_id, warehouse_id, transaction_date)`
* index on `(warehouse_id, transaction_date)`
* index on `(transaction_type, transaction_date)`
* index on `(source_doc_type, source_doc_id)`
* foreign key to `items(id)` on `item_id`
* foreign key to `warehouses(id)` on `warehouse_id`
* foreign key to `uoms(id)` on `uom_id`

Behavior rules:

* this table is append-only
* stock balance queries must derive on-hand stock from these rows only
* no direct stock edit table exists or is allowed outside ledger-based posting flows

## `shortage_reason_codes`

Columns:

* `id`
* `code`
* `name`
* `description`
* `affects_supplier_balance`
* `affects_stock`
* `requires_approval`
* `is_active`
* audit fields

Constraints:

* unique index on `code`

## `shortage_ledger_entries`

Columns:

* `id`
* `purchase_receipt_id`
* `purchase_receipt_line_id`
* `purchase_order_id`
* `purchase_order_line_id`
* `item_id`
* `component_item_id`
* `expected_qty`
* `actual_qty`
* `shortage_qty`
* `resolved_physical_qty`
* `resolved_financial_qty_equivalent`
* `open_qty`
* `shortage_value`
* `resolved_amount`
* `open_amount`
* `shortage_reason_code_id`
* `affects_supplier_balance`
* `approval_status`
* `status`
* audit fields

Constraints:

* unique index on `(purchase_receipt_line_id, component_item_id)`
* index on `purchase_receipt_id`
* index on `purchase_receipt_line_id`
* index on `purchase_order_id`
* index on `purchase_order_line_id`
* index on `item_id`
* index on `component_item_id`
* index on `(status, item_id)`
* index on `(status, component_item_id)`
* foreign key to `purchase_receipts(id)` on `purchase_receipt_id`
* foreign key to `purchase_receipt_lines(id)` on `purchase_receipt_line_id`
* foreign key to `purchase_orders(id)` on `purchase_order_id`
* foreign key to `purchase_order_lines(id)` on `purchase_order_line_id`
* foreign key to `items(id)` on `item_id`
* foreign key to `items(id)` on `component_item_id`
* foreign key to `shortage_reason_codes(id)` on `shortage_reason_code_id`

Behavior rules:

* shortage state is derived and updated only through posted shortage resolution allocations
* `open_qty = shortage_qty - resolved_physical_qty - resolved_financial_qty_equivalent`
* shortage rows remain open until the full shortage quantity is covered

## `shortage_resolutions`

Columns:

* `id`
* `resolution_no`
* `supplier_id`
* `resolution_type`
* `resolution_date`
* `total_qty`
* `total_amount`
* `currency`
* `notes`
* `status`
* `approved_by`
* audit fields

Constraints:

* unique index on `resolution_no`
* index on `(supplier_id, resolution_type, status, resolution_date)`
* index on `(supplier_id, status, resolution_date)`
* foreign key to `suppliers(id)` on `supplier_id`

## `shortage_resolution_allocations`

Columns:

* `id`
* `resolution_id`
* `shortage_ledger_id`
* `allocation_type`
* `allocated_qty`
* `allocated_amount`
* `valuation_rate`
* `financial_qty_equivalent`
* `allocation_method`
* `sequence_no`
* audit fields

Constraints:

* unique index on `(resolution_id, sequence_no)`
* unique index on `(resolution_id, shortage_ledger_id)`
* index on `(shortage_ledger_id, allocation_type)`
* foreign key to `shortage_resolutions(id)` on `resolution_id`
* foreign key to `shortage_ledger_entries(id)` on `shortage_ledger_id`

Behavior rules:

* physical allocations store `allocated_qty` only
* financial allocations store `allocated_qty`, `valuation_rate`, calculated `allocated_amount`, and `financial_qty_equivalent`
* one shortage row may appear only once inside the same resolution

## Schema Addendum

Additional schema introduced for returns and reversal tracking:

### `purchase_returns`

Columns:

* `id`
* `return_no`
* `supplier_id`
* `reference_receipt_id`
* `return_date`
* `notes`
* `status`
* reversal tracking fields
* audit fields

Constraints:

* unique index on `return_no`
* index on `(supplier_id, status, return_date)`
* foreign key to `suppliers(id)` on `supplier_id`
* foreign key to `purchase_receipts(id)` on `reference_receipt_id`

### `purchase_return_lines`

Columns:

* `id`
* `purchase_return_id`
* `line_no`
* `item_id`
* `component_id`
* `warehouse_id`
* `return_qty`
* `uom_id`
* `base_qty`
* `reference_receipt_line_id`
* audit fields

Constraints:

* unique index on `(purchase_return_id, line_no)`
* foreign key to `purchase_returns(id)` on `purchase_return_id`
* foreign key to `items(id)` on `item_id`
* foreign key to `items(id)` on `component_id`
* foreign key to `warehouses(id)` on `warehouse_id`
* foreign key to `uoms(id)` on `uom_id`
* foreign key to `purchase_receipt_lines(id)` on `reference_receipt_line_id`

Behavior rules:

* linked return rows are validated against remaining returnable quantity from posted, non-reversed receipts and returns
* duplicate logical rows are blocked in application validation
* `base_qty` is mandatory for returnable quantity and proportional receipt-value reduction calculations

### `document_reversals`

Columns:

* `id`
* `reversal_no`
* `reversed_document_type`
* `reversed_document_id`
* `reversal_date`
* `reversal_reason`
* audit fields

Constraints:

* unique index on `reversal_no`
* unique index on `(reversed_document_type, reversed_document_id)`

## `supplier_statement_entries`

Columns:

* `id`
* `supplier_id`
* `entry_date`
* `effect_type`
* `source_doc_type`
* `source_doc_id`
* `source_line_id`
* `debit`
* `credit`
* `running_balance`
* `currency`
* `notes`
* audit fields

Constraints:

* unique index on `(source_doc_type, source_doc_id, source_line_id, effect_type)`
* index on `(supplier_id, entry_date)`
* index on `(supplier_id, effect_type, entry_date)`
* index on `(supplier_id, source_doc_type, entry_date)`
* foreign key to `suppliers(id)` on `supplier_id`

Behavior rules:

* this table is append-only
* no direct supplier balance edit table exists or is allowed
* `running_balance` is stored as `previous + credit - debit`
* purchase receipt statement rows use `purchase_receipts.supplier_payable_amount`
* PO-linked receipt payable amount is calculated from received quantities and linked PO line `unit_price`
* payment statement rows use `source_line_id = payment_allocations.id` for allocation traceability
* purchase return statement rows use `source_line_id = purchase_returns.reference_receipt_id` when linked so supplier target state can reduce the correct receipt

## `payments`

Columns:

* `id`
* `payment_no`
* `party_type`
* `party_id`
* `direction`
* `amount`
* `payment_date`
* `currency`
* `exchange_rate`
* `payment_method`
* `reference_note`
* `notes`
* `status`
* audit fields

Constraints:

* unique index on `payment_no`
* index on `(party_type, party_id, payment_date)`
* index on `(party_type, party_id, direction, status, payment_date)`
* index on `status`
* index on `payment_method`
* index on `direction`
* foreign key to `suppliers(id)` on `party_id` for the current supplier payment flow

Behavior rules:

* posted payments are immutable
* payment posting creates supplier statement rows only through posted allocations

## `payment_allocations`

Columns:

* `id`
* `payment_id`
* `target_doc_type`
* `target_doc_id`
* `target_line_id`
* `allocated_amount`
* `allocation_order`
* audit fields

Constraints:

* unique index on `(payment_id, allocation_order)`
* index on `(target_doc_type, target_doc_id, target_line_id)`
* index on `payment_id`

Behavior rules:

* duplicate logical targets are blocked in application validation
* foreign key to `payments(id)` on `payment_id`

Behavior rules:

* payment allocations are mandatory before posting
* open amount is derived from the active source document amount minus posted payment allocations
* purchase receipt target amount is reduced by posted, non-reversed purchase returns before payment open amount is calculated
* current supplier payment targets are `PurchaseReceipt` and `ShortageResolution`
