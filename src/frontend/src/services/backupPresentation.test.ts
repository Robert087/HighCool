import { describe, expect, it } from "vitest";
import {
  backupHealthTone,
  backupIntegrityTone,
  canRestoreBackup,
  formatBytes,
  restorePreflightTone,
} from "./backupPresentation";

describe("backup presentation helpers", () => {
  it("maps backup health and integrity to badge tones", () => {
    expect(backupHealthTone("Healthy")).toBe("success");
    expect(backupHealthTone("Warning")).toBe("warning");
    expect(backupHealthTone("Error")).toBe("danger");
    expect(backupHealthTone("Unknown")).toBe("neutral");

    expect(backupIntegrityTone("Verified")).toBe("success");
    expect(backupIntegrityTone("Failed")).toBe("danger");
    expect(backupIntegrityTone("Unknown")).toBe("neutral");
  });

  it("allows restore only when integrity has not failed and preflight is valid", () => {
    expect(canRestoreBackup("Verified", "Valid")).toBe(true);
    expect(canRestoreBackup("Unknown", "Valid")).toBe(true);
    expect(canRestoreBackup("Verified", "ChecksumMismatch")).toBe(false);
    expect(canRestoreBackup("Failed", "Valid")).toBe(false);
    expect(restorePreflightTone("WrongInstallation")).toBe("danger");
  });

  it("formats backup sizes without exposing paths or raw byte noise", () => {
    expect(formatBytes(null)).toBe("settings.backup.notAvailable");
    expect(formatBytes(512)).toBe("512 B");
    expect(formatBytes(1024)).toBe("1.00 KB");
    expect(formatBytes(1024 * 1024 * 12)).toBe("12.0 MB");
  });
});
