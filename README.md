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

Desktop runtime connection fix on 2026-07-26 changed the main Tauri WebView to load bundled frontend assets and resolve the selected backend origin through a safe runtime command. The desktop backend still binds only to `127.0.0.1:<dynamic-port>`, the frontend no longer guesses the port, and the generated app-data JWT secret is passed to `ERP.Api` through environment variables without requiring a manual `Authentication__JwtSecret` export. Windows verification remains pending.

Batch 6 on 2026-07-26 added Cloudflare R2 cloud backup support for the desktop backup/restore screen. The backend owns all R2 communication through S3-compatible APIs, stores R2 credentials encrypted in the desktop local data directory, queues automatic/manual uploads with retry state, lists local/cloud/combined backup history, downloads cloud backups through manifest/checksum verification before existing restore flow use, and applies cloud retention. The UI is localized in English and Arabic and keeps local backup/restore usable when cloud backup is disabled or unavailable. Live R2 credential verification still needs to be run in a real environment.

Batch 7 on 2026-07-26 added a native Windows NSIS installer build path for the Tauri desktop shell. The Windows build publishes the ASP.NET backend for `win-x64` as a self-contained Release deployment, keeps `PublishSingleFile=false`, leaves trimming disabled, bundles the published backend directory as an immutable application resource, and keeps runtime databases, logs, backups, pending uploads, settings, and keys under the Tauri application data directory instead of the installation directory. Native Windows installer generation and smoke testing remain pending until run on Windows 10/11 x64.

### Windows Installer Build

Build Windows installers only from native Windows 10/11 x64, not WSL. Required build tools are Git, Node.js 20 with npm 10, .NET SDK 8.0, Rust stable with the `x86_64-pc-windows-msvc` target, Visual Studio Build Tools with the C++ workload and Windows SDK, and the Tauri CLI installed through this workspace's npm dependencies. HighCool uses the installed or bootstrapped Microsoft Edge WebView2 runtime; MSI/WiX packaging is not enabled.

```powershell
Set-Location C:\Path\To\HighCool
dotnet tool restore
dotnet restore src/backend/ERP.sln
Set-Location C:\Path\To\HighCool\src\frontend
npm install
Set-Location C:\Path\To\HighCool\src\desktop
npm install
rustup target add x86_64-pc-windows-msvc
npm run desktop:build:windows
```

The one-command installer build is:

```powershell
Set-Location C:\Path\To\HighCool\src\desktop
npm run desktop:build:windows
```

The expected NSIS installer output is under:

```text
C:\Path\To\HighCool\src\desktop\target\x86_64-pc-windows-msvc\release\bundle\nsis\
```

The build script validates prerequisites, checks that `package.json`, `src-tauri/tauri.conf.json`, and `src-tauri/Cargo.toml` use the same desktop version, cleans only generated desktop publish and NSIS bundle output, builds the React frontend, publishes `ERP.Api` to `src/desktop/backend-publish/win-x64`, and runs `tauri build --target x86_64-pc-windows-msvc --bundles nsis`. The installer is configured as a per-user NSIS installer with product name `HighCool`, application identifier `com.highcool.desktop`, version `0.1.0`, English language, Start Menu folder `HighCool`, downgrade prevention, and WebView2 bootstrapper download. Publisher, code-signing certificate, MSI packaging, and native Windows icon validation are pending project decisions or native Windows verification.

On Windows, Tauri derives the app-data root from the identifier. Expected production paths are:

```text
%APPDATA%\com.highcool.desktop\Data\highcool.db
%APPDATA%\com.highcool.desktop\Data\highcool.db-wal
%APPDATA%\com.highcool.desktop\Data\highcool.db-shm
%APPDATA%\com.highcool.desktop\Backups\
%APPDATA%\com.highcool.desktop\PendingBackups\
%APPDATA%\com.highcool.desktop\Logs\
%APPDATA%\com.highcool.desktop\Data\cloud-backup-settings.json
%APPDATA%\com.highcool.desktop\Data\cloud-backup-queue.json
%APPDATA%\com.highcool.desktop\Data\Keys\highcool-local-backup.key
%APPDATA%\com.highcool.desktop\Keys\highcool-desktop-jwt.key
%APPDATA%\com.highcool.desktop\Data\TestTooling\
```

