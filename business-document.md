1. System Goal

Build a standalone operational ERP system that manages:

Suppliers and supplier statements
Customers and customer statements
Items and components
Warehousing and stock movement
Purchasing and receiving
Sales and collections
Supplier shortage control
Supplier commission revenue
Employee advances and payroll

The system is operations-first and should maintain accurate:

stock quantities
party balances
shortage balances
payroll obligations
commission earnings

without depending on ERP frameworks.

2. Core Business Scope

The system must support the following business entities and flows:

Supplier
Supplier Statement
Customer
Customer Statement
Items that contain components
Components that may also be sold separately
Warehouse
Stock Adjustment
Purchase Order
Purchase Receipt
Payments (Pay / Receive)
Partial and full payment logic
Sales Invoice
UOM
Controlled UOM Conversion
Supplier Commissions as revenue
Employees
Employee Advances
Employee Salaries
Shortage control flow
3. Production-Ready Modules
3.1 Master Data & Configuration

This module contains the foundational records used by the whole system.

Entities
Suppliers
Customers
Employees
Warehouses
Items
Components
Units of Measure
UOM Conversion Rules
Commission Rules
Shortage Reason Codes
Key Rules
An item may be:
a sellable item
a component of another item
both
A component may be sold independently if flagged as sellable
UOM conversion must be explicitly defined and controlled per item when needed
Warehouse behavior must be validated before any stock transaction
3.2 Procurement & Receipt Control

Handles supplier ordering and actual receiving.

Documents
Purchase Order
Purchase Receipt
Purchase Return
Core Logic
Purchase Order defines expected items and quantities
Purchase Receipt records actual delivered items and actual delivered components
System detects shortages during receipt validation
Shortages create obligations against the supplier
Important Production Rule

Purchase receipt should not only store “received item qty”, but also allow recording:

actual received component quantities per line
shortage reason if mismatch exists
whether shortage is supplier fault or internal handling issue
3.3 Shortage Control

This is one of the most critical modules.

Purpose

Tracks component shortages linked to suppliers and receipts.

Core Logic

When receiving a composite item:

system expands expected components using BOM/component definition
system compares expected component quantities vs actual component quantities received
if actual < expected:
create shortage ledger entries
assign shortage reason
keep shortage open until resolved
Production Improvement

Not every shortage should automatically become “supplier debt”.

Possible shortage reasons:

Supplier Missing Quantity
Damaged on Receipt
Internal Counting Error
Warehouse Loss
Manual Correction
BOM/Definition Error

Only shortage reasons marked as supplier accountable should affect supplier statement.

3.4 Sales & Revenue

Handles customer sales and supplier-based commission revenue.

Documents
Sales Invoice
Sales Return
Commission Ledger
Core Logic
Sales invoice increases customer balance
Payment reduces customer balance
Sale may generate supplier commission revenue
Sales return reverses stock, balance, and commission impact if applicable
3.5 Finance Operations

Tracks operational balances, not formal accounting journal entries.

Documents
Payments
Payment Allocations
Supplier Statement Entries
Customer Statement Entries
Employee Advance Entries
Salary Entries
Production Rule

Every financial movement must be represented by:

source document
effect type
allocation target
running balance effect
3.6 Warehousing & Stock Control

Manages stock across warehouses using explicit stock movement tracking.

Documents
Stock Ledger Entry
Stock Adjustment
Purchase Receipt Stock Impact
Sales Invoice Stock Impact
Return Stock Impact
Resolution Stock Impact
Core Principle

Never update stock by directly editing item quantities only.
All stock changes must flow through a Stock Ledger.

3.7 HR & Payroll

Handles employees, advances, salary generation, and deductions.

Documents
Employee
Employee Advance
Salary Slip
Salary Deduction Lines
Payroll Run
Core Logic
employee advance increases amount owed by employee
salary run checks unpaid advances
configured deductions are applied automatically
final net salary is calculated and stored
4. Required Production Additions

دي أهم الإضافات اللي هتنقل السيستم من “concept” إلى “production”.

4.1 Payment Allocation
Why it is required

وجود Payment لوحده بدون allocation هيعمل مشاكل كبيرة:

مش هتعرف الفلوس دي اتخصصت لأنهي invoice
مش هتعرف partial payment اتوزع إزاي
statements هتبقى غير دقيقة
reconciliation هتبقى صعبة
Required Design
Payments

Stores:

