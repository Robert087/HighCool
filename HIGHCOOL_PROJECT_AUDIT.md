# HighCool Project Technical Discovery and Audit

Audit date: 2026-07-25  
Repository: `/root/HighCool`  
Branch: `main`  
Audit mode: static analysis plus safe, non-mutating command checks  

## Post-Audit Update - Desktop Foundation Batch 1

On 2026-07-25, Batch 1 addressed and locally verified the destructive SQLite startup finding by replacing implicit reset behavior with explicit Development-only reset configuration, central local storage path resolution, explicit database provider configuration, application database metadata, database health checks, and local-only verified SQLite backups. The verification pass confirmed backend build/tests, SQLite migration application, two-start persistence, invalid-schema preservation, backup integrity, backup endpoint authorization, frontend install/build/tests, and runtime smoke. See `HIGHCOOL_DESKTOP_FOUNDATION_BATCH1_RESULT.md`.

## Post-Audit Update - Desktop Foundation Batch 2

On 2026-07-25, Batch 2 added and backend-verified encrypted backup manifests, mandatory pre-upgrade local backup orchestration, upgrade/restore journals, restore preflight and restore execution service contracts, local retention, a desktop SQLite configuration profile, and startup diagnostics. See `HIGHCOOL_DESKTOP_FOUNDATION_BATCH2_RESULT.md`.

## Post-Audit Update - Desktop Foundation Batch 3

On 2026-07-25, Batch 3 added a separated Tauri desktop development shell, repeatable self-contained backend publish workflow, Desktop-profile loopback backend startup, token-protected startup diagnostics/shutdown endpoints, same-origin React static hosting through ASP.NET, minimal Tauri capabilities, and desktop build/tests. The Linux release build and non-interactive backend/static/API smoke checks passed, but the real interactive desktop-window smoke did not fully pass in the container graphics session and must be repeated on a normal desktop environment. See `HIGHCOOL_DESKTOP_FOUNDATION_BATCH3_RESULT.md`.

## Post-Audit Update - Desktop Foundation Batch 3.1

On 2026-07-25, Batch 3.1 fixed observed WSLg desktop-shell defects: readiness support-code extraction for chunked/plain responses, transient `HC-Unavailable` retry handling, app-exit backend cleanup, and Linux forced-termination parent-death cleanup for the backend child. Targeted WSLg checks verified startup-to-main transition, one loopback backend, same-origin React serving, auth-required behavior, authenticated API/ERP route access, single-instance process behavior, normal-close backend cleanup, restart persistence, and forced-termination orphan cleanup. The full interactive/devtools/external-navigation/protected-shutdown/startup-failure matrix still needs a normal desktop-session run. See `HIGHCOOL_DESKTOP_FOUNDATION_BATCH3_RESULT.md`.

## Post-Audit Update - Desktop Foundation Batch 3.2

On 2026-07-25, Batch 3.2 reran the automated backend/frontend/desktop regression suite and fixed the documented desktop Cargo verification command path by adding a `src/desktop` Cargo workspace and moving release profile settings to the workspace root. The available environment was WSL2/WSLg, so the required normal Linux desktop and Windows verification matrices remain unexecuted and Batch 3 is still not fully approved. See `HIGHCOOL_DESKTOP_FOUNDATION_BATCH3_RESULT.md`.

## Post-Audit Update - Pre-Commit Security Hardening

On 2026-07-25, pre-commit security hardening removed the committed JWT fallback secret, made JWT startup fail closed outside Development/Testing unless a valid non-placeholder secret is configured, preserved desktop app-data secret reuse with environment-variable delivery to the backend, removed the committed default SQL Server `sa` credential, and changed public password-reset/email-verification request endpoints to generic token-free responses backed by an internal delivery abstraction. Invitation delivery/log sanitization, real email integration, rate limits, and broader browser/session perimeter hardening remain open.

The final desktop installer/updater, restore UI, scheduled backups, Cloudflare R2/cloud backups, production Windows key provider, Windows packaging verification, and fully approved desktop-host restore/window/failure-state smoke remain incomplete.

## Evidence convention

- **Confirmed** means the behavior is directly supported by code, configuration, migrations, tests, or an executed command.
- **Interpretation** means the likely product impact is inferred from confirmed implementation details.
- No existing application file was modified. Only the three requested audit documents were created.
- Secrets were not copied into this report. Configuration is described by key name and behavior only.

## 1. Executive Summary

HighCool is a modular-monolith ERP application with an ASP.NET Core 8 minimal API, EF Core 8, SQL Server as the default database, SQLite for development, and a React 18/TypeScript/Vite frontend. The codebase is far beyond the “initial scaffold” described by the root `README.md`: it contains organization-aware identity, role permissions, procurement, receiving, returns, shortage resolution, inventory ledgers, supplier statements, supplier payments, reversals, and a bilingual shell.

The strongest area is the procurement posting model. Receipt, return, payment, shortage-resolution, and reversal workflows use application/infrastructure services, database transactions, append-only stock/statement ledgers, allocation records, immutable posted states, and a meaningful backend test suite. Organization query filters and server-side permission filters also provide a useful baseline.

The application is not production-ready. The most serious confirmed remaining findings are:

1. Invitation tokens are still not delivered through a safe user journey, and historical/current invitation audit payloads need sanitization review.
2. No real email provider, public app base URL, localized link templates, throttling, or frontend forgot/reset/verify/accept-invitation journey exists.
3. Tenant-scoped business keys such as supplier/customer/item/UOM codes and document numbers have globally unique indexes that omit `OrganizationId`, causing cross-tenant collisions and an existence side channel.
4. Organization setup and feature gates are explicitly bypassed in both API and UI. New organizations remain `SetupCompleted = false`, but all modules are exposed. Setup mutation endpoints require only membership, not an administrative permission.
5. Warehouse and branch scopes are modeled and administrable, but `EnsureWarehouseAccessAsync` is never called by a business workflow and branch access is never evaluated.
6. Financial rows can mix null/default and user-selected currencies, while balances and running balances are aggregated without currency partitioning or exchange-rate application.
7. Posting validates shared open quantities inside ordinary transactions but has no row version, serializable isolation, or locking strategy; concurrent posts can race over PO remaining quantity, returnable quantity, shortage quantity, or payment target balance.
8. Desktop shell foundation now exists and builds with Tauri, and Batch 3.1 fixed observed WSLg readiness/lifecycle defects, but final desktop packaging, full interactive desktop smoke approval, restore UI, scheduled/cloud backups, production Windows key protection, and production operations remain incomplete even though the local SQLite safety foundation includes encrypted backups, manifests, retention, restore service contracts, and upgrade journals.

Estimated completion:

- **Against the procurement/inventory slice currently represented in code:** approximately 70%.
- **Against the full ERP mission in `AGENTS.md`:** approximately 45%. Sales, customer statements/collections, commission tracking, employees, advances, payroll, robust offline drafts, deployment operations, and production identity delivery are absent.

The feature matrix contains 47 audited feature rows: 0 `Complete`, 21 `Mostly Complete`, 8 `Partially Implemented`, 5 `Broken`, 3 `Placeholder`, 7 `Not Implemented`, and 3 `Unused`. “Complete” is intentionally strict: a page plus API is not marked complete while material production, security, isolation, localization, or test gaps remain.

## 2. Technology Stack

| Layer | Confirmed technology | Version/evidence | Notes |
|---|---|---|---|
| Backend runtime | ASP.NET Core minimal API | `net8.0`; `src/backend/Api/ERP.Api.csproj` | Endpoints are mapped in `Program.cs`; no MVC controllers. |
| ORM | Entity Framework Core | 8.0.4 | SQL Server and SQLite providers; migrations live in Infrastructure. |
| Production/default DB | SQL Server | `UseSqlServer` in `Infrastructure/DependencyInjection.cs` | Default connection comes from `ConnectionStrings:DefaultConnection`. |
| Development DB | SQLite | EF Core SQLite 8.0.4 | `appsettings.Development.json`; startup initializer calls `EnsureCreated`. |
| Backend validation | FluentValidation | 11.11.0 | Applied to master data and operational documents, but not identity/settings DTOs. |
| Authentication | JWT bearer + ASP.NET password hasher | JWT bearer 8.0.4 | Stateful server session row is checked on every validated token. |
| Frontend | React / React DOM | 18.3.1 | Functional components and hooks. |
| Routing | React Router DOM | 6.28.0 | Central route table in `AppRoutes.tsx`. |
| Language/build | TypeScript / Vite | TS 5.6.2; Vite 5.4.10 | Strict TypeScript; Vite dev proxy targets port 5080. |
| Backend tests | xUnit | 2.9.0 | 91 `[Fact]`/`[Theory]` occurrences; service and WebApplicationFactory tests. |
| Frontend tests | Vitest | 4.1.5 declared | 11 test cases in 3 files. No React Testing Library dependency. |
| Package manager | npm | lockfile version used by npm | Node 22.23.1 and npm 10.9.8 were available during audit. |
| UI styling | Repository-native CSS/tokens | `design/tokens.css`, `styles/ui.css`, `styles/global.css` | No component framework dependency. |
| Localization | Custom context/dictionaries | `i18n/*`, 2,874-line `messages.ts` | English/Arabic dictionaries and document direction support. |

