# HighCool Desktop Foundation Batch 3 Result

Date: 2026-07-25  
Workspace: `/root/HighCool`  
Scope: basic Tauri desktop development shell around the existing React frontend and ASP.NET Core backend

## Summary

Batch 3 adds a separated desktop workspace under `src/desktop` using Tauri 2. The shell reuses the existing React app and ASP.NET Core API, starts one tracked local backend process with the `Desktop` profile, waits for token-protected startup diagnostics, and opens the React UI.

No ERP posting, procurement, inventory, payment, supplier balance, stock, shortage, reversal, or accounting calculations were changed. No cloud backup provider, final customer installer, or automatic updater was added.

## Selected architecture

Selected architecture after the 2026-07-26 runtime fix: **Tauri serves bundled React assets; ASP.NET is the loopback API and safety backend**.

```text
HighCool Desktop Process
  -> Tauri startup window
  -> tracked ASP.NET Core backend child process
  -> bundled Tauri React frontend assets
  -> safe runtime command returns http://127.0.0.1:<selected-port>
  -> React API clients call the selected loopback backend origin
  -> local SQLite database and existing local backup/restore services
```

The original Batch 3 implementation opened the main WebView at `http://127.0.0.1:<selected-port>/index.html`. The 2026-07-26 runtime fix moved the frontend shell back into bundled Tauri assets so backend HTTP is used for API traffic only.

## Why Tauri

Tauri was selected because the repository did not already contain another approved desktop shell and Tauri 2 is the current stable line. Official Tauri documentation describes Tauri as a way to build desktop apps using existing web frontends and documents the Rust/native prerequisites for Linux and Windows.

References checked:

- https://v2.tauri.app/blog/tauri-20/
- https://v2.tauri.app/start/prerequisites/

## Tool and dependency versions

- Node.js: `v22.23.1`
- npm: `10.9.8`
- Rust: `rustc 1.97.1`
- Cargo: `cargo 1.97.1`
- Tauri CLI npm package: `@tauri-apps/cli 2.11.4`
- Tauri Rust crate resolved by Cargo: `tauri 2.11.5`
- Single-instance Rust plugin: `tauri-plugin-single-instance 2.4.3`

## Files changed for Batch 3

Batch 3 source files:

- `.gitignore`
- `HIGHCOOL_DESKTOP_FOUNDATION_BATCH3_RESULT.md`
- `src/backend/Api/Endpoints/DesktopEndpoints.cs`
- `src/backend/Api/Program.cs`
- `src/backend/Api/appsettings.Desktop.json`
- `src/desktop/package.json`
- `src/desktop/package-lock.json`
- `src/desktop/scripts/desktop-utils.mjs`
- `src/desktop/scripts/publish-backend.mjs`
- `src/desktop/tests/desktop-utils.test.mjs`
- `src/desktop/src-tauri/Cargo.toml`
- `src/desktop/src-tauri/Cargo.lock`
- `src/desktop/src-tauri/build.rs`
- `src/desktop/src-tauri/tauri.conf.json`
- `src/desktop/src-tauri/capabilities/default.json`
- `src/desktop/src-tauri/assets/startup.html`
- `src/desktop/src-tauri/icons/icon.png`
- `src/desktop/src-tauri/src/main.rs`
- `src/desktop/src-tauri/gen/schemas/*`
- `src/frontend/src/services/api.ts`
- `src/frontend/src/services/authApi.test.ts`

Batch 1 and Batch 2 files were already present as approved dirty worktree changes and were preserved.

Generated build outputs are intentionally ignored:

- `src/backend/Api/wwwroot/`
- `src/desktop/backend-publish/`
- `src/desktop/src-tauri/target/`

## Backend publish strategy

The desktop workspace publishes the backend through `src/desktop/scripts/publish-backend.mjs`.

The strategy is self-contained publish for the selected runtime:

```bash
dotnet publish src/backend/Api/ERP.Api.csproj \
  -c Release \
  -r <runtime> \
  --self-contained true
```

The script first builds the React frontend and copies `src/frontend/dist` into both `src/backend/Api/wwwroot` and `src/desktop/src-tauri/assets`, then publishes the API into `src/desktop/backend-publish/<runtime>`. The Linux verification used `linux-x64`. `HIGHCOOL_DESKTOP_RUNTIME` can override runtime selection.

