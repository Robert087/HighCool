import type {
  BackupHealthStatus,
  BackupIntegrityStatus,
  BackupReason,
  BackupStatus,
  RestorePreflightStatus,
} from "./backupApi";
import { formatNumber } from "../i18n/format";

export type Tone = "neutral" | "primary" | "success" | "warning" | "danger";

export function backupHealthTone(status: BackupHealthStatus): Tone {
  if (status === "Healthy") return "success";
  if (status === "Warning") return "warning";
  if (status === "Error") return "danger";
  return "neutral";
}

export function backupStatusTone(status: BackupStatus): Tone {
  if (status === "Succeeded") return "success";
  if (status === "Canceled") return "warning";
  return "danger";
}

export function backupIntegrityTone(status: BackupIntegrityStatus): Tone {
  if (status === "Verified") return "success";
  if (status === "Failed") return "danger";
  return "neutral";
}

export function restorePreflightTone(status: RestorePreflightStatus | null | undefined): Tone {
  if (status === "Valid") return "success";
  if (!status) return "neutral";
  return "danger";
}

export function canRestoreBackup(integrityStatus: BackupIntegrityStatus, preflightStatus?: RestorePreflightStatus | null) {
  return integrityStatus !== "Failed" && (!preflightStatus || preflightStatus === "Valid");
}

export function backupReasonLabelKey(reason: BackupReason) {
  return `settings.backup.reason.${reason}`;
}

export function backupStatusLabelKey(status: BackupStatus) {
  return `settings.backup.status.${status}`;
}

export function backupIntegrityLabelKey(status: BackupIntegrityStatus) {
  return `settings.backup.integrity.${status}`;
}

export function restorePreflightLabelKey(status: RestorePreflightStatus | null | undefined) {
  return status ? `settings.backup.restorePreflight.${status}` : "settings.backup.notAvailable";
}

export function formatBytes(value: number | null | undefined) {
  if (value == null) {
    return "settings.backup.notAvailable";
  }

  if (value < 1024) {
    return `${formatNumber(value, { maximumFractionDigits: 0 })} B`;
  }

  const units = ["KB", "MB", "GB", "TB"];
  let size = value / 1024;
  let unitIndex = 0;
  while (size >= 1024 && unitIndex < units.length - 1) {
    size /= 1024;
    unitIndex += 1;
  }

  return `${formatNumber(size, {
    maximumFractionDigits: size >= 10 ? 1 : 2,
    minimumFractionDigits: size >= 10 ? 1 : 2,
  })} ${units[unitIndex]}`;
}
