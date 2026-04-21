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
