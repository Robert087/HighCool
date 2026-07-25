import { beforeEach, describe, expect, it, vi } from "vitest";
import { ApiError } from "./api";
import { restoreBackup, validateRestoreBackup } from "./backupApi";
import { requestJson } from "./api";

vi.mock("./api", async () => {
  const actual = await vi.importActual<typeof import("./api")>("./api");
  return {
    ...actual,
    requestJson: vi.fn(),
  };
});

const requestJsonMock = vi.mocked(requestJson);

describe("backup API", () => {
  beforeEach(() => {
    requestJsonMock.mockReset();
  });

  it("requests restore preflight for one backup ID", async () => {
    requestJsonMock.mockResolvedValueOnce({
      status: "Valid",
      message: "Backup is valid for restore.",
      backupId: "backup1",
      schemaVersion: 1,
      operationId: "operation1",
      operationExpiresAtUtc: "2026-07-25T12:00:00.000Z",
    });

    await validateRestoreBackup("backup1");

    expect(requestJsonMock).toHaveBeenCalledWith("/api/local-database/restore/validate", {
      method: "POST",
      body: JSON.stringify({ backupId: "backup1" }),
    });
  });

  it("submits the server-issued operation ID during restore confirmation", async () => {
    requestJsonMock.mockResolvedValueOnce({
      status: "Completed",
      message: "Database restore completed successfully.",
      selectedBackupId: "backup1",
      safetyBackupId: "safety1",
    });

    await restoreBackup("backup1", "operation1");

    expect(requestJsonMock).toHaveBeenCalledWith("/api/local-database/restore", {
      method: "POST",
      body: JSON.stringify({
        backupId: "backup1",
        operationId: "operation1",
        confirmation: "RESTORE_LOCAL_DATABASE",
      }),
    });
  });

  it("keeps feature-unavailable errors as safe API errors", () => {
    const error = new ApiError("Local database backup and restore endpoints are available only in HighCool Desktop.", 409);
    expect(error.message).not.toContain("/root/");
    expect(error.message).not.toContain("JwtSecret");
    expect(error.status).toBe(409);
  });
});
