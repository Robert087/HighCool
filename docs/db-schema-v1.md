# Database Schema v1 — Procurement and Inventory

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

## `purchase_order_lines`

Columns:

* `id`
* `purchase_order_id`
* `line_no`
* `item_id`
* `ordered_qty`
* `uom_id`
* `notes`
* audit fields

Constraints:

* unique index on `(purchase_order_id, line_no)`
* foreign key to `purchase_orders(id)` on `purchase_order_id`
* foreign key to `items(id)` on `item_id`
* foreign key to `uoms(id)` on `uom_id`

## `purchase_receipts`

Columns:

* `id`
* `receipt_no`
* `supplier_id`
* `warehouse_id`
* `purchase_order_id`
* `receipt_date`
* `notes`
* `status`
* audit fields

Constraints:

* unique index on `receipt_no`
* foreign key to `suppliers(id)` on `supplier_id`
* foreign key to `warehouses(id)` on `warehouse_id`
* foreign key to `purchase_orders(id)` on `purchase_order_id`

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
* foreign key to `shortage_resolutions(id)` on `resolution_id`
* foreign key to `shortage_ledger_entries(id)` on `shortage_ledger_id`

Behavior rules:

* physical allocations store `allocated_qty` only
* financial allocations store `allocated_qty`, `valuation_rate`, calculated `allocated_amount`, and `financial_qty_equivalent`

## `supplier_statement_entries`

Columns:

* `id`
* `supplier_id`
* `effect_type`
* `source_doc_type`
* `source_doc_id`
* `source_line_id`
* `amount_delta`
* `running_balance`
* `currency`
* `transaction_date`
* `notes`
* audit fields

Constraints:

* unique index on `(source_doc_id, source_line_id, effect_type)`
* index on `(supplier_id, transaction_date)`
* foreign key to `suppliers(id)` on `supplier_id`

Behavior rules:

* this table is append-only
* no direct supplier balance edit table exists or is allowed
