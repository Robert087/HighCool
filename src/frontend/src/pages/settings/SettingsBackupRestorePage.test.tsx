import { describe, expect, it, vi, afterEach } from "vitest";
import type {
  BackupCenterSummary,
  BackupDetails,
  BackupListItem,
  BackupRetentionSettings,
  CloudBackupConfiguration,
  CloudBackupConnectionTestResult,
  CloudBackupListItem,
  CloudBackupStatusSummary,
  RestorePreflightResult,
} from "../../services/backupApi";
import type { BackupRestoreOperation } from "./backupRestorePageState";

const retention: BackupRetentionSettings = {
  enabled: true,
  manualCount: 10,
  scheduledCount: 24,
  beforeMigrationCount: 10,
  beforeRestoreCount: 10,
  beforeApplicationUpdateCount: 10,
  minimumAgeHoursBeforeDeletion: 24,
};

const summary: BackupCenterSummary = {
  health: "Healthy",
  healthReasons: [],
  databaseFileName: "highcool.db",
  databaseSizeBytes: 2048,
  availableBackupCount: 1,
  lastSuccessfulBackupAtUtc: "2026-07-25T10:00:00Z",
  lastIntegrityVerificationAtUtc: "2026-07-25T10:30:00Z",
  backupStorageUsedBytes: 4096,
  encryptionStatus: "AES-256-GCM",
  retentionEnabled: true,
  retentionStatus: "Enabled",
  applicationVersion: "1.0.0",
  databaseSchemaVersion: 5,
  retentionSettings: retention,
};

const backup: BackupListItem = {
  backupId: "backup-001",
  createdAtUtc: "2026-07-25T10:00:00Z",
  reason: "Manual",
  status: "Succeeded",
  sizeBytes: 4096,
  applicationVersion: "1.0.0",
  databaseSchemaVersion: 5,
  integrityStatus: "Verified",
  lastVerifiedAtUtc: "2026-07-25T10:30:00Z",
};

const cloudStatus: CloudBackupStatusSummary = {
  status: "Ready",
  message: "Cloud backup is ready.",
  enabled: true,
  configured: true,
  queuedCount: 0,
  uploadingCount: 0,
  failedCount: 0,
  lastSuccessfulUploadAtUtc: "2026-07-25T10:35:00Z",
};

const cloudConfiguration: CloudBackupConfiguration = {
  enabled: true,
  autoUploadAfterBackup: true,
  bucketName: "highcool-backups",
  endpoint: "https://aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa.r2.cloudflarestorage.com",
  accessKey: "abcd...wxyz",
  hasAccessKey: true,
  hasSecretKey: true,
  prefix: "desktop",
  retentionCount: 30,
  connectionTimeoutSeconds: 30,
  retryCount: 3,
};

const cloudBackup: CloudBackupListItem = {
  backupId: backup.backupId,
  createdAtUtc: backup.createdAtUtc,
  sizeBytes: backup.sizeBytes,
  checksumSha256: "encrypted-sha",
  localStatus: "Succeeded",
  cloudStatus: "Present",
  uploadStatus: "Uploaded",
  verificationStatus: "Verified",
  syncStatus: "InSync",
  lastUploadedAtUtc: "2026-07-25T10:35:00Z",
  cloudObjectKey: "desktop/backups/backup-001/backup.manifest.json",
};

const successfulConnectionResult: CloudBackupConnectionTestResult = {
  success: true,
  succeeded: true,
  status: "Succeeded",
  category: "None",
  message: "Cloud backup connection verified.",
  stage: "Completed",
  statusCode: null,
  providerErrorCode: null,
  cleanupSucceeded: true,
  testedAtUtc: "2026-07-25T10:45:00Z",
};

const failedConnectionResult: CloudBackupConnectionTestResult = {
  success: false,
  succeeded: false,
  status: "InvalidCredentials",
  category: "InvalidCredentials",
  message: "The R2 credentials are invalid.",
  stage: "Write",
  statusCode: 403,
  providerErrorCode: "SignatureDoesNotMatch",
  cleanupSucceeded: true,
  testedAtUtc: "2026-07-25T10:45:00Z",
};

const details: BackupDetails = {
  ...backup,
  applicationVersion: "1.0.0",
  databaseSchemaVersion: 5,
  manifestVersion: 1,
  encryptionAlgorithm: "AES-256-GCM",
  backupSizeBytes: 4096,
  originalDatabaseSizeBytes: 2048,
  compressionStatus: "None",
  restoreCompatibilityStatus: "Valid",
  restoreCompatibilityMessage: "Ready to restore",
  databaseFileName: "highcool.db",
  encryptedSha256: "encrypted-sha",
  plainSha256: "plain-sha",
};

