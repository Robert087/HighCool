import type { BackupIntegrityStatus, RestorePreflightResult } from "../../services/backupApi";
import { canRestoreBackup } from "../../services/backupPresentation";

export type BackupRestoreOperation =
  | "backup"
  | "verify"
  | "restore"
  | "retention"
  | "cloudSettings"
  | "cloudTest"
  | "cloudUpload"
  | "cloudDownload"
  | "cloudDelete"
  | null;

export function backupOperation(): BackupRestoreOperation {
  return "backup";
}

export function verifyOperation(): BackupRestoreOperation {
  return "verify";
}

export function restoreOperation(): BackupRestoreOperation {
  return "restore";
}

export function retentionOperation(): BackupRestoreOperation {
  return "retention";
}

export function cloudSettingsOperation(): BackupRestoreOperation {
  return "cloudSettings";
}

export function cloudTestOperation(): BackupRestoreOperation {
  return "cloudTest";
}

export function cloudUploadOperation(): BackupRestoreOperation {
  return "cloudUpload";
}

export function cloudDownloadOperation(): BackupRestoreOperation {
  return "cloudDownload";
}

export function cloudDeleteOperation(): BackupRestoreOperation {
  return "cloudDelete";
}

export function canSubmitRestore(
  integrityStatus: BackupIntegrityStatus,
  preflight: RestorePreflightResult | null | undefined) {
  return preflight?.status === "Valid" &&
    Boolean(preflight.operationId) &&
    canRestoreBackup(integrityStatus, preflight.status);
}

export function normalizeRetentionNumber(field: string, value: number) {
  if (!Number.isFinite(value)) {
    return field === "minimumAgeHoursBeforeDeletion" ? 0 : 1;
  }

  return field === "minimumAgeHoursBeforeDeletion"
    ? Math.max(0, Math.trunc(value))
    : Math.max(1, Math.trunc(value));
}
