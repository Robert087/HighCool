# AGENTS.md

## Project mission

Build a production-ready ERP web application for:

* procurement
* inventory and stock ledger
* supplier and customer statements
* sales and collections
* payments and allocations
* shortage control and resolution
* supplier commission tracking
* employee advances and payroll

## Core business rules

* The server is the source of truth.
* Posting is always server-side.
* Offline is draft-only.
* Posted documents are immutable.
* Corrections happen through cancel, reversal, return, or adjustment flows.
* Stock is derived from stock ledger entries only.
* Statements are generated from posted business documents only.
* No manual statement entries.
* All critical posting flows must be idempotent.
* All critical data must be auditable.

## Architecture rules

* Use a modular monolith structure.
* Keep business logic out of controllers.
* Put workflow logic in application/domain services.
* Keep entities focused on domain state and invariants.
* Use explicit document statuses: Draft, Posted, Canceled.
* Use append-only ledger patterns for stock, statements, shortage, and commission effects.
* Never silently overwrite posted business effects.

## Backend rules

* Stack: ASP.NET Core Web API + SQL Server + EF Core.
* Use FluentValidation for command/request validation.
* Use clear service classes for posting workflows.
* Every persistence change must include migrations.
* Add audit fields to critical tables.
* Protect posting endpoints from duplicate execution.

## Frontend rules

* Stack: React + TypeScript.
* Build responsive UI for desktop and mobile browsers.
* Offline support is limited to local draft storage only.
* Use IndexedDB for offline drafts.
* Do not implement offline posting.
* Show clear online/offline and pending-draft states.

## Data rules

* Every stock-affecting document must generate stock ledger rows.
* Every financial movement must be traceable to a source document.
* Allocation tables are mandatory where partial settlement exists.
* UOM conversion must be explicit and item-aware where needed.
* Returns must reverse effects proportionally and traceably.

## Quality rules

* Prefer simple, readable code over clever abstractions.
* Write unit tests for posting, allocation, shortage, and reversal logic.
* Add integration tests for end-to-end document flows.
* Update docs when schema or behavior changes.
* Do not change business rules unless explicitly instructed.

## Required docs to read before major implementation

* docs/business-document.md
* docs/architecture.md
* docs/master-execution-document.md
* docs/mvp-scope.md
* docs/posting-matrix.md
* docs/domain-model.md
* docs/db-schema-v1.md
* docs/api-spec-v1.md

## Task execution format

For each task:

1. Read relevant docs first.
2. State the implementation plan briefly.
3. Implement backend, frontend, DB, and tests as needed.
4. Verify build/test status.
5. Summarize changes clearly.

## Definition of done

A task is complete only when:

* code is implemented
* build passes
* migrations are added if needed
* tests are added/updated
* docs are updated if behavior/schema changed
* no existing flow is broken
