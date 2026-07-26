import { requestJson } from "./api";

export type BackupReason = "Manual" | "Scheduled" | "BeforeMigration" | "BeforeRestore" | "BeforeApplicationUpdate";
export type BackupStatus = "Succeeded" | "Failed" | "Canceled";
export type BackupHealthStatus = "Healthy" | "Warning" | "Error" | "Unknown";
export type BackupIntegrityStatus = "Unknown" | "Verified" | "Failed";
export type CloudBackupStatus = "Disabled" | "NotConfigured" | "Ready" | "Offline" | "Error";
export type CloudBackupUploadStatus = "NotQueued" | "Queued" | "Uploading" | "Uploaded" | "Failed" | "Canceled";
export type CloudBackupObjectStatus = "Missing" | "Present";
export type CloudBackupSyncStatus =
  | "LocalOnly"
  | "CloudOnly"
  | "InSync"
  | "OutOfSync"
  | "Queued"
  | "Uploading"
  | "Failed"
  | "Downloading"
  | "LegacyUntrusted"
  | "Corrupted"
  | "MissingRemotePayload"
  | "MissingRemoteManifest";
export type CloudBackupFailureCategory =
  | "None"
  | "TransientNetworkFailure"
  | "Timeout"
  | "Throttling"
  | "ServiceUnavailable"
  | "DnsFailure"
  | "AuthenticationFailure"
  | "AuthorizationFailure"
  | "InvalidBucket"
  | "InvalidEndpoint"
  | "MissingLocalFile"
  | "ChecksumMismatch"
  | "InvalidManifest"
  | "Cancellation"
  | "QueueCorruption";
export type CloudBackupConnectionFailureCategory =
  | "None"
  | "InvalidConfiguration"
  | "CredentialsMissing"
  | "CredentialsUnreadable"
  | "InvalidCredentials"
  | "AccessDenied"
  | "BucketNotFound"
  | "EndpointRejected"
  | "DnsFailure"
  | "TlsFailure"
  | "Timeout"
  | "NetworkUnavailable"
  | "WriteDenied"
  | "ReadDenied"
  | "DeleteDenied"
  | "ContentVerificationFailed"
  | "CleanupFailed"
  | "UnknownProviderFailure";
export type CloudBackupConnectionTestStage =
  | "Validation"
  | "Credentials"
  | "ClientCreation"
  | "List"
  | "Write"
  | "Read"
  | "ChecksumVerification"
  | "DeleteCleanup"
  | "Completed";
export type CloudBackupCredentialUpdateMode = "Preserve" | "Replace" | "Clear";
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

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  appliedFilters: unknown;
  sort: {
    sortBy: string;
    direction: "Asc" | "Desc";
  };
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

export interface CloudBackupConfiguration {
  enabled: boolean;
  autoUploadAfterBackup: boolean;
  bucketName: string;
  endpoint: string;
  accessKey: string;
  hasAccessKey: boolean;
  hasSecretKey: boolean;
  prefix: string;
  retentionCount: number;
  connectionTimeoutSeconds: number;
  retryCount: number;
}

export interface CloudBackupConfigurationRequest {
  enabled: boolean;
  autoUploadAfterBackup: boolean;
  bucketName: string;
  endpoint: string;
  accessKey?: string | null;
  secretKey?: string | null;
  prefix?: string | null;
  retentionCount: number;
  connectionTimeoutSeconds: number;
  retryCount: number;
  credentialUpdateMode?: CloudBackupCredentialUpdateMode;
}

export interface CloudBackupStatusSummary {
  status: CloudBackupStatus;
  message: string;
  enabled: boolean;
  configured: boolean;
  queuedCount: number;
  uploadingCount: number;
  failedCount: number;
  lastSuccessfulUploadAtUtc: string | null;
}

export interface CloudBackupConnectionTestResult {
  success: boolean;
  succeeded: boolean;
  status: string;
  category: CloudBackupConnectionFailureCategory;
  message: string;
  stage: CloudBackupConnectionTestStage;
  statusCode: number | null;
  providerErrorCode: string | null;
  cleanupSucceeded: boolean;
  testedAtUtc: string;
}

export interface CloudBackupQueueItem {
  queueId: string;
  backupId: string;
  status: CloudBackupUploadStatus;
  attempts: number;
  maxAttempts: number;
  queuedAtUtc: string;
  startedAtUtc: string | null;
  completedAtUtc: string | null;
  nextAttemptAtUtc: string | null;
  lastError: string | null;
  failureCategory: CloudBackupFailureCategory;
}

export interface CloudBackupListItem {
  backupId: string;
  createdAtUtc: string;
  sizeBytes: number;
  checksumSha256: string | null;
  localStatus: BackupStatus;
  cloudStatus: CloudBackupObjectStatus;
  uploadStatus: CloudBackupUploadStatus;
  verificationStatus: BackupIntegrityStatus;
  syncStatus: CloudBackupSyncStatus;
  lastUploadedAtUtc: string | null;
  cloudObjectKey: string | null;
}

export interface CloudBackupDownloadResult {
  backupId: string;
  downloaded: boolean;
  message: string;
  integrityStatus: BackupIntegrityStatus;
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

export function getCloudBackupStatus() {
  return requestJson<CloudBackupStatusSummary>("/api/local-database/cloud/status");
}

export function getCloudBackupConfiguration() {
  return requestJson<CloudBackupConfiguration>("/api/local-database/cloud/configuration");
}

export function saveCloudBackupConfiguration(settings: CloudBackupConfigurationRequest) {
  return requestJson<CloudBackupConfiguration>("/api/local-database/cloud/configuration", {
    method: "PUT",
    body: JSON.stringify(settings),
  });
}

export function testCloudBackupConnection() {
  return requestJson<CloudBackupConnectionTestResult>("/api/local-database/cloud/test-connection", {
    method: "POST",
  });
}

export function listCloudBackups(page = 1, pageSize = 20) {
  return requestJson<PagedResult<CloudBackupListItem>>(`/api/local-database/cloud/backups?page=${page}&pageSize=${pageSize}`);
}

export function listCombinedCloudBackups(page = 1, pageSize = 20) {
  return requestJson<PagedResult<CloudBackupListItem>>(`/api/local-database/cloud/sync?page=${page}&pageSize=${pageSize}`);
}

export function uploadCloudBackup(backupId: string, force = false) {
  return requestJson<CloudBackupQueueItem>(`/api/local-database/cloud/backups/${encodeURIComponent(backupId)}/upload`, {
    method: "POST",
    body: JSON.stringify({ force }),
  });
}

export function retryCloudUpload(queueId: string) {
  return requestJson<CloudBackupQueueItem>(`/api/local-database/cloud/uploads/${encodeURIComponent(queueId)}/retry`, {
    method: "POST",
  });
}

export function cancelCloudUpload(queueId: string) {
  return requestJson<CloudBackupQueueItem>(`/api/local-database/cloud/uploads/${encodeURIComponent(queueId)}/cancel`, {
    method: "POST",
  });
}

export function downloadCloudBackup(backupId: string) {
  return requestJson<CloudBackupDownloadResult>(`/api/local-database/cloud/backups/${encodeURIComponent(backupId)}/download`, {
    method: "POST",
  });
}

export function deleteCloudBackup(backupId: string) {
  return requestJson<{ message: string }>(`/api/local-database/cloud/backups/${encodeURIComponent(backupId)}`, {
    method: "DELETE",
  });
}