id
party_type
party_id
direction (Pay / Receive)
amount
date
currency
exchange_rate if needed
payment_method
reference_note
status
Payment_Allocations

Stores:

id
payment_id
target_doc_type
target_doc_id
allocated_amount
allocation_order
created_at
Rules
one payment may allocate to many documents
one invoice may be settled by many payments
partial payment must keep remaining balance open
overpayment must become:
credit balance
or unallocated balance
allocations must be immutable after posting except through reversal flow
4.2 Shortage Reasoning
Why it is required

النسخة الأصلية بتعتبر كل shortage كأنه supplier debt، وده مش دايمًا صحيح

Required Design
Shortage_Reason_Codes

Fields:

id
code
name
description
affects_supplier_balance (bool)
affects_stock (bool)
requires_approval (bool)
is_active
Shortage_Ledger

Must include:

shortage_reason_code_id
accountability_type
approval_status
created_by
reviewed_by
Rules
shortage reason is mandatory
supplier statement impact only if reason affects supplier balance
internal reasons must not reduce supplier payable or create false supplier debt
approved shortages only can proceed to financial resolution
4.3 Stock Ledger / Warehouse Transactions
Why it is required

من غير stock ledger:

أي stock errors هيبقى صعب تتراجع
returns and adjustments هتبقى غير مضمونة
warehouse reporting هيبقى ضعيف
Required Design
Stock_Ledger_Entries

Fields:

id
item_id
warehouse_id
transaction_type
source_doc_type
source_doc_id
qty_in
qty_out
uom_id
base_qty
running_balance_qty
unit_cost
total_cost
transaction_date
created_at
Transaction Types
Purchase Receipt
Sales Invoice
Stock Adjustment In
Stock Adjustment Out
Purchase Return
Sales Return
Shortage Physical Resolution
Opening Balance
Transfer In
Transfer Out
Rules
stock must always be recalculable from ledger
no silent stock edits
every stock-affecting document must generate ledger rows
all ledger entries should be append-only
corrections happen through reversing entries, not overwriting old records
4.4 UOM Rules, Precision, and Validation
Why it is required

وجود conversion بس من غير rules هيعمل مشاكل:

rounding errors
mismatch in stock
wrong pricing
wrong receipt validation
Required Design
UOMs
id
code
name
precision
allows_fraction (bool)
Item_UOM_Conversions
id
item_id
from_uom_id
to_uom_id
factor
rounding_mode
min_fraction
is_active
Rules
each item must have a base UOM
all stock ledger quantities must be stored in base UOM
purchase/sales UOM may differ from stock UOM
conversion must be item-aware where needed
rounding mode must be explicit:
floor
ceil
nearest
fractional selling must be configurable per item/UOM
conversion changes should not silently affect old transactions
4.5 Commission Logic Details
Why it is required

النسخة الحالية فيها فكرة commission كويسة، لكن محتاجة تعريف واضح جدًا وقت البرمجة

Required Questions the system must answer
commission is applied on which items?
commission comes from which supplier?
is it percentage or fixed amount?
calculated per item line or invoice total?
when is it earned?
when is it canceled?
how is return handled?
Required Design
Supplier_Commission_Rules
id
supplier_id
item_id
commission_type (percentage / fixed)
commission_value
basis (qty / amount / invoice_line)
valid_from
valid_to
priority
is_active
Supplier_Commission_Entries
id
sales_invoice_id
sales_invoice_line_id
supplier_id
item_id
commission_rule_id
commission_amount
status
reversed_by
created_at
Rules
commission should be generated only on eligible invoice lines
returns must reverse commission proportionally
commission must be traceable to original invoice line
multiple supplier commission rules must use priority resolution
no duplicate commission generation on invoice edit/resubmit
4.6 Returns
Why it is required

returns جزء أساسي في أي production system:

Purchase Return
Sales Return
بدونهم balances and stock هيتكسروا
Required Design
Purchase_Return
linked to purchase receipt
reduces supplier delivered quantity
may reopen shortage depending on case
may reverse stock entries
may affect supplier statement
Sales_Return
linked to sales invoice
restores stock if returnable
reduces customer balance or creates credit
reverses commission if applicable
Rules
returns should reference original source document
partial return must be supported
stock, balance, and commission effects must reverse proportionally
closed periods may require credit-note style handling instead of destructive changes
4.7 Shortage Resolution Allocation
Why it is required