const validPreflight: RestorePreflightResult = {
  backupId: backup.backupId,
  status: "Valid",
  message: "Ready to restore",
  schemaVersion: backup.databaseSchemaVersion,
  operationId: "restore-operation-001",
  operationExpiresAtUtc: "2026-07-25T10:40:00Z",
};

interface PageState {
  backups?: BackupListItem[];
  canCreateBackup?: boolean;
  canExecuteRestore?: boolean;
  canValidateRestore?: boolean;
  cloudBackups?: CloudBackupListItem[];
  cloudConfiguration?: CloudBackupConfiguration;
  cloudStatus?: CloudBackupStatusSummary;
  combinedBackups?: CloudBackupListItem[];
  clearCloudCredentialsOpen?: boolean;
  activeTab?: "local" | "cloud" | "combined";
  details?: BackupDetails | null;
  error?: string;
  loading?: boolean;
  operation?: BackupRestoreOperation;
  replaceCloudCredentials?: boolean;
  restoreWizard?: unknown;
  summary?: BackupCenterSummary | null;
  verifyingBackupId?: string | null;
  cloudDelete?: { backup: CloudBackupListItem; confirmationText: string } | null;
  cloudConnectionResult?: CloudBackupConnectionTestResult | null;
  cloudSettingsDirty?: boolean;
}

