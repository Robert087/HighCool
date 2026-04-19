# Business Document — Operational ERP System

## 1. System Goal

Build a standalone operational ERP system that manages:

* Suppliers and supplier statements
* Customers and customer statements
* Items and components
* Warehousing and stock movement
* Purchasing and receiving
* Sales and collections
* Supplier shortage control
* Supplier commission revenue
* Employee advances and payroll

The system is **operations-first** and must maintain accurate:

* Stock quantities
* Party balances
* Shortage balances
* Payroll obligations
* Commission earnings

This system should be built **without depending on ERP frameworks**.

---

## 2. Core Business Scope

The system must support the following business entities and flows:

* Supplier
* Supplier Statement
* Customer
* Customer Statement
* Items that contain components
* Components that may also be sold separately
* Warehouse
* Stock Adjustment
* Purchase Order
* Purchase Receipt
* Payments (Pay / Receive)
* Partial and full payment logic
* Sales Invoice
* UOM
* Controlled UOM Conversion
* Supplier Commissions as Revenue
* Employees
* Employee Advances
* Employee Salaries
* Shortage control flow

---

## 3. Production-Ready Modules

### 3.1 Master Data and Configuration

This module contains the foundational records used by the whole system.

#### Entities

* Suppliers
* Customers
* Employees
* Warehouses
* Items
* Components
* Units of Measure
* UOM Conversion Rules
* Commission Rules
* Shortage Reason Codes

#### Key Rules

* An item may be:

  * a sellable item
  * a component of another item
  * both
* A component may be sold independently if flagged as sellable.
* UOM conversion must be explicitly defined and controlled per item when needed.
* Warehouse behavior must be validated before any stock transaction.

---

### 3.2 Procurement and Receipt Control

This module handles supplier ordering and actual receiving.

#### Documents

* Purchase Order
* Purchase Receipt
* Purchase Return

#### Core Logic

* A Purchase Order defines expected items and quantities.
* A Purchase Receipt records actual delivered items and actual delivered components.
* The system detects shortages during receipt validation.
* Shortages may create obligations against the supplier depending on the shortage reason.

#### Important Production Rule

A Purchase Receipt should not only store the received item quantity. It must also allow recording:

* Actual received component quantities per line
* Shortage reason if a mismatch exists
* Whether the shortage is due to supplier fault or internal handling issues

---

### 3.3 Shortage Control

This is one of the most critical modules.

#### Purpose

Track component shortages linked to suppliers and receipts.

#### Core Logic

When receiving a composite item:

1. The system expands expected components using the BOM or component definition.
2. The system compares expected component quantities against actual received component quantities.
3. If actual quantity is less than expected quantity:

   * Create shortage ledger entries
   * Assign shortage reason
   * Keep the shortage open until resolved

#### Production Improvement

Not every shortage should automatically become supplier debt.

#### Possible Shortage Reasons

* Supplier Missing Quantity
* Damaged on Receipt
* Internal Counting Error
* Warehouse Loss
* Manual Correction
* BOM / Definition Error

Only shortage reasons marked as **supplier accountable** should affect the supplier statement.

---

### 3.4 Sales and Revenue

This module handles customer sales and supplier-based commission revenue.

#### Documents

* Sales Invoice
* Sales Return
* Commission Ledger

#### Core Logic

* A Sales Invoice increases the customer balance.
* A payment reduces the customer balance.
* A sale may generate supplier commission revenue.
* A Sales Return reverses stock, balance, and commission impact if applicable.

---

### 3.5 Finance Operations

This module tracks operational balances, not formal accounting journal entries.

#### Documents

* Payments
* Payment Allocations
* Supplier Statement Entries
* Customer Statement Entries
* Employee Advance Entries
* Salary Entries

#### Production Rule

Every financial movement must be represented by:

* Source document
* Effect type
* Allocation target
* Running balance effect

---

### 3.6 Warehousing and Stock Control

This module manages stock across warehouses using explicit stock movement tracking.

#### Documents

* Stock Ledger Entry
* Stock Adjustment
* Purchase Receipt Stock Impact
* Sales Invoice Stock Impact
* Return Stock Impact
* Resolution Stock Impact

