use base64::Engine;
use rand::RngCore;
use serde::Serialize;
#[cfg(unix)]
use std::os::unix::fs::PermissionsExt;
#[cfg(target_os = "linux")]
use std::os::unix::process::CommandExt;
use std::{
    fs::{self, File, OpenOptions},
    io::{Read, Write},
    net::TcpStream,
    path::{Path, PathBuf},
    process::{Child, Command, Stdio},
    sync::{Arc, Mutex},
    thread,
    time::{Duration, Instant},
};
use tauri::{AppHandle, Emitter, Manager, State, WebviewUrl, WebviewWindowBuilder};

const PORT_START: u16 = 17600;
const PORT_END: u16 = 17699;
const STARTUP_TIMEOUT_SECONDS: u64 = 45;
const SHUTDOWN_TIMEOUT_SECONDS: u64 = 8;
const LOG_MAX_BYTES: u64 = 1_048_576;

#[derive(Default, Clone)]
struct DesktopState {
    inner: Arc<Mutex<DesktopRuntime>>,
}

#[derive(Default)]
struct DesktopRuntime {
    child: Option<Child>,
    backend_url: Option<String>,
    startup_token: Option<String>,
    app_data_dir: Option<PathBuf>,
    main_window_opened: bool,
    last_support: SupportInformation,
}

#[derive(Default, Clone, Serialize)]
struct SupportInformation {
    highcool_version: String,
    desktop_shell_version: String,
    backend_url: Option<String>,
    startup_status: String,
    support_code: String,
    backend_exit_code: Option<i32>,
}

#[derive(Clone, Serialize)]
struct StartupStatusPayload {
    title: String,
    message: String,
    support_code: String,
    failed: bool,
}

fn main() {
    let app = tauri::Builder::default()
        .plugin(tauri_plugin_single_instance::init(|app, _args, _cwd| {
            if let Some(window) = app
                .get_webview_window("main")
                .or_else(|| app.get_webview_window("startup"))
            {
                let _ = window.show();
                let _ = window.set_focus();
            }
        }))
        .manage(DesktopState::default())
        .invoke_handler(tauri::generate_handler![
            retry_startup,
            copy_support_information,
            exit_app
        ])
        .setup(|app| {
            let handle = app.handle().clone();
            let state = app.state::<DesktopState>().inner.clone();
            thread::spawn(move || start_backend_and_open_window(handle, state));
            Ok(())
        })
        .on_window_event(|window, event| {
            if matches!(event, tauri::WindowEvent::CloseRequested { .. }) {
                let should_stop_backend = if window.label() == "main" {
                    true
                } else if window.label() == "startup" {
                    let state = window.state::<DesktopState>().inner.clone();
                    let runtime = state.lock().expect("desktop state poisoned");
                    !runtime.main_window_opened
                } else {
                    false
                };

                if should_stop_backend {
                    let state = window.state::<DesktopState>().inner.clone();
                    log_desktop_event(
                        &state,
                        &format!("window close requested: {}", window.label()),
                    );
                    stop_backend(state);
                    if window.label() == "main" {
                        window.app_handle().exit(0);
                    }
                }
            }
        })
        .build(tauri::generate_context!())
        .expect("failed to build HighCool desktop shell");

    app.run(|app, event| {
        if matches!(event, tauri::RunEvent::Exit) {
            let state = app.state::<DesktopState>().inner.clone();
            log_desktop_event(&state, "desktop runtime exiting");
            stop_backend(state);
        }
    });
}

#[tauri::command]
fn retry_startup(app: AppHandle, state: State<DesktopState>) {
    log_desktop_event(&state.inner, "startup retry requested");
    stop_backend(state.inner.clone());
    {
        let mut runtime = state.inner.lock().expect("desktop state poisoned");
        runtime.main_window_opened = false;
    }
    let handle = app.clone();
    let runtime = state.inner.clone();
    thread::spawn(move || start_backend_and_open_window(handle, runtime));
}

#[tauri::command]
fn copy_support_information(state: State<DesktopState>) -> String {
    let runtime = state.inner.lock().expect("desktop state poisoned");
    serde_json::to_string_pretty(&runtime.last_support)
        .unwrap_or_else(|_| "Support information unavailable".to_string())
}