The published backend includes `appsettings.Desktop.json` and static React assets. It does not include development databases, customer data, backups, cloud credentials, or a final installer.

## Backend process lifecycle

Desktop startup:

1. Resolve app data directory.
2. Create local `Data`, `Backups`, `PendingBackups`, `Logs`, and `Keys` directories.
3. Resolve the backend executable from either `HIGHCOOL_BACKEND_EXECUTABLE`, the development publish folder, or bundled Tauri resources.
4. Generate a per-launch startup token.
5. Load or create a local JWT signing secret file under the app data `Keys` directory.
6. Start the published backend as a tracked child process.
7. Pass runtime settings through environment variables, not command-line arguments.
8. Capture backend stdout/stderr into app-data logs.
9. Wait for readiness before opening the main UI.

Desktop shutdown:

1. Request backend shutdown through `POST /api/desktop/shutdown`.
2. The endpoint is available only in the `Desktop` environment, only from loopback, and only with the per-launch startup token.
3. Wait up to 8 seconds.
4. Kill only the tracked child process if graceful shutdown does not complete.

## Port and readiness strategy

- Port range: `17600..17699`.
- Binding: `ASPNETCORE_URLS=http://127.0.0.1:<port>`.
- The backend never binds to `0.0.0.0` in desktop mode.
- Readiness uses `GET /api/desktop/startup-diagnostics`.
- The diagnostics endpoint requires:
  - `Desktop` environment,
  - loopback remote IP,
  - `X-HighCool-Startup-Token`,
  - fixed-time token comparison.
- The shell treats `HC-Healthy` as ready and maps unsafe diagnostic support codes to a startup failure state.
- Startup timeout: 45 seconds.
- Poll delay: 350 ms.

## Frontend serving and API URL strategy

Desktop mode now uses bundled Tauri frontend assets. Tauri opens:

```text
index.html from the Tauri asset bundle
```

Rust exposes `get_backend_runtime_info`, which returns only the selected `apiOrigin`, `healthUrl`, and `desktopMode`. The React API helper resolves that origin before requests, rejects non-loopback/portless origins, does not persist the origin, and keeps browser/web behavior unchanged.

Existing user authentication remains required. The startup token is only for local process coordination and diagnostics; it is not a user authentication mechanism and is never returned to the frontend.

## 2026-07-26 Runtime Connection Fix

The observed WSLg failure was:

```text
Could not connect to 127.0.0.1
Connection refused
```

Root cause:

- The main WebView depended on backend HTTP navigation.
- The Linux backend child used `PR_SET_PDEATHSIG` but was spawned from a short-lived startup thread. When that thread exited after readiness, `ERP.Api` received the parent-death signal and exited.

Fix:

- Main WebView loads bundled frontend assets.
- Frontend uses a runtime command to obtain the dynamic backend origin.
- The startup thread remains alive as backend supervisor while the child exists.
- Desktop CORS is limited to Tauri/loopback origins.

WSLg verification on 2026-07-26 confirmed startup without manual `Authentication__JwtSecret`, one live backend child, loopback-only binding, `/health` = `OK`, unauthenticated `/api/auth/me` = `401`, cleanup after close, and dynamic fallback to `17601` when `17600` was occupied.

## Single-instance strategy

The shell uses the official Tauri v2 single-instance plugin. A second launch attempts to focus the existing `main` or `startup` window and must not start a separate backend process against the same SQLite database.

Platform limitation: the Linux container smoke environment showed EGL/Zink graphics warnings and did not keep the main webview alive long enough to fully validate interactive second-launch behavior. Static/API/backend pieces were validated, but a durable real-window single-instance smoke needs to be repeated on a normal desktop session.

## Startup UI and diagnostics

The Tauri startup asset is `src/desktop/src-tauri/assets/startup.html`.

It provides:

- startup status text,
- support code display,
- Retry,
- Copy support information,
- Exit.

It does not implement the final restore wizard and does not expose raw backend logs to normal users.

## Logging behavior

Logs are written under the local application data directory:

```text
HighCool app data/
  Logs/
    desktop.log
    backend.log
```

Logs rotate at approximately 1 MiB. Basic sanitization redacts common authorization/JWT/startup-token text. Desktop support copy also redacts secrets and sensitive local paths. No external telemetry was added.

