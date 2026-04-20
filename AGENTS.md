# AGENTS.md

## Project Mission

Build a production-ready ERP web application for:

* procurement
* inventory and stock ledger
* supplier and customer statements
* sales and collections
* payments and allocations
* shortage control and resolution
* supplier commission tracking
* employee advances and payroll

---

## Environment (Linux-First)

* Target OS: Linux (Ubuntu recommended)
* Development must be runnable via terminal (bash)
* Use forward slashes `/` for all paths
* Avoid Windows-specific tools or assumptions
* Prefer CLI workflows over GUI tools

---

## Core Business Rules

* The server is the source of truth
* Posting is always server-side
* Offline is draft-only
* Posted documents are immutable
* Corrections happen through cancel, reversal, return, or adjustment flows
* Stock is derived from stock ledger entries only
* Statements are generated from posted business documents only
* No manual statement entries
* All critical posting flows must be idempotent
* All critical data must be auditable

---

## Architecture Rules

* Use a modular monolith structure
* Keep business logic out of controllers
* Put workflow logic in application/domain services
* Keep entities focused on domain state and invariants
* Use explicit document statuses: Draft, Posted, Canceled
* Use append-only ledger patterns for:

  * stock
  * statements
  * shortage
  * commission
* Never overwrite posted business effects

---

## Backend Rules

* Stack: ASP.NET Core Web API + SQL Server + EF Core
* Use FluentValidation for request validation
* Use service classes for posting workflows
* Every persistence change must include migrations
* Add audit fields to critical tables
* Protect posting endpoints from duplicate execution (idempotency)

---

## Frontend Rules

* Stack: React + TypeScript
* Build responsive UI (desktop + mobile)
* Offline support = drafts only
* Use IndexedDB for local drafts
* Do NOT implement offline posting
* Show clear:

  * online/offline status
  * pending drafts state

---

## Data Rules

* Every stock-affecting document must generate stock ledger rows
* Every financial movement must be traceable to a source document
* Allocation tables are mandatory for partial settlement
* UOM conversion must be explicit and item-aware
* Returns must reverse effects proportionally and traceably

---

## Quality Rules

* Prefer simple and readable code
* Avoid over-engineering
* Write unit tests for:

  * posting logic
  * allocation logic
  * shortage logic
  * reversal logic
* Add integration tests for document flows
* Update docs when schema or behavior changes
* Do not change business rules unless explicitly instructed

---

## Required Docs Before Implementation

Always read before implementing major features:

* docs/business-document.md
* docs/architecture.md
* docs/master-execution-document.md
* docs/mvp-scope.md
* docs/posting-matrix.md
* docs/domain-model.md
* docs/db-schema-v1.md
* docs/api-spec-v1.md

---

## Task Execution Rules (Codex)

For each task:

1. Read relevant docs
2. Provide a short implementation plan
3. Implement:

   * backend
   * database changes
   * frontend (if required)
   * tests
4. Verify:

   * build passes
   * tests pass
5. Summarize changes clearly

---

## Product direction
This product must look like a modern B2B enterprise SaaS
UX should be clear, fast, structured, and premium
Avoid consumer-style playful UI
Avoid visual clutter and weak hierarchy
Design principles
Clean layout
Strong hierarchy
Consistent spacing
Reusable patterns
Accessibility-aware
Responsive
Server-friendly data UX
Mandatory patterns
All list pages use the same toolbar pattern
All tables support search, filters, pagination
All forms use labels above fields
All destructive actions require confirmation
All pages include loading/empty/error states
All status values use unified badges
All page headers follow one pattern
Technical rules
Reuse components before creating new ones
No inline styling unless unavoidable
Use shared tokens only
Keep components small and composable
Separate presentational and business logic when possible
UX rules
Search visible by default on list pages
Quick filters visible, advanced filters in popover/drawer
Pagination always at bottom right/consistent position
Selected rows count visible in bulk actions
No modal for long forms; use page or drawer
Validation inline, not only toast-based

## Development Commands (Linux)

### Backend

```bash
cd src/backend/ERP.Api
dotnet run
```

### Frontend

```bash
cd src/frontend
npm install
npm run dev
```

### Database (EF Core)

```bash
dotnet ef migrations add InitialCreate
dotnet ef database update
```

---

## Definition of Done

A task is complete only when:

* Code is implemented
* Build passes
* Migrations are included (if needed)
* Tests are added or updated
* Documentation is updated (if required)
* No existing flows are broken

---

## Final Rule

> The system must always favor correctness, traceability, and safety over speed or shortcuts.
