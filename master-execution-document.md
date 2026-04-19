# ERP System – Master Execution Document (v1)

---

# 1. Project Mission

Build a **production-ready ERP web application** that manages:

* Procurement
* Inventory & Stock Ledger
* Supplier & Customer Statements
* Sales & Collections
* Payments & Allocations
* Shortage Control & Resolution
* Commission Engine
* HR & Payroll

---

# 2. Core Principles (Non-Negotiable)

1. **Server is the source of truth**
2. **Posting is always server-side**
3. **No direct edits on posted data**
4. **All effects must go through ledgers**
5. **Offline = Draft only**
6. **All critical operations must be idempotent**
7. **All financial and stock data must be auditable**

---

# 3. System Type

* Web Application (Responsive)
* Mobile accessible via browser
* Optional PWA support
* Centralized backend

---

# 4. Tech Stack

## Backend

* ASP.NET Core Web API
* SQL Server
* EF Core
* FluentValidation
* xUnit

## Frontend

* React + TypeScript
* React Query
* React Hook Form

## Offline

* IndexedDB (Drafts only)
* Service Worker (basic caching)

---

# 5. Development Strategy

## Approach

* Modular Monolith
* Domain-driven structure
* Feature-by-feature delivery
* End-to-end completion per module

## Rule

> Never start a module unless it can be finished end-to-end

---

# 6. Project Structure

```text
/src
  /backend
    /Api
    /Application
    /Domain
    /Infrastructure
  /frontend

/docs
  business-document.md
  architecture.md
  implementation-plan.md
  mvp-scope.md
  posting-matrix.md
  domain-model.md
  db-schema-v1.md
  api-spec-v1.md

AGENTS.md
README.md
```

---

# 7. Development Phases

---

## Phase 0 — Foundation

### Goal

Prepare working environment

### Tasks

* Setup repo
* Setup backend & frontend
* Setup DB connection
* Base migration
* Health check API
* Basic UI layout
* AGENTS.md creation

### Done When

* Backend runs
* Frontend runs
* DB connected
* First migration applied

---

## Phase 1 — Master Data

### Modules

* Suppliers
* Customers
* Employees
* Warehouses
* UOMs
* Items
* Item Components
* Shortage Reason Codes
* Commission Rules

### Done When

* CRUD APIs
* UI forms
* Validation rules
* Search & filter
* Audit fields implemented

---

## Phase 2 — Procurement & Inventory Core

### Modules

* Purchase Orders
* Purchase Receipts
* Receipt Components
* Stock Ledger
* Shortage Detection
* Shortage Ledger

### Services

* PurchaseReceiptPostingService
* StockLedgerService
* ShortageDetectionService

### Done When

* Draft → Post → Cancel works
* Stock ledger entries generated
* Shortage entries generated
* Validation enforced

---

## Phase 3 — Supplier Statement

### Modules

* Supplier Statement Entries
* Balance tracking

### Rules

* No manual entries
* Generated from documents only

### Done When

* Supplier balance correct
* Statement traceable to source docs

---

## Phase 4 — Sales Core

### Modules

* Sales Invoices
* Customer Statement Entries

### Services

* SalesInvoicePostingService

### Done When

* Sales posting creates:

  * stock out
  * customer debit

---

## Phase 5 — Payments & Allocation

### Modules

* Payments
* Payment Allocations

### Services

* PaymentPostingService
* PaymentAllocationService

### Done When

* Partial payments work
* Multiple allocations supported
* Over-allocation prevented

---

## Phase 6 — Returns

### Modules

* Purchase Returns
* Sales Returns

### Done When

* Stock reversed
* Balances corrected
* Linked to original documents

---

## Phase 7 — Shortage Resolution

### Modules

* Shortage Resolutions
* Allocation per shortage

### Done When

* FIFO allocation works
* Shortages close correctly
* Financial/physical handled

---

## Phase 8 — Commission Engine

### Modules

* Commission Rules
* Commission Entries

### Done When

* Commission generated on sales
* Reversed on returns
* Fully traceable

---

## Phase 9 — HR & Payroll

### Modules

* Employee Advances
* Salary Slips
* Payroll Runs

### Done When

* Salary calculation works
* Advances deducted automatically

---

## Phase 10 — Offline Draft Support

### Rules

* Offline = Draft only
* No posting offline

### Features

* Save Draft locally
* Pending drafts screen
* Manual submit

### Done When

* Draft survives refresh/restart
* Submit works after reconnect

---

## Phase 11 — Reporting

### Reports

* Supplier Statement
* Customer Statement
* Stock Ledger
* Stock Balance
* Shortage Report
* Payment Status
* Commission Report

### Done When

* Reports reflect posted data only

---

## Phase 12 — Production Hardening

### Tasks

* Role-based access
* Logging
* Error handling
* Performance tuning
* Backup strategy
* Deployment pipeline

### Done When

* System deployable
* Stable under load
* Secure

---

# 8. Posting Rules (Critical)

Each document must:

* Have status:

  * Draft
  * Posted
  * Canceled

* Generate:

  * Ledger entries
  * Statement entries
  * Allocation entries (if applicable)

* Be:

  * Idempotent
  * Immutable after posting

---

# 9. Offline Strategy

## Allowed Offline

* Draft creation
* Draft editing

## Not Allowed Offline

* Posting
* Stock updates
* Financial updates

## Sync Flow

* Store locally (IndexedDB)
* Show pending drafts
* User submits manually

---

# 10. Definition of Done (Global)

A feature is complete only if:

* Backend implemented
* Frontend implemented
* DB migration created
* Validation added
* Tests written
* Documentation updated
* No broken flows

---

# 11. Coding Rules

* No business logic in controllers
* Use services for posting
* Validate all inputs
* Never edit posted data directly
* Use clear naming
* Keep code simple and readable

---

# 12. Testing Strategy

* Unit tests for:

  * posting logic
  * allocation logic
  * shortage logic

* Integration tests for:

  * document flows

---

# 13. Execution Rule with Codex

Each task must include:

* Context files to read
* Clear requirements
* Expected outputs
* Definition of done

---

# 14. First 10 Tasks

1. Setup project structure
2. Setup backend
3. Setup frontend
4. Setup DB
5. Implement suppliers
6. Implement warehouses
7. Implement UOMs
8. Implement items
9. Implement item components
10. Implement purchase receipt draft

---

# 15. Final Goal

Deliver a system that is:

* Accurate
* Traceable
* Scalable
* Maintainable
* Production-ready

---

# End of Document

