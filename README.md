# HighCool ERP Foundation

HighCool is a modular monolith ERP web application with:

* ASP.NET Core Web API backend
* React + TypeScript frontend
* EF Core persistence
* Procurement, inventory, supplier financial, shortage, identity, and settings foundations

The system is still under active development. It now has a basic Tauri desktop development-shell foundation, but it is not yet a finished customer desktop application, installer, updater, or production deployment package.

## Project Structure

```text
src/
  backend/
    Api/
    Application/
    Domain/
    Infrastructure/
    tests/
  desktop/
  frontend/
docs/
```

## Backend

### Prerequisites

* .NET SDK 8.0+

### Run

```bash
cd src/backend/Api
dotnet restore
dotnet run
```

In development, the API explicitly uses SQLite by default:

```json
{
  "Database": {
    "Provider": "Sqlite",
    "SqliteFileName": "highcool-dev.db"
  }
}
```

The SQLite file is resolved through the local storage service. Empty `LocalStorage` paths use a repository-local development directory under the API content root. Production/default configuration still targets SQL Server, but SQLite can now be selected intentionally for future desktop mode without requiring SQL Server on the final user machine.

Authentication signing secrets are fail-closed outside local development/test profiles. Production and ordinary non-development hosts must provide a strong `Authentication__JwtSecret` from the environment or secret store; placeholder values and short values are rejected at startup. Development/test runs that omit the secret generate a local key outside the repository. The desktop shell generates/reuses its own app-data JWT key file and passes it to the backend through environment variables, not command-line arguments.

Development database reset is disabled by default:

```json
{
  "LocalDatabase": {
    "AllowDevelopmentReset": false
  }
}
```

Only the Development environment can use `LocalDatabase:AllowDevelopmentReset=true`, and only for explicit developer recovery of unsupported local schemas. Normal startup must not delete a valid SQLite database.

### Desktop Safety Profile

A desktop-oriented backend profile is available at [appsettings.Desktop.json](src/backend/Api/appsettings.Desktop.json). It selects local SQLite, keeps reset disabled, uses loopback-safe host settings, and enables local backup retention defaults. It is configuration foundation only; it is not a desktop shell or installer.

Batch 2 desktop-foundation verification on 2026-07-25 added encrypted local SQLite backups with versioned manifests, restore preflight/restore service contracts, mandatory pre-upgrade backup orchestration, upgrade/restore journals, local retention policy, and startup diagnostics. Backups are selected by backup ID, not arbitrary client paths.

Batch 3 desktop-foundation verification on 2026-07-25 added a separated Tauri desktop workspace under [src/desktop](src/desktop), a repeatable backend publish process, Desktop-profile loopback backend startup, token-protected startup diagnostics and shutdown endpoints, same-origin React static hosting through ASP.NET, minimal Tauri capabilities, and desktop build/tests. The Linux release build passed, but the real interactive window smoke needs to be repeated on a normal desktop session before treating the shell as fully approved.

Batch 3.1 on 2026-07-25 fixed observed desktop-shell lifecycle defects in WSLg: readiness support-code parsing, transient `HC-Unavailable` handling, app-exit backend cleanup, and Linux parent-death cleanup for forced shell termination. WSLg targeted checks verified startup-to-main transition, one loopback backend, same-origin React serving, auth-required behavior, single-instance process behavior, normal-close backend cleanup, restart preservation, and forced-termination orphan cleanup. Full approval is still pending a normal desktop-session run of the remaining devtools, external-navigation, protected-shutdown, and startup-failure matrix.

Batch 3.2 on 2026-07-25 reran the full automated backend/frontend/desktop regression suite and fixed the documented desktop Cargo command path by adding a `src/desktop` Cargo workspace. Full desktop approval remains blocked because this workspace is WSLg, not a normal Linux desktop, and Windows verification was not available.

Pre-commit security hardening on 2026-07-25 removed the unsafe JWT fallback secret, removed the default SQL Server credential from committed configuration, stopped public reset/verification endpoints from returning raw tokens, added an internal auth-message delivery abstraction, and tightened desktop/local secret ignores.