No Redux, React Query, form library, OpenAPI client generator, background job framework, cache server, message bus, email provider, object storage provider, telemetry SDK, Docker configuration, reverse-proxy configuration, or CI/CD workflow was found.

## 3. Architecture Overview

### Backend

The backend is a layered modular monolith:

```text
HTTP request
  -> ASP.NET minimal endpoint
  -> JWT authentication and session validation
  -> organization/setup/feature filter (currently bypassed)
  -> permission filter
  -> FluentValidation where wired
  -> application interface
  -> infrastructure workflow/query service
  -> EF Core AppDbContext
  -> SQL Server or SQLite
```

- `Domain` contains entities, enums, document states, and ledger types.
- `Application` contains DTOs, request records, validators, interfaces, permission constants, and pagination contracts.
- `Infrastructure` implements services, query models, posting/reversal flows, authentication, authorization, and persistence.
- `Api` performs route binding and HTTP result mapping.

This generally follows the documented rule that controllers/endpoints remain thin. Important exceptions are repeated validation/error translation in endpoint files and the 1,681-line `OrganizationAdministrationService`.

### Frontend

```text
main.tsx
  -> BrowserRouter
  -> App
     -> I18nProvider
     -> AuthProvider
     -> FeatureConfigurationProvider
     -> ToastProvider
     -> AppShell
     -> AppRoutes / RouteGate
     -> page
     -> typed service function
     -> requestJson(fetch)
     -> /api through Vite proxy or VITE_API_BASE_URL
```

State management is local React state plus Context for authentication, features, localization, toast, and confirmation. There is no general query cache; master-data option lists and the dashboard use module-level caches. API calls use `fetch` directly and do not use request cancellation.

### Document and ledger model

- Operational documents use `Draft`, `Posted`, and `Canceled`.
- Purchase receipts, returns, payments, and shortage resolutions post inside EF transactions.
- Stock and supplier statements are append-only at `AppDbContext.SaveChanges` level.
- Reversals create `DocumentReversal` rows and opposite ledger effects.
- Partial settlement is represented by `PaymentAllocation` and `ShortageResolutionAllocation`.
- Organization-wide EF query filters apply to all `IOrganizationScopedEntity` types.

## 4. Repository Structure

| Path | Purpose and status |
|---|---|
| `AGENTS.md` | Current product mission, architecture, quality, localization, performance, and definition-of-done rules. Authoritative for this audit. |
| `README.md` | Setup notes, but materially stale: it says no business logic/data models exist and contains obsolete absolute links. |
| `docs/` | Business, architecture, schema, API, posting, MVP, and UI documents. Useful design intent, but `mvp-scope.md` is stale and conflicts with implemented modules. |
| Root `architecture.md`, `business-document.md`, `master-execution-document.md` | Older/non-identical duplicates of documents under `docs/`; creates documentation ambiguity. |
| `run.sh` | Starts API and Vite concurrently, kills child processes on exit. Assumes dependencies are already installed. |
| `.config/dotnet-tools.json` | Pins `dotnet-ef` 8.0.4. |
| `src/backend/ERP.sln` | Backend solution. |
| `src/backend/Api/` | API entry point, endpoint groups, CORS, JSON enum conversion, health endpoint, launch profile, appsettings. |
| `src/backend/Application/` | Contracts, DTOs, validators, permissions, pagination, service interfaces. Complete as a layer but several request families lack validators. |
| `src/backend/Domain/` | Identity, master-data, purchasing, inventory, shortage, payment, statement, and reversal entities. Active. |
| `src/backend/Infrastructure/` | EF Core, migrations, security, master-data services, query services, posting/reversal services, development DB initialization. Active; contains the largest and highest-risk logic. |
| `src/backend/tests/ERP.Application.Tests/` | Unit/service tests and HTTP integration tests using SQLite/InMemory and `WebApplicationFactory`. Active. |
| `src/frontend/src/app/` | Root component/provider composition. |
| `src/frontend/src/routes/` | Route definitions and auth/permission/feature gates. Feature/setup checks are compiled off through temporary constants. |
| `src/frontend/src/features/auth/` | Auth context, token storage, feature configuration context. |
| `src/frontend/src/services/` | Handwritten API types/builders, dashboard aggregation, permission constants, presentation helpers. |
| `src/frontend/src/pages/` | Master data, procurement, inventory, shortages, finance, auth, workspace, setup, and settings screens. Several settings screens are unreachable. |
| `src/frontend/src/components/` | Shared UI primitives, table/filter/page patterns, master-data-specific and item-specific components. |
| `src/frontend/src/i18n/` | English/Arabic messages, runtime translator, provider, formatters. Active, but many screens still contain hardcoded English. |
| `src/frontend/src/design/`, `styles/` | Tokens and global/component/page CSS. Large CSS files, no CSS module isolation. |
| `src/frontend/index.html`, `vite.config.ts`, `tsconfig*.json` | Frontend entry/build configuration and dev API proxy. |

No `public/` directory, E2E directory, Docker files, `.env.example`, CI workflow, deployment manifest, monitoring configuration, or backup scripts were found.

## 5. Application Startup Flow

### Development

1. `run.sh` starts `dotnet run` in `src/backend/Api` and `npm run dev -- --host 0.0.0.0` in `src/frontend`.
2. ASP.NET reads `appsettings.json`, then the Development override.
3. `AddInfrastructure` selects SQLite when `DatabaseProvider=Sqlite`.
4. `DevelopmentDatabaseInitializer` runs only for Development + SQLite, checks schema, calls `EnsureCreated`, and seeds organization-scoped demo master data.
5. API listens on `http://localhost:5080` from `launchSettings.json`.
6. Vite listens on `0.0.0.0:5173` and proxies `/api` to the API.
7. React mounts providers, checks `hc-access-token` in local storage, calls `/api/auth/me`, and renders routes.

### Production

- `dotnet run`/published ASP.NET is the only server startup implementation found.
- The default provider is SQL Server and requires `ConnectionStrings:DefaultConnection`.
- The frontend builds static assets with `npm run build`; no production static host or ASP.NET SPA integration is configured.
- CORS permits only two localhost origins. A production deployment therefore needs same-origin hosting or a code/configuration change.
- Database migrations are not automatically applied in production.

### Startup risk

`DevelopmentDatabaseInitializer.IsPartiallyInitializedAsync` treats “has user tables but lacks `__EFMigrationsHistory`” as partial. `EnsureCreated` normally creates tables without migration history. On the next development startup, `ResetSqliteDatabaseFileAsync` deletes that database. The existing tests verify rebuild behavior but do not verify persistence across two normal initializer starts.

## 6. Authentication and Authorization

### Confirmed login/session flow

1. `POST /api/auth/login` normalizes email and loads the global `UserAccount`.
2. ASP.NET `PasswordHasher<UserAccount>` verifies the password.
3. Failed attempts increment; reaching the organization-derived limit locks the account for 30 minutes.
4. The first active organization membership is selected.
5. A random session secret is hashed into `UserSession`; the raw session secret is not returned or used later.
6. A JWT contains user, organization, membership, and session IDs.
7. Frontend stores the JWT in `localStorage` under `hc-access-token`.
8. Each authenticated request validates JWT signature/lifetime and then checks the server-side session, membership, and user state.
9. `/api/auth/me` builds workspace, roles, permissions, and organization options.
10. Logout revokes either the current session or all user sessions.