#### Core Principle

Never update stock by directly editing item quantities only.

All stock changes must flow through a **Stock Ledger**.

---

### 3.7 HR and Payroll

This module handles employees, advances, salary generation, and deductions.

#### Documents

* Employee
* Employee Advance
* Salary Slip
* Salary Deduction Lines
* Payroll Run

#### Core Logic

* Employee advance increases the amount owed by the employee.
* Salary run checks unpaid advances.
* Configured deductions are applied automatically.
* Final net salary is calculated and stored.

---

## 4. Required Production Additions

These additions are essential to move the system from concept stage to production-ready stage.

---

### 4.1 Payment Allocation

#### Why It Is Required

A payment without allocation creates major issues:

* You cannot know which invoice the payment was applied to.
* You cannot track how a partial payment was distributed.
* Statements become inaccurate.
* Reconciliation becomes difficult.

#### Required Design

##### `payments`

Stores:

* `id`
* `party_type`
* `party_id`
* `direction` (`Pay` / `Receive`)
* `amount`
* `date`
* `currency`
* `exchange_rate` if needed
* `payment_method`
* `reference_note`
* `status`

##### `payment_allocations`

Stores:

* `id`
* `payment_id`
* `target_doc_type`
* `target_doc_id`
* `allocated_amount`
* `allocation_order`
* `created_at`

#### Rules

* One payment may allocate to many documents.
* One invoice may be settled by many payments.
* Partial payment must keep the remaining balance open.
* Overpayment must become either:

  * credit balance
  * or unallocated balance
* Allocations must be immutable after posting, except through reversal flow.

---

### 4.2 Shortage Reasoning

#### Why It Is Required

The original concept treats every shortage as supplier debt, but that is not always correct.

#### Required Design

##### `shortage_reason_codes`

Fields:

* `id`
* `code`
* `name`
* `description`
* `affects_supplier_balance` (`bool`)
* `affects_stock` (`bool`)
* `requires_approval` (`bool`)
* `is_active`

##### `shortage_ledger`

Must include:

* `shortage_reason_code_id`
* `accountability_type`
* `approval_status`
* `created_by`
* `reviewed_by`

#### Rules

* Shortage reason is mandatory.
* Supplier statement impact happens only if the reason affects supplier balance.
* Internal reasons must not reduce supplier payable or create false supplier debt.
* Approved shortages only can proceed to financial resolution.

---

### 4.3 Stock Ledger and Warehouse Transactions

#### Why It Is Required

Without a stock ledger:

* Stock errors become difficult to trace
* Returns and adjustments become unreliable
* Warehouse reporting becomes weak

#### Required Design

##### `stock_ledger_entries`

Fields:

* `id`
* `item_id`
* `warehouse_id`
* `transaction_type`
* `source_doc_type`
* `source_doc_id`
* `qty_in`
* `qty_out`
* `uom_id`
* `base_qty`
* `running_balance_qty`
* `unit_cost`
* `total_cost`
* `transaction_date`
* `created_at`

#### Transaction Types

* Purchase Receipt
* Sales Invoice
* Stock Adjustment In
* Stock Adjustment Out
* Purchase Return
* Sales Return
* Shortage Physical Resolution
* Opening Balance
* Transfer In
* Transfer Out

#### Rules

* Stock must always be recalculable from the ledger.
* No silent stock edits are allowed.
* Every stock-affecting document must generate ledger rows.
* All ledger entries should be append-only.
* Corrections must happen through reversing entries, not by overwriting old records.

---

### 4.4 UOM Rules, Precision, and Validation

#### Why It Is Required

Having conversion without strict rules creates:

* Rounding errors
* Stock mismatches
* Wrong pricing
* Wrong receipt validation

#### Required Design

##### `uoms`

* `id`
* `code`
* `name`
* `precision`
* `allows_fraction` (`bool`)

##### `item_uom_conversions`

* `id`
* `item_id`
* `from_uom_id`
* `to_uom_id`
* `factor`
* `rounding_mode`
* `min_fraction`
* `is_active`

#### Rules

