# ERP System – Master Execution Document (v2)

---

## 1. Project Mission

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

## 2. Core Principles (Non-Negotiable)

1. Server is the source of truth
2. Posting is always server-side
3. No direct edits on posted data
4. All effects must go through ledgers
5. Offline = Draft only
6. All critical operations must be idempotent
7. All financial and stock data must be auditable

---

## 3. System Architecture Summary

* Web Application (Responsive)
* Mobile via browser
* Optional PWA support
* Centralized backend

---

## 4. Tech Stack

### Backend

* ASP.NET Core Web API
* SQL Server
* EF Core
* FluentValidation
* xUnit

### Frontend

* React + TypeScript
* React Query
* React Hook Form

### Offline

* IndexedDB (Draft storage only)
* Service Worker (basic caching)

---

## 5. Development Strategy

### Approach

* Modular Monolith
* Domain-driven structure
* Feature-by-feature delivery
* End-to-end completion per module

### Execution Rule

> Never start a module unless it can be completed end-to-end (API + DB + UI + validation + tests)

---

## 6. Project Structure

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
  master-execution-document.md
  mvp-scope.md
  posting-matrix.md
  domain-model.md
  db-schema-v1.md
  api-spec-v1.md

AGENTS.md
README.md
```

---

## 7. Development Roadmap

---

### Phase 0 — Foundation

#### Goal

Prepare full working development environment

#### Tasks

* Initialize repo
* Setup backend & frontend
* Setup database connection
* Create base migration
* Add health check endpoint
* Create base UI shell
* Add AGENTS.md

#### Done When

* Backend runs
* Frontend runs
* DB connected
* First migration applied

---

### Phase 1 — Master Data

#### Modules

* Suppliers
* Customers
* Employees
* Warehouses
* UOMs
* Items
* Item Components
* Shortage Reason Codes
* Commission Rules

#### Done When

* CRUD APIs complete
* UI forms available
* Validation rules applied
* Search/filter implemented
* Audit fields present

---

### Phase 2 — Procurement & Inventory Core

#### Modules

* Purchase Orders
* Purchase Receipts
* Receipt Components
* Stock Ledger
* Shortage Detection
* Shortage Ledger

#### Core Services

* PurchaseReceiptPostingService
* StockLedgerService
* ShortageDetectionService

#### Done When

* Draft → Post → Cancel flow works
* Stock ledger entries generated correctly
* Shortage entries generated correctly
* Validation rules enforced

---

### Phase 3 — Supplier Statement

#### Modules

* Supplier Statement Entries
* Balance calculation

#### Rules

* No manual entries
* Generated only from documents

#### Done When

* Supplier balance accurate
* Statement traceable to source documents

---

### Phase 4 — Sales Core

#### Modules

* Sales Invoices
* Customer Statement Entries

#### Core Service

* SalesInvoicePostingService

#### Done When

* Posting creates:

  * Stock out entries
  * Customer debit entries

---

### Phase 5 — Payments & Allocation

#### Modules

* Payments
* Payment Allocations

#### Core Services

* PaymentPostingService
* PaymentAllocationService

#### Done When

* Partial payments supported
* Multi-allocation supported
* Over-allocation prevented

---

### Phase 6 — Returns

#### Modules

* Purchase Returns
* Sales Returns

#### Done When

* Stock reversed correctly
* Balances corrected
* Linked to original documents

---

### Phase 7 — Shortage Resolution

#### Modules

* Shortage Resolutions
* Resolution Allocations

#### Done When

* FIFO allocation works
* Shortages close correctly
* Physical and financial resolutions handled

---

### Phase 8 — Commission Engine

#### Modules

* Commission Rules
* Commission Entries

#### Done When

* Commission generated on eligible sales
* Reversed on returns
* Fully traceable

---

### Phase 9 — HR & Payroll

#### Modules

* Employee Advances
* Salary Slips
* Payroll Runs

#### Done When

* Salary calculation works
* Advances deducted automatically

---

### Phase 10 — Offline Draft Support

#### Rules

* Offline = Draft only
* No posting offline

#### Features

* Local draft storage (IndexedDB)
* Pending drafts screen
* Manual submission

#### Done When

* Draft survives refresh/restart
* Draft can be submitted after reconnect

---

### Phase 11 — Reporting

#### Reports

* Supplier Statement
* Customer Statement
* Stock Ledger
* Stock Balance
* Shortage Report
* Payment Status
* Commission Report

#### Done When

* Reports are accurate and based on posted data only

---

### Phase 12 — Production Hardening

#### Tasks

* Role-based access control
* Logging and monitoring
* Error handling
* Performance tuning
* Backup strategy
* Deployment pipeline

#### Done When

* System is deployable
* Stable under load
* Secure

---

## 8. Posting Rules (Critical)

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

## 9. Offline Strategy Summary

### Allowed Offline

* Draft creation
* Draft editing

### Not Allowed Offline

* Posting
* Stock updates
* Financial updates

### Sync Flow

* Store locally (IndexedDB)
* Show pending drafts
* User submits manually

---

## 10. Global Definition of Done

A feature is complete only if:

* Backend implemented
* Frontend implemented
* DB migration created
* Validation added
* Tests written
* Documentation updated
* No regression introduced

---

## 11. Coding Rules

* No business logic in controllers
* Use services for workflows and posting
* Validate all inputs
* Never edit posted data directly
* Use clear and explicit naming
* Prefer simplicity over abstraction

---

## 12. Testing Strategy

### Unit Tests

* Posting logic
* Allocation logic
* Shortage logic

### Integration Tests

* End-to-end document flows

---

## 13. Execution Rules with Codex

Each task must include:

* Context files (docs)
* Clear requirements
* Expected deliverables
* Definition of done

---

## 14. Initial Execution Tasks

1. Setup project structure
2. Setup backend
3. Setup frontend
4. Setup database
5. Implement suppliers
6. Implement warehouses
7. Implement UOMs
8. Implement items
9. Implement item components
10. Implement purchase receipt draft

---

## 15. Final Goal

Deliver a system that is:

* Accurate
* Traceable
* Scalable
* Maintainable
* Production-ready

---

# End of Document