Batch 5 on 2026-07-25 added the authenticated `/settings/backup-restore` experience for managed local backups. The UI supports summary, history, details, manual backup, verification, restore preflight, restore execution, and retention settings. Restore now requires a server-issued, expiring, single-use preflight operation ID bound to the selected backup, authenticated user, installation, and compatibility metadata, plus the existing typed confirmation phrase. Local database endpoints are Desktop-only, with a Testing-only capability used by automated tests.

### Health Check

```bash
curl http://localhost:5080/health
```

Expected response:

```text
OK
```

## Frontend

### Prerequisites

* Node.js 20+
* npm 10+

### Run

```bash
cd src/frontend
npm install
npm run dev
```

The Vite dev server will print the local URL in the terminal.

### Build

```bash
cd src/frontend
npm run build
```

## Notes

* The backend includes active ERP business logic and data models.
* SQLite local storage and a basic Tauri shell are foundation work for a future single-PC desktop distribution.
* Local encrypted backup/restore safety services and the authenticated restore UI exist, but scheduled backups, cloud backups, updater, and final installer are not implemented yet.
* The desktop shell is not yet customer-ready because the full interactive/failure/security matrix still needs a normal Linux/Windows desktop environment.

## Database Baseline

### Prerequisites

* SQL Server reachable from the backend connection string
* `dotnet tool restore`

### Default Connection

The default backend connection string is defined in [appsettings.json](src/backend/Api/appsettings.json).

For local development, [appsettings.Development.json](src/backend/Api/appsettings.Development.json) switches the provider to SQLite and auto-creates a seeded `highcool-dev.db` file when the API starts.

You can override SQL Server from the terminal:

```bash
export Database__Provider="SqlServer"
export ConnectionStrings__DefaultConnection="Server=localhost;Database=HighCool;User Id=<username>;Password=<password>;TrustServerCertificate=True"
```

Do not commit real connection strings, JWT secrets, certificates, backup files, or local SQLite databases. Production startup requires a strong `Authentication__JwtSecret`; use environment variables or a deployment secret store rather than appsettings files.

You can intentionally use a local SQLite file:

```bash
export Database__Provider="Sqlite"
export Database__SqliteFileName="highcool.db"
```

Batch 1 desktop-foundation verification on 2026-07-25 confirmed that local SQLite startup preserves data across consecutive starts, rejects unsupported schemas without deleting the file when reset is disabled, and can create an authenticated local backup with integrity/checksum validation.

Batch 2 desktop-foundation verification on 2026-07-25 added encrypted backups, manifests, local retention, restore validation/execution service contracts, mandatory pre-upgrade backup orchestration, upgrade/restore journals, and startup diagnostics.

Batch 3 desktop-foundation verification on 2026-07-25 added the Tauri development shell and self-contained backend publish workflow. This is still foundation work only; scheduled backups, Cloudflare R2, final installer, updater, and Windows packaging verification remain out of scope.

Batch 3.1 desktop-foundation verification on 2026-07-25 fixed observed WSLg readiness and lifecycle defects. It remains foundation work only; final approval requires a normal desktop-session verification matrix and Windows remains unverified.

Batch 3.2 desktop-foundation verification on 2026-07-25 kept automated checks green and made the documented desktop Cargo checks runnable from `src/desktop`; it did not complete the required normal Linux/Windows desktop approval matrix.

Pre-commit security hardening on 2026-07-25 confirmed committed defaults no longer include the old SQL Server `sa` credential or the unsafe JWT fallback. Public password-reset and email-verification requests now return generic acceptance responses; the generated one-time tokens are delivered only through the internal delivery abstraction, which is currently a no-op outside tests until a real email provider is selected.

Batch 5 desktop backup/restore verification on 2026-07-25 added the `/settings/backup-restore` page and hardened restore execution with server-issued preflight operation IDs, operation serialization, fresh preflight validation, safety backup creation, and replay/expiry/user/backup binding checks. Scheduled/cloud backups, backup deletion, final installer/updater, Windows packaging, and normal-desktop interactive approval remain pending.

### Create Or Apply Migrations

```bash
dotnet tool restore
dotnet ef migrations list --project src/backend/Infrastructure/ERP.Infrastructure.csproj --startup-project src/backend/Api/ERP.Api.csproj
dotnet ef database update --project src/backend/Infrastructure/ERP.Infrastructure.csproj --startup-project src/backend/Api/ERP.Api.csproj
```
