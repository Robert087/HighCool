import { useEffect, useMemo, useState } from "react";
import {
  Badge,
  Button,
  Card,
  Checkbox,
  DataTable,
  EmptyState,
  Field,
  Input,
  SkeletonLoader,
  useToast,
} from "../../components/ui";
import { useAuth } from "../../features/auth/AuthProvider";
import { useI18n } from "../../i18n";
import { Permissions } from "../../services/permissions";
import { ApiError } from "../../services/api";
import {
  createManualBackup,
  getBackupDetails,
  getBackupSummary,
  listBackups,
  restoreBackup,
  saveBackupRetentionSettings,
  validateRestoreBackup,
  verifyBackup,
  type BackupCenterSummary,
  type CloudBackupConfiguration,
  type CloudBackupConnectionTestResult,
  type CloudBackupListItem,
  type CloudBackupStatusSummary,
  type BackupDetails,
  type BackupListItem,
  type BackupRetentionSettings,
  type RestorePreflightResult,
  type RestoreResult,
  deleteCloudBackup,
  downloadCloudBackup,
  getCloudBackupConfiguration,
  getCloudBackupStatus,
  listCloudBackups,
  listCombinedCloudBackups,
  saveCloudBackupConfiguration,
  testCloudBackupConnection,
  uploadCloudBackup,
} from "../../services/backupApi";
import {
  backupHealthTone,
  backupIntegrityLabelKey,
  backupIntegrityTone,
  backupReasonLabelKey,
  backupStatusLabelKey,
  backupStatusTone,
  cloudObjectLabelKey,
  cloudObjectTone,
  cloudConnectionCategoryDescriptionKey,
  cloudConnectionCategoryTitleKey,
  cloudConnectionStageLabelKey,
  cloudSyncLabelKey,
  cloudSyncTone,
  cloudUploadLabelKey,
  cloudUploadTone,
  formatBytes,
  restorePreflightLabelKey,
  restorePreflightTone,
} from "../../services/backupPresentation";
import { SettingsScaffold } from "./SettingsScaffold";
import {
  backupOperation,
  canSubmitRestore,
  cloudDeleteOperation,
  cloudDownloadOperation,
  cloudSettingsOperation,
  cloudTestOperation,
  cloudUploadOperation,
  normalizeRetentionNumber,
  retentionOperation,
  restoreOperation,
  verifyOperation,
  type BackupRestoreOperation,
} from "./backupRestorePageState";

type Operation = BackupRestoreOperation;
type BackupTab = "local" | "cloud" | "combined";