This hybrid JWT/session validation is a sound revocation baseline. Password hashing, token hashing, failed-login tracking, and active membership checks are implemented.

### Permission evaluation

- Backend permission enforcement uses `PermissionEndpointFilter` for business endpoints and internal `EnsurePermissionAsync` checks for settings services.
- Owners bypass permission checks using `OrganizationMembership.IsOwner`.
- Permission dependencies are expanded (for example `post` implies `view`).
- Frontend `RouteGate` checks workspace permission strings; it infers owner from a role named “owner.”
- Most mutation buttons are not permission-aware. Form routes require only view permissions, so users with view-only access can see save/post/reverse controls and receive backend 403 responses.
- With `DISABLE_FEATURE_GATING = true`, the sidebar returns all navigation items before permission filtering, so unauthorized modules are deliberately shown even though route/API guards eventually deny them.

### Security gaps

| Severity | Confirmed issue | Evidence/impact |
|---|---|---|
| Critical | Invitation raw token is persisted in audit JSON. | `OrganizationAdministrationService.InviteUserAsync` passes `{ token = rawToken }` to `IAuditLogService`. No invitation email is sent. |
| Critical | Setup mutations require only organization membership. | `SaveSetupAsync` and `CompleteSetupAsync` call `EnsureOrganizationAccessAsync`, not an admin permission. They can change organization, feature, workflow, stock, and security configuration. |
| High | Two-factor/OTP configuration is cosmetic. | `ForceTwoFactor`, `EnableEmailOtp`, and `requiresTwoFactor` exist, but login still returns a usable JWT and there is no challenge/verify endpoint. |
| High | Recovery/verification delivery is still not production-complete. | Public reset/verification request endpoints now return generic token-free responses and deliver tokens through `IAuthMessageDeliveryService`, but the default implementation is no-op until an email provider/link flow is selected. |
| High | JWT in `localStorage` is exposed to any successful XSS. | `authStorage.ts`. No HttpOnly/Secure cookie alternative or CSP configuration was found. |
| High | No rate limiter exists on login, reset, verification, signup, or invitation acceptance. | `Program.cs` has no rate-limiting middleware. Lockout is account-based only. |
| Medium | No global session-expiry redirect. | `requestJson` clears local storage on 401 but `AuthProvider.workspace` remains populated until reload/logout. |
| Medium | No HTTPS redirection/HSTS/forwarded-header setup is present. | `Program.cs`. Deployment could supply this externally, but none is in the repository. |
| Medium | Identity/settings requests lack FluentValidation. | They use manual checks with inconsistent response shapes and no centralized validation. |
| Resolved on 2026-07-25 | JWT signing secret fallback removed. | `JwtSigningConfiguration` now rejects missing/short/placeholder secrets outside Development/Testing; Development/Testing generate a local key outside the repo when omitted; Desktop receives a generated app-data key through environment variables. |
| Resolved on 2026-07-25 | Password-reset/email-verification request endpoints no longer return raw tokens or reveal account existence. | `IdentityEndpoints` returns generic `AuthRequestAcceptedResponse`; `IdentityService` sends the generated token only through `IAuthMessageDeliveryService`; identity tests assert public payloads are token-free and token consumption still works through test delivery. |
| Resolved on 2026-07-25 | Default committed SQL Server credential removed. | `appsettings.json` and the design-time context factory no longer contain a default `sa` password connection string. |

## 7. Organization and Data Isolation

### What works

- Every business aggregate/ledger/master entity inspected implements `IOrganizationScopedEntity` directly or through `OrganizationScopedAuditableEntity`/`BusinessDocument`.
- `AppDbContext` dynamically applies an organization query filter.
- New organization-scoped entities receive the request organization ID during `SaveChanges`.
- JWT validation binds user, organization, membership, and session.
- Most deliberate `IgnoreQueryFilters` calls in identity/settings code include an explicit organization or user predicate.

### Critical isolation and scoping problems

1. **Global uniqueness across tenants.** EF configurations use globally unique indexes for `Customer.Code`, `Supplier.Code`, `Warehouse.Code`, `Uom.Code`, `Item.Code`, `ShortageReasonCode.Code`, UOM conversion tuples, and every document number. They must include `OrganizationId`. Today, two organizations cannot independently use common codes such as `PCS`, `MAIN`, or `PO-...`; a conflict response can also reveal that another tenant owns a value.
2. **Warehouse access is not enforced.** `AuthorizationService.EnsureWarehouseAccessAsync` exists but has no caller outside its declaration. Warehouse-restricted memberships can list stock across all warehouses and create/post receipts/returns against any warehouse.
3. **Branch access is metadata only.** Branch scopes can be configured and copied from invitations, but no business entity/query/workflow evaluates them.
4. **Feature isolation is disabled.** `OrganizationSetupEndpointFilter` loads the organization and intentionally ignores `_requireCompletedSetup` and `_requiredFeatures`. The frontend does the same through constants.
5. **Seed data belongs to `Guid.Empty`.** Development seeding runs under system context and creates organization-scoped records without an organization ID. Authenticated organizations cannot see those rows through normal filters.
6. **No composite organization foreign keys.** Database FKs use entity IDs only. Service/query filters normally prevent cross-organization references, but the database cannot enforce that parent and child organization IDs match.

## 8. Module-by-Module Analysis

### Identity and workspace

- Main files: `IdentityEndpoints.cs`, `IdentityService.cs`, `JwtTokenService.cs`, `AuthProvider.tsx`, `authApi.ts`.
- Implemented: signup, login, logout, current workspace, organization switch, server sessions, lockout, password reset primitives, verification primitives, invitation acceptance.
- Partial/broken: email delivery provider absent; reset/verification request endpoints are token-free but delivery is no-op outside tests; no frontend reset, forgot-password, verify, or accept-invitation routes; 2FA not enforced.
- Tests: `IdentityApiTests.cs`, `authApi.test.ts`.

### Organization setup and settings

- Main files: `OrganizationSecurityEndpoints.cs`, `OrganizationAdministrationService.cs`, `SetupOrganizationPage.tsx`, `settingsApi.ts`, settings pages.
- Backend includes organization, workflow, feature, stock, security, user, role, invitation, session, audit, and profile APIs.
- Only `/settings/users` and `/settings/roles` are routed. Organization, profiles, invitations, security, sessions, audit log, and features routes redirect to `/workspace`; corresponding page files are unused.
- The setup screen exists but `/setup/organization` redirects to workspace.
- Setup/feature gating is disabled at API and UI levels.

### Master data

- Customers: create/read/update, activate/deactivate.
- Suppliers: create/read/update/deactivate; no reactivate endpoint.
- Warehouses, UOMs, UOM conversions, items/BOM: create/read/update/deactivate.
- Validated server-side with FluentValidation.
- List APIs are unbounded arrays; pages filter on server but paginate in browser. Item list loads full component graphs.
- New/edit routes use view permission rather than create/edit permission, and controls are not hidden by action permission.
- No delete flow; deactivation is consistent with master-data safety.
- Tests cover validators, APIs, and item rules, but frontend coverage is absent.

### Purchase orders

- Routes: `/purchase-orders`, `/purchase-orders/new`, `/purchase-orders/:id/edit` plus procurement alias.
- API: paginated list/detail, available receipt lines, draft create/update, post, cancel.
- Server validates supplier, items/UOMs, quantities/prices, draft mutability, receipt dependency on cancel, and remaining receipt progress.
- UI supports create/edit/post/cancel and “create receipt from PO.”
- “Delete” is a deliberate placeholder toast; no delete API exists despite delete permission constants.
- Document number generation ignores configured organization prefixes and uses a timestamp.
- Posting/canceling does not populate `PostedAt`, `PostedBy`, `CanceledAt`, or `CanceledBy`.

### Purchase receipts

- Routes: list/new/edit; supports PO-linked and manual receipts.
- API: paginated list/detail, draft create/update, post, reverse.
- Posting transaction creates stock ledger, optional supplier statement effect, BOM-derived shortages, and status change.
- Over-receipt and supplier/PO consistency validation exist.
- UI supports line/component entry, PO prefill, post, and reverse.
- Delete action is a placeholder.
- Concurrent receipts can both validate the same remaining PO quantity before commit.
- Receipt statement currency is stored as null, and stock receipt costs remain null.

### Purchase returns

