import { describe, expect, it } from "vitest";
import type { RestorePreflightResult } from "../../services/backupApi";
import {
  backupOperation,
  canSubmitRestore,
  normalizeRetentionNumber,
  retentionOperation,
  restoreOperation,
  verifyOperation,
} from "./backupRestorePageState";

function preflight(overrides: Partial<RestorePreflightResult> = {}): RestorePreflightResult {
  return {
    status: "Valid",
    message: "Backup is valid for restore.",
    backupId: "backup1",
    schemaVersion: 1,
    operationId: "operation1",
    operationExpiresAtUtc: "2026-07-25T12:00:00.000Z",
    ...overrides,
  };
}

describe("backup restore page state", () => {
  it("uses the correct operation state for each action", () => {
    expect(backupOperation()).toBe("backup");
    expect(verifyOperation()).toBe("verify");
    expect(restoreOperation()).toBe("restore");
    expect(retentionOperation()).toBe("retention");
  });

  it("requires a valid server-issued operation before restore can submit", () => {
    expect(canSubmitRestore("Verified", preflight())).toBe(true);
    expect(canSubmitRestore("Unknown", preflight())).toBe(true);
    expect(canSubmitRestore("Verified", preflight({ operationId: null }))).toBe(false);
    expect(canSubmitRestore("Verified", preflight({ status: "ChecksumMismatch" }))).toBe(false);
    expect(canSubmitRestore("Failed", preflight())).toBe(false);
    expect(canSubmitRestore("Verified", null)).toBe(false);
  });

  it("normalizes retention number inputs before save", () => {
    expect(normalizeRetentionNumber("manualCount", 0)).toBe(1);
    expect(normalizeRetentionNumber("manualCount", 2.9)).toBe(2);
    expect(normalizeRetentionNumber("minimumAgeHoursBeforeDeletion", -1)).toBe(0);
    expect(normalizeRetentionNumber("minimumAgeHoursBeforeDeletion", Number.NaN)).toBe(0);
  });
});