Fresh installs create the application-data directories on first launch, create or migrate the SQLite database through backend startup, and bind the backend only to `http://127.0.0.1:<dynamic-port>`. Upgrades replace application binaries and bundled resources without touching the application-data directory. Normal uninstall removes installed binaries but must leave application data in place. Optional manual cleanup is to uninstall HighCool, confirm no HighCool process is running, back up any required files, then remove `%APPDATA%\com.highcool.desktop` manually.

Windows smoke-test checklist:

```text
Fresh install: install HighCool, launch from Start Menu, verify only one instance opens, verify backend readiness, verify database creation, complete organization setup, create sample business data, create a local backup, configure R2, upload a cloud backup, close and reopen, and confirm data persists.
Upgrade: install version N, create data and backups, install version N+1 over it, verify database/WAL/SHM files, keys, settings, backups, pending uploads, logs, and R2 configuration remain, verify migrations execute, and verify backup/restore still works.
Uninstall/reinstall: uninstall HighCool, confirm binaries are removed, confirm app data remains, reinstall, and confirm the existing database/configuration/backups are detected.
PC-loss simulation: install on a clean Windows profile or VM, configure required R2 credentials/settings, list cloud backups, restore, and verify the organization snapshot or equivalent business data.
Failure cases: backend cannot start, database locked, database migration failure, WebView2 unavailable, R2 unavailable, corrupted backup, insufficient disk space, interrupted installer, and interrupted upgrade.
```

Before approving a customer installer, run the native Windows command above, inspect the generated NSIS `.exe`, install it in a clean Windows user profile, complete the smoke-test checklist, and confirm the installed bundle contains no JWT secrets, R2 credentials, encryption keys, SQLite databases, WAL/SHM files, backups, logs, local app-data files, `.env` files, test snapshots, or seed manifests.

Batch 6.1 on 2026-07-26 hardened the R2 integration with strict Cloudflare R2 endpoint validation, DNS safety checks, HMAC-authenticated backup manifests, atomic queue writes with recovery, explicit retry categories, checksum-based sync status, safer retention pagination/ownership rules, cloud delete confirmation, and explicit credential replace/clear behavior. It still requires a disposable live R2 smoke test before staging approval.

### Organization Test Data Tooling

The restore-smoke organization test-data tool is intended only for Development, Testing, and Desktop environments. Do not run it against production data. The tool uses GUID organization IDs, deterministic seed-run IDs, JSON manifests, and verification snapshots.

List organizations from the local desktop SQLite database without writing to it:

```bash
python3 - <<'PY'
import sqlite3

path = "/root/.local/share/com.highcool.desktop/Data/highcool.db"
con = sqlite3.connect(f"file:{path}?mode=ro", uri=True)

rows = con.execute("""
    SELECT
        Id AS OrganizationId,
        Name,
        SetupCompleted,
        CreatedAt,
        UpdatedAt
    FROM Organizations
    ORDER BY Name
""").fetchall()

for row in rows:
    print(row)

con.close()
PY
```

Use this environment block when running the tooling against the desktop local database. The JWT value is a local tooling value used to boot the Desktop-profile host; do not commit real JWT secrets, R2 credentials, SQLite databases, manifests, snapshots, backups, or logs.

```bash
export DOTNET_ENVIRONMENT=Desktop
export Database__Provider=Sqlite
export Database__SqliteFileName=highcool.db
export LocalStorage__DataDirectory=/root/.local/share/com.highcool.desktop/Data
export LocalStorage__BackupDirectory=/root/.local/share/com.highcool.desktop/Backups
export LocalStorage__PendingBackupDirectory=/root/.local/share/com.highcool.desktop/PendingBackups
export LocalStorage__LogDirectory=/root/.local/share/com.highcool.desktop/Logs
export Authentication__JwtSecret=<LOCAL_TOOLING_JWT_SECRET_AT_LEAST_32_CHARS>
```