- Routes and full draft/detail/post UI exist.
- Posting creates stock OUT and a proportional supplier statement reduction when a receipt financial basis exists.
- Remaining returnable quantity and duplicate receipt-line validation exist.
- No reversal endpoint for a posted purchase return, although `PurchaseReturn` has reversal fields and the correction rule is otherwise reversal-based.
- Concurrent returns can race against the same remaining quantity.

### Shortages

- Receipt posting detects shortages from BOM expected-versus-actual component quantities.
- Open shortage list/detail is paginated and read-only.
- Physical/financial resolution drafts, FIFO suggestions, posting allocations, statement/stock effects, and reversal exist.
- Shortage reason codes have only an active lookup endpoint; no administration UI/API.
- Concurrent resolutions can validate and settle the same open quantity.

### Inventory

- Append-only stock ledger and derived stock balance endpoints are paginated/filterable/sortable.
- UI has stock movement and stock balance screens with filters, loading, error, empty, and pagination states.
- SQLite balance calculation loads all filtered ledger rows into memory before grouping.
- No stock adjustment, transfer, opening balance, count, reservation, batch, serial, or expiry workflow exists despite organization flags/permissions.
- Negative-stock configuration is not enforced by posting flows.

### Supplier statements and payments

- Supplier statement list, supplier-specific list, and summary are read-only and document-derived.
- Supplier payments use mandatory allocations to receipt payables or financial shortage receivables.
- Payment posting and reversals create traceable statement rows.
- Returns reduce open receipt targets before allocation.
- Multi-currency behavior is unsafe: receipt/return statement currency may be null, payment/resolution currency is user-set, exchange rate is stored but not applied, and summaries/running balances are not partitioned by currency.
- Payment and shortage form defaults hardcode EGP rather than organization currency.

### Dashboard and shell

- Dashboard has no dedicated server summary endpoint. `dashboardApi.ts` fans out to twelve list endpoints.
- It requests `pageSize=1` for statement and shortage lists, then sums only the first returned row, so displayed quantities/debits/credits are wrong whenever more than one row matches.
- It requests 250 stock-balance rows, but the server clamps page size to 100, so negative-stock count is incomplete beyond the first 100 balances.
- Supplier issue computation downloads the complete unbounded supplier list.
- Notifications are a static `enterpriseNotifications` array, “AI active” is equivalent to browser online state, pending drafts are always shown as zero, and global search only searches a static navigation index.

### Localization and RTL

- The root provider sets locale and direction, and shared format helpers exist.
- English and Arabic dictionaries are extensive.
- Many operational/master-data screens still ship visible English literals in headings, filters, table columns, validation messages, toasts, placeholders, status badges, and form buttons. Examples include `SuppliersPage.tsx`, `PurchaseOrderFormPage.tsx`, `PurchaseReceiptFormPage.tsx`, `PaymentFormPage.tsx`, and `ShortageResolutionFormPage.tsx`.
- `translateText` can translate keys passed through shared primitives, but literal English remains English.
- No automated RTL/localization key completeness test exists.

## 9. Implemented Features

The following are genuinely end-to-end enough to be considered implemented, though not production-complete:

- Server-session-backed JWT login/logout/current workspace and organization switching.
- Organization EF query filters and role-based endpoint permissions.
- Customers, suppliers, warehouses, UOMs, UOM conversions, and item/BOM maintenance.
- Purchase-order draft/post/cancel and receipt-progress calculation.
- Manual and PO-linked purchase-receipt draft/post workflow.
- BOM-based shortage detection.
- Open-shortage queries and physical/financial shortage resolution with allocations.
- Append-only stock ledger and stock-balance reporting.
- Append-only supplier statements and supplier balance summaries.
- Supplier payments with partial/multi-target allocations.
- Purchase returns with stock and supplier financial effects.
- Receipt, payment, and shortage-resolution reversal workflows.
- Paginated/filterable/sortable operational list APIs.
- Responsive shared page/table/filter/form components.
- English/Arabic application infrastructure and locale-aware formatting helpers.

Production-readiness qualifiers for each feature are recorded in `HIGHCOOL_FEATURE_MATRIX.md`.

## 10. Partially Implemented Features

- Organization setup wizard and feature configuration: data model/API/UI exist, but routing and enforcement are bypassed.
- Settings: complete backend surface, but most screens are redirected/unreachable.
- User invitations: creation/acceptance primitives exist, but no mail delivery or usable frontend acceptance flow.
- Email verification/password recovery: token tables and services exist, public request endpoints are token-free, but production delivery and frontend flows are missing.
- 2FA/email OTP: flags only.
- Warehouse/branch scope: persistence/admin only; no operational enforcement.
- Dashboard: visually complete, but aggregates are client-composed and several are numerically wrong.
- Offline indicator: online/offline display exists, but IndexedDB draft persistence and pending-draft tracking do not.
- Master-data pagination: cosmetic client pagination over unbounded API results.
- Multi-currency: fields exist without safe ledger/accounting semantics.
- Audit metadata: generic create/update fields work, but posting/cancel audit fields are never populated.
- Organization prefixes: configurable but ignored by document generators.

## 11. Missing Features

Relative to the full mission and existing flags/contracts:

- Sales orders/invoices, sales posting, customer receivables, customer statements, collections, and customer payment allocation.
- Quotes, CRM, documents/files, email, and real notifications.
- Supplier commission tracking.
- Employee directory, advances, payroll, and payroll posting.
- Stock adjustments, transfers, counts, opening balances, reservations, batch/serial/expiry tracking.
- Purchase-return reversal/cancel correction flow.
- Shortage reason-code administration.
- Offline IndexedDB draft store, draft conflict handling, and pending draft queue.
- Two-factor challenge/verification and OTP delivery.
- Email delivery for invitations, reset, and verification.
- Dedicated dashboard/report summary endpoints.
- Production static hosting, proxy, Docker, CI/CD, secrets strategy, monitoring, structured operational logging, backup/restore, and runbook.
- E2E tests, accessibility tests, RTL visual tests, concurrency tests, SQL Server integration tests, load/performance tests.

## 12. API Inventory

All business/settings endpoints require JWT unless explicitly marked public. Business endpoints also use permission filters; organization settings enforce permissions inside `OrganizationAdministrationService`, except setup status/get/save/complete and feature configuration, which require membership only. Bodies are JSON records from `Application`; responses are DTO JSON with string enums. Errors are generally 400/403/404/409, but no global exception contract exists.

