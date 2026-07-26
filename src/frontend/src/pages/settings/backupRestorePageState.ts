import { useState, type Dispatch, type SetStateAction } from "react";
import type {
  BackupCenterSummary,
  BackupDetails,
  BackupIntegrityStatus,
  BackupListItem,
  BackupRetentionSettings,
  CloudBackupConfiguration,
  CloudBackupConnectionTestResult,
  CloudBackupListItem,
  CloudBackupStatusSummary,
  RestorePreflightResult,
  RestoreResult,
} from "../../services/backupApi";
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

export type BackupTab = "local" | "cloud" | "combined";

export interface RestoreWizardState {
  backup: BackupListItem;
  details: BackupDetails | null;
  preflight: RestorePreflightResult | null;
  result: RestoreResult | null;
  acceptedWarning: boolean;
  confirmationText: string;
  error: string;
  loading: boolean;
  stageKey: string;
}

export interface CloudDeleteState {
  backup: CloudBackupListItem;
  confirmationText: string;
}

export const EMPTY_RETENTION: BackupRetentionSettings = {
  enabled: true,
  manualCount: 10,
  scheduledCount: 24,
  beforeMigrationCount: 10,
  beforeRestoreCount: 10,
  beforeApplicationUpdateCount: 10,
  minimumAgeHoursBeforeDeletion: 24,
};

export const EMPTY_CLOUD_CONFIGURATION: CloudBackupConfiguration = {
  enabled: false,
  autoUploadAfterBackup: false,
  bucketName: "",
  endpoint: "",
  accessKey: "",
  hasAccessKey: false,
  hasSecretKey: false,
  prefix: "",
  retentionCount: 30,
  connectionTimeoutSeconds: 30,
  retryCount: 3,
};

export interface BackupRestorePageState {
  summary: BackupCenterSummary | null;
  setSummary: Dispatch<SetStateAction<BackupCenterSummary | null>>;
  backups: BackupListItem[];
  setBackups: Dispatch<SetStateAction<BackupListItem[]>>;
  cloudStatus: CloudBackupStatusSummary | null;
  setCloudStatus: Dispatch<SetStateAction<CloudBackupStatusSummary | null>>;
  cloudConfiguration: CloudBackupConfiguration;
  setCloudConfiguration: Dispatch<SetStateAction<CloudBackupConfiguration>>;
  cloudAccessKey: string;
  setCloudAccessKey: Dispatch<SetStateAction<string>>;
  cloudSecretKey: string;
  setCloudSecretKey: Dispatch<SetStateAction<string>>;
  replaceCloudCredentials: boolean;
  setReplaceCloudCredentials: Dispatch<SetStateAction<boolean>>;
  clearCloudCredentialsOpen: boolean;
  setClearCloudCredentialsOpen: Dispatch<SetStateAction<boolean>>;
  cloudBackups: CloudBackupListItem[];
  setCloudBackups: Dispatch<SetStateAction<CloudBackupListItem[]>>;
  combinedBackups: CloudBackupListItem[];
  setCombinedBackups: Dispatch<SetStateAction<CloudBackupListItem[]>>;
  activeTab: BackupTab;
  setActiveTab: Dispatch<SetStateAction<BackupTab>>;
  retention: BackupRetentionSettings;
  setRetention: Dispatch<SetStateAction<BackupRetentionSettings>>;
  details: BackupDetails | null;
  setDetails: Dispatch<SetStateAction<BackupDetails | null>>;
  restoreWizard: RestoreWizardState | null;
  setRestoreWizard: Dispatch<SetStateAction<RestoreWizardState | null>>;
  loading: boolean;
  setLoading: Dispatch<SetStateAction<boolean>>;
  error: string;
  setError: Dispatch<SetStateAction<string>>;
  operation: BackupRestoreOperation;
  setOperation: Dispatch<SetStateAction<BackupRestoreOperation>>;
  verifyingBackupId: string | null;
  setVerifyingBackupId: Dispatch<SetStateAction<string | null>>;
  cloudDelete: CloudDeleteState | null;
  setCloudDelete: Dispatch<SetStateAction<CloudDeleteState | null>>;
  cloudConnectionResult: CloudBackupConnectionTestResult | null;
  setCloudConnectionResult: Dispatch<SetStateAction<CloudBackupConnectionTestResult | null>>;
  cloudSettingsDirty: boolean;
  setCloudSettingsDirty: Dispatch<SetStateAction<boolean>>;
}

export function useBackupRestorePageState(): BackupRestorePageState {
  const [summary, setSummary] = useState<BackupCenterSummary | null>(null);
  const [backups, setBackups] = useState<BackupListItem[]>([]);
  const [cloudStatus, setCloudStatus] = useState<CloudBackupStatusSummary | null>(null);
  const [cloudConfiguration, setCloudConfiguration] = useState<CloudBackupConfiguration>(EMPTY_CLOUD_CONFIGURATION);
  const [cloudAccessKey, setCloudAccessKey] = useState("");
  const [cloudSecretKey, setCloudSecretKey] = useState("");
  const [replaceCloudCredentials, setReplaceCloudCredentials] = useState(false);
  const [clearCloudCredentialsOpen, setClearCloudCredentialsOpen] = useState(false);
  const [cloudBackups, setCloudBackups] = useState<CloudBackupListItem[]>([]);
  const [combinedBackups, setCombinedBackups] = useState<CloudBackupListItem[]>([]);
  const [activeTab, setActiveTab] = useState<BackupTab>("local");
  const [retention, setRetention] = useState<BackupRetentionSettings>(EMPTY_RETENTION);
  const [details, setDetails] = useState<BackupDetails | null>(null);
  const [restoreWizard, setRestoreWizard] = useState<RestoreWizardState | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [operation, setOperation] = useState<BackupRestoreOperation>(null);
  const [verifyingBackupId, setVerifyingBackupId] = useState<string | null>(null);
  const [cloudDelete, setCloudDelete] = useState<CloudDeleteState | null>(null);
  const [cloudConnectionResult, setCloudConnectionResult] = useState<CloudBackupConnectionTestResult | null>(null);
  const [cloudSettingsDirty, setCloudSettingsDirty] = useState(false);

  return {
    summary,
    setSummary,
    backups,
    setBackups,
    cloudStatus,
    setCloudStatus,
    cloudConfiguration,
    setCloudConfiguration,
    cloudAccessKey,
    setCloudAccessKey,
    cloudSecretKey,
    setCloudSecretKey,
    replaceCloudCredentials,
    setReplaceCloudCredentials,
    clearCloudCredentialsOpen,
    setClearCloudCredentialsOpen,
    cloudBackups,
    setCloudBackups,
    combinedBackups,
    setCombinedBackups,
    activeTab,
    setActiveTab,
    retention,
    setRetention,
    details,
    setDetails,
    restoreWizard,
    setRestoreWizard,
    loading,
    setLoading,
    error,
    setError,
    operation,
    setOperation,
    verifyingBackupId,
    setVerifyingBackupId,
    cloudDelete,
    setCloudDelete,
    cloudConnectionResult,
    setCloudConnectionResult,
    cloudSettingsDirty,
    setCloudSettingsDirty,
  };
}

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