#[tauri::command]
fn exit_app(app: AppHandle, state: State<DesktopState>) {
    log_desktop_event(&state.inner, "desktop exit requested");
    stop_backend(state.inner.clone());
    app.exit(0);
}

fn start_backend_and_open_window(app: AppHandle, state: Arc<Mutex<DesktopRuntime>>) {
    emit_status(
        &app,
        "Starting backend",
        "Starting the local HighCool backend.",
        "HC-StartingBackend",
        false,
    );

    let app_data_dir = match app.path().app_data_dir() {
        Ok(path) => path,
        Err(_) => {
            fail_startup(
                &app,
                &state,
                "Configuration invalid",
                "HighCool could not resolve a local application data directory.",
                "HC-AppData",
            );
            return;
        }
    };

    if let Err(error) = prepare_local_directories(&app_data_dir) {
        let _ = write_desktop_log(
            &app_data_dir,
            &format!("startup directory preparation failed: {error}"),
        );
        fail_startup(
            &app,
            &state,
            "Configuration invalid",
            "HighCool could not prepare local application directories.",
            "HC-LocalPaths",
        );
        return;
    }

    let backend_executable = match resolve_backend_executable(&app) {
        Some(path) => path,
        None => {
            let _ = write_desktop_log(&app_data_dir, "backend executable was not found");
            fail_startup(
                &app,
                &state,
                "Startup failed",
                "The bundled HighCool backend was not found.",
                "HC-BackendMissing",
            );
            return;
        }
    };

    let startup_token = random_secret();
    let jwt_secret =
        get_or_create_secret(&app_data_dir.join("Keys").join("highcool-desktop-jwt.key"))
            .unwrap_or_else(|_| random_secret());

    for port in PORT_START..=PORT_END {
        let _ = write_desktop_log(
            &app_data_dir,
            &format!("starting backend on loopback port {port}"),
        );
        emit_status(
            &app,
            "Checking local database",
            "Waiting for the local backend health checks.",
            "HC-Readiness",
            false,
        );
        match spawn_backend(
            &backend_executable,
            &app_data_dir,
            port,
            &startup_token,
            &jwt_secret,
        ) {
            Ok(child) => {
                {
                    let mut runtime = state.lock().expect("desktop state poisoned");
                    runtime.child = Some(child);
                    runtime.backend_url = Some(format!("http://127.0.0.1:{port}"));
                    runtime.startup_token = Some(startup_token.clone());
                    runtime.app_data_dir = Some(app_data_dir.clone());
                    runtime.last_support = SupportInformation {
                        highcool_version: "unknown".to_string(),
                        desktop_shell_version: env!("CARGO_PKG_VERSION").to_string(),
                        backend_url: runtime.backend_url.clone(),
                        startup_status: "Starting".to_string(),
                        support_code: "HC-Readiness".to_string(),
                        backend_exit_code: None,
                    };
                }

                match wait_for_readiness(
                    &state,
                    port,
                    &startup_token,
                    Duration::from_secs(STARTUP_TIMEOUT_SECONDS),
                ) {
                    ReadinessOutcome::Ready => {
                        let _ = write_desktop_log(
                            &app_data_dir,
                            &format!("backend readiness succeeded on loopback port {port}"),
                        );
                        emit_status(
                            &app,
                            "Starting application",
                            "Opening HighCool.",
                            "HC-Ready",
                            false,
                        );
                        {
                            let mut runtime = state.lock().expect("desktop state poisoned");
                            runtime.main_window_opened = true;
                        }
                        open_main_window(&app, port);
                        if let Some(startup) = app.get_webview_window("startup") {
                            let _ = write_desktop_log(
                                &app_data_dir,
                                "startup window hidden after readiness",
                            );
                            let _ = startup.hide();
                        }
                        return;
                    }
                    ReadinessOutcome::EarlyExit(code) => {
                        update_exit_code(&state, code);
                        let _ = write_desktop_log(
                            &app_data_dir,
                            &format!("backend exited early on port {port}: {code:?}"),
                        );
                    }
                    ReadinessOutcome::Timeout => {
                        let _ = write_desktop_log(
                            &app_data_dir,
                            &format!("backend startup timed out on port {port}"),
                        );
                        stop_backend(state.clone());
                        fail_startup(
                            &app,
                            &state,
                            "Startup failed",
                            "HighCool did not become ready before the startup timeout.",
                            "HC-StartupTimeout",
                        );
                        return;
                    }
                    ReadinessOutcome::Unhealthy(code) => {
                        stop_backend(state.clone());
                        fail_startup(
                            &app,
                            &state,
                            "Startup failed",
                            "HighCool local diagnostics reported an unsafe startup state.",
                            &code,
                        );
                        return;
                    }
                }
            }
            Err(error) => {
                let _ = write_desktop_log(
                    &app_data_dir,
                    &format!("failed to start backend on port {port}: {error}"),
                );
            }
        }
    }

    fail_startup(
        &app,
        &state,
        "Startup failed",
        "No safe local loopback port was available for HighCool.",
        "HC-PortUnavailable",
    );
}

fn spawn_backend(
    executable: &Path,
    app_data_dir: &Path,
    port: u16,
    startup_token: &str,
    jwt_secret: &str,
) -> std::io::Result<Child> {
    let logs_dir = app_data_dir.join("Logs");
    let data_dir = app_data_dir.join("Data");
    let backups_dir = app_data_dir.join("Backups");
    let pending_dir = app_data_dir.join("PendingBackups");

    let mut command = Command::new(executable);
    command
        .current_dir(executable.parent().unwrap_or_else(|| Path::new(".")))
        .env("ASPNETCORE_ENVIRONMENT", "Desktop")
        .env("ASPNETCORE_URLS", format!("http://127.0.0.1:{port}"))
        .env("Database__Provider", "Sqlite")
        .env("LocalStorage__DataDirectory", data_dir)
        .env("LocalStorage__BackupDirectory", backups_dir)
        .env("LocalStorage__PendingBackupDirectory", pending_dir)
        .env("LocalStorage__LogDirectory", logs_dir.clone())
        .env("LocalDatabase__AllowDevelopmentReset", "false")
        .env("Desktop__StartupToken", startup_token)
        .env("Authentication__JwtSecret", jwt_secret)
        .stdin(Stdio::null())
        .stdout(Stdio::piped())
        .stderr(Stdio::piped());

    #[cfg(target_os = "linux")]
    unsafe {
        command.pre_exec(|| {
            let result = libc::prctl(libc::PR_SET_PDEATHSIG, libc::SIGTERM);
            if result == 0 {
                Ok(())
            } else {
                Err(std::io::Error::last_os_error())
            }
        });
    }

    let mut child = command.spawn()?;

    if let Some(stdout) = child.stdout.take() {
        pipe_log(stdout, logs_dir.join("backend.log"));
    }

    if let Some(stderr) = child.stderr.take() {
        pipe_log(stderr, logs_dir.join("backend.log"));
    }

    Ok(child)
}

fn wait_for_readiness(
    state: &Arc<Mutex<DesktopRuntime>>,
    port: u16,
    startup_token: &str,
    timeout: Duration,
) -> ReadinessOutcome {
    let started = Instant::now();
    while started.elapsed() < timeout {
        {
            let mut runtime = state.lock().expect("desktop state poisoned");
            if let Some(child) = runtime.child.as_mut() {
                match child.try_wait() {
                    Ok(Some(status)) => return ReadinessOutcome::EarlyExit(status.code()),
                    Ok(None) => {}
                    Err(_) => return ReadinessOutcome::EarlyExit(None),
                }
            }
        }

        match read_startup_diagnostics(port, startup_token) {
            Ok(code) if code == "HC-Healthy" => return ReadinessOutcome::Ready,
            Ok(code) if is_transient_readiness_code(&code) => {
                thread::sleep(Duration::from_millis(350));
            }
            Ok(code) if code.starts_with("HC-") => return ReadinessOutcome::Unhealthy(code),
            _ => thread::sleep(Duration::from_millis(350)),
        }
    }

    ReadinessOutcome::Timeout
}

fn is_transient_readiness_code(code: &str) -> bool {
    matches!(code, "HC-Unavailable" | "HC-DatabaseMissing")
}

enum ReadinessOutcome {
    Ready,
    EarlyExit(Option<i32>),
    Timeout,
    Unhealthy(String),
}

fn read_startup_diagnostics(port: u16, startup_token: &str) -> Result<String, std::io::Error> {
    let mut stream = TcpStream::connect(("127.0.0.1", port))?;
    stream.set_read_timeout(Some(Duration::from_secs(2)))?;
    stream.set_write_timeout(Some(Duration::from_secs(2)))?;
    let request = format!(
        "GET /api/desktop/startup-diagnostics HTTP/1.1\r\nHost: 127.0.0.1:{port}\r\nX-HighCool-Startup-Token: {startup_token}\r\nConnection: close\r\n\r\n"
    );
    stream.write_all(request.as_bytes())?;
    let mut response = String::new();
    stream.read_to_string(&mut response)?;
    if !response.starts_with("HTTP/1.1 200") && !response.starts_with("HTTP/1.0 200") {
        return Ok("HC-Unavailable".to_string());
    }

    Ok(extract_support_code(&response).unwrap_or_else(|| "HC-Unavailable".to_string()))
}

fn extract_support_code(response: &str) -> Option<String> {
    let body = response.split("\r\n\r\n").nth(1).unwrap_or(response);
    if let Ok(json) = serde_json::from_str::<serde_json::Value>(body) {
        if let Some(code) = json
            .pointer("/diagnostics/supportCode")
            .and_then(|value| value.as_str())
        {
            return Some(code.to_string());
        }
    }

    let marker = "\"supportCode\":\"";
    let start = response.find(marker)? + marker.len();
    let end = response[start..].find('"')?;
    Some(response[start..start + end].to_string())
}

fn open_main_window(app: &AppHandle, port: u16) {
    let url = format!("http://127.0.0.1:{port}/index.html");
    let parsed = url.parse().expect("loopback backend URL should parse");
    let _ = WebviewWindowBuilder::new(app, "main", WebviewUrl::External(parsed))
        .title("HighCool")
        .maximized(true)
        .visible(true)
        .build();
}

fn stop_backend(state: Arc<Mutex<DesktopRuntime>>) {
    let (mut child, backend_url, startup_token, app_data_dir) = {
        let mut runtime = state.lock().expect("desktop state poisoned");
        (
            runtime.child.take(),
            runtime.backend_url.clone(),
            runtime.startup_token.clone(),
            runtime.app_data_dir.clone(),
        )
    };

    if let Some(mut tracked_child) = child.take() {
        if let Some(directory) = app_data_dir.as_deref() {
            let _ = write_desktop_log(directory, "backend shutdown requested");
        }

        if let (Some(url), Some(token)) = (backend_url, startup_token) {
            if let Some(port) = parse_loopback_port(&url) {
                if let Err(error) = request_backend_shutdown(port, &token) {
                    if let Some(directory) = app_data_dir.as_deref() {
                        let _ = write_desktop_log(
                            directory,
                            &format!("backend graceful shutdown request failed: {error}"),
                        );
                    }
                }
            }
        }

        let started = Instant::now();
        loop {
            match tracked_child.try_wait() {
                Ok(Some(status)) => {
                    if let Some(directory) = app_data_dir.as_deref() {
                        let _ = write_desktop_log(
                            directory,
                            &format!("backend exited with status: {status}"),
                        );
                    }
                    break;
                }
                Ok(None) if started.elapsed() < Duration::from_secs(SHUTDOWN_TIMEOUT_SECONDS) => {
                    thread::sleep(Duration::from_millis(150));
                }
                _ => {
                    if let Some(directory) = app_data_dir.as_deref() {
                        let _ = write_desktop_log(
                            directory,
                            "backend did not exit before timeout; terminating tracked child",
                        );
                    }
                    let _ = tracked_child.kill();
                    let _ = tracked_child.wait();
                    break;
                }
            }
        }
    }

    let mut runtime = state.lock().expect("desktop state poisoned");
    runtime.main_window_opened = false;
}

fn log_desktop_event(state: &Arc<Mutex<DesktopRuntime>>, message: &str) {
    let app_data_dir = {
        let runtime = state.lock().expect("desktop state poisoned");
        runtime.app_data_dir.clone()
    };

    if let Some(directory) = app_data_dir {
        let _ = write_desktop_log(&directory, message);
    }
}

fn parse_loopback_port(url: &str) -> Option<u16> {
    url.strip_prefix("http://127.0.0.1:")
        .or_else(|| url.strip_prefix("http://localhost:"))
        .and_then(|port| port.parse::<u16>().ok())
}