| Area | Method and URL | Request/response and caller | Status/notes |
|---|---|---|---|
| Service | `GET /`; `GET /health` | Anonymous; application metadata/plain `OK`. | Used for diagnostics; no dependency health checks. |
| Auth (public) | `POST /api/auth/signup`; `login`; `forgot-password`; `reset-password`; `request-email-verification`; `verify-email`; `accept-invitation` | Auth request DTOs -> auth response/no content/generic acceptance. Frontend uses signup/login/verify only. | Reset/verification/invitation UI incomplete; reset/verification request responses are generic and token-free, but delivery provider is not implemented. |
| Auth (protected) | `POST /api/auth/logout`; `GET /api/auth/me`; `POST /api/auth/switch-organization` | Bearer JWT; frontend `authApi.ts` uses all three. | Active server session checked for each request. |
| Customers | `GET/POST /api/customers`; `GET/PUT /api/customers/{id}`; `POST .../{id}/activate`; `POST .../{id}/deactivate` | Search/isActive list; create/update DTO; frontend master-data pages/forms use all. | Unbounded list; validation exists. |
| Suppliers | `GET/POST /api/suppliers`; `GET/PUT /api/suppliers/{id}`; `POST .../{id}/deactivate` | Search/isActive; supplier DTO; pages/forms and selectors use all. | No activate endpoint; unbounded list. |
| Warehouses | `GET/POST /api/warehouses`; `GET/PUT /api/warehouses/{id}`; `POST .../{id}/deactivate` | Search/isActive; pages/forms/selectors use all. | Unbounded; warehouse access scope not applied. |
| UOMs | `GET/POST /api/uoms`; `GET/PUT /api/uoms/{id}`; `POST .../{id}/deactivate` | Search/isActive; pages/forms/selectors use all. | Unbounded. |
| UOM conversions | `GET/POST /api/uom-conversions`; `GET/PUT /api/uom-conversions/{id}`; `POST .../{id}/deactivate` | Search/isActive; pages/forms and quantity conversion use them. | Unbounded; globally unique tenant tuple. |
| Items | `GET/POST /api/items`; `GET/PUT /api/items/{id}`; `POST .../{id}/deactivate` | Search/isActive; nested component DTOs; item pages/forms/selectors. | List loads full BOM graphs; unbounded. |
| Purchase orders | `GET/POST /api/purchase-orders`; `GET/PUT /api/purchase-orders/{id}`; `GET .../{id}/available-lines-for-receipt`; `POST .../{id}/post`; `POST .../{id}/cancel` | Paginated list/detail/upsert; used by PO list/form and receipt form. | Mapping appears consistent. No delete API. |
| Purchase receipts | `GET/POST /api/purchase-receipts`; `GET/PUT /api/purchase-receipts/{id}`; `POST .../{id}/post`; `POST .../{id}/reverse` | Paginated list/detail/upsert/reversal; used by receipt pages, return form, dashboard. | Posting/reversal implemented. No delete. |
| Purchase returns | `GET/POST /api/purchase-returns`; `GET/PUT /api/purchase-returns/{id}`; `POST .../{id}/post` | Paginated list/detail/upsert; used by return pages/dashboard. | No reverse/cancel endpoint. |
| Shortage reasons | `GET /api/shortage-reason-codes` | Active array; used by receipt form. | Lookup only; no administration. |
| Shortages | `GET /api/shortages/open`; `GET /api/shortages/{id}` | Paginated filters/detail; open-shortage and resolution form use them. | Correct ID mapping appears consistent. |
| Shortage resolutions | `GET/POST /api/shortage-resolutions`; `GET/PUT .../{id}`; `GET .../{id}/allocations`; `POST .../{id}/post`; `POST .../suggest-allocations`; `POST .../{id}/reverse` | Paginated list, nested detail/allocations, upsert, FIFO suggestion, reversal. | Full UI integration. Concurrent open-quantity race remains. |
| Payments | `GET/POST /api/payments`; `GET/PUT .../{id}`; `GET .../{id}/allocations`; `POST .../{id}/post`; `POST .../{id}/reverse` | Paginated list/detail/upsert and allocations; payment pages use all. | Full UI integration. Concurrent target allocation race remains. |
| Open balances | `GET /api/suppliers/{supplierId}/open-balances?direction=...` | Paginated targets; payment form uses it. | Direction/target mapping consistent. |
| Supplier statements | `GET /api/supplier-statements`; `GET /api/suppliers/{supplierId}/statement`; `GET .../statement/summary` | Paginated entries and summary; statement page/dashboard use them. | Read-only as required; multi-currency unsafe. |
| Inventory | `GET /api/stock-ledger`; `GET /api/stock-ledger/item/{itemId}`; `GET /api/stock-balance`; `GET /api/stock-balance/item/{itemId}` | Paginated/filterable DTOs; stock pages/dashboard. | Item-specific functions exist in API but frontend uses general functions only. |
| Setup/settings | `GET /api/settings/organization/setup-status`; `GET/PUT .../organization/setup`; `POST .../organization/setup/complete`; `GET/PUT .../organization`; `GET/PUT .../security`; `GET .../features` | Setup/settings DTOs. Setup page and feature provider call these; most dedicated settings pages are unrouted. | Setup mutation authorization is insufficient; gates disabled. |
| Users | `GET /api/settings/users`; `POST .../users/{membershipId}/suspend|activate`; `PUT .../roles`; `PUT .../users/{membershipId}`; `POST .../users/transfer-ownership` | Paginated user/invite union and access mutation DTOs. Routed users page uses list/invite/status/single-role change only. | Full access update and ownership transfer have no current UI. |
| Roles | `GET/POST /api/settings/roles`; `PUT .../roles/{roleId}`; `POST .../{id}/clone|activate|deactivate`; `GET .../permissions/matrix` | Roles/matrix. Routed roles page lists and updates permission sets. | Create/clone/activate/deactivate APIs are not integrated in current page. |
| Invitations | `GET/POST /api/settings/invitations`; `POST .../{id}/revoke` | Invitation DTO array. Users page creates invites; unused invitation page lists only. | Revoke API unused; no delivery. |
| Sessions/audit/profiles | `GET /api/settings/sessions`; `GET .../audit-log`; `GET/POST .../profiles`; `PUT .../profiles/{id}` | Arrays; audit query accepts filters/page. Dedicated pages are redirected. | Audit API has no max page size or paged envelope; mutation profile APIs unused. |

### Important data-flow traces

**PO-linked receipt:** form loads active references and posted PO candidate -> fetches available lines/details -> saves draft -> server re-resolves supplier/PO/line/UOM and calculates payable -> post transaction validates remaining quantity -> writes stock/statement/shortage rows -> returns detail -> UI switches to posted/read-only.

**Supplier payment:** form selects supplier/direction -> loads open targets -> user creates mandatory allocations -> server validates target type, supplier, open balance, and total -> transaction writes one statement row per allocation and posts document -> target state is subsequently derived from posted allocations.

**Shortage settlement:** form queries open shortages/suggestions -> saves resolution/allocation rows -> post reloads current shortages -> validates open quantity -> applies physical stock or financial statement effect -> updates shortage open/resolved fields -> posts document.

**Reversal:** UI captures date/reason -> endpoint validates request/permission -> reversal service checks downstream dependencies -> transaction creates reversal audit document, opposite ledger effects, and original-document reversal linkage -> returns reversal DTO.

### Cross-contract and identifier findings

- Entity/document/line/allocation IDs are consistently GUIDs serialized as strings. Target polymorphism is explicit through `targetDocType`/`targetDocId`/optional `targetLineId`; no competing “object ID” convention was found.
- Tenant selection is never sent as a business query parameter; it comes from JWT claims and EF query filters, which is the correct trust boundary.
- Create/update mappings generally send DTO-shaped JSON and server services re-resolve referenced IDs. Optional linkage uses null/undefined consistently enough in the major forms.
- Delete is intentionally absent for posted business documents and master data uses deactivation. PO/receipt UIs nevertheless show fake draft-delete actions.
- Operational date filters are inconsistently constructed: inventory converts local input boundaries to UTC ISO strings, while several other service builders send date-only strings. A single date/timezone contract is needed around the organization timezone.
- Backend inventory enums have drifted from `inventoryApi.ts`: `PurchaseReturn`, `ShortageResolutionReversal`, and `DocumentReversal` are missing from frontend unions. Unknown stock transaction values fall through to the “shortage physical resolution” label.
- API JSON is cast directly to generic `T`; there is no runtime schema validation. Contract drift therefore compiles and fails/mislabels only at runtime.
- Hardcoded identifiers/defaults include document prefixes (`PO`, `PR`, `RTN`, `PAY`, `REV`), EGP defaults in finance forms/dashboard, localhost API fallback URLs, and a zero pending-draft count.

## 13. Database and Data Models

### Main entity groups

| Group | Entities/tables |
|---|---|
| Identity | `organizations`, `organization_security_settings`, `user_accounts`, `organization_memberships`, `user_profiles`, roles/permissions, membership role/branch/warehouse access, invitations and invitation access, sessions, reset/verification tokens, audit logs |
| Master data | customers, suppliers, warehouses, UOMs, UOM conversions, items, item components |
| Procurement | purchase orders/lines, purchase receipts/lines/components, purchase returns/lines |
| Inventory/shortage | stock ledger entries, shortage reason codes, shortage ledger entries, shortage resolutions/allocations |
| Finance | supplier statement entries, payments/allocations |
| Corrections | document reversals plus reversal linkage on supported business documents |

### Migrations

- 21 migrations were discovered from `20260419193505_InitialBaseline` through `20260725120000_AddApplicationDatabaseMetadata`, plus the model snapshot.
- The latest metadata migration was discovered by `dotnet ef migrations list`.
- The full migration chain was applied to an isolated temporary SQLite database during Batch 1 verification after provider-compatible migration fixes. Production SQL Server migration execution still requires a live SQL Server verification environment.
- The checked-in schema documentation does not cover the full identity/setup schema and should not be treated as a current ERD.

### Correctness controls