## Security configuration

- Tauri capabilities are minimal: `core:default`.
- No unrestricted filesystem API is exposed to React.
- No arbitrary shell execution API is exposed to React.
- CSP is configured in `tauri.conf.json`.
- Backend access is restricted to loopback URLs.
- Desktop diagnostics and shutdown endpoints require the per-launch startup token and loopback.
- Existing ERP authorization and backup/restore permissions remain unchanged.
- No automatic admin account, embedded admin password, JWT validation bypass, cloud integration, or updater was added.

## Development commands

From `src/desktop`:

```bash
npm ci
npm test
npm run prepare:desktop
npm run desktop:dev
npm run desktop:build
```

The desktop build command performs:

1. React production build.
2. Backend self-contained publish.
3. Tauri build.

Linux native prerequisites were installed during verification:

- `pkg-config`
- `libdbus-1-dev`
- `libglib2.0-dev`
- `libgtk-3-dev`
- `libwebkit2gtk-4.1-dev`
- `libayatana-appindicator3-dev`
- `librsvg2-dev`

Future Windows builds require the official Tauri Windows prerequisites, including Microsoft C++ Build Tools and WebView2. Windows packaging was not executed in this Linux environment.

## Tests added

Frontend:

- Desktop API base URL accepts explicit loopback origins.
- Desktop API base URL rejects external origins.

Desktop Node tests:

- Loopback backend URL restriction.
- Desktop backend environment selects Desktop profile and loopback binding without command-line secrets.
- Support text redacts secrets and sensitive local paths.

Rust test:

- Loopback backend URL port parser rejects non-supported origins.

## Verification results

Backend:

```text
dotnet tool restore                                  Passed
dotnet restore src/backend/ERP.sln                  Passed
dotnet build src/backend/ERP.sln --no-restore       Passed
dotnet test src/backend/ERP.sln --no-build          Passed: 117 tests
```

EF:

```text
dotnet ef migrations list ...                       Passed with expected SQL Server connectivity warning
```

The command listed migrations through:

```text
20260725124500_AddDesktopFoundationBatch2Safety
```

Frontend:

```text
npm ci                                              Passed
npm run build                                       Passed
npm test                                            Passed: 3 files, 13 tests
```

Desktop:

```text
npm ci                                              Passed
npm test                                            Passed: 3 tests
npm run desktop:build                               Passed
```

Rust/Tauri:

```text
cargo fmt --check                                   Passed
cargo check                                         Passed
cargo test                                          Passed: 1 test
```

Whitespace:

```text
git diff --check                                    Passed
```

## Smoke-test result

Linux desktop smoke was partially successful in this container:

Verified:

- Tauri release binary launches.
- The shell starts a backend child process.
- The backend uses `Hosting environment: Desktop`.
- The backend binds to `127.0.0.1:17600`.
- `/health` returns `OK`.
- `/index.html` returns the React production HTML with `<div id="root"></div>`.
- Unauthenticated `/api/auth/me` returns `401`, confirming normal login/auth is still required.
- The local SQLite database is created under isolated app data.
- Restart against the same isolated app-data directory preserved the SQLite database inode.

Not fully verified in this container:

- Durable interactive main-window session.
- Login through the desktop webview.
- Second launch focus behavior.
- Closing the actual desktop window through a normal user window-manager close gesture.

Reason: the container display produced EGL/Zink graphics warnings and the main webview did not remain alive during the idle smoke, while backend/static/API behavior was otherwise correct. This should be repeated on a normal Linux desktop session before approving a customer-facing desktop milestone.

## Windows items not verified

- Windows build prerequisites.
- Windows Tauri build.
- Windows backend self-contained publish.
- Windows app-data path behavior.
- Windows single-instance behavior.
- Windows WebView2 runtime behavior.
- Windows signing/installer packaging.
- Windows production key protection.

## Remaining risks

- The interactive desktop smoke is incomplete in the current Linux container display session.
- The placeholder icon is not final branding.
- The backend still has the broader known JWT hardening and production security items from the audit.
- No final installer, updater, restore wizard UI, scheduled backup, or cloud backup integration exists.
- Windows packaging is not verified.
- Desktop logs currently use basic redaction; deeper structured logging/redaction should be part of hardening.

## Recommended Batch 4 scope

1. Run and fix the interactive desktop smoke on a real Linux desktop session.
2. Add a backend `/api/desktop/shutdown-readiness` or equivalent if restore/migration active-state detection needs richer shutdown protection.
3. Add desktop E2E/window automation where practical.
4. Add final desktop restore-required/recovery UX design, still without implementing cloud providers.
5. Prepare Windows build verification on an actual Windows runner.
6. Replace the placeholder icon with approved brand assets.
7. Continue JWT/secret hardening from the audit before any customer desktop packaging.

## Approval recommendation

Batch 3 is **not yet safe to approve as a fully verified desktop shell** because the real interactive desktop smoke did not fully pass in this environment.

It is safe to approve the non-interactive Batch 3 foundation pieces that were verified: separated Tauri workspace, backend publish, loopback Desktop backend startup, startup diagnostics, static React serving, frontend API URL guard, minimal Tauri permissions, tests, and Linux release build.

---

# Batch 3.1 Interactive Verification Update

Date: 2026-07-25  
Environment: Ubuntu 24.04.1 LTS under WSL2/WSLg  
Display details: `DISPLAY=:0`, `WAYLAND_DISPLAY=wayland-0`, WSLg Weston files present under `/mnt/wslg`; no conventional `XDG_CURRENT_DESKTOP`/`DESKTOP_SESSION` value was exposed.  
WebView runtime: WebKitGTK `2.52.3`; GTK `3.24.41`.  
Tauri CLI: `tauri-cli 2.11.4`.  
Windows: not available and not verified.

## Batch 3.1 defects found and fixed

1. **Readiness response parsing defect**
   - Symptom: the backend started and stayed healthy, but the startup window did not transition to the ERP UI because the shell sometimes classified diagnostics as `HC-Unavailable`.
   - Root cause: the shell used a tiny raw TCP HTTP parser and attempted to parse only the response body as JSON; Kestrel/WebKit-era responses can be chunked or otherwise not shaped exactly as assumed.
   - Fix: `extract_support_code` now extracts `diagnostics.supportCode` from either a plain JSON body or the full HTTP response text, including chunked responses.
   - Regression test added: `extracts_support_code_from_plain_or_chunked_http_response`.

2. **Transient readiness classification defect**
   - Symptom: `HC-Unavailable`, which is a shell-local “diagnostics not readable yet” condition, could be treated as fatal.
   - Fix: `HC-Unavailable` and `HC-DatabaseMissing` are retried until the startup timeout; real Batch 2 unsafe states such as `HC-DatabaseUnavailable`, `HC-DatabaseCorrupt`, and `HC-UnsupportedSchema` remain fatal.
   - Regression test added: `classifies_only_startup_unavailable_codes_as_transient`.

3. **Window-manager close/orphan backend defect**
   - Symptom: under WSLg, a normal window-manager close could exit the desktop process without running the previous `CloseRequested` backend cleanup path, leaving the backend process alive.
   - Fix: added a Tauri `RunEvent::Exit` cleanup hook that idempotently stops the tracked backend on application exit.

4. **Forced desktop termination/orphan backend risk on Linux**
   - Symptom: force-killing the desktop shell could leave the backend child process alive.
   - Fix: on Linux, the backend child is started with `prctl(PR_SET_PDEATHSIG, SIGTERM)` so the OS sends SIGTERM to the backend if the desktop parent process dies unexpectedly.
   - Dependency added: `libc = "0.2"` in the Tauri crate.

5. **Diagnostics observability**
   - Added sanitized lifecycle log lines for backend start, readiness success, startup-window hide, shutdown request, backend exit status, and runtime exit.

## Batch 3.1 files changed

- `src/desktop/src-tauri/Cargo.toml`
- `src/desktop/src-tauri/Cargo.lock`
- `src/desktop/src-tauri/src/main.rs`
- `HIGHCOOL_DESKTOP_FOUNDATION_BATCH3_RESULT.md`
- status docs updated where applicable

## Automated regression checks rerun

Backend:

```text
dotnet restore src/backend/ERP.sln                  Passed
dotnet build src/backend/ERP.sln --no-restore       Passed
dotnet test src/backend/ERP.sln --no-build          Passed: 117 tests
```

Frontend:

```text
npm ci                                              Passed
npm run build                                       Passed
npm test                                            Passed: 3 files, 13 tests
```

Desktop/Rust:

```text
npm ci                                              Passed
cargo fmt --check                                   Passed
cargo check                                         Passed
cargo test                                          Passed: 3 tests
npm test                                            Passed: 3 tests
npm run desktop:build                               Passed
git diff --check                                    Passed
```

## Interactive verification actually executed

Verified in WSLg targeted runs:

- Startup window appears first as a 520x420 `HighCool` window.
- Exactly one backend process starts.
- Backend uses the Desktop environment.
- Backend binds only to `127.0.0.1:17600`.
- Readiness succeeds.
- Startup window hides after readiness.
- Main ERP window appears as a visible 1908x1047 `HighCool` window.
- The main window renders the React UI.
- Unauthenticated `/api/auth/me` returns `401`.
- A real test owner can be created/authenticated through the local desktop backend API.
- Authenticated `/api/auth/me` returns `200`.
- Existing ERP route `/api/uom-conversions` returns `200`.
- Permissioned local diagnostics return `HC-Healthy`.
- Unauthenticated desktop diagnostics and shutdown endpoints return `401`.
- Permissioned encrypted manual backup succeeds and creates `.db.enc` plus manifest files.
- SQLite database remains present.
- `application_database_metadata` has exactly one row.
- Single-instance second launch exits while the first instance remains alive and the backend PID remains unchanged.
- Normal main-window close stops the desktop process and, after the Batch 3.1 exit-hook fix, removes the backend process.
- Restart against the same app-data directory preserves the database inode and metadata row.
- Force-killing the desktop process no longer leaves a backend orphan on Linux after the parent-death-signal fix.
- Launch after forced termination starts cleanly with exactly one backend process.

## Interactive verification limitations

Not fully verified in this WSLg environment:

- Browser developer tools inspection for blocked API requests.
- External-navigation behavior through an in-window clicked external link.
- All requested startup failure scenarios. Only the startup/readiness failure path that was actually observed was debugged and fixed.
- Protected-operation shutdown while backup/upgrade/restore is actively running.
- Full UI login typing with xdotool: WSLg/X11 automation triggered a GDK `BadDrawable` error in one combined script run. API authentication and real webview route rendering were verified instead.
- Windows 10/11 behavior.

## Batch 3.1 approval recommendation

Batch 3 is **improved but still not fully approved** against the Batch 3.1 definition of done, because not every requested interactive/failure/security scenario was actually executed successfully in this WSLg environment.

The previously blocking desktop-shell defects that were observed in Batch 3.1 were fixed and regression-checked. A normal Linux desktop session, not WSLg automation, should run the remaining UI/devtools/failure-state matrix before marking the desktop shell fully approved.

---

# Batch 3.2 Full Desktop Verification Attempt

Date: 2026-07-25  
Environment available in this workspace: Ubuntu 24.04.1 LTS under WSL2/WSLg, not a normal Ubuntu desktop VM or physical Linux desktop.  
Display details: `DISPLAY=:0`, `WAYLAND_DISPLAY=wayland-0`, no `XDG_CURRENT_DESKTOP` or `DESKTOP_SESSION` exposed.  
WebView runtime: WebKitGTK `2.52.3`; GTK `3.24.41`.  
.NET SDK/runtime: SDK `8.0.129`, ASP.NET Core runtime `8.0.29`.  
Node/npm: Node.js `v22.23.1`, npm `10.9.8`.  
Rust/Cargo: `rustc 1.97.1`, `cargo 1.97.1`.  
Tauri CLI: `tauri-cli 2.11.4`.  
Windows: not available in this workspace and not verified.

## Batch 3.2 scope result

Batch 3.2 could not produce full desktop approval from this workspace because the task explicitly requires:

- a primary Linux approval run on a normal Ubuntu desktop VM, physical Ubuntu desktop, or another normal Linux graphical desktop, not WSLg;
- a Windows 10/11 verification pass.

Those environments were not available here. The WSLg run was used only as a non-approving local smoke and regression aid.

## Batch 3.2 defect found and fixed

