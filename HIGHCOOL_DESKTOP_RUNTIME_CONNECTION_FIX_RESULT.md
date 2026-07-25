# HighCool Desktop Runtime Connection Fix Result

Date: 2026-07-26

## Observed Symptom

The Tauri launcher reported backend readiness on a dynamic loopback port, but the desktop window showed a native WebKit connection-refused page for `127.0.0.1`.

## Root Cause

Two runtime wiring defects combined:

- The main WebView loaded `http://127.0.0.1:<selected-port>/index.html`, so the application shell itself depended on backend HTTP navigation.
- On Linux, the backend child used `PR_SET_PDEATHSIG` but was spawned from a short-lived startup thread. After readiness, that thread returned, the backend received the parent-death signal, and `ERP.Api` exited cleanly. The WebView was then left pointing at a loopback backend that was no longer listening.

The desktop JWT secret generation path was not the cause. The launcher generated/reused the app-data key, stored it with restrictive permissions, and passed it to `ERP.Api` through `Authentication__JwtSecret` without command-line exposure.

## Fix

- The main WebView now loads bundled Tauri frontend assets instead of navigating to the backend origin.
- The desktop build preparation copies the React production build into both backend `wwwroot` and Tauri bundled assets.
- Rust exposes a safe `get_backend_runtime_info` command returning only `apiOrigin`, `healthUrl`, and `desktopMode`.
- The frontend resolves that runtime origin before API requests and rejects non-loopback or portless origins.
- A desktop runtime gate renders application-owned startup/connection/unavailable states with Retry.
- The Linux backend parent-death signal is preserved, but the startup thread now remains alive as a backend supervisor while the child exists.
- Desktop CORS allows only Tauri/loopback origins in Desktop mode.

## Verification

Interactive WSLg/runtime verification completed without manually exporting `Authentication__JwtSecret`:

- Startup readiness succeeded on `127.0.0.1:17600`.
- The main WebView loaded bundled assets from Tauri's asset server instead of backend HTTP.
- Exactly one `highcool-desktop` process and one live `ERP.Api` child remained after startup.
- `ERP.Api` listened only on `127.0.0.1:17600`.
- `GET /health` returned `OK`.
- `GET /api/auth/me` returned `401` before login, as expected.
- Closing the dev shell left no `highcool-desktop`, `ERP.Api`, or loopback listener behind.
- With `17600` occupied by a dummy listener, the backend selected `17601` and `/health` returned `OK`.

## Remaining Limits

- Windows packaging and Windows runtime verification remain pending.
- Full normal-desktop UI/devtools/external-navigation/failure-state approval remains pending outside WSLg.
- Scheduled backups, cloud backups, backup deletion, installer, and updater work remain out of scope.
