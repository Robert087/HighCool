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
* `unit_price`
* `uom_id`
* `notes`
* audit fields

Rules:

* `ordered_qty > 0`
* `unit_price >= 0`
* line numbers are unique inside the PO

## Purchase Receipt

Fields:

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

Rules:

* receipts may be manual or PO-linked
* only `Draft` receipts are editable
* only `Draft` receipts can be posted
* posting is idempotent
* posted receipts are immutable
* posted receipts generate supplier statement rows from the receipt header payable amount
* for PO-linked receipts, `supplier_payable_amount` is calculated from received quantities and linked PO line unit prices
* for manual receipts, `supplier_payable_amount` is the explicit procurement financial basis used for supplier statement and payment allocation until manual receipt line pricing exists

## Purchase Receipt Line

Fields:

* `id`
* `purchase_receipt_id`
* `line_no`
* `purchase_order_line_id`
* `item_id`
* `ordered_qty_snapshot`
* `received_qty`
* `unit_price` from the linked PO line in read DTOs
* `line_amount = received_qty x unit_price` for PO-linked receipt read DTOs
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
* `remaining_receivable_qty = ordered_qty - posted non-reversed received_qty`
* posted receipt lines expose `remaining_returnable_qty = posted received qty - posted active returned qty`

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
* `shortage_reason_code_id` is optional and informational only
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

Rules:

* created only when `expected_qty > actual_qty`
* `shortage_qty = expected_qty - actual_qty`
* new shortages start as `Open`
* `resolved_physical_qty` starts at `0`
* `resolved_financial_qty_equivalent` starts at `0`
* `open_qty` starts at `shortage_qty`
* `open_qty = shortage_qty - resolved_physical_qty - resolved_financial_qty_equivalent`
* `final_physical_component_qty = actual_qty + resolved_physical_qty`
* financial resolution reduces open shortage quantity but does not increase physical component quantity
* status is derived from `open_qty`
* value fields remain nullable until a valuation basis is known
* shortage lifecycle state is updated only through posted shortage resolutions
* one shortage row may be resolved many times over time
* one shortage row may be settled by both physical and financial resolutions over time
* shortage closes only when the full shortage quantity has been covered
* shortage reason is informational and does not restrict physical or financial resolution eligibility
* actionable shortage lists expose only rows with `open_qty > 0`

## Shortage Resolution

Fields:

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

Rules:

* new shortage resolutions start as `Draft`
* only `Draft` resolutions are editable
* only `Draft` resolutions can be posted
* one resolution may allocate across multiple shortage rows
* one shortage row may be allocated by multiple resolutions over time
* the same shortage row cannot appear twice inside one resolution document

## Shortage Resolution Allocation

Fields:

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

Rules:

* allocation rows are mandatory before posting
* physical resolution uses `allocated_qty`
* financial resolution uses `allocated_qty` plus `valuation_rate`
* financial `allocated_amount = allocated_qty x valuation_rate`
* `financial_qty_equivalent = allocated_qty`
* one shortage row may receive multiple allocations across multiple resolution documents

## Supplier Statement Entry

Fields:

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

Rules:

* append-only
* created only from posted supplier-affecting business documents
* purchase receipt posting writes supplier statement rows
* purchase return posting writes supplier statement rows only when a valid supplier financial basis is available from the referenced receipt valuation basis
* shortage financial resolution writes supplier statement rows with source allocation traceability
* physical shortage resolution does not write supplier statement rows
* supplier payment posting writes supplier statement rows with source allocation traceability
* purchase receipt reversal writes opposite supplier statement rows using source type `PurchaseReceiptReversal` and effect type `PurchaseReceiptReversal`
* payment reversal writes opposite supplier statement rows using source type `PaymentReversal` and effect type `PaymentReversal`
* shortage financial resolution reversal writes opposite supplier statement rows using source type `ShortageResolutionReversal` and effect type `ShortageResolutionReversal`
* no manual statement entry flow exists
* financial supplier statement rows must not be stored with both `debit = 0` and `credit = 0`
* `running_balance = previous_running_balance + credit - debit`
* purchase receipt rows currently use the explicit receipt header payable amount until receipt line pricing is implemented explicitly
* current source document typing must remain explicit and auditable:
  * `PurchaseReceipt`
  * `PurchaseReturn`
  * `Payment`
  * `PurchaseReceiptReversal`
  * `PaymentReversal`
  * `ShortageFinancialResolution`
  * `ShortageResolutionReversal`

## Payment

Fields:

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

Rules:

* supplier payments are the currently supported procurement payment flow
* new payments start as `Draft`
* only `Draft` payments are editable
* only `Draft` payments can be posted
* posted payments are immutable
* payment posting is idempotent
* `direction = OutboundToParty` means the company pays the supplier and reduces supplier payable balance
* `direction = InboundFromParty` means money is received from the supplier and reduces supplier receivable balance created by financial shortage resolutions
* no direct supplier balance edit is allowed outside posted source documents

## Payment Allocation

Fields:

* `id`
* `payment_id`
* `target_doc_type`
* `target_doc_id`
* `target_line_id`
* `allocated_amount`
* `allocation_order`
* audit fields

Rules:

* allocation rows are mandatory before payment posting
* one payment may allocate across many target documents
* one target document may be settled by many payments over time
* partial settlement is supported
* over-allocation is blocked
* current procurement targets are:
  * `PurchaseReceipt` for outbound supplier payments
  * `ShortageResolution` for inbound supplier payments against financial shortage receivables
* payment amount must equal total allocated amount before posting

## Purchase Return Addendum

Fields:

* `id`
* `return_no`
* `supplier_id`
* `reference_receipt_id`
* `return_date`
* `notes`
* `status`
* reversal tracking fields
* audit fields

Rules:

* purchase returns start as `Draft`
* only `Draft` purchase returns are editable or postable
* posted purchase returns are immutable
* receipt-linked and manual supplier-plus-item returns are both allowed
* returned quantity cannot exceed the remaining returnable quantity on posted, non-reversed receipt history
* partial and repeated returns against the same receipt are supported
* posting creates stock ledger `OUT` rows
* posting creates supplier statement rows only when a valid referenced receipt financial basis produces a positive return amount
* posting must not create a fake zero-value supplier statement row when no financial basis is available
* duplicate logical rows are blocked inside the same return document
* receipt-linked supplier statement entries keep the reference receipt id for financial target state reduction

## Supplier Financial Target State

Fields:

* `target_doc_type`
* `target_doc_id`
* `original_amount`
* `adjusted_amount`
* `net_amount`
* `allocated_amount`
* `open_amount`
* `status`

Rules:

* receipt target `adjusted_amount` is reduced by posted, non-reversed purchase returns linked to that receipt
* payment target `open_amount` uses only posted, non-reversed source documents and posted, non-reversed payment allocations
* target status moves through `Open`, `PartiallySettled`, `Settled`, or `Reversed`
* actionable payment allocation lists include only targets with `open_amount > 0`

## Reversal Addendum

Rules:

* posted purchase receipts, supplier payments, and shortage resolutions are corrected only through reversal actions
* reversal writes opposite business effects without deleting the original effects
* supplier statement reversal rows must keep explicit reversal source typing and opposite debit/credit values relative to the original financial rows
* the original posted document remains visible and auditable
* one active reversal is allowed for each supported posted document