دي نقطة محورية جدًا.
لو عندك supplier shortage كثيرة، فلازم resolution واحدة تقدر توزع على أكتر من shortage row، والعكس.

Required Design
Shortage_Resolutions
id
supplier_id
resolution_type (Physical / Financial)
resolution_date
total_qty if physical
total_amount if financial
currency
notes
status
approved_by
Shortage_Resolution_Allocations
id
resolution_id
shortage_ledger_id
allocated_qty
allocated_amount
valuation_rate
allocation_method
sequence_no
Rules
one resolution may settle multiple shortage rows
one shortage row may be settled by multiple resolutions over time
FIFO allocation should be default unless manually overridden with approval
physical resolution decreases open shortage qty
financial resolution decreases open shortage value
shortage is closed only when fully resolved by qty/value according to rule
allocation rows are mandatory for traceability
5. Suggested Production Database Schema
5.1 Master Tables
suppliers
customers
employees
warehouses
items
item_components
uoms
item_uom_conversions
shortage_reason_codes
supplier_commission_rules
5.2 Procurement Tables
purchase_orders
purchase_order_lines
purchase_receipts
purchase_receipt_lines
purchase_receipt_components
purchase_returns
purchase_return_lines
5.3 Shortage Tables
shortage_ledger
shortage_resolutions
shortage_resolution_allocations
5.4 Sales Tables
sales_invoices
sales_invoice_lines
sales_returns
sales_return_lines
supplier_commission_entries
5.5 Finance Tables
payments
payment_allocations
supplier_statement_entries
customer_statement_entries
5.6 Stock Tables
stock_ledger_entries
stock_adjustments
stock_adjustment_lines
warehouse_transfers
5.7 HR Tables
employee_advances
salary_slips
salary_deduction_lines
payroll_runs
6. Required Statement Model
Supplier Statement must include
purchase receipts
shortage supplier-accountable entries
shortage financial resolutions
supplier payments
commission receivable/payable handling depending on business rule
returns
Customer Statement must include
sales invoices
payments received
sales returns
credits / unapplied balances
Important Rule

Statements should not be manually entered.
They should be generated from posted business documents.

7. Event-Driven Posting Rules

كل document بعد ما يتعمله post لازم يولد أثر واضح.

Purchase Receipt

Generates:

stock ledger entries
shortage ledger rows if mismatch exists
supplier statement impact if applicable
Sales Invoice

Generates:

stock ledger out entries
customer statement debit
commission entries if eligible
Payment

Generates:

statement movement
payment allocations
Purchase Return

Generates:

stock reversal
supplier statement adjustment
possible shortage re-open logic
Sales Return

Generates:

stock reversal
customer statement credit
commission reversal
Shortage Resolution

Generates:

allocation rows
supplier statement adjustment if financial
stock movement if physical replacement affects stock
8. Technical Production Rules
8.1 Posting Strategy

Use explicit document status:

draft
posted
canceled

Only posted documents affect stock/balance/statements.

8.2 Immutability

Posted financial and stock rows should not be edited directly.
Corrections should happen via:

reversal
adjustment
return
cancellation flow
8.3 Auditability

Every critical row should store:

created_by
updated_by
created_at
updated_at
source_doc_type
source_doc_id
8.4 Validation

Need strict validation for:

negative allocations
over-allocation
stock below allowed threshold
missing UOM conversion
shortage resolution above open qty/value
duplicate posting
return qty greater than original qty
8.5 Idempotency

Posting APIs must protect against duplicate execution.
Important in:

invoice posting
payment posting
stock posting
commission generation
9. Recommended Coding Architecture

لو هتعمله coding من الصفر، فالأفضل تمشي كده:

Layers
API Layer
Application/Service Layer
Domain Layer
Repository/Data Access Layer
Background Jobs / Queue Layer
Reporting Layer
Suggested Services
PurchaseReceiptPostingService
ShortageDetectionService
ShortageResolutionService
StockPostingService
PaymentAllocationService
StatementBuilderService
CommissionCalculationService
SalesReturnService
PayrollCalculationService
Key Principle

Business logic should not live inside controllers or routes.
It should live inside services/domain logic.

10. What Changed from the Original Document

النسخة الأصلية كانت قوية جدًا في الفكرة العامة والـ shortage flow ، لكن النسخة دي زودت:

payment allocation
shortage reasons and accountability
stock ledger
strict UOM conversion rules
detailed commission engine
purchase/sales returns
shortage resolution allocation model
posting and immutability rules
auditability and traceability