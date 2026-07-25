export const loopbackHosts = new Set(["localhost", "127.0.0.1", "[::1]", "::1"]);

export function isAllowedDesktopBackendUrl(value) {
  try {
    const url = new URL(value);
    return url.protocol === "http:" && loopbackHosts.has(url.hostname) && url.port.length > 0;
  } catch {
    return false;
  }
}

export function sanitizeSupportText(value) {
  return String(value)
    .replace(/Bearer\s+[A-Za-z0-9._~+/=-]+/gi, "Bearer [redacted]")
    .replace(/(JwtSecret|StartupToken|Password|Authorization|ConnectionStrings?__DefaultConnection)\s*[:=]\s*[^\s,;]+/gi, "$1=[redacted]")
    .replace(/[A-Za-z]:\\[^,\n\r\t ]+/g, "[path]")
    .replace(/\/(?:home|root|Users)\/[^,\n\r\t ]+/g, "[path]");
}

export function buildBackendEnvironment({ port, appDataDirectory, startupToken, jwtSecret }) {
  const root = appDataDirectory.replace(/\/$/, "");
  return {
    ASPNETCORE_ENVIRONMENT: "Desktop",
    ASPNETCORE_URLS: `http://127.0.0.1:${port}`,
    Database__Provider: "Sqlite",
    LocalStorage__DataDirectory: `${root}/Data`,
    LocalStorage__BackupDirectory: `${root}/Backups`,
    LocalStorage__PendingBackupDirectory: `${root}/PendingBackups`,
    LocalStorage__LogDirectory: `${root}/Logs`,
    LocalDatabase__AllowDevelopmentReset: "false",
    Desktop__StartupToken: startupToken,
    Authentication__JwtSecret: jwtSecret,
  };
}
