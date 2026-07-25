import { requestJson } from "./api";

export type BackupReason = "Manual" | "Scheduled" | "BeforeMigration" | "BeforeRestore" | "BeforeApplicationUpdate";
export type BackupStatus = "Succeeded" | "Failed" | "Canceled";
export type BackupHealthStatus = "Healthy" | "Warning" | "Error" | "Unknown";
export type BackupIntegrityStatus = "Unknown" | "Verified" | "Failed";
export type RestorePreflightStatus =
  | "Valid"
  | "BackupNotFound"
  | "ManifestInvalid"
  | "ChecksumMismatch"
  | "DecryptionFailed"
  | "CorruptDatabase"
  | "UnsupportedSchema"
  | "NewerSchema"
  | "WrongInstallation"
  | "InsufficientDiskSpace"
  | "DestinationUnavailable";
export type RestoreStatus = "Completed" | "Failed" | "Rejected";

export interface BackupHealthReason {
  code: string;
  message: string;
}

export interface BackupRetentionSettings {
  enabled: boolean;
  manualCount: number;
  scheduledCount: number;
  beforeMigrationCount: number;
  beforeRestoreCount: number;
  beforeApplicationUpdateCount: number;
  minimumAgeHoursBeforeDeletion: number;
}

export interface BackupCenterSummary {
  health: BackupHealthStatus;
  healthReasons: BackupHealthReason[];
  lastSuccessfulBackupAtUtc: string | null;
  lastIntegrityVerificationAtUtc: string | null;
  databaseFileName: string | null;
  databaseSizeBytes: number | null;
  availableBackupCount: number;
  backupStorageUsedBytes: number;
  encryptionStatus: string;
  retentionEnabled: boolean;
  retentionStatus: string;
  applicationVersion: string | null;
  databaseSchemaVersion: number | null;
  retentionSettings: BackupRetentionSettings;
}

export interface BackupListItem {
  backupId: string;
  createdAtUtc: string;
  reason: BackupReason;
  status: BackupStatus;
  sizeBytes: number;
  applicationVersion: string | null;
  databaseSchemaVersion: number | null;
  integrityStatus: BackupIntegrityStatus;
  lastVerifiedAtUtc: string | null;
}

export interface BackupDetails {
  backupId: string;
  createdAtUtc: string;
  reason: BackupReason;
  status: BackupStatus;
  applicationVersion: string;
  databaseSchemaVersion: number;
  manifestVersion: number;
  encryptionAlgorithm: string;
  backupSizeBytes: number;
  originalDatabaseSizeBytes: number;
  compressionStatus: string;
  encryptedSha256: string;
  plainSha256: string;
  integrityStatus: BackupIntegrityStatus;
  lastVerifiedAtUtc: string | null;
  restoreCompatibilityStatus: RestorePreflightStatus | null;
  restoreCompatibilityMessage: string | null;
  databaseFileName: string;
}

export interface BackupResult {
  status: BackupStatus;
  backupId: string;
  timestampUtc: string;
  sizeBytes: number;
  checksumSha256: string | null;
  reason: BackupReason;
  message: string;
  backupFileName: string | null;
  manifestFileName: string | null;
}

export interface BackupIntegrityVerificationResult {
  backupId: string;
  status: BackupIntegrityStatus;
  verifiedAtUtc: string;
  message: string;
}

export interface RestorePreflightResult {
  status: RestorePreflightStatus;
  message: string;
  backupId: string | null;
  schemaVersion: number | null;
  operationId: string | null;
  operationExpiresAtUtc: string | null;
}

export interface RestoreResult {
  status: RestoreStatus;
  message: string;
  selectedBackupId: string | null;
  safetyBackupId: string | null;
}

export function getBackupSummary() {
  return requestJson<BackupCenterSummary>("/api/local-database/backups/summary");
}

export function listBackups() {
  return requestJson<BackupListItem[]>("/api/local-database/backups");
}

export function getBackupDetails(backupId: string) {
  return requestJson<BackupDetails>(`/api/local-database/backups/${encodeURIComponent(backupId)}`);
}

export function createManualBackup() {
  return requestJson<BackupResult>("/api/local-database/backups", {
    method: "POST",
  });
}

export function verifyBackup(backupId: string) {
  return requestJson<BackupIntegrityVerificationResult>(`/api/local-database/backups/${encodeURIComponent(backupId)}/verify`, {
    method: "POST",
  });
}

export function validateRestoreBackup(backupId: string) {
  return requestJson<RestorePreflightResult>("/api/local-database/restore/validate", {
    method: "POST",
    body: JSON.stringify({ backupId }),
  });
}

export function restoreBackup(backupId: string, operationId: string) {
  return requestJson<RestoreResult>("/api/local-database/restore", {
    method: "POST",
    body: JSON.stringify({ backupId, operationId, confirmation: "RESTORE_LOCAL_DATABASE" }),
  });
}

export function getBackupRetentionSettings() {
  return requestJson<BackupRetentionSettings>("/api/local-database/backup-retention");
}

export function saveBackupRetentionSettings(settings: BackupRetentionSettings) {
  return requestJson<BackupRetentionSettings>("/api/local-database/backup-retention", {
    method: "PUT",
    body: JSON.stringify(settings),
  });
}