- Decimal precision is configured on quantity/money fields.
- Ledger and traceability indexes exist.
- Unique indexes protect many idempotent source-document effects.
- `GuardAppendOnlyLedgers` rejects update/delete of stock and supplier statement rows.
- Audit timestamps/actors are assigned in `AppDbContext`.
- Operational list queries generally use `AsNoTracking`, projections, pagination, filters, and deterministic secondary sorts.

### Data-model risks

| Severity | Risk | Evidence and recommendation |
|---|---|---|
| Critical | Tenant uniqueness indexes omit `OrganizationId`. | All master/document configuration files. Rebuild unique indexes as `(OrganizationId, business key...)` through a migration and add cross-tenant tests. |
| Critical | Multi-currency balances can combine unlike currencies. | `SupplierStatementPostingService`, `SupplierBalanceService`, payment/resolution currency fields. Either enforce one organization currency end-to-end or partition/convert every ledger and allocation calculation. |
| Critical | Concurrent posting is not protected at the business invariant row. | `BeginTransactionAsync` uses provider default isolation; no row version/lock. Use optimistic concurrency tokens with retry/conflict results or serializable/target-row locking for remaining-quantity/amount invariants. |
| Medium | SQLite desktop host/restore drill is still incomplete. | Batch 3.1 fixed observed WSLg readiness/lifecycle defects and verified targeted startup, single-instance, shutdown, restart, and forced-termination behavior, but the full desktop devtools/external-navigation/protected-shutdown/startup-failure matrix is not complete. Restore UI, production Windows key provider, scheduled backups, Windows packaging, and CI/runtime restore drills still need completion. |
| High | Posted/canceled audit columns remain null. | Posting/cancellation services set status and `UpdatedBy`, but never `PostedAt/By` or `CanceledAt/By`. Populate and test these fields. |
| High | Organization settings contain flags with no enforcement. | Negative stock, approval, posting workflow, reversals, batch/serial/expiry, transfers, adjustments. Do not expose flags until behavior exists, or implement enforcement. |
| Medium | Global document numbers use hardcoded prefixes/timestamps. | PO/receipt/return/payment/resolution/reversal services. Implement organization-scoped transactional numbering using configured prefixes. |
| Medium | SQLite stock balance groups after loading all matching ledger rows. | `StockBalanceService` SQLite branch. Acceptable for tiny local DBs only; test and document the limit or use translatable SQL. |
| Medium | Master option lists are complete write DTOs/full graphs. | `ItemService.ListAsync` includes components. Introduce bounded lightweight option endpoints. |

## 14. Technical Debt

### Large files and mixed responsibilities

- `OrganizationAdministrationService.cs` — 1,681 lines spanning setup, organization, security, users, roles, invitations, sessions, audit, profiles, validation, mapping, and feature computation.
- `ShortageResolutionService.cs` — 841 lines.
- `PurchaseReceiptService.cs` — 814 lines.
- `IdentityService.cs` — 707 lines.
- `PurchaseOrderService.cs` — 622 lines.
- `PurchaseReceiptFormPage.tsx` — 1,063 lines.
- `AppShell.tsx` — 939 lines.
- `ShortageResolutionFormPage.tsx` — 871 lines.
- `PaymentFormPage.tsx` — 819 lines.
- `messages.ts` — 2,874 lines.

Recommended split: command/query services by use case, shared numbering and mutation helpers, dedicated form hooks/view components, and per-domain translation dictionaries.

### Duplicate/inconsistent patterns

- Endpoint files duplicate create/update validation and exception-to-HTTP logic. Use a global exception handler and a validation endpoint filter.
- Master-data modules each reproduce load/filter/client-pagination/deactivate UI code.
- Master data returns arrays, operational lists return `PagedResult`, audit logs return an array with page inputs, and active lookups are also arrays.
- Payment update uses the create permission; shortage resolution update uses create permission. Other modules have explicit edit permission.
- Frontend permission constants omit create/edit/post/cancel/reverse keys, preventing consistent action-level checks.
- Multiple settings implementations coexist: `OrganizationSettingsPage.tsx`, individual `pages/settings/*`, and workspace redirects.
- Root and `docs/` copies of core documents have diverged.

### Type and React quality

- TypeScript is strict and no material `any`, `@ts-ignore`, or unsafe-cast pattern was found in application code.
- API DTOs are handwritten and can drift from C# records. There is no schema/code generation or contract test.
- Fetch requests are guarded with local `active` booleans but are not canceled; rapid search/filter changes still consume server work.
- There is no query-cache library. Module-level caches do not naturally scope all data to the active organization:
  - master-data cache keys are namespace-only and are not cleared by `switchOrganization`;
  - dashboard snapshot cache is global and not keyed by organization.
  This can display the previous organization’s option/dashboard data for up to the cache TTL after an organization switch.
- `FeatureConfigurationProvider.load` has no error state; a rejected request can produce an unhandled promise while leaving features null.
- Dashboard aggregation generates many requests on first render and computes summaries from incomplete pages.

### Documentation debt

- `README.md` claims there is no ERP business logic and references paths from other machines.
- `docs/mvp-scope.md` says supplier statements and shortage resolution are not implemented.
- `docs/api-spec-v1.md` omits identity/settings and some current details.
- `docs/db-schema-v1.md` omits most identity/setup schema.
- The root duplicate docs are not identical to their `docs/` equivalents.

## 15. Bugs and Risks

| Severity | Description | Exact files/functions | Current vs expected | Recommended solution |
|---|---|---|---|---|
| Resolved in Batch 1 / extended in Batch 2 and Batch 3 | Normal SQLite dev data can be deleted on restart. | `DevelopmentDatabaseInitializer.EnsureSqliteDatabaseIsReadyAsync`; local data services; Batch 1/Batch 2 desktop foundation tests; Batch 3 Tauri shell and desktop publish workflow | Local verification confirmed two-start persistence, invalid-schema preservation, explicit Development-only reset gating, encrypted backup manifests, restore preflight, retention, startup diagnostics, Desktop-profile loopback backend startup, and static React serving. | Keep as CI regression coverage and complete real desktop-window smoke, desktop-host restore drill/UI/key-provider work. |
| Critical | Cross-tenant unique collisions. | `Persistence/Configurations/*Configuration.cs` | Codes/numbers are globally unique. Expected uniqueness per organization. | Migration to organization-inclusive indexes; update docs/tests. |
| Critical | Concurrent overposting. | Receipt/return/payment/shortage posting and validation services | Two transactions can observe the same open quantity/amount. Expected single-winner invariant. | Add concurrency token/serializable locking and integration tests with parallel posts. |
| Critical | Multi-currency ledger aggregation. | `SupplierBalanceService`, `SupplierStatementPostingService`, `PaymentService` | Unlike currencies can share a running/summary balance. Expected enforced base currency or currency-separated accounting. | Define product rule, validate currency, convert or partition ledgers. |
| High | Dashboard financial/quantity totals use one row. | `dashboardApi.loadFreshDashboardSnapshot` | Requests one item then reduces `items`. Expected full aggregate. | Add dedicated server summary endpoint. |
| High | Dashboard negative-stock count is capped. | `dashboardApi.ts`, `PaginationRequest.MaxPageSize` | Client asks 250; server returns max 100. Expected total negative count. | Server aggregate query. |
| High | Stock transaction/source TypeScript unions are stale. | `services/inventoryApi.ts`; `StockMovementPage.formatTransactionType`; backend `StockTransactionType.cs`/`SourceDocumentType.cs` | Backend returns `PurchaseReturn`, `ShortageResolutionReversal`, and `DocumentReversal`, but frontend types omit them and the formatter labels unknown transactions as physical shortage resolution. | Generate/synchronize enums, add explicit exhaustive mapping, filters, translations, and contract tests. |
| High | Organization-scoped frontend caches leak across organization switch. | `masterDataApi.ts` option cache; `dashboardApi.ts` snapshot cache; `AuthProvider.switchOrganization` | Cache has no org key/invalidation. Expected tenant-isolated cache. | Key caches by organization and clear on switch/logout/mutation. |
| High | Setup can be edited by any active member. | `OrganizationAdministrationService.SaveSetupAsync`, `CompleteSetupAsync` | Membership is sufficient. Expected owner/admin permission. | Require `settings.organization.manage` and security permission where applicable. |
| High | Disabled feature behavior is inconsistent. | `OrganizationSetupEndpointFilter`, temporary flags, receipt/return/shortage posting | UI/API gates are bypassed; receipt conditionally suppresses effects while other flows do not consistently do so. | Restore centralized feature policy and test every affected posting path. |
| High | Posted/canceled audit fields are not written. | All posting services; `PurchaseOrderCancellationService` | Status changes but document-specific audit fields remain null. | Set fields atomically and cover in tests. |
| Medium | Audit log API can be unbounded per request. | `ListAuditLogsAsync` | Positive page size has no maximum and response lacks total count. | Use `PaginationRequest`/`PagedResult`. |
| Medium | Users list materializes all memberships/invites before paging. | `OrganizationAdministrationService.ListUsersAsync` | Pagination happens in memory. | Union/project and page in SQL, or separate queries with bounded merge. |
| Medium | Master-data APIs are unbounded. | All master-data services/endpoints | Browser pagination downloads all matching rows. | Add standard server pagination, lightweight options endpoints, and indexes. |
| Medium | API errors are inconsistent/unhandled. | Endpoint `ExecuteAsync` helpers; `Program.cs` | Some DB/concurrency exceptions become generic 500; payloads differ. | Central ProblemDetails middleware and exception mapping. |
| Medium | 401 clears storage but not active auth context. | `services/api.ts`, `AuthProvider.tsx` | Route may stay rendered after expiry. | Central unauthorized event/context reset and redirect. |
| Medium | PO/receipt delete menu is nonfunctional. | `PurchaseOrdersPage.handleDelete`, `PurchaseReceiptsPage.handleDelete` | Confirmation ends in “unavailable” toast. | Remove action or define safe draft-delete/cancel product rule and implement end-to-end. |
| Medium | Development seed data is invisible to tenants. | `DevelopmentDatabaseInitializer.SeedAsync` | Rows get `Guid.Empty` and query filters hide them. | Seed per test/demo organization or remove misleading seed. |
| Low | Static `StatusPanel.tsx` is unused. | `components/StatusPanel.tsx` | Dead component with hardcoded strings. | Remove or integrate/localize. |

