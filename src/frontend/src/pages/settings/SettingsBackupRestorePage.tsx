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
  type BackupDetails,
  type BackupListItem,
  type BackupRetentionSettings,
  type RestorePreflightResult,
  type RestoreResult,
} from "../../services/backupApi";
import {
  backupHealthTone,
  backupIntegrityLabelKey,
  backupIntegrityTone,
  backupReasonLabelKey,
  backupStatusLabelKey,
  backupStatusTone,
  formatBytes,
  restorePreflightLabelKey,
  restorePreflightTone,
} from "../../services/backupPresentation";
import { SettingsScaffold } from "./SettingsScaffold";
import {
  backupOperation,
  canSubmitRestore,
  normalizeRetentionNumber,
  retentionOperation,
  restoreOperation,
  verifyOperation,
  type BackupRestoreOperation,
} from "./backupRestorePageState";

type Operation = BackupRestoreOperation;

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

const EMPTY_RETENTION: BackupRetentionSettings = {
  enabled: true,
  manualCount: 10,
  scheduledCount: 24,
  beforeMigrationCount: 10,
  beforeRestoreCount: 10,
  beforeApplicationUpdateCount: 10,
  minimumAgeHoursBeforeDeletion: 24,
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

export function SettingsBackupRestorePage() {
  const { hasPermission } = useAuth();
  const { formatDate, t } = useI18n();
  const { showToast } = useToast();
  const [summary, setSummary] = useState<BackupCenterSummary | null>(null);
  const [backups, setBackups] = useState<BackupListItem[]>([]);
  const [retention, setRetention] = useState<BackupRetentionSettings>(EMPTY_RETENTION);
  const [details, setDetails] = useState<BackupDetails | null>(null);
  const [restoreWizard, setRestoreWizard] = useState<RestoreWizardState | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [operation, setOperation] = useState<Operation>(null);
  const [verifyingBackupId, setVerifyingBackupId] = useState<string | null>(null);

  const canCreateBackup = hasPermission(Permissions.SettingsDatabaseBackupCreate);
  const canValidateRestore = hasPermission(Permissions.SettingsDatabaseRestoreValidate);
  const canExecuteRestore = hasPermission(Permissions.SettingsDatabaseRestoreExecute);
  const operationActive = operation !== null;

  async function load() {
    try {
      setLoading(true);
      const [summaryResponse, backupsResponse] = await Promise.all([
        getBackupSummary(),
        listBackups(),
      ]);
      setSummary(summaryResponse);
      setBackups(backupsResponse);
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
      await load();
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
    </SettingsScaffold>
  );
}