Preview a deterministic medium restore-smoke seed:

```bash
dotnet run --no-build \
  --project src/backend/Tools/ERP.Tools/ERP.Tools.csproj \
  -- seed-org-test-data \
  --organization-id <ORGANIZATION_ID> \
  --profile restore-smoke \
  --scale medium \
  --seed 20260726 \
  --dry-run
```

Create the seed data for real:

```bash
dotnet run --no-build \
  --project src/backend/Tools/ERP.Tools/ERP.Tools.csproj \
  -- seed-org-test-data \
  --organization-id <ORGANIZATION_ID> \
  --profile restore-smoke \
  --scale medium \
  --seed 20260726
```

If the same deterministic seed run already exists, add `--force` to delete and recreate only that manifest-scoped seed run. The seed command prints the `runId`, `manifestPath`, `snapshotPath`, and entity `counts`. The seed-run ID format is `restore-smoke-<first-8-lowercase-organization-guid-hex>-<seed>`.

Preview a test-data-only reset:

```bash
dotnet run --no-build \
  --project src/backend/Tools/ERP.Tools/ERP.Tools.csproj \
  -- reset-org-data \
  --organization-id <ORGANIZATION_ID> \
  --test-data-only \
  --seed-run-id <SEED_RUN_ID> \
  --dry-run \
  --skip-safety-backup
```

Execute a test-data-only reset:

```bash
dotnet run --no-build \
  --project src/backend/Tools/ERP.Tools/ERP.Tools.csproj \
  -- reset-org-data \
  --organization-id <ORGANIZATION_ID> \
  --test-data-only \
  --seed-run-id <SEED_RUN_ID> \
  --execute \
  --confirmation RESET-ORG-<organization-guid-lowercase> \
  --skip-safety-backup
```

The reset confirmation is case-sensitive and must use the lowercase organization GUID. Keep full organization reset as a dry-run-only operator workflow unless you have a fresh verified backup and an explicit recovery plan:

```bash
dotnet run --no-build \
  --project src/backend/Tools/ERP.Tools/ERP.Tools.csproj \
  -- reset-org-data \
  --organization-id <ORGANIZATION_ID> \
  --dry-run
```

Compare current organization data against a generated snapshot:

```bash
dotnet run --no-build \
  --project src/backend/Tools/ERP.Tools/ERP.Tools.csproj \
  -- verify-org-restore \
  --organization-id <ORGANIZATION_ID> \
  --snapshot <SNAPSHOT_PATH>
```

Expected restore drill sequence: verification passes before reset, fails after the seeded data is removed, and passes again after restoring the backup that contains the seed run.

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
* Local encrypted backup/restore safety services, Cloudflare R2 backup integration, and the authenticated restore UI exist, but scheduled backups, updater, and final installer are not implemented yet.
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

Desktop runtime connection fix on 2026-07-26 resolved the WSLg connection-refused path by loading bundled frontend assets, adding a runtime backend-origin handshake, and keeping the Linux backend supervisor thread alive while the child process exists. No manual JWT secret export is required for desktop startup.

Batch 6 desktop cloud-backup verification on 2026-07-26 added encrypted Cloudflare R2 configuration, cloud connection testing, bounded cloud/combined history endpoints, upload queue retry/cancel operations, download-and-verify flow into the local backup catalog, cloud retention, localized UI tabs, and service tests. Batch 6.1 hardened endpoint validation, authenticated manifests, queue durability, retry categories, sync comparison, retention ownership, delete confirmation, and credential editing. Scheduled backups, final installer/updater, Windows packaging, live R2 smoke, and normal-desktop interactive approval remain pending.

### Create Or Apply Migrations

```bash
dotnet tool restore
dotnet ef migrations list --project src/backend/Infrastructure/ERP.Infrastructure.csproj --startup-project src/backend/Api/ERP.Api.csproj
dotnet ef database update --project src/backend/Infrastructure/ERP.Infrastructure.csproj --startup-project src/backend/Api/ERP.Api.csproj
```