fn request_backend_shutdown(port: u16, startup_token: &str) -> Result<(), std::io::Error> {
    let mut stream = TcpStream::connect(("127.0.0.1", port))?;
    stream.set_read_timeout(Some(Duration::from_secs(2)))?;
    stream.set_write_timeout(Some(Duration::from_secs(2)))?;
    let request = format!(
        "POST /api/desktop/shutdown HTTP/1.1\r\nHost: 127.0.0.1:{port}\r\nX-HighCool-Startup-Token: {startup_token}\r\nContent-Length: 0\r\nConnection: close\r\n\r\n"
    );
    stream.write_all(request.as_bytes())?;

    let mut response = String::new();
    let _ = stream.read_to_string(&mut response);
    Ok(())
}

fn emit_status(app: &AppHandle, title: &str, message: &str, support_code: &str, failed: bool) {
    let _ = app.emit(
        "startup-status",
        StartupStatusPayload {
            title: title.to_string(),
            message: message.to_string(),
            support_code: support_code.to_string(),
            failed,
        },
    );
}

fn fail_startup(
    app: &AppHandle,
    state: &Arc<Mutex<DesktopRuntime>>,
    title: &str,
    message: &str,
    support_code: &str,
) {
    {
        let mut runtime = state.lock().expect("desktop state poisoned");
        runtime.last_support.startup_status = title.to_string();
        runtime.last_support.support_code = support_code.to_string();
    }

    emit_status(app, title, message, support_code, true);
}

fn update_exit_code(state: &Arc<Mutex<DesktopRuntime>>, code: Option<i32>) {
    let mut runtime = state.lock().expect("desktop state poisoned");
    runtime.last_support.backend_exit_code = code;
}

fn resolve_backend_executable(app: &AppHandle) -> Option<PathBuf> {
    if let Ok(path) = std::env::var("HIGHCOOL_BACKEND_EXECUTABLE") {
        let candidate = PathBuf::from(path);
        if candidate.exists() {
            return Some(candidate);
        }
    }

    let dev_candidate = PathBuf::from(env!("CARGO_MANIFEST_DIR"))
        .join("..")
        .join("backend-publish")
        .join("linux-x64")
        .join(if cfg!(windows) {
            "ERP.Api.exe"
        } else {
            "ERP.Api"
        });
    if dev_candidate.exists() {
        return Some(dev_candidate);
    }

    app.path()
        .resource_dir()
        .ok()
        .map(|dir| {
            dir.join("backend-publish")
                .join("linux-x64")
                .join(if cfg!(windows) {
                    "ERP.Api.exe"
                } else {
                    "ERP.Api"
                })
        })
        .filter(|candidate| candidate.exists())
}

fn prepare_local_directories(app_data_dir: &Path) -> std::io::Result<()> {
    for directory in ["Data", "Backups", "PendingBackups", "Logs", "Keys"] {
        let path = app_data_dir.join(directory);
        fs::create_dir_all(&path)?;
        restrict_to_current_user(&path)?;
    }

    Ok(())
}

fn get_or_create_secret(path: &Path) -> std::io::Result<String> {
    if path.exists() {
        return fs::read_to_string(path).map(|value| value.trim().to_string());
    }

    let secret = random_secret();
    if let Some(parent) = path.parent() {
        fs::create_dir_all(parent)?;
        restrict_to_current_user(parent)?;
    }
    fs::write(path, &secret)?;
    restrict_to_current_user(path)?;
    Ok(secret)
}

#[cfg(unix)]
fn restrict_to_current_user(path: &Path) -> std::io::Result<()> {
    let mode = if path.is_dir() { 0o700 } else { 0o600 };
    fs::set_permissions(path, fs::Permissions::from_mode(mode))
}

#[cfg(not(unix))]
fn restrict_to_current_user(_path: &Path) -> std::io::Result<()> {
    Ok(())
}

fn random_secret() -> String {
    let mut bytes = [0_u8; 48];
    rand::thread_rng().fill_bytes(&mut bytes);
    base64::engine::general_purpose::URL_SAFE_NO_PAD.encode(bytes)
}

fn pipe_log<R>(mut reader: R, path: PathBuf)
where
    R: Read + Send + 'static,
{
    thread::spawn(move || {
        let mut buffer = [0_u8; 1024];
        loop {
            match reader.read(&mut buffer) {
                Ok(0) | Err(_) => break,
                Ok(count) => {
                    let line = String::from_utf8_lossy(&buffer[..count]);
                    let _ = append_log(&path, &sanitize_log_line(&line));
                }
            }
        }
    });
}