function escapeHtml(value: string) {
  return value
    .replace(/&/g, "&amp;")
    .replace(/"/g, "&quot;");
}

async function renderPage(state: PageState = {}) {
  vi.resetModules();

  const stateValues = [
    state.summary ?? summary,
    state.backups ?? [backup],
    state.cloudStatus ?? cloudStatus,
    state.cloudConfiguration ?? cloudConfiguration,
    "",
    "",
    state.replaceCloudCredentials ?? false,
    state.clearCloudCredentialsOpen ?? false,
    state.cloudBackups ?? [cloudBackup],
    state.combinedBackups ?? [cloudBackup],
    state.activeTab ?? "local",
    retention,
    state.details ?? null,
    state.restoreWizard ?? null,
    state.loading ?? false,
    state.error ?? "",
    state.operation ?? null,
    state.verifyingBackupId ?? null,
    state.cloudDelete ?? null,
    state.cloudConnectionResult ?? null,
    state.cloudSettingsDirty ?? false,
  ];

  vi.doMock("react", async () => {
    const actual = await vi.importActual<typeof import("react")>("react");
    let stateIndex = 0;

    return {
      ...actual,
      useEffect: () => undefined,
      useMemo: <T,>(factory: () => T) => factory(),
      useState: <T,>(initialValue: T | (() => T)) => {
        const value = stateIndex < stateValues.length
          ? stateValues[stateIndex]
          : typeof initialValue === "function"
            ? (initialValue as () => T)()
            : initialValue;
        stateIndex += 1;

        return [value, vi.fn()] as const;
      },
    };
  });

  vi.doMock("../../features/auth/AuthProvider", () => ({
    useAuth: () => ({
      hasPermission: (permission: string) => {
        if (permission === "settings.database_backup.create") {
          return state.canCreateBackup ?? true;
        }

        if (permission === "settings.database_restore.validate") {
          return state.canValidateRestore ?? true;
        }

        if (permission === "settings.database_restore.execute") {
          return state.canExecuteRestore ?? true;
        }

        return true;
      },
    }),
  }));

  vi.doMock("../../i18n", async () => {
    const actual = await vi.importActual<typeof import("../../i18n")>("../../i18n");

    return {
      ...actual,
      useI18n: () => ({
        formatDate: (value: string) => value,
        t: (key: string) => key,
        translateText: (value: string) => value,
      }),
    };
  });

  vi.doMock("../../components/ui", async () => {
    const actual = await vi.importActual<typeof import("../../components/ui")>("../../components/ui");

    return {
      ...actual,
      useToast: () => ({ showToast: vi.fn() }),
    };
  });

  vi.doMock("../../services/backupApi", () => ({
    createManualBackup: vi.fn(),
    getBackupDetails: vi.fn(),
    getBackupSummary: vi.fn(),
    listBackups: vi.fn(),
    restoreBackup: vi.fn(),
    deleteCloudBackup: vi.fn(),
    downloadCloudBackup: vi.fn(),
    getCloudBackupConfiguration: vi.fn(),
    getCloudBackupStatus: vi.fn(),
    listCloudBackups: vi.fn(),
    listCombinedCloudBackups: vi.fn(),
    saveCloudBackupConfiguration: vi.fn(),
    saveBackupRetentionSettings: vi.fn(),
    testCloudBackupConnection: vi.fn(),
    uploadCloudBackup: vi.fn(),
    validateRestoreBackup: vi.fn(),
    verifyBackup: vi.fn(),
  }));

  const [{ renderToStaticMarkup }, { MemoryRouter }, { SettingsBackupRestorePage }] = await Promise.all([
    import("react-dom/server"),
    import("react-router-dom"),
    import("./SettingsBackupRestorePage"),
  ]);

  return renderToStaticMarkup(
    <MemoryRouter>
      <SettingsBackupRestorePage />
    </MemoryRouter>,
  );
}

afterEach(() => {
  vi.doUnmock("react");
  vi.doUnmock("../../features/auth/AuthProvider");
  vi.doUnmock("../../i18n");
  vi.doUnmock("../../components/ui");
  vi.doUnmock("../../services/backupApi");
});

describe("SettingsBackupRestorePage", () => {
  it("renders the loading state before backup data is available", async () => {
    const html = await renderPage({ loading: true, summary: null, backups: [] });

    expect(html).toContain("hc-skeleton");
    expect(html).toContain("settings.backup.actions.backupNow");
  });

  it("renders summary cards and the empty history state", async () => {
    const html = await renderPage({ backups: [] });

    expect(html).toContain("settings.backup.summary.database");
    expect(html).toContain("highcool.db");
    expect(html).toContain("settings.backup.emptyTitle");
    expect(html).not.toContain("backup-001");
  });

  it("renders backup history rows without exposing raw paths", async () => {
    const html = await renderPage();

    expect(html).toContain("backup-001");
    expect(html).toContain("settings.backup.actions.verify");
    expect(html).toContain("settings.backup.actions.restore");
    expect(html).not.toContain("/root/");
    expect(html).not.toContain("/tmp/");
  });

  it("renders feature-unavailable and permission-denied states safely", async () => {
    const featureUnavailableHtml = await renderPage({
      error: "Local database features are unavailable in this host.",
      summary: null,
      backups: [],
    });

    expect(featureUnavailableHtml).toContain("settings.backup.errorTitle");
    expect(featureUnavailableHtml).toContain("common.retry");
    expect(featureUnavailableHtml).not.toContain("/root/");

    const permissionDeniedHtml = await renderPage({
      canCreateBackup: false,
      canExecuteRestore: false,
      canValidateRestore: false,
    });

    expect(permissionDeniedHtml).toContain(`disabled=""`);
    expect(permissionDeniedHtml).toContain("settings.backup.actions.backupNow");
  });

  it("keeps backup, verify, restore, and retention progress states distinct", async () => {
    const backupHtml = await renderPage({ operation: "backup" });
    expect(backupHtml).toContain(`<button class="hc-button hc-button--primary hc-button--md" disabled="" type="button">app.loading</button>`);
    expect(backupHtml).toContain("settings.backup.actions.saveRetention");

    const verifyHtml = await renderPage({ operation: "verify", verifyingBackupId: backup.backupId });
    expect(verifyHtml).toContain("app.loading");
    expect(verifyHtml).toContain("settings.backup.actions.backupNow");

    const retentionHtml = await renderPage({ operation: "retention" });
    expect(retentionHtml).toContain(`<button class="hc-button hc-button--primary hc-button--md" disabled="" type="button">settings.backup.actions.backupNow</button>`);
    expect(retentionHtml).toContain(`<button class="hc-button hc-button--primary hc-button--md" disabled="" type="button">app.loading</button>`);
  });

  it("renders the details dialog with safe backup metadata only", async () => {
    const html = await renderPage({ details });

    expect(html).toContain("settings.backup.detailsTitle");
    expect(html).toContain("encrypted-sha");
    expect(html).toContain("plain-sha");
    expect(html).toContain("highcool.db");
    expect(html).not.toContain("EncryptionKey");
    expect(html).not.toContain("/root/");
  });

  it("blocks restore confirmation until a valid preflight operation exists", async () => {
    const htmlWithoutOperation = await renderPage({
      restoreWizard: {
        backup,
        details,
        preflight: { ...validPreflight, operationId: "" },
        result: null,
        acceptedWarning: true,
        confirmationText: "RESTORE",
        error: "",
        loading: false,
        stageKey: "settings.backup.stage.idle",
      },
    });

    expect(htmlWithoutOperation).toContain("settings.backup.restoreTitle");
    expect(htmlWithoutOperation).toContain(`settings.backup.actions.restoreNow</button>`);
    expect(htmlWithoutOperation).toContain(`disabled=""`);

    const htmlWithOperation = await renderPage({
      restoreWizard: {
        backup,
        details,
        preflight: validPreflight,
        result: null,
        acceptedWarning: true,
        confirmationText: "RESTORE",
        error: "",
        loading: false,
        stageKey: "settings.backup.stage.idle",
      },
    });

    expect(htmlWithOperation).toContain("Ready to restore");
    expect(htmlWithOperation).toContain(`settings.backup.actions.restoreNow</button>`);
    expect(htmlWithOperation).not.toContain(escapeHtml(validPreflight.operationId ?? ""));
  });

  it("renders cloud credential and delete confirmations from explicit state only", async () => {
    const cloudHtml = await renderPage({ activeTab: "cloud" });
    expect(cloudHtml).toContain("settings.backup.cloud.fields.replaceCredentials");
    expect(cloudHtml).toContain("settings.backup.cloud.actions.clearCredentials");
    expect(cloudHtml).not.toContain("settings.backup.cloud.deleteConfirmTitle");

    const clearHtml = await renderPage({ clearCloudCredentialsOpen: true });
    expect(clearHtml).toContain("settings.backup.cloud.clearCredentialsTitle");

    const deleteHtml = await renderPage({
      cloudDelete: {
        backup: cloudBackup,
        confirmationText: "",
      },
    });
    expect(deleteHtml).toContain("settings.backup.cloud.deleteConfirmTitle");
    expect(deleteHtml).toContain(cloudBackup.backupId);
  });

  it("renders a successful categorized cloud connection result", async () => {
    const html = await renderPage({
      activeTab: "cloud",
      cloudConnectionResult: successfulConnectionResult,
    });

    expect(html).toContain("settings.backup.cloud.connectionSucceededTitle");
    expect(html).toContain("Cloud backup connection verified.");
    expect(html).toContain("settings.backup.cloud.connectionStage.Completed");
    expect(html).toContain("settings.backup.cloud.connectionCleanup.succeeded");
  });

  it("renders categorized cloud connection failures without secrets", async () => {
    const html = await renderPage({
      activeTab: "cloud",
      cloudConnectionResult: failedConnectionResult,
    });

    expect(html).toContain("settings.backup.cloud.connectionCategory.InvalidCredentials.title");
    expect(html).toContain("settings.backup.cloud.connectionCategory.InvalidCredentials.description");
    expect(html).toContain("settings.backup.cloud.connectionStage.Write");
    expect(html).toContain("403");
    expect(html).toContain("SignatureDoesNotMatch");
    expect(html).not.toContain("secret");
    expect(html).not.toContain("raw exception");
  });

  it("keeps the generic unknown fallback as the final cloud connection message", async () => {
    const html = await renderPage({
      activeTab: "cloud",
      cloudConnectionResult: {
        ...failedConnectionResult,
        status: "UnknownProviderFailure",
        category: "UnknownProviderFailure",
        stage: "ClientCreation",
        statusCode: null,
        providerErrorCode: null,
      },
    });

    expect(html).toContain("settings.backup.cloud.connectionCategory.UnknownProviderFailure.title");
    expect(html).toContain("settings.backup.cloud.connectionCategory.UnknownProviderFailure.description");
  });

  it("shows cloud test loading state and suppresses stale results when state is reset", async () => {
    const loadingHtml = await renderPage({
      activeTab: "cloud",
      operation: "cloudTest",
      cloudConnectionResult: failedConnectionResult,
    });

    expect(loadingHtml).toContain("app.loading");
    expect(loadingHtml).toContain(`disabled=""`);

    const resetHtml = await renderPage({
      activeTab: "cloud",
      cloudConnectionResult: null,
    });

    expect(resetHtml).not.toContain("settings.backup.cloud.connectionCategory.InvalidCredentials.title");
    expect(resetHtml).not.toContain("SignatureDoesNotMatch");
  });
});