* Each item must have a base UOM.
* All stock ledger quantities must be stored in base UOM.
* Purchase and sales UOM may differ from stock UOM.
* Conversion must be item-aware where needed.
* Rounding mode must be explicit:

  * `floor`
  * `ceil`
  * `nearest`
* Fractional selling must be configurable per item/UOM.
* Conversion changes must not silently affect old transactions.

---

### 4.5 Commission Logic Details

#### Why It Is Required

The commission idea is strong, but it needs a much clearer implementation definition.

#### Required Questions

The system must answer:

* Commission applies to which items?
* Commission comes from which supplier?
* Is it a percentage or fixed amount?
* Is it calculated per item line or invoice total?
* When is it earned?
* When is it canceled?
* How are returns handled?

#### Required Design

##### `supplier_commission_rules`

* `id`
* `supplier_id`
* `item_id`
* `commission_type` (`percentage` / `fixed`)
* `commission_value`
* `basis` (`qty` / `amount` / `invoice_line`)
* `valid_from`
* `valid_to`
* `priority`
* `is_active`

##### `supplier_commission_entries`

* `id`
* `sales_invoice_id`
* `sales_invoice_line_id`
* `supplier_id`
* `item_id`
* `commission_rule_id`
* `commission_amount`
* `status`
* `reversed_by`
* `created_at`

#### Rules

* Commission should be generated only on eligible invoice lines.
* Returns must reverse commission proportionally.
* Commission must be traceable to the original invoice line.
* Multiple commission rules must use priority resolution.
* No duplicate commission generation on invoice edit or resubmit.

---

### 4.6 Returns

#### Why It Is Required

Returns are essential in any production system:

* Purchase Return
* Sales Return

Without them, balances and stock will break.

#### Required Design

##### `purchase_returns`

* Linked to purchase receipt
* Reduces supplier delivered quantity
* May reopen shortage depending on case
* May reverse stock entries
* May affect supplier statement

##### `sales_returns`

* Linked to sales invoice
* Restores stock if returnable
* Reduces customer balance or creates credit
* Reverses commission if applicable

#### Rules

* Returns should reference the original source document.
* Partial returns must be supported.
* Stock, balance, and commission effects must reverse proportionally.
* Closed periods may require credit-note style handling instead of destructive changes.

---

### 4.7 Shortage Resolution Allocation

#### Why It Is Required

This is a central design point.

If there are many supplier shortages, then one resolution must be able to settle multiple shortage rows, and one shortage row must be able to receive multiple resolutions over time.

#### Required Design

##### `shortage_resolutions`

* `id`
* `supplier_id`
* `resolution_type` (`Physical` / `Financial`)
* `resolution_date`
* `total_qty` if physical
* `total_amount` if financial
* `currency`
* `notes`
* `status`
* `approved_by`

##### `shortage_resolution_allocations`

* `id`
* `resolution_id`
* `shortage_ledger_id`
* `allocated_qty`
* `allocated_amount`
* `valuation_rate`
* `allocation_method`
* `sequence_no`

#### Rules

* One resolution may settle multiple shortage rows.
* One shortage row may be settled by multiple resolutions over time.
* FIFO allocation should be default unless manually overridden with approval.
* Physical resolution decreases open shortage quantity.
* Financial resolution decreases open shortage value.
* A shortage is closed only when fully resolved by quantity or value according to the rule.
* Allocation rows are mandatory for traceability.

---

## 5. Suggested Production Database Schema

### 5.1 Master Tables

* `suppliers`
* `customers`
* `employees`
* `warehouses`
* `items`
* `item_components`
* `uoms`
* `item_uom_conversions`
* `shortage_reason_codes`
* `supplier_commission_rules`

### 5.2 Procurement Tables

* `purchase_orders`
* `purchase_order_lines`
* `purchase_receipts`
* `purchase_receipt_lines`
* `purchase_receipt_components`
* `purchase_returns`
* `purchase_return_lines`

### 5.3 Shortage Tables

* `shortage_ledger`
* `shortage_resolutions`
* `shortage_resolution_allocations`

### 5.4 Sales Tables