fn write_desktop_log(app_data_dir: &Path, message: &str) -> std::io::Result<()> {
    append_log(&app_data_dir.join("Logs").join("desktop.log"), message)
}

fn append_log(path: &Path, message: &str) -> std::io::Result<()> {
    rotate_log_if_needed(path)?;
    let mut file = OpenOptions::new().create(true).append(true).open(path)?;
    writeln!(file, "{message}")?;
    Ok(())
}

fn rotate_log_if_needed(path: &Path) -> std::io::Result<()> {
    if let Ok(metadata) = fs::metadata(path) {
        if metadata.len() > LOG_MAX_BYTES {
            let rotated = path.with_extension("log.1");
            let _ = fs::remove_file(&rotated);
            fs::rename(path, rotated)?;
            let _ = File::create(path)?;
        }
    }

    Ok(())
}

fn sanitize_log_line(value: &str) -> String {
    value
        .replace("Authorization: Bearer", "Authorization: Bearer [redacted]")
        .replace("JwtSecret", "JwtSecret=[redacted]")
        .replace("StartupToken", "StartupToken=[redacted]")
}

#[cfg(test)]
mod tests {
    use super::parse_loopback_port;
    #[cfg(unix)]
    use std::os::unix::fs::PermissionsExt;

    #[test]
    fn parses_only_supported_loopback_backend_urls() {
        assert_eq!(parse_loopback_port("http://127.0.0.1:17600"), Some(17600));
        assert_eq!(parse_loopback_port("http://localhost:17699"), Some(17699));
        assert_eq!(parse_loopback_port("http://example.com:17600"), None);
        assert_eq!(parse_loopback_port("https://127.0.0.1:17600"), None);
    }

    #[test]
    fn classifies_only_startup_unavailable_codes_as_transient() {
        assert!(super::is_transient_readiness_code("HC-Unavailable"));
        assert!(super::is_transient_readiness_code("HC-DatabaseMissing"));
        assert!(!super::is_transient_readiness_code(
            "HC-DatabaseUnavailable"
        ));
        assert!(!super::is_transient_readiness_code("HC-DatabaseCorrupt"));
        assert!(!super::is_transient_readiness_code("HC-UnsupportedSchema"));
    }

    #[test]
    fn extracts_support_code_from_plain_or_chunked_http_response() {
        let plain = "HTTP/1.1 200 OK\r\nContent-Type: application/json\r\n\r\n{\"diagnostics\":{\"supportCode\":\"HC-Healthy\"}}";
        let chunked = "HTTP/1.1 200 OK\r\nTransfer-Encoding: chunked\r\n\r\n2a\r\n{\"diagnostics\":{\"supportCode\":\"HC-Healthy\"}}\r\n0\r\n\r\n";

        assert_eq!(
            super::extract_support_code(plain),
            Some("HC-Healthy".to_string())
        );
        assert_eq!(
            super::extract_support_code(chunked),
            Some("HC-Healthy".to_string())
        );
    }

    #[test]
    fn desktop_secret_is_reused_and_restricted() {
        let directory = std::env::temp_dir().join(format!(
            "highcool-desktop-secret-test-{}",
            super::random_secret()
        ));
        let secret_path = directory.join("Keys").join("highcool-desktop-jwt.key");

        let first = super::get_or_create_secret(&secret_path).expect("secret created");
        let second = super::get_or_create_secret(&secret_path).expect("secret reused");

        assert_eq!(first, second);
        assert!(secret_path.exists());
        assert!(!secret_path.starts_with(std::env::current_dir().expect("cwd")));

        #[cfg(unix)]
        {
            let key_dir_mode = std::fs::metadata(secret_path.parent().unwrap())
                .expect("key dir metadata")
                .permissions()
                .mode()
                & 0o777;
            let secret_mode = std::fs::metadata(&secret_path)
                .expect("secret metadata")
                .permissions()
                .mode()
                & 0o777;
            assert_eq!(0o700, key_dir_mode);
            assert_eq!(0o600, secret_mode);
        }

        let _ = std::fs::remove_dir_all(directory);
    }
}