1. **Documented desktop Cargo commands failed from `src/desktop`**
   - Symptom: the required `cargo fmt --check` command failed with `could not find Cargo.toml in /root/HighCool/src/desktop or any parent directory`.
   - Root cause: the Rust crate lived under `src/desktop/src-tauri`, while the documented verification command is run from `src/desktop`.
   - Fix: added a workspace manifest at `src/desktop/Cargo.toml` with `src-tauri` as the member.
   - Follow-up cleanup: moved the release profile from `src/desktop/src-tauri/Cargo.toml` to the workspace root so optimized release builds keep `panic = "abort"`, `codegen-units = 1`, and `lto = true` without Cargo ignoring the profile.
   - Generated release output now lands under `src/desktop/target/`; `.gitignore` was updated to exclude it.

## Batch 3.2 exact files changed

- `.gitignore`
- `src/desktop/Cargo.toml`
- `src/desktop/Cargo.lock`
- `src/desktop/src-tauri/Cargo.toml`
- `HIGHCOOL_DESKTOP_FOUNDATION_BATCH3_RESULT.md`
- status docs updated where applicable

## Batch 3.2 automated regression checks

Backend:

```text
dotnet tool restore                                  Passed
dotnet restore src/backend/ERP.sln                  Passed
dotnet build src/backend/ERP.sln --no-restore       Passed; 0 warnings, 0 errors
dotnet test src/backend/ERP.sln --no-build          Passed: 117 tests
```

Frontend:

```text
cd src/frontend
npm ci                                              Passed; npm audit reported 6 existing vulnerabilities
npm run build                                       Passed; Vite chunk-size warning only
npm test                                            Passed: 3 files, 13 tests
```

Desktop:

```text
cd src/desktop
npm ci                                              Passed; 0 vulnerabilities
cargo fmt --check                                   Initially failed before the workspace fix; passed after fix
cargo check                                         Passed
cargo test                                          Passed: 3 tests
npm test                                            Passed: 3 tests
npm run desktop:build                               Passed
git diff --check                                    Passed
```

After moving the Cargo release profile to the workspace root, the affected checks were rerun:

```text
cargo fmt --check                                   Passed
cargo check                                         Passed
cargo test                                          Passed: 3 tests
npm run desktop:build                               Passed
git diff --check                                    Passed
```

## Batch 3.2 WSLg smoke result

Executed only as a non-approving local smoke in WSLg:

- The real release desktop binary launched with isolated `XDG_DATA_HOME`/`XDG_CONFIG_HOME`.
- Desktop lifecycle logs recorded backend startup on loopback port `17600`.
- Desktop lifecycle logs recorded backend readiness success.
- Desktop lifecycle logs recorded startup-window hide after readiness.
- No `ERP.Api` process remained after the WSLg shell exited/was closed.
- The app data/log directories were isolated under `/tmp/highcool-b32-smoke*`.

Limitations of the WSLg smoke:

- This is not the required normal Linux graphical desktop approval run.
- In the repeated Batch 3.2 WSLg smoke, the WebView/runtime exited before reliable UI login, API probing, developer-tools inspection, or external-navigation checks could be completed.
- Therefore, Batch 3.2 did not verify UI login, existing ERP page loading, WebView developer-tools security state, external-navigation behavior, the full shutdown matrix, the full startup-failure matrix, or the persistence matrix.

## Batch 3.2 unverified required matrix

Not executed in this workspace:

- normal Linux desktop environment approval run;
- Windows 10/11 verification;
- WebView developer-tools inspection;
- external-navigation security matrix;
- full single-instance repeated matrix on normal desktop;
- graceful shutdown during active API request, active backup, readiness polling, simulated protected lock, hidden/minimized window, and slow backend stop;
- startup-failure scenarios for occupied port range, missing backend executable, invalid desktop configuration, corrupt SQLite database, unsupported newer schema, unwritable data directory, early backend exit, readiness timeout, and invalid startup-token response;
- persistence matrix through UI-created sentinel record, encrypted backup preservation, forced exit, and failed-startup recovery.

## Batch 3.2 approval decision

Batch 3 is **not fully approved** after Batch 3.2.

The automated regression suite and Linux release build are green, and the documented desktop Cargo commands now work from `src/desktop`. Full approval remains blocked on the required normal Linux desktop and Windows verification environments and on the unexecuted interactive/security/failure/persistence matrices.
