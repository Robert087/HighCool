# Domain Model — Procurement and Inventory v1

## Customer

Fields:

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

Rules:

* `code` is required and unique
* `name` is required
* `credit_limit >= 0`
* `email` is optional but must be valid when supplied
* customers are deactivated instead of hard-deleted

## Purchase Order

Fields:

* `id`
* `po_no`
* `supplier_id`
* `order_date`
* `expected_date`
* `notes`
* `status`
* audit fields

Relationships:

* belongs to one supplier
* owns zero or more `PurchaseOrderLine` rows

Rules:

* new POs start as `Draft`
* only `Draft` POs are editable
* only `Draft` POs can be posted
* posted POs can be canceled
* PO has no stock or shortage effect

## Purchase Order Line

Fields:

* `id`
* `purchase_order_id`
* `line_no`
* `item_id`
* `ordered_qty`
* `uom_id`
* `notes`
* audit fields

Rules:

* `ordered_qty > 0`
* line numbers are unique inside the PO

## Purchase Receipt

Fields:

* `id`
* `receipt_no`
* `supplier_id`
* `warehouse_id`
* `purchase_order_id`
* `receipt_date`
* `notes`
* `status`
* audit fields

Rules:

* receipts may be manual or PO-linked
* only `Draft` receipts are editable
* only `Draft` receipts can be posted
* posting is idempotent
* posted receipts are immutable

## Purchase Receipt Line

Fields:

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

Rules:

* `received_qty > 0`
* line numbers are unique inside the receipt
* when linked to PO:
  * `purchase_order_line_id` is required
  * `ordered_qty_snapshot` is required
  * snapshot equals PO line ordered quantity
  * `received_qty` cannot exceed remaining posted PO quantity

## Purchase Receipt Line Component

Fields:

* `id`
* `purchase_receipt_line_id`
* `component_item_id`
* `expected_qty`
* `actual_received_qty`
* `uom_id`
* `shortage_reason_code_id`
* `notes`
* audit fields

Rules:

* rows are auto-filled from the selected item's embedded component definitions
* `expected_qty` is system-derived as `received_qty x item component quantity`
* `actual_received_qty` defaults to `expected_qty` and remains editable
* `actual_received_qty >= 0`
* `shortage_reason_code_id` is required only when `actual_received_qty < expected_qty`
* one component row per component item inside the same receipt line

## Stock Ledger Entry

Fields:

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

Rules:

* append-only
* purchase receipt posting writes stock `IN`
* `base_qty` is stored in item base UOM
* `running_balance_qty` is derived per `(item_id, warehouse_id)`
* stock balance is always derived from stock ledger rows only
* stock ledger rows cannot be edited or deleted after they are written
* direct stock quantity edits are not allowed anywhere in the system
* corrections must be represented as reversing or adjustment ledger entries

## Shortage Reason Code

Fields:

* `id`
* `code`
* `name`
* `description`
* `affects_supplier_balance`
* `affects_stock`
* `requires_approval`
* `is_active`
* audit fields

## Shortage Ledger Entry

Fields:

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

Rules:

* append-only
* created only when `expected_qty > actual_qty`
* `shortage_qty = expected_qty - actual_qty`
* new shortages start as `Open`