* `sales_invoices`
* `sales_invoice_lines`
* `sales_returns`
* `sales_return_lines`
* `supplier_commission_entries`

### 5.5 Finance Tables

* `payments`
* `payment_allocations`
* `supplier_statement_entries`
* `customer_statement_entries`

### 5.6 Stock Tables

* `stock_ledger_entries`
* `stock_adjustments`
* `stock_adjustment_lines`
* `warehouse_transfers`

### 5.7 HR Tables

* `employee_advances`
* `salary_slips`
* `salary_deduction_lines`
* `payroll_runs`

---

## 6. Required Statement Model

### Supplier Statement Must Include

* Purchase receipts
* Supplier-accountable shortage entries
* Shortage financial resolutions
* Supplier payments
* Commission receivable/payable handling depending on business rule
* Returns

### Customer Statement Must Include

* Sales invoices
* Payments received
* Sales returns
* Credits / unapplied balances

### Important Rule

Statements should **not** be manually entered.

They should be generated from **posted business documents**.

---

## 7. Event-Driven Posting Rules

Each document, once posted, must generate a clear business effect.

### Purchase Receipt

Generates:

* Stock ledger entries
* Shortage ledger rows if mismatch exists
* Supplier statement impact if applicable

### Sales Invoice

Generates:

* Stock ledger out entries
* Customer statement debit
* Commission entries if eligible

### Payment

Generates:

* Statement movement
* Payment allocations

### Purchase Return

Generates:

* Stock reversal
* Supplier statement adjustment
* Possible shortage reopen logic

### Sales Return

Generates:

* Stock reversal
* Customer statement credit
* Commission reversal

### Shortage Resolution

Generates:

* Allocation rows
* Supplier statement adjustment if financial
* Stock movement if physical replacement affects stock

---

## 8. Technical Production Rules

### 8.1 Posting Strategy

Use explicit document status:

* `draft`
* `posted`
* `canceled`

Only posted documents affect stock, balances, and statements.

### 8.2 Immutability

Posted financial and stock rows should not be edited directly.

Corrections should happen through:

* Reversal
* Adjustment
* Return
* Cancellation flow

### 8.3 Auditability

Every critical row should store:

* `created_by`
* `updated_by`
* `created_at`
* `updated_at`
* `source_doc_type`
* `source_doc_id`

### 8.4 Validation

Strict validation is required for:

* Negative allocations
* Over-allocation
* Stock below allowed threshold
* Missing UOM conversion
* Shortage resolution above open quantity/value
* Duplicate posting
* Return quantity greater than original quantity

### 8.5 Idempotency

Posting APIs must protect against duplicate execution.

This is especially important in:

* Invoice posting
* Payment posting
* Stock posting
* Commission generation

---

## 9. Recommended Coding Architecture

If the system is built from scratch, the recommended structure is:

### Layers

* API Layer
* Application / Service Layer
* Domain Layer
* Repository / Data Access Layer
* Background Jobs / Queue Layer
* Reporting Layer

### Suggested Services

* `PurchaseReceiptPostingService`
* `ShortageDetectionService`
* `ShortageResolutionService`
* `StockPostingService`
* `PaymentAllocationService`
* `StatementBuilderService`
* `CommissionCalculationService`
* `SalesReturnService`
* `PayrollCalculationService`

### Key Principle

Business logic should not live inside controllers or routes.

It should live inside services and domain logic.

---

## 10. What Changed from the Original Document

The original document was already strong in the overall idea and shortage flow. This version adds the production-level details needed for implementation:

* Payment allocation
* Shortage reasons and accountability
* Stock ledger
* Strict UOM conversion rules
* Detailed commission engine
* Purchase and sales returns
* Shortage resolution allocation model
* Posting and immutability rules
* Auditability and traceability

---

## 11. Final Direction

This system should be implemented as a **production-ready operational ERP**, not as a lightweight CRUD app.

The implementation must prioritize:

* Traceability
* Controlled posting
* Ledger-based stock movement
* Statement accuracy
* Clear shortage accountability
* Allocation-driven settlements
* Safe reversals instead of destructive edits