## 16. Security Concerns

Priority security remediation order:

1. Stop logging invitation secrets; rotate/invalidate affected non-production invitation tokens and sanitize historical audit data where applicable.
2. Add email delivery with one-time reset/verification/invitation links, app base URL configuration, throttling, resend controls, and token revocation.
3. Keep JWT fail-closed behavior and token-free recovery/verification responses under CI regression coverage.
4. Enforce administrative permission on setup completion/mutation.
5. Enforce warehouse/branch access in every list/detail/mutation/posting/query path.
6. Add rate limits and security headers/HTTPS deployment requirements.
7. Implement or remove 2FA/OTP claims and UI; never signal “requires two factor” after already granting a full token.
8. Decide whether browser tokens remain in local storage. If so, add a strict CSP and XSS review; preferably use a hardened HttpOnly/Secure/SameSite session-cookie model.

No evidence of SQL string concatenation, raw password storage, manual supplier/stock ledger entry endpoints, or client-authoritative posting was found. EF parameterization, password hashing, token hashing, server permission checks, and append-only guards are positive controls.

## 17. Testing Status

### Test inventory

- Backend: 91 test declarations across 19 test source files.
- Strongest backend coverage:
  - purchase receipt drafts/posting;
  - shortage resolution posting;
  - payments/allocations;
  - reversals;
  - identity API;
  - stock ledger queries/API;
  - supplier statement queries/API;
  - master-data validation/API.
- Frontend: 11 Vitest test cases:
  - pagination SSR markup;
  - auth API URL/payload/fallback;
  - supplier statement presentation/grouping.
- No E2E, browser component interaction, accessibility, RTL, visual regression, offline, performance, SQL Server, migration-application, multi-tenant uniqueness, cross-tenant cache, or concurrency test suite.
- No skipped test marker was found by static search.

### Command results

| Command | Result |
|---|---|
| `git status --short` | Passed; worktree was clean before report creation. |
| `dotnet --info` | Passed: .NET SDK 8.0.129; ASP.NET Core runtime 8.0.29. |
| `node --version` | Passed: `v22.23.1`. |
| `npm --version` | Passed: `10.9.8`. |
| Dependency/build-output inspection | Frontend `node_modules` absent; backend `bin` absent. |
| `npm ci` in `src/frontend` | Passed after lockfile synchronization. |
| `npm test` in `src/frontend` | Passed: 3 files, 11 tests. |
| `npm run build` in `src/frontend` | Passed; Vite chunk-size warning only. |
| `dotnet build src/backend/ERP.sln --no-restore` | Passed; 0 warnings, 0 errors. |
| `dotnet test src/backend/ERP.sln --no-build` | Passed: 112 passed, 0 failed, 0 skipped. |
| Static/test verification | Passed during 2026-07-25 pre-commit security hardening: backend solution build; 123 backend tests; 13 frontend tests; 3 desktop Node tests; 4 desktop Rust tests; desktop release build. |
| Lint | Not run; no lint script/configuration exists. |
| EF migration list/update | Passed for Batch 1 scope: metadata migration discovered and full chain applied to isolated temporary SQLite. Batch 2 added `20260725124500_AddDesktopFoundationBatch2Safety` for installation ID plus upgrade/restore journals. |

The original audit was analysis-only. The Batch 1 verification task later installed frontend dependencies with `npm ci` and restored .NET tools/packages as required for build/test verification.

### Reliability gaps to test next

- Repeat normal development startup without data loss.
- Cross-tenant reuse of every business code/number.
- Concurrent post of two receipts/returns/payments/resolutions targeting the same open balance.
- Warehouse-restricted list/detail/create/post behavior.
- Organization switch while dashboard/master option caches are warm.
- Currency mismatch and multi-currency balance behavior.
- Feature-disabled posting effects.
- Session expiry and frontend redirect.
- Production SQL Server migrations and workflow integration.

## 18. Environment Variables

| Key | Purpose | Required/recommendation |
|---|---|---|
| `ASPNETCORE_ENVIRONMENT` | Selects Development/Production configuration. | Development launch profile sets `Development`. |
| `DatabaseProvider` | `SqlServer` or `Sqlite`. | Defaults to SQL Server. |
| `ConnectionStrings__DefaultConnection` | Database connection string. | Required in production; do not commit credentials. |
| `Authentication__JwtSecret` | JWT HMAC signing key. | Required outside Development/Testing; short and placeholder values are rejected. Desktop supplies a generated app-data secret via environment. |
| `Authentication__Issuer` | JWT issuer. | Configure explicitly in production. |
| `Authentication__Audience` | JWT audience. | Configure explicitly in production. |
| `VITE_API_BASE_URL` | Frontend API origin/base URL. | Optional for same-origin; required when API is hosted separately. |
| `Logging__LogLevel__Default` | ASP.NET log level. | Optional configuration. |
| `Logging__LogLevel__Microsoft_AspNetCore` | Framework log level when expressed as environment variable. | Optional. |
| `AllowedHosts` | ASP.NET host filtering. | Configure production hosts. |

Missing but recommended configuration includes email provider/from address/public link base, CORS origins, token lifetimes, proxy/forwarded headers, telemetry/OTLP endpoint, storage provider, and explicit development DB reset behavior.

## 19. Local Setup Instructions

These are repository-derived instructions; they were not fully executable in the audit container.

1. Install .NET SDK 8, Node.js 20+ (the audit host had Node 22), and npm 10+.
2. From the repository root, restore backend tools and packages:

   ```bash
   dotnet tool restore
   dotnet restore src/backend/ERP.sln
   ```

3. Install frontend packages reproducibly:

   ```bash
   cd src/frontend
   npm ci
   cd ../..
   ```

4. For default local SQLite, use the Development environment. Batch 1 verification confirmed normal startup is non-destructive with reset disabled.
5. Start both processes:

   ```bash
   ./run.sh
   ```

   Or separately:

   ```bash
   cd src/backend/Api
   dotnet run
   ```

   ```bash
   cd src/frontend
   npm run dev
   ```

6. Open `http://localhost:5173`; API health is `http://localhost:5080/health`.
7. For SQL Server, set `DatabaseProvider=SqlServer` and `ConnectionStrings__DefaultConnection`, then apply migrations:

   ```bash
   dotnet ef database update \
     --project src/backend/Infrastructure/ERP.Infrastructure.csproj \
     --startup-project src/backend/Api/ERP.Api.csproj
   ```