interface RestoreWizardState {
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

interface CloudDeleteState {
  backup: CloudBackupListItem;
  confirmationText: string;
}

const EMPTY_RETENTION: BackupRetentionSettings = {
  enabled: true,
  manualCount: 10,
  scheduledCount: 24,
  beforeMigrationCount: 10,
  beforeRestoreCount: 10,
  beforeApplicationUpdateCount: 10,
  minimumAgeHoursBeforeDeletion: 24,
};

const EMPTY_CLOUD_CONFIGURATION: CloudBackupConfiguration = {
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

function valueOrUnavailable(value: string | number | null | undefined) {
  return value == null || value === "" ? "settings.backup.notAvailable" : String(value);
}

function DetailRow({ label, value }: { label: string; value: string }) {
  const { t } = useI18n();
  return (
    <div className="hc-backup-detail-row">
      <span>{t(label)}</span>
      <strong>{value.startsWith("settings.") ? t(value) : value}</strong>
    </div>
  );
}

function DetailsDialog({
  details,
  onClose,
}: {
  details: BackupDetails | null;
  onClose: () => void;
}) {
  const { formatDate, t } = useI18n();

  if (!details) {
    return null;
  }

  return (
    <div className="hc-access-dialog" role="dialog" aria-modal="true" aria-labelledby="hc-backup-details-title">
      <button className="hc-access-dialog__backdrop" type="button" aria-label={t("app.close")} onClick={onClose} />
      <div className="hc-access-dialog__panel hc-backup-dialog">
        <div className="hc-access-dialog__header">
          <div>
            <p className="hc-access-dialog__eyebrow">{t("settings.backup.detailsEyebrow")}</p>
            <h2 className="hc-access-dialog__title" id="hc-backup-details-title">{t("settings.backup.detailsTitle")}</h2>
          </div>
          <Button size="sm" variant="ghost" onClick={onClose}>app.close</Button>
        </div>
        <div className="hc-backup-detail-grid">
          <DetailRow label="settings.backup.fields.backupId" value={details.backupId} />
          <DetailRow label="settings.backup.fields.createdAt" value={formatDate(details.createdAtUtc, { dateStyle: "medium", timeStyle: "short" })} />
          <DetailRow label="settings.backup.fields.type" value={t(backupReasonLabelKey(details.reason))} />
          <DetailRow label="settings.backup.fields.applicationVersion" value={details.applicationVersion} />
          <DetailRow label="settings.backup.fields.schemaVersion" value={String(details.databaseSchemaVersion)} />
          <DetailRow label="settings.backup.fields.manifestVersion" value={String(details.manifestVersion)} />
          <DetailRow label="settings.backup.fields.encryption" value={details.encryptionAlgorithm} />
          <DetailRow label="settings.backup.fields.backupSize" value={formatBytes(details.backupSizeBytes)} />
          <DetailRow label="settings.backup.fields.originalDatabaseSize" value={formatBytes(details.originalDatabaseSizeBytes)} />
          <DetailRow label="settings.backup.fields.compression" value={details.compressionStatus === "None" ? t("settings.backup.compression.none") : details.compressionStatus} />
          <DetailRow label="settings.backup.fields.integrity" value={t(backupIntegrityLabelKey(details.integrityStatus))} />
          <DetailRow label="settings.backup.fields.lastVerified" value={details.lastVerifiedAtUtc ? formatDate(details.lastVerifiedAtUtc, { dateStyle: "medium", timeStyle: "short" }) : t("settings.backup.notAvailable")} />
          <DetailRow label="settings.backup.fields.restoreCompatibility" value={t(restorePreflightLabelKey(details.restoreCompatibilityStatus))} />
          <DetailRow label="settings.backup.fields.databaseFileName" value={details.databaseFileName} />
        </div>
        <div className="hc-backup-hash-list">
          <DetailRow label="settings.backup.fields.encryptedSha256" value={details.encryptedSha256} />
          <DetailRow label="settings.backup.fields.plainSha256" value={details.plainSha256} />
        </div>
      </div>
    </div>
  );
}

function RestoreWizard({
  state,
  onChange,
  onClose,
  onRunPreflight,
  onRestore,
}: {
  state: RestoreWizardState | null;
  onChange: (next: Partial<RestoreWizardState>) => void;
  onClose: () => void;
  onRunPreflight: () => void;
  onRestore: () => void;
}) {
  const { formatDate, t } = useI18n();

  if (!state) {
    return null;
  }

  const restoreAllowed = canSubmitRestore(
    state.details?.integrityStatus ?? state.backup.integrityStatus,
    state.preflight);
  const confirmationMatches = state.confirmationText.trim().toUpperCase() === "RESTORE";
  const completed = state.result?.status === "Completed";

  return (
    <div className="hc-access-dialog" role="dialog" aria-modal="true" aria-labelledby="hc-restore-wizard-title">
      <button className="hc-access-dialog__backdrop" type="button" aria-label={t("app.close")} onClick={completed || !state.loading ? onClose : undefined} />
      <div className="hc-access-dialog__panel hc-backup-dialog hc-backup-dialog--wide">
        <div className="hc-access-dialog__header">
          <div>
            <p className="hc-access-dialog__eyebrow">{t("settings.backup.restoreEyebrow")}</p>
            <h2 className="hc-access-dialog__title" id="hc-restore-wizard-title">{t("settings.backup.restoreTitle")}</h2>
          </div>
          <Button size="sm" variant="ghost" disabled={state.loading} onClick={onClose}>app.close</Button>
        </div>

        <div className="hc-restore-steps" aria-label={t("settings.backup.restoreStepsLabel")}>
          {[
            "settings.backup.restoreStep.select",
            "settings.backup.restoreStep.review",
            "settings.backup.restoreStep.preflight",
            "settings.backup.restoreStep.confirm",
            "settings.backup.restoreStep.execute",
            "settings.backup.restoreStep.restart",
          ].map((stepKey) => (
            <span key={stepKey}>{t(stepKey)}</span>
          ))}
        </div>

        <div className="hc-backup-restore-body">
          <Card muted padding="md">
            <div className="hc-backup-selected">
              <div>
                <span>{t("settings.backup.fields.selectedBackup")}</span>
                <strong>{formatDate(state.backup.createdAtUtc, { dateStyle: "medium", timeStyle: "short" })}</strong>
              </div>
              <Badge tone={backupIntegrityTone(state.details?.integrityStatus ?? state.backup.integrityStatus)}>
                {t(backupIntegrityLabelKey(state.details?.integrityStatus ?? state.backup.integrityStatus))}
              </Badge>
            </div>
          </Card>

          {state.preflight ? (
            <Card padding="md">
              <div className="hc-backup-selected">
                <div>
                  <span>{t("settings.backup.fields.preflight")}</span>
                  <strong>{state.preflight.message}</strong>
                </div>
                <Badge tone={restorePreflightTone(state.preflight.status)}>{t(restorePreflightLabelKey(state.preflight.status))}</Badge>
              </div>
            </Card>
          ) : (
            <Button isLoading={state.loading && state.stageKey === "settings.backup.stage.preflight"} onClick={onRunPreflight}>
              settings.backup.actions.runPreflight
            </Button>
          )}

          <Card padding="md" className="hc-backup-warning-card">
            <h3>{t("settings.backup.restoreWarningTitle")}</h3>
            <p>{t("settings.backup.restoreWarningDescription")}</p>
            <p>{t("settings.backup.restoreSafetyBackupNote")}</p>
          </Card>

          <Checkbox
            checked={state.acceptedWarning}
            disabled={!restoreAllowed || state.loading || completed}
            label="settings.backup.restoreConfirmCheckbox"
            onChange={(event) => onChange({ acceptedWarning: event.target.checked })}
          />
          <Field label="settings.backup.restoreConfirmText" hint="settings.backup.restoreConfirmHint">
            <Input
              aria-label="settings.backup.restoreConfirmText"
              disabled={!restoreAllowed || state.loading || completed}
              value={state.confirmationText}
              onChange={(event) => onChange({ confirmationText: event.target.value })}
            />
          </Field>

          {state.loading ? <p className="hc-backup-operation-stage">{t(state.stageKey)}</p> : null}
          {state.error ? <p className="hc-field-error">{state.error}</p> : null}
          {state.result ? (
            <Card padding="md" muted>
              <DetailRow label="settings.backup.restoreResult" value={state.result.message} />
              <DetailRow label="settings.backup.fields.safetyBackupId" value={valueOrUnavailable(state.result.safetyBackupId)} />
              <p className="hc-backup-restart-note">{t("settings.backup.restoreRestartGuidance")}</p>
            </Card>
          ) : null}
        </div>

        <div className="hc-access-dialog__actions">
          <Button variant="ghost" disabled={state.loading} onClick={onClose}>app.close</Button>
          <Button
            variant="danger"
            disabled={!restoreAllowed || !state.acceptedWarning || !confirmationMatches || state.loading || completed}
            isLoading={state.loading && state.stageKey === "settings.backup.stage.restore"}
            onClick={onRestore}
          >
            settings.backup.actions.restoreNow
          </Button>
        </div>
      </div>
    </div>
  );
}

function CloudDeleteDialog({
  state,
  loading,
  onChange,
  onClose,
  onConfirm,
}: {
  state: CloudDeleteState | null;
  loading: boolean;
  onChange: (confirmationText: string) => void;
  onClose: () => void;
  onConfirm: () => void;
}) {
  const { formatDate, t } = useI18n();

  if (!state) {
    return null;
  }

  const confirmationMatches = state.confirmationText.trim() === state.backup.backupId;

  return (
    <div className="hc-access-dialog" role="dialog" aria-modal="true" aria-labelledby="hc-cloud-delete-title">
      <button className="hc-access-dialog__backdrop" type="button" aria-label={t("app.close")} onClick={loading ? undefined : onClose} />
      <div className="hc-access-dialog__panel hc-backup-dialog">
        <div className="hc-access-dialog__header">
          <div>
            <p className="hc-access-dialog__eyebrow">{t("settings.backup.cloud.deleteConfirmEyebrow")}</p>
            <h2 className="hc-access-dialog__title" id="hc-cloud-delete-title">{t("settings.backup.cloud.deleteConfirmTitle")}</h2>
          </div>
          <Button size="sm" variant="ghost" disabled={loading} onClick={onClose}>app.close</Button>
        </div>
        <div className="hc-backup-restore-body">
          <Card muted padding="md">
            <DetailRow label="settings.backup.fields.backupId" value={state.backup.backupId} />
            <DetailRow label="settings.backup.fields.createdAt" value={formatDate(state.backup.createdAtUtc, { dateStyle: "medium", timeStyle: "short" })} />
            <DetailRow label="settings.backup.columns.size" value={formatBytes(state.backup.sizeBytes)} />
          </Card>
          <p className="hc-form-help">{t("settings.backup.cloud.deleteConfirmDescription")}</p>
          <Field label="settings.backup.cloud.deleteConfirmField" hint="settings.backup.cloud.deleteConfirmHint">
            <Input value={state.confirmationText} disabled={loading} onChange={(event) => onChange(event.target.value)} />
          </Field>
          <div className="hc-access-dialog__actions">
            <Button variant="secondary" disabled={loading} onClick={onClose}>common.cancel</Button>
            <Button variant="danger" disabled={!confirmationMatches || loading} isLoading={loading} onClick={onConfirm}>
              settings.backup.cloud.actions.deleteCloud
            </Button>
          </div>
        </div>
      </div>
    </div>
  );
}

function CloudCredentialsClearDialog({
  open,
  loading,
  onClose,
  onConfirm,
}: {
  open: boolean;
  loading: boolean;
  onClose: () => void;
  onConfirm: () => void;
}) {
  const { t } = useI18n();

  if (!open) {
    return null;
  }

  return (
    <div className="hc-access-dialog" role="dialog" aria-modal="true" aria-labelledby="hc-cloud-clear-credentials-title">
      <button className="hc-access-dialog__backdrop" type="button" aria-label={t("app.close")} onClick={loading ? undefined : onClose} />
      <div className="hc-access-dialog__panel hc-backup-dialog">
        <div className="hc-access-dialog__header">
          <div>
            <p className="hc-access-dialog__eyebrow">{t("settings.backup.cloud.clearCredentialsEyebrow")}</p>
            <h2 className="hc-access-dialog__title" id="hc-cloud-clear-credentials-title">{t("settings.backup.cloud.clearCredentialsTitle")}</h2>
          </div>
          <Button size="sm" variant="ghost" disabled={loading} onClick={onClose}>app.close</Button>
        </div>
        <p className="hc-form-help">{t("settings.backup.cloud.clearCredentialsDescription")}</p>
        <div className="hc-access-dialog__actions">
          <Button variant="secondary" disabled={loading} onClick={onClose}>common.cancel</Button>
          <Button variant="danger" disabled={loading} isLoading={loading} onClick={onConfirm}>
            settings.backup.cloud.actions.clearCredentials
          </Button>
        </div>
      </div>
    </div>
  );
}

export function SettingsBackupRestorePage() {
  const { hasPermission } = useAuth();
  const { formatDate, t } = useI18n();
  const { showToast } = useToast();
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
  const [operation, setOperation] = useState<Operation>(null);
  const [verifyingBackupId, setVerifyingBackupId] = useState<string | null>(null);
  const [cloudDelete, setCloudDelete] = useState<CloudDeleteState | null>(null);
  const [cloudConnectionResult, setCloudConnectionResult] = useState<CloudBackupConnectionTestResult | null>(null);
  const [cloudSettingsDirty, setCloudSettingsDirty] = useState(false);

  const canCreateBackup = hasPermission(Permissions.SettingsDatabaseBackupCreate);
  const canValidateRestore = hasPermission(Permissions.SettingsDatabaseRestoreValidate);
  const canExecuteRestore = hasPermission(Permissions.SettingsDatabaseRestoreExecute);
  const operationActive = operation !== null;

  async function load() {
    try {
      setLoading(true);
      const [
        summaryResponse,
        backupsResponse,
        cloudStatusResponse,
        cloudConfigurationResponse,
        cloudBackupsResponse,
        combinedBackupsResponse,
      ] = await Promise.all([
        getBackupSummary(),
        listBackups(),
        getCloudBackupStatus(),
        getCloudBackupConfiguration(),
        listCloudBackups(),
        listCombinedCloudBackups(),
      ]);
      setSummary(summaryResponse);
      setBackups(backupsResponse);
      setCloudStatus(cloudStatusResponse);
      setCloudConfiguration(cloudConfigurationResponse);
      setCloudAccessKey("");
      setCloudSecretKey("");
      setReplaceCloudCredentials(false);
      setCloudBackups(cloudBackupsResponse.items);
      setCombinedBackups(combinedBackupsResponse.items);
      setCloudConnectionResult(null);
      setCloudSettingsDirty(false);
      setRetention(summaryResponse.retentionSettings);
      setError("");
    } catch (loadError) {
      setError(loadError instanceof ApiError ? loadError.message : t("settings.backup.loadError"));
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    void load();
  }, []);

  const summaryCards = useMemo(() => {
    if (!summary) {
      return [];
    }

    return [
      { label: "settings.backup.summary.health", value: t(`settings.backup.health.${summary.health}`), tone: backupHealthTone(summary.health) },
      { label: "settings.backup.summary.lastBackup", value: summary.lastSuccessfulBackupAtUtc ? formatDate(summary.lastSuccessfulBackupAtUtc, { dateStyle: "medium", timeStyle: "short" }) : t("settings.backup.notAvailable") },
      { label: "settings.backup.summary.lastVerification", value: summary.lastIntegrityVerificationAtUtc ? formatDate(summary.lastIntegrityVerificationAtUtc, { dateStyle: "medium", timeStyle: "short" }) : t("settings.backup.notAvailable") },
      { label: "settings.backup.summary.database", value: valueOrUnavailable(summary.databaseFileName) },
      { label: "settings.backup.summary.databaseSize", value: formatBytes(summary.databaseSizeBytes) },
      { label: "settings.backup.summary.availableBackups", value: String(summary.availableBackupCount) },
      { label: "settings.backup.summary.storageUsed", value: formatBytes(summary.backupStorageUsedBytes) },
      { label: "settings.backup.summary.encryption", value: summary.encryptionStatus },
      { label: "settings.backup.summary.retention", value: summary.retentionStatus },
      { label: "settings.backup.summary.version", value: summary.applicationVersion ?? t("settings.backup.notAvailable") },
    ];
  }, [formatDate, summary, t]);

  async function runManualBackup() {
    try {
      setOperation(backupOperation());
      const result = await createManualBackup();
      showToast({
        tone: result.status === "Succeeded" ? "success" : "danger",
        title: result.status === "Succeeded" ? t("settings.backup.backupCreatedTitle") : t("settings.backup.backupFailedTitle"),
        description: result.message,
      });
    } catch (requestError) {
      showToast({
        tone: "danger",
        title: t("settings.backup.backupFailedTitle"),
        description: requestError instanceof ApiError ? requestError.message : t("settings.backup.safeFailure"),
      });
    } finally {
      setOperation(null);
    }
  }

  async function openDetails(backupId: string) {
    try {
      const response = await getBackupDetails(backupId);
      setDetails(response);
    } catch (requestError) {
      showToast({
        tone: "danger",
        title: t("settings.backup.detailsFailedTitle"),
        description: requestError instanceof ApiError ? requestError.message : t("settings.backup.safeFailure"),
      });
    }
  }

  async function runVerify(backupId: string) {
    try {
      setOperation(verifyOperation());
      setVerifyingBackupId(backupId);
      const result = await verifyBackup(backupId);
      showToast({
        tone: result.status === "Verified" ? "success" : "danger",
        title: result.status === "Verified" ? t("settings.backup.verifiedTitle") : t("settings.backup.verifyFailedTitle"),
        description: result.message,
      });
      await load();
    } catch (requestError) {
      showToast({
        tone: "danger",
        title: t("settings.backup.verifyFailedTitle"),
        description: requestError instanceof ApiError ? requestError.message : t("settings.backup.safeFailure"),
      });
    } finally {
      setVerifyingBackupId(null);
      setOperation(null);
    }
  }

  async function startRestore(backup: BackupListItem) {
    try {
      const response = await getBackupDetails(backup.backupId);
      setRestoreWizard({
        backup,
        details: response,
        preflight: null,
        result: null,
        acceptedWarning: false,
        confirmationText: "",
        error: "",
        loading: false,
        stageKey: "settings.backup.stage.idle",
      });
    } catch (requestError) {
      showToast({
        tone: "danger",
        title: t("settings.backup.restoreFailedTitle"),
        description: requestError instanceof ApiError ? requestError.message : t("settings.backup.safeFailure"),
      });
    }
  }

  async function runRestorePreflight() {
    if (!restoreWizard) return;

    try {
      setRestoreWizard((current) => current ? { ...current, loading: true, stageKey: "settings.backup.stage.preflight", error: "" } : current);
      const preflight = await validateRestoreBackup(restoreWizard.backup.backupId);
      setRestoreWizard((current) => current ? { ...current, preflight, loading: false } : current);
    } catch (requestError) {
      setRestoreWizard((current) => current ? {
        ...current,
        loading: false,
        error: requestError instanceof ApiError ? requestError.message : t("settings.backup.safeFailure"),
      } : current);
    }
  }

  async function runRestore() {
    if (!restoreWizard?.preflight?.operationId) return;

    try {
      setOperation(restoreOperation());
      setRestoreWizard((current) => current ? { ...current, loading: true, stageKey: "settings.backup.stage.restore", error: "" } : current);
      const result = await restoreBackup(restoreWizard.backup.backupId, restoreWizard.preflight.operationId);
      setRestoreWizard((current) => current ? { ...current, result, loading: false, stageKey: "settings.backup.stage.restart" } : current);
      showToast({
        tone: result.status === "Completed" ? "success" : "danger",
        title: result.status === "Completed" ? t("settings.backup.restoreCompletedTitle") : t("settings.backup.restoreFailedTitle"),
        description: result.message,
      });
      await load();
    } catch (requestError) {
      setRestoreWizard((current) => current ? {
        ...current,
        loading: false,
        error: requestError instanceof ApiError ? requestError.message : t("settings.backup.safeFailure"),
      } : current);
    } finally {
      setOperation(null);
    }
  }

  async function saveRetention() {
    try {
      setOperation(retentionOperation());
      const saved = await saveBackupRetentionSettings(retention);
      setRetention(saved);
      showToast({ tone: "success", title: t("settings.backup.retentionSavedTitle") });
      await load();
    } catch (requestError) {
      showToast({
        tone: "danger",
        title: t("settings.backup.retentionFailedTitle"),
        description: requestError instanceof ApiError ? requestError.message : t("settings.backup.safeFailure"),
      });
    } finally {
      setOperation(null);
    }
  }

  function updateRetention(field: keyof BackupRetentionSettings, value: number | boolean) {
    setRetention((current) => ({
      ...current,
      [field]: typeof value === "number" ? normalizeRetentionNumber(field, value) : value,
    }));
  }

  function updateCloudConfiguration(field: keyof CloudBackupConfiguration, value: string | number | boolean) {
    setCloudConnectionResult(null);
    setCloudSettingsDirty(true);
    setCloudConfiguration((current) => ({
      ...current,
      [field]: typeof value === "number" ? Math.max(1, Math.trunc(value)) : value,
    }));
  }

  async function saveCloudSettings() {
    try {
      setOperation(cloudSettingsOperation());
      const saved = await saveCloudBackupConfiguration({
        enabled: cloudConfiguration.enabled,
        autoUploadAfterBackup: cloudConfiguration.autoUploadAfterBackup,
        bucketName: cloudConfiguration.bucketName,
        endpoint: cloudConfiguration.endpoint,
        accessKey: replaceCloudCredentials ? cloudAccessKey.trim() : null,
        secretKey: replaceCloudCredentials ? cloudSecretKey.trim() : null,
        prefix: cloudConfiguration.prefix,
        retentionCount: cloudConfiguration.retentionCount,
        connectionTimeoutSeconds: cloudConfiguration.connectionTimeoutSeconds,
        retryCount: cloudConfiguration.retryCount,
        credentialUpdateMode: replaceCloudCredentials ? "Replace" : "Preserve",
      });
      setCloudConfiguration(saved);
      setCloudAccessKey("");
      setCloudSecretKey("");
      setReplaceCloudCredentials(false);
      setCloudConnectionResult(null);
      setCloudSettingsDirty(false);
      showToast({ tone: "success", title: t("settings.backup.cloud.settingsSavedTitle") });
      await load();
    } catch (requestError) {
      showToast({
        tone: "danger",
        title: t("settings.backup.cloud.settingsFailedTitle"),
        description: requestError instanceof ApiError ? requestError.message : t("settings.backup.safeFailure"),
      });
    } finally {
      setOperation(null);
    }
  }

  async function clearCloudCredentials() {
    try {
      setOperation(cloudSettingsOperation());
      const saved = await saveCloudBackupConfiguration({
        enabled: cloudConfiguration.enabled,
        autoUploadAfterBackup: cloudConfiguration.autoUploadAfterBackup,
        bucketName: cloudConfiguration.bucketName,
        endpoint: cloudConfiguration.endpoint,
        accessKey: null,
        secretKey: null,
        prefix: cloudConfiguration.prefix,
        retentionCount: cloudConfiguration.retentionCount,
        connectionTimeoutSeconds: cloudConfiguration.connectionTimeoutSeconds,
        retryCount: cloudConfiguration.retryCount,
        credentialUpdateMode: "Clear",
      });
      setCloudConfiguration(saved);
      setCloudAccessKey("");
      setCloudSecretKey("");
      setReplaceCloudCredentials(false);
      setCloudConnectionResult(null);
      setCloudSettingsDirty(false);
      setClearCloudCredentialsOpen(false);
      showToast({ tone: "success", title: t("settings.backup.cloud.credentialsClearedTitle") });
      await load();
    } catch (requestError) {
      showToast({
        tone: "danger",
        title: t("settings.backup.cloud.settingsFailedTitle"),
        description: requestError instanceof ApiError ? requestError.message : t("settings.backup.safeFailure"),
      });
    } finally {
      setOperation(null);
    }
  }

  async function testCloudConnection() {
    if (operationActive) {
      return;
    }

    if (cloudSettingsDirty || replaceCloudCredentials || cloudAccessKey.trim().length > 0 || cloudSecretKey.trim().length > 0) {
      showToast({
        tone: "warning",
        title: t("settings.backup.cloud.connectionSaveRequiredTitle"),
        description: t("settings.backup.cloud.connectionSaveRequiredDescription"),
      });
      return;
    }

    try {
      setOperation(cloudTestOperation());
      const result = await testCloudBackupConnection();
      setCloudConnectionResult(result);
      showToast({
        tone: result.succeeded ? "success" : "danger",
        title: result.succeeded ? t("settings.backup.cloud.connectionSucceededTitle") : t(cloudConnectionCategoryTitleKey(result.category)),
        description: result.succeeded ? result.message : t(cloudConnectionCategoryDescriptionKey(result.category)),
      });
    } catch (requestError) {
      showToast({
        tone: "danger",
        title: t("settings.backup.cloud.connectionFailedTitle"),
        description: requestError instanceof ApiError ? requestError.message : t("settings.backup.safeFailure"),
      });
    } finally {
      setOperation(null);
    }
  }

  async function queueCloudUpload(backupId: string) {
    try {
      setOperation(cloudUploadOperation());
      const item = await uploadCloudBackup(backupId, true);
      showToast({ tone: "success", title: t("settings.backup.cloud.uploadQueuedTitle"), description: item.backupId });
      await load();
    } catch (requestError) {
      showToast({
        tone: "danger",
        title: t("settings.backup.cloud.uploadFailedTitle"),
        description: requestError instanceof ApiError ? requestError.message : t("settings.backup.safeFailure"),
      });
    } finally {
      setOperation(null);
    }
  }

  async function downloadFromCloud(backupId: string) {
    try {
      setOperation(cloudDownloadOperation());
      const result = await downloadCloudBackup(backupId);
      showToast({
        tone: result.integrityStatus === "Verified" ? "success" : "warning",
        title: t("settings.backup.cloud.downloadedTitle"),
        description: result.message,
      });
      await load();
    } catch (requestError) {
      showToast({
        tone: "danger",
        title: t("settings.backup.cloud.downloadFailedTitle"),
        description: requestError instanceof ApiError ? requestError.message : t("settings.backup.safeFailure"),
      });
    } finally {
      setOperation(null);
    }
  }

  async function deleteFromCloud(backupId: string) {
    try {
      setOperation(cloudDeleteOperation());
      await deleteCloudBackup(backupId);
      setCloudDelete(null);
      showToast({ tone: "success", title: t("settings.backup.cloud.deletedTitle") });
      await load();
    } catch (requestError) {
      showToast({
        tone: "danger",
        title: t("settings.backup.cloud.deleteFailedTitle"),
        description: requestError instanceof ApiError ? requestError.message : t("settings.backup.safeFailure"),
      });
    } finally {
      setOperation(null);
    }
  }

  const actions = (
    <>
      <Button variant="secondary" disabled={loading || operationActive} onClick={() => void load()}>
        common.retry
      </Button>
      <Button disabled={!canCreateBackup || operationActive} isLoading={operation === "backup"} onClick={() => void runManualBackup()}>
        settings.backup.actions.backupNow
      </Button>
    </>
  );

  return (
    <SettingsScaffold title="settings.backup.title" description="settings.backup.description" actions={actions}>
      {loading ? (
        <div className="hc-backup-page">
          {Array.from({ length: 8 }, (_, index) => (
            <SkeletonLoader key={index} height={index === 0 ? "5rem" : "2.5rem"} />
          ))}
        </div>
      ) : null}
      {error ? (
        <EmptyState
          title="settings.backup.errorTitle"
          description="settings.backup.errorDescription"
          action={<Button variant="secondary" onClick={() => void load()}>common.retry</Button>}
        />
      ) : null}
      {!loading && !error && summary ? (
        <div className="hc-backup-page">
          <section className="hc-backup-summary-grid">
            {summaryCards.map((card) => (
              <Card key={card.label} padding="md" className="hc-backup-summary-card">
                <span>{t(card.label)}</span>
                <strong>{card.value.startsWith("settings.") ? t(card.value) : card.value}</strong>
                {"tone" in card ? <Badge tone={card.tone}>{card.value}</Badge> : null}
              </Card>
            ))}
          </section>

          <Card padding="md" muted>
            <div className="hc-backup-health-reasons">
              {summary.healthReasons.map((reason) => (
                <Badge key={reason.code} tone={backupHealthTone(summary.health)}>{t(`settings.backup.healthReason.${reason.code}`)}</Badge>
              ))}
            </div>
          </Card>

          <div className="hc-backup-tabs" role="tablist" aria-label={t("settings.backup.tabs.label")}>
            {[
              ["local", "settings.backup.tabs.local"],
              ["cloud", "settings.backup.tabs.cloud"],
              ["combined", "settings.backup.tabs.combined"],
            ].map(([tab, label]) => (
              <Button
                key={tab}
                size="sm"
                variant={activeTab === tab ? "primary" : "secondary"}
                onClick={() => setActiveTab(tab as BackupTab)}
              >
                {label}
              </Button>
            ))}
          </div>

          {activeTab === "local" ? (
          <section className="hc-backup-section">
            <div className="hc-backup-section__header">
              <div>
                <h2>{t("settings.backup.historyTitle")}</h2>
                <p>{t("settings.backup.historyDescription")}</p>
              </div>
            </div>

            <DataTable
              hasData={backups.length > 0}
              emptyState={<EmptyState title="settings.backup.emptyTitle" description="settings.backup.emptyDescription" />}
              columns={(
                <tr>
                  <th scope="col">{t("settings.backup.columns.createdAt")}</th>
                  <th scope="col">{t("settings.backup.columns.type")}</th>
                  <th scope="col">{t("settings.backup.columns.status")}</th>
                  <th scope="col">{t("settings.backup.columns.size")}</th>
                  <th scope="col">{t("settings.backup.columns.version")}</th>
                  <th scope="col">{t("settings.backup.columns.integrity")}</th>
                  <th scope="col" className="hc-table__head-actions">{t("common.actions")}</th>
                </tr>
              )}
              rows={backups.map((backup) => (
                <tr key={backup.backupId} className="hc-table__row">
                  <td>
                    <div className="hc-table__cell-strong">
                      <span className="hc-table__title">{formatDate(backup.createdAtUtc, { dateStyle: "medium", timeStyle: "short" })}</span>
                      <span className="hc-table__subtitle">{backup.backupId}</span>
                    </div>
                  </td>
                  <td>{t(backupReasonLabelKey(backup.reason))}</td>
                  <td><Badge tone={backupStatusTone(backup.status)}>{t(backupStatusLabelKey(backup.status))}</Badge></td>
                  <td>{formatBytes(backup.sizeBytes)}</td>
                  <td>{backup.applicationVersion ?? t("settings.backup.notAvailable")} / {backup.databaseSchemaVersion ?? t("settings.backup.notAvailable")}</td>
                  <td>
                    <div className="hc-table__status-stack">
                      <Badge tone={backupIntegrityTone(backup.integrityStatus)}>{t(backupIntegrityLabelKey(backup.integrityStatus))}</Badge>
                      <span className="hc-table__subtitle">
                        {backup.lastVerifiedAtUtc ? formatDate(backup.lastVerifiedAtUtc, { dateStyle: "medium", timeStyle: "short" }) : t("settings.backup.notAvailable")}
                      </span>
                    </div>
                  </td>
                  <td className="hc-table__cell-actions">
                    <div className="hc-table__actions">
                      <Button size="sm" variant="secondary" onClick={() => void openDetails(backup.backupId)}>common.view</Button>
                      <Button
                        size="sm"
                        variant="secondary"
                        disabled={!canCreateBackup || operationActive}
                        isLoading={verifyingBackupId === backup.backupId}
                        onClick={() => void runVerify(backup.backupId)}
                      >
                        settings.backup.actions.verify
                      </Button>
                      <Button
                        size="sm"
                        variant="danger"
                        disabled={!canValidateRestore || !canExecuteRestore || operationActive || backup.integrityStatus === "Failed"}
                        onClick={() => void startRestore(backup)}
                      >
                        settings.backup.actions.restore
                      </Button>
                    </div>
                  </td>
                </tr>
              ))}
            />
          </section>
          ) : null}

          {activeTab === "cloud" ? (
            <>
              <section className="hc-backup-section">
                <div className="hc-backup-section__header">
                  <div>
                    <h2>{t("settings.backup.cloud.statusTitle")}</h2>
                    <p>{t("settings.backup.cloud.statusDescription")}</p>
                  </div>
                  <Badge tone={cloudStatus?.status === "Ready" ? "success" : cloudStatus?.status === "Disabled" ? "neutral" : "warning"}>
                    {cloudStatus ? t(`settings.backup.cloud.status.${cloudStatus.status}`) : t("settings.backup.notAvailable")}
                  </Badge>
                </div>
                <Card padding="md">
                  <div className="hc-backup-cloud-status-grid">
                    <DetailRow label="settings.backup.cloud.fields.queue" value={String(cloudStatus?.queuedCount ?? 0)} />
                    <DetailRow label="settings.backup.cloud.fields.uploading" value={String(cloudStatus?.uploadingCount ?? 0)} />
                    <DetailRow label="settings.backup.cloud.fields.failed" value={String(cloudStatus?.failedCount ?? 0)} />
                    <DetailRow
                      label="settings.backup.cloud.fields.lastUpload"
                      value={cloudStatus?.lastSuccessfulUploadAtUtc ? formatDate(cloudStatus.lastSuccessfulUploadAtUtc, { dateStyle: "medium", timeStyle: "short" }) : t("settings.backup.notAvailable")}
                    />
                  </div>
                </Card>
              </section>

              <section className="hc-backup-section">
                <div className="hc-backup-section__header">
                  <div>
                    <h2>{t("settings.backup.cloud.settingsTitle")}</h2>
                    <p>{t("settings.backup.cloud.settingsDescription")}</p>
                  </div>
                  <div className="hc-table__actions">
                    <Button variant="secondary" disabled={operationActive} isLoading={operation === "cloudTest"} onClick={() => void testCloudConnection()}>
                      settings.backup.cloud.actions.testConnection
                    </Button>
                    <Button disabled={!canCreateBackup || operationActive} isLoading={operation === "cloudSettings"} onClick={() => void saveCloudSettings()}>
                      settings.backup.cloud.actions.saveSettings
                    </Button>
                  </div>
                </div>

                <Card padding="md">
                  <div className="hc-backup-cloud-settings-grid">
                    <Checkbox
                      checked={cloudConfiguration.enabled}
                      label="settings.backup.cloud.fields.enabled"
                      description="settings.backup.cloud.fields.enabledDescription"
                      onChange={(event) => updateCloudConfiguration("enabled", event.target.checked)}
                    />
                    <Checkbox
                      checked={cloudConfiguration.autoUploadAfterBackup}
                      label="settings.backup.cloud.fields.autoUpload"
                      description="settings.backup.cloud.fields.autoUploadDescription"
                      onChange={(event) => updateCloudConfiguration("autoUploadAfterBackup", event.target.checked)}
                    />
                    <Field label="settings.backup.cloud.fields.bucket">
                      <Input value={cloudConfiguration.bucketName} onChange={(event) => updateCloudConfiguration("bucketName", event.target.value)} />
                    </Field>
                    <Field label="settings.backup.cloud.fields.endpoint" hint="settings.backup.cloud.fields.endpointHint">
                      <Input value={cloudConfiguration.endpoint} onChange={(event) => updateCloudConfiguration("endpoint", event.target.value)} />
                    </Field>
	                    <Field label="settings.backup.cloud.fields.prefix">
	                      <Input value={cloudConfiguration.prefix} onChange={(event) => updateCloudConfiguration("prefix", event.target.value)} />
	                    </Field>
	                    <Checkbox
	                      checked={replaceCloudCredentials}
	                      label="settings.backup.cloud.fields.replaceCredentials"
	                      description={cloudConfiguration.hasAccessKey || cloudConfiguration.hasSecretKey ? "settings.backup.cloud.fields.replaceCredentialsDescription" : "settings.backup.cloud.fields.replaceCredentialsMissingDescription"}
	                      onChange={(event) => {
                          setCloudConnectionResult(null);
                          setCloudSettingsDirty(true);
                          setReplaceCloudCredentials(event.target.checked);
                        }}
	                    />
	                    <div className="hc-table__actions">
	                      <Button
	                        variant="secondary"
	                        disabled={operationActive || (!cloudConfiguration.hasAccessKey && !cloudConfiguration.hasSecretKey)}
	                        onClick={() => setClearCloudCredentialsOpen(true)}
	                      >
	                        settings.backup.cloud.actions.clearCredentials
	                      </Button>
	                    </div>
	                    {replaceCloudCredentials ? (
	                      <>
	                        <Field label="settings.backup.cloud.fields.accessKey" hint="settings.backup.cloud.fields.credentialReplaceRequired">
	                          <Input value={cloudAccessKey} onChange={(event) => {
                              setCloudConnectionResult(null);
                              setCloudSettingsDirty(true);
                              setCloudAccessKey(event.target.value);
                            }} />
	                        </Field>
	                        <Field label="settings.backup.cloud.fields.secretKey" hint="settings.backup.cloud.fields.credentialReplaceRequired">
	                          <Input type="password" value={cloudSecretKey} onChange={(event) => {
                              setCloudConnectionResult(null);
                              setCloudSettingsDirty(true);
                              setCloudSecretKey(event.target.value);
                            }} />
	                        </Field>
	                      </>
	                    ) : null}
                    <Field label="settings.backup.cloud.fields.retentionCount">
                      <Input type="number" min={1} value={cloudConfiguration.retentionCount} onChange={(event) => updateCloudConfiguration("retentionCount", Number(event.target.value))} />
                    </Field>
                    <Field label="settings.backup.cloud.fields.timeout">
                      <Input type="number" min={3} value={cloudConfiguration.connectionTimeoutSeconds} onChange={(event) => updateCloudConfiguration("connectionTimeoutSeconds", Number(event.target.value))} />
                    </Field>
                    <Field label="settings.backup.cloud.fields.retryCount">
                      <Input type="number" min={1} value={cloudConfiguration.retryCount} onChange={(event) => updateCloudConfiguration("retryCount", Number(event.target.value))} />
                    </Field>
                  </div>
                </Card>
                {cloudConnectionResult ? (
                  <Card padding="md">
                    <div className="hc-backup-cloud-test-result">
                      <div>
                        <Badge tone={cloudConnectionResult.succeeded ? "success" : "danger"}>
                          {cloudConnectionResult.succeeded
                            ? t("settings.backup.cloud.connectionSucceededTitle")
                            : t(cloudConnectionCategoryTitleKey(cloudConnectionResult.category))}
                        </Badge>
                        <p className="hc-form-help">
                          {cloudConnectionResult.succeeded
                            ? cloudConnectionResult.message
                            : t(cloudConnectionCategoryDescriptionKey(cloudConnectionResult.category))}
                        </p>
                      </div>
                      <div className="hc-backup-cloud-status-grid">
                        <DetailRow label="settings.backup.cloud.connectionDetails.category" value={t(cloudConnectionCategoryTitleKey(cloudConnectionResult.category))} />
                        <DetailRow label="settings.backup.cloud.connectionDetails.stage" value={t(cloudConnectionStageLabelKey(cloudConnectionResult.stage))} />
                        <DetailRow label="settings.backup.cloud.connectionDetails.statusCode" value={cloudConnectionResult.statusCode == null ? t("settings.backup.notAvailable") : String(cloudConnectionResult.statusCode)} />
                        <DetailRow label="settings.backup.cloud.connectionDetails.providerCode" value={cloudConnectionResult.providerErrorCode ?? t("settings.backup.notAvailable")} />
                        <DetailRow
                          label="settings.backup.cloud.connectionDetails.cleanup"
                          value={cloudConnectionResult.cleanupSucceeded
                            ? t("settings.backup.cloud.connectionCleanup.succeeded")
                            : t("settings.backup.cloud.connectionCleanup.failed")}
                        />
                        <DetailRow
                          label="settings.backup.cloud.connectionDetails.testedAt"
                          value={formatDate(cloudConnectionResult.testedAtUtc, { dateStyle: "medium", timeStyle: "short" })}
                        />
                      </div>
                    </div>
                  </Card>
                ) : null}
              </section>

              <section className="hc-backup-section">
                <div className="hc-backup-section__header">
                  <div>
                    <h2>{t("settings.backup.cloud.historyTitle")}</h2>
                    <p>{t("settings.backup.cloud.historyDescription")}</p>
                  </div>
                </div>
                <DataTable
                  hasData={cloudBackups.length > 0}
                  emptyState={<EmptyState title="settings.backup.cloud.emptyTitle" description="settings.backup.cloud.emptyDescription" />}
                  columns={(
                    <tr>
                      <th scope="col">{t("settings.backup.columns.createdAt")}</th>
                      <th scope="col">{t("settings.backup.columns.size")}</th>
                      <th scope="col">{t("settings.backup.cloud.columns.upload")}</th>
                      <th scope="col">{t("settings.backup.cloud.columns.sync")}</th>
                      <th scope="col" className="hc-table__head-actions">{t("common.actions")}</th>
                    </tr>
                  )}
                  rows={cloudBackups.map((backup) => (
                    <tr key={backup.backupId} className="hc-table__row">
                      <td>
                        <div className="hc-table__cell-strong">
                          <span className="hc-table__title">{formatDate(backup.createdAtUtc, { dateStyle: "medium", timeStyle: "short" })}</span>
                          <span className="hc-table__subtitle">{backup.backupId}</span>
                        </div>
                      </td>
                      <td>{formatBytes(backup.sizeBytes)}</td>
                      <td><Badge tone={cloudUploadTone(backup.uploadStatus)}>{t(cloudUploadLabelKey(backup.uploadStatus))}</Badge></td>
                      <td><Badge tone={cloudSyncTone(backup.syncStatus)}>{t(cloudSyncLabelKey(backup.syncStatus))}</Badge></td>
                      <td className="hc-table__cell-actions">
                        <div className="hc-table__actions">
                          <Button size="sm" variant="secondary" disabled={!canValidateRestore || operationActive} isLoading={operation === "cloudDownload"} onClick={() => void downloadFromCloud(backup.backupId)}>
                            settings.backup.cloud.actions.download
                          </Button>
	                          <Button size="sm" variant="danger" disabled={!canCreateBackup || operationActive} isLoading={operation === "cloudDelete" && cloudDelete?.backup.backupId === backup.backupId} onClick={() => setCloudDelete({ backup, confirmationText: "" })}>
	                            settings.backup.cloud.actions.deleteCloud
	                          </Button>
                        </div>
                      </td>
                    </tr>
                  ))}
                />
              </section>
            </>
          ) : null}

          {activeTab === "combined" ? (
            <section className="hc-backup-section">
              <div className="hc-backup-section__header">
                <div>
                  <h2>{t("settings.backup.cloud.combinedTitle")}</h2>
                  <p>{t("settings.backup.cloud.combinedDescription")}</p>
                </div>
              </div>
              <DataTable
                hasData={combinedBackups.length > 0}
                emptyState={<EmptyState title="settings.backup.emptyTitle" description="settings.backup.emptyDescription" />}
                columns={(
                  <tr>
                    <th scope="col">{t("settings.backup.columns.createdAt")}</th>
                    <th scope="col">{t("settings.backup.columns.size")}</th>
                    <th scope="col">{t("settings.backup.cloud.columns.local")}</th>
                    <th scope="col">{t("settings.backup.cloud.columns.cloud")}</th>
                    <th scope="col">{t("settings.backup.cloud.columns.upload")}</th>
                    <th scope="col">{t("settings.backup.cloud.columns.sync")}</th>
                    <th scope="col" className="hc-table__head-actions">{t("common.actions")}</th>
                  </tr>
                )}
                rows={combinedBackups.map((backup) => (
                  <tr key={backup.backupId} className="hc-table__row">
                    <td>
                      <div className="hc-table__cell-strong">
                        <span className="hc-table__title">{formatDate(backup.createdAtUtc, { dateStyle: "medium", timeStyle: "short" })}</span>
                        <span className="hc-table__subtitle">{backup.backupId}</span>
                      </div>
                    </td>
                    <td>{formatBytes(backup.sizeBytes)}</td>
                    <td><Badge tone={backupStatusTone(backup.localStatus)}>{t(backupStatusLabelKey(backup.localStatus))}</Badge></td>
                    <td><Badge tone={cloudObjectTone(backup.cloudStatus)}>{t(cloudObjectLabelKey(backup.cloudStatus))}</Badge></td>
                    <td><Badge tone={cloudUploadTone(backup.uploadStatus)}>{t(cloudUploadLabelKey(backup.uploadStatus))}</Badge></td>
                    <td><Badge tone={cloudSyncTone(backup.syncStatus)}>{t(cloudSyncLabelKey(backup.syncStatus))}</Badge></td>
                    <td className="hc-table__cell-actions">
                      <div className="hc-table__actions">
                        <Button size="sm" variant="secondary" disabled={!canCreateBackup || operationActive || backup.localStatus !== "Succeeded"} isLoading={operation === "cloudUpload"} onClick={() => void queueCloudUpload(backup.backupId)}>
                          settings.backup.cloud.actions.upload
                        </Button>
                        <Button size="sm" variant="secondary" disabled={!canValidateRestore || operationActive || backup.cloudStatus !== "Present"} isLoading={operation === "cloudDownload"} onClick={() => void downloadFromCloud(backup.backupId)}>
                          settings.backup.cloud.actions.download
                        </Button>
                      </div>
                    </td>
                  </tr>
                ))}
              />
            </section>
          ) : null}

          {activeTab === "local" ? (
          <section className="hc-backup-section">
            <div className="hc-backup-section__header">
              <div>
                <h2>{t("settings.backup.retentionTitle")}</h2>
                <p>{t("settings.backup.retentionDescription")}</p>
              </div>
              <Button disabled={!canCreateBackup || operationActive} isLoading={operation === "retention"} onClick={() => void saveRetention()}>
                settings.backup.actions.saveRetention
              </Button>
            </div>

            <Card padding="md">
              <div className="hc-backup-retention-grid">
                <Checkbox
                  checked={retention.enabled}
                  label="settings.backup.retention.enabled"
                  description="settings.backup.retention.enabledDescription"
                  onChange={(event) => updateRetention("enabled", event.target.checked)}
                />
                {[
                  ["manualCount", "settings.backup.retention.manualCount"],
                  ["beforeMigrationCount", "settings.backup.retention.beforeMigrationCount"],
                  ["beforeRestoreCount", "settings.backup.retention.beforeRestoreCount"],
                  ["beforeApplicationUpdateCount", "settings.backup.retention.beforeApplicationUpdateCount"],
                  ["minimumAgeHoursBeforeDeletion", "settings.backup.retention.minimumAgeHoursBeforeDeletion"],
                ].map(([field, label]) => (
                  <Field key={field} label={label}>
                    <Input
                      aria-label={label}
                      min={field === "minimumAgeHoursBeforeDeletion" ? 0 : 1}
                      type="number"
                      value={retention[field as keyof BackupRetentionSettings] as number}
                      onChange={(event) => updateRetention(field as keyof BackupRetentionSettings, Number(event.target.value))}
                    />
                  </Field>
                ))}
              </div>
            </Card>
          </section>
          ) : null}
        </div>
      ) : null}

      <DetailsDialog details={details} onClose={() => setDetails(null)} />
      <RestoreWizard
        state={restoreWizard}
        onChange={(next) => setRestoreWizard((current) => current ? { ...current, ...next } : current)}
        onClose={() => setRestoreWizard(null)}
        onRunPreflight={() => void runRestorePreflight()}
        onRestore={() => void runRestore()}
      />
      <CloudDeleteDialog
        state={cloudDelete}
        loading={operation === "cloudDelete"}
        onChange={(confirmationText) => setCloudDelete((current) => current ? { ...current, confirmationText } : current)}
        onClose={() => setCloudDelete(null)}
        onConfirm={() => cloudDelete ? void deleteFromCloud(cloudDelete.backup.backupId) : undefined}
      />
      <CloudCredentialsClearDialog
        open={clearCloudCredentialsOpen}
        loading={operation === "cloudSettings"}
        onClose={() => setClearCloudCredentialsOpen(false)}
        onConfirm={() => void clearCloudCredentials()}
      />
    </SettingsScaffold>
  );
}