8. Configure JWT issuer/audience/secret explicitly before any non-local use.

## 20. Deployment Overview

There is no complete deployment implementation.

- Backend can be published with standard `dotnet publish`, but no repository command or artifact pipeline is defined.
- Frontend can build to Vite `dist`, but no static host/proxy configuration exists.
- SQL Server is the intended production database; migrations are manual.
- CORS is hardcoded for localhost only.
- API health reports process availability only, not database readiness.
- No container, orchestration, CI/CD, environment template, secret store integration, TLS/proxy config, migration job, backup policy, restore test, monitoring, alerting, error tracking, or runbook exists.
- Logging uses ASP.NET defaults plus auditable business rows. There is no structured trace/correlation strategy.

Production deployment should remain blocked until Phase 0 security/data-integrity work and Phase 5 operations are complete.

## 21. Recommended Refactoring

1. Split identity and organization administration by bounded responsibility: setup, organization/security, membership/invitations, roles/permissions, sessions/audit, profiles.
2. Introduce centralized ProblemDetails exception mapping and a reusable FluentValidation endpoint filter.
3. Create organization-aware cache/query keys and a single cache invalidation event on login/logout/organization switch.
4. Replace dashboard fan-out with server summary/read-model endpoints.
5. Standardize every list on `PagedResult`, including master data and audit logs; create separate bounded lookup DTO endpoints.
6. Add a document-number service using organization configuration and concurrency-safe sequences.
7. Add a reusable posting concurrency policy and make conflict responses explicit (`409`).
8. Split large form pages into data hooks, domain validation/mapping, header/actions, and line/allocation grids.
9. Generate TypeScript API types/client from an OpenAPI contract or add cross-language contract tests.
10. Break localization dictionaries by domain and add a static test that all visible literals are keys available in both locales.
11. Remove/route one canonical settings implementation; delete unreachable duplicates only after product confirmation.
12. Consolidate duplicate docs into `docs/` and mark status/current implementation dates.

## 22. Prioritized Remaining Work

The detailed, acceptance-testable backlog is in `HIGHCOOL_REMAINING_WORK.md`. Highest order:

1. Keep SQLite startup preservation, encrypted backup/manifest, retention, diagnostics, restore-preflight behavior, and Tauri loopback startup covered in CI; complete real desktop-window smoke, desktop-host restore drill, restore UI, scheduled backup policy, and production Windows key provider.
2. Finish safe invitation/email delivery and remove invitation token audit exposure.
3. Fix organization-scoped unique indexes and add tenant isolation tests.
4. Add posting concurrency protection for every shared open balance/quantity.
5. Define and enforce single-currency or full multi-currency accounting.
6. Restore setup/feature authorization and gating, then enforce warehouse/branch scope.
7. Fix organization-aware frontend caches and dashboard aggregation.
8. Make build/test/CI reproducible and green.
9. Complete settings/identity delivery flows and localization.
10. Implement missing ERP business modules only after the foundation is stable.

## 23. Proposed Implementation Roadmap

### Phase 0 — Stabilization

**Goals:** prevent data loss, token compromise, cross-tenant conflicts, invalid financial balances, and concurrent overposting.

**Tasks:** SQLite initializer regression coverage; invitation token log sanitization; production email/link delivery; tenant index migration; concurrency controls; currency rule; setup authorization; cache tenant isolation; global error contract.

**Dependencies:** product choice on currency and draft deletion; migration/backup plan for existing databases; email provider choice.

**Risks:** unique-index migration may expose existing global collisions; concurrency changes affect all posting tests; invitation/email changes require frontend and provider decisions.

**Definition of done:** no implicit database deletion; app fails closed without production secrets; no raw security token in response/log/audit; two tenants can reuse codes safely; concurrent tests prove no overposting; financial summaries cannot mix currencies; builds/tests pass in CI.

### Phase 1 — Core Completion

**Goals:** make the current procurement/inventory/supplier-financial slice operationally complete.

**Tasks:** restore setup/feature flow; enforce warehouse/branch scopes; complete invitation/reset/verification/2FA; purchase-return correction flow; shortage-reason management; safe document numbering; implement real offline draft store if it remains MVP; complete/reconcile settings routes.

**Dependencies:** Phase 0 policies, email delivery, product decisions on return reversal and offline conflict behavior.

**Risks:** enabling gates can reveal invalid organization configurations; access scopes may change user-visible datasets.

**Definition of done:** every exposed control persists or is removed; feature-disabled modules are consistently inaccessible; scoped users see only authorized data; all identity journeys are usable without audit-log token access; current slice has complete backend/UI/integration coverage.

### Phase 2 — Consistency and Refactoring

**Goals:** unify contracts, permissions, localization, forms, and list performance.

**Tasks:** paginated master data/audit; lookup endpoints; action-level permissions; ProblemDetails/validation; split large services/pages; generated API types; organization-aware caching; canonical settings UI/docs; remove hardcoded UI strings.

**Dependencies:** stable Phase 1 flows.

**Risks:** broad API/UI contract migration; localization regression.

**Definition of done:** one list/error/validation/permission pattern; no unbounded operational lists; no visible hardcoded shipped strings; all caches tenant-keyed; major files have bounded responsibility.

### Phase 3 — Testing and Quality

**Goals:** make regressions observable before deployment.

**Tasks:** CI; SQL Server integration/migration tests; E2E business journeys; concurrency/multi-tenant/security tests; accessibility/RTL tests; offline tests; coverage reporting; static lint/format checks.

**Dependencies:** reproducible SDK/package environment and stable contracts.

**Risks:** existing latent defects will surface and require fixes.

**Definition of done:** clean restore/build/lint/typecheck/unit/integration/E2E pipeline; no skipped critical tests; migration-from-production-baseline test passes.

### Phase 4 — UX and Performance

**Goals:** accurate, fast, accessible, bilingual ERP workflows.

**Tasks:** server dashboard summaries; request cancellation/query caching; form decomposition/lazy options; keyboard/accessibility review; both-direction responsive validation; meaningful draft/connection status; query/index profiling.

**Dependencies:** Phase 2 list APIs and Phase 3 measurement.

**Risks:** premature caching can reintroduce stale tenant data; UI changes can affect dense operational workflows.

**Definition of done:** dashboard metrics reconcile to SQL; bounded request counts; target response budgets met; WCAG-oriented audit issues resolved; Arabic/English workflows pass E2E.

### Phase 5 — Deployment Readiness

**Goals:** safe, supportable production operation.

**Tasks:** container or documented host deployment; same-origin proxy/CORS; secrets; migration job/rollback; backups/restore drill; structured logs/traces/metrics; health/readiness; alerts; CSP/security headers; release/runbook documentation.

**Dependencies:** Phases 0–4 and selected production platform.

**Risks:** infrastructure/provider decisions and migration downtime.

**Definition of done:** repeatable production/staging deployment; secret rotation; verified backup restore; monitored readiness; rollback tested; operator runbook approved.

## 24. Questions Requiring Product Clarification

1. Is HighCool single-currency per organization, or must documents/settlements support true multi-currency and exchange gains/losses?
2. Should public signup remain available, or should organization creation be invitation/admin controlled?
3. Which email provider and public application URL should deliver verification, reset, and invitation links?
4. Is two-factor mandatory when `ForceTwoFactor` is enabled, and which factors are supported?
5. Should organization setup remain editable after completion, and by which permissions?
6. Are branch scopes required now? No operational entity currently has a branch key.
7. Should warehouse-limited users be allowed to view cross-warehouse document headers with hidden lines, or should documents be entirely excluded?
8. What is the correction flow for a posted purchase return: reversal, cancel, or a return-of-return document?
9. Are draft deletes required? If yes, which documents/master records may be hard-deleted and what audit is required?
10. Should organization prefixes drive generated numbers, and is numbering sequential by fiscal year/document type?
11. Are manual receipts allowed to retain header-level payable amount, and how should their line cost/return valuation be assigned?
12. Is negative stock ever permitted for returns/reversals/adjustments?
13. Which organization flags are committed MVP behavior versus future configuration placeholders?
14. What offline draft conflict/ownership/encryption/retention behavior is required?
15. Which of the full-mission modules—sales, commissions, employees/advances/payroll—comes next after procurement stabilization?
16. What are the production hosting, SQL Server edition, backup RPO/RTO, retention, and data-residency requirements?
