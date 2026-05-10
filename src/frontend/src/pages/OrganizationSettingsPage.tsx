import { useEffect, useMemo, useState, type Dispatch, type SetStateAction } from "react";
import { useSearchParams } from "react-router-dom";
import { Badge, Button, Card, Checkbox, DataTable, EmptyState, Field, Input, PageHeader, Select, SkeletonLoader, useToast } from "../components/ui";
import { useI18n } from "../i18n";
import { ApiError } from "../services/api";
import {
  getOrganizationSetup,
  getSecuritySettings,
  listAuditLog,
  listInvitations,
  listRoles,
  listSessions,
  listUsers,
  saveOrganizationSetup,
  updateSecuritySettings,
  type AuditLogItem,
  type Invitation,
  type OrganizationSetup,
  type SaveOrganizationSetupRequest,
  type SecuritySettings,
  type SessionItem,
  type UserListItem,
  type AuthRole,
} from "../services/settingsApi";

type SettingsTab =
  | "company-profile"
  | "features"
  | "workflow"
  | "stock"
  | "users"
  | "roles"
  | "invitations"
  | "security"
  | "sessions"
  | "audit-log"
  | "localization"
  | "document-numbering";

const tabs: SettingsTab[] = [
  "company-profile",
  "features",
  "workflow",
  "stock",
  "users",
  "roles",
  "invitations",
  "security",
  "sessions",
  "audit-log",
  "localization",
  "document-numbering",
];

function toRequestModel(setup: OrganizationSetup): SaveOrganizationSetupRequest {
  return {
    ...setup.organization,
    ...setup.features,
    ...setup.workflow,
    ...setup.stock,
    defaultWarehouseName: null,
    setupStep: setup.status.setupStep,
    setupVersion: setup.status.setupVersion,
  };
}

export function OrganizationSettingsPage() {
  const [searchParams, setSearchParams] = useSearchParams();
  const activeTab = (searchParams.get("tab") as SettingsTab | null) ?? "company-profile";
  const { formatDate, t } = useI18n();
  const { showToast } = useToast();
  const [form, setForm] = useState<SaveOrganizationSetupRequest | null>(null);
  const [warehouses, setWarehouses] = useState<OrganizationSetup["warehouses"]>([]);
  const [users, setUsers] = useState<UserListItem[]>([]);
  const [roles, setRoles] = useState<AuthRole[]>([]);
  const [invitations, setInvitations] = useState<Invitation[]>([]);
  const [sessions, setSessions] = useState<SessionItem[]>([]);
  const [auditLogs, setAuditLogs] = useState<AuditLogItem[]>([]);
  const [security, setSecurity] = useState<SecuritySettings | null>(null);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState("");

  useEffect(() => {
    let active = true;

    async function load() {
      try {
        setLoading(true);
        const [setup, securityResponse, usersResponse, rolesResponse, invitationsResponse, sessionsResponse, auditResponse] = await Promise.all([
          getOrganizationSetup(),
          getSecuritySettings(),
          listUsers(),
          listRoles(),
          listInvitations(),
          listSessions(),
          listAuditLog({ page: 1, pageSize: 50 }),
        ]);

        if (!active) {
          return;
        }

        setForm(toRequestModel(setup));
        setWarehouses(setup.warehouses);
        setSecurity(securityResponse);
        setUsers(usersResponse.items);
        setRoles(rolesResponse);
        setInvitations(invitationsResponse);
        setSessions(sessionsResponse);
        setAuditLogs(auditResponse);
        setError("");
      } catch (loadError) {
        if (active) {
          setError(loadError instanceof ApiError ? loadError.message : t("settings.loadError"));
        }
      } finally {
        if (active) {
          setLoading(false);
        }
      }
    }

    void load();

    return () => {
      active = false;
    };
  }, [t]);

  const canShowStock = form ? form.enableInventory || form.enableWarehouses : false;

  const tabLabels = useMemo<Record<SettingsTab, string>>(() => ({
    "company-profile": "settings.tabs.companyProfile",
    features: "settings.tabs.features",
    workflow: "settings.tabs.workflow",
    stock: "settings.tabs.stock",
    users: "settings.tabs.users",
    roles: "settings.tabs.roles",
    invitations: "settings.tabs.invitations",
    security: "settings.tabs.security",
    sessions: "settings.tabs.sessions",
    "audit-log": "settings.tabs.auditLog",
    localization: "settings.tabs.localization",
    "document-numbering": "settings.tabs.documentNumbering",
  }), []);

  async function handleSave() {
    if (!form) {
      return;
    }

    try {
      setSaving(true);
      const response = await saveOrganizationSetup(form);
      setForm(toRequestModel(response));
      setWarehouses(response.warehouses);
      setError("");
      showToast({
        tone: "success",
        title: "settings.organization.savedTitle",
        description: "settings.organization.savedDescription",
      });
    } catch (saveError) {
      setError(saveError instanceof ApiError ? saveError.message : t("settings.saveError"));
    } finally {
      setSaving(false);
    }
  }

  async function handleSecuritySave() {
    if (!security) {
      return;
    }

    try {
      setSaving(true);
      const response = await updateSecuritySettings(security);
      setSecurity(response);
      setError("");
      showToast({
        tone: "success",
        title: "settings.security.savedTitle",
        description: "settings.security.savedDescription",
      });
    } catch (saveError) {
      setError(saveError instanceof ApiError ? saveError.message : t("settings.saveError"));
    } finally {
      setSaving(false);
    }
  }

  return (
    <section className="hc-settings-page">
      <PageHeader
        eyebrow="settings.eyebrow"
        title="settings.organizationSettingsTitle"
        description="settings.organizationSettingsDescription"
      />

      {loading ? (
        <Card padding="lg">
          <div className="hc-skeleton-stack">
            <SkeletonLoader variant="rect" height="4rem" />
            <SkeletonLoader variant="rect" height="18rem" />
          </div>
        </Card>
      ) : error && !form ? (
        <Card padding="lg">
          <EmptyState title="settings.errorTitle" description={error} />
        </Card>
      ) : form ? (
        <div className="hc-settings-layout hc-settings-layout--single">
          <Card className="hc-settings-tabs-card" padding="md">
            <div className="hc-settings-tabs">
              {tabs.map((tab) => (
                <button
                  key={tab}
                  className={`hc-settings-tabs__tab ${activeTab === tab ? "hc-settings-tabs__tab--active" : ""}`}
                  type="button"
                  onClick={() => setSearchParams({ tab })}
                >
                  {t(tabLabels[tab])}
                </button>
              ))}
            </div>
          </Card>

          <Card padding="lg">
            {error ? <p className="auth-card__error">{error}</p> : null}
            {(activeTab === "company-profile" || activeTab === "localization" || activeTab === "document-numbering") ? (
              <div className="hc-settings-form__grid">
                {(activeTab === "company-profile" || activeTab === "localization") ? (
                  <>
                    <Field label="auth.organizationName"><Input value={form.name} onChange={(event) => setForm((current) => current ? { ...current, name: event.target.value } : current)} /></Field>
                    <Field label="setup.fields.logoPlaceholder"><Input value={form.logo ?? ""} onChange={(event) => setForm((current) => current ? { ...current, logo: event.target.value || null } : current)} /></Field>
                    <Field label="setup.fields.address"><Input value={form.address ?? ""} onChange={(event) => setForm((current) => current ? { ...current, address: event.target.value || null } : current)} /></Field>
                    <Field label="setup.fields.phone"><Input value={form.phone ?? ""} onChange={(event) => setForm((current) => current ? { ...current, phone: event.target.value || null } : current)} /></Field>
                    <Field label="setup.fields.taxId"><Input value={form.taxId ?? ""} onChange={(event) => setForm((current) => current ? { ...current, taxId: event.target.value || null } : current)} /></Field>
                    <Field label="setup.fields.commercialRegistry"><Input value={form.commercialRegistry ?? ""} onChange={(event) => setForm((current) => current ? { ...current, commercialRegistry: event.target.value || null } : current)} /></Field>
                    <Field label="setup.fields.currency"><Input value={form.defaultCurrency} onChange={(event) => setForm((current) => current ? { ...current, defaultCurrency: event.target.value } : current)} /></Field>
                    <Field label="setup.fields.timezone"><Input value={form.timezone} onChange={(event) => setForm((current) => current ? { ...current, timezone: event.target.value } : current)} /></Field>
                    <Field label="setup.fields.defaultLanguage">
                      <Select value={form.defaultLanguage} onChange={(event) => setForm((current) => current ? { ...current, defaultLanguage: event.target.value } : current)}>
                        <option value="en">setup.languages.en</option>
                        <option value="ar">setup.languages.ar</option>
                      </Select>
                    </Field>
                    <div className="hc-settings-form__checks">
                      <Checkbox checked={form.rtlEnabled} label="setup.fields.rtlEnabled" onChange={(event) => setForm((current) => current ? { ...current, rtlEnabled: event.target.checked } : current)} />
                    </div>
                  </>
                ) : null}
                {activeTab === "document-numbering" ? (
                  <>
                    <Field label="setup.fields.purchaseOrderPrefix"><Input value={form.purchaseOrderPrefix} onChange={(event) => setForm((current) => current ? { ...current, purchaseOrderPrefix: event.target.value } : current)} /></Field>
                    <Field label="setup.fields.purchaseReceiptPrefix"><Input value={form.purchaseReceiptPrefix} onChange={(event) => setForm((current) => current ? { ...current, purchaseReceiptPrefix: event.target.value } : current)} /></Field>
                    <Field label="setup.fields.purchaseReturnPrefix"><Input value={form.purchaseReturnPrefix} onChange={(event) => setForm((current) => current ? { ...current, purchaseReturnPrefix: event.target.value } : current)} /></Field>
                    <Field label="setup.fields.paymentPrefix"><Input value={form.paymentPrefix} onChange={(event) => setForm((current) => current ? { ...current, paymentPrefix: event.target.value } : current)} /></Field>
                  </>
                ) : null}
              </div>
            ) : null}

            {activeTab === "features" ? (
              <div className="hc-settings-form__checks">
                <Checkbox checked={form.enableProcurement} label="setup.features.procurement" onChange={(event) => setForm((current) => current ? { ...current, enableProcurement: event.target.checked } : current)} />
                <Checkbox checked={form.enablePurchaseOrders} label="setup.features.purchaseOrders" onChange={(event) => setForm((current) => current ? { ...current, enablePurchaseOrders: event.target.checked } : current)} />
                <Checkbox checked={form.enablePurchaseReceipts} label="setup.features.purchaseReceipts" onChange={(event) => setForm((current) => current ? { ...current, enablePurchaseReceipts: event.target.checked } : current)} />
                <Checkbox checked={form.enableInventory} label="setup.features.inventory" onChange={(event) => setForm((current) => current ? { ...current, enableInventory: event.target.checked } : current)} />
                <Checkbox checked={form.enableWarehouses} label="setup.features.warehouses" onChange={(event) => setForm((current) => current ? { ...current, enableWarehouses: event.target.checked } : current)} />
                <Checkbox checked={form.enableMultipleWarehouses} label="setup.features.multipleWarehouses" onChange={(event) => setForm((current) => current ? { ...current, enableMultipleWarehouses: event.target.checked } : current)} />
                <Checkbox checked={form.enableSupplierManagement} label="setup.features.supplierManagement" onChange={(event) => setForm((current) => current ? { ...current, enableSupplierManagement: event.target.checked } : current)} />
                <Checkbox checked={form.enableSupplierFinancials} label="setup.features.supplierFinancials" onChange={(event) => setForm((current) => current ? { ...current, enableSupplierFinancials: event.target.checked } : current)} />
                <Checkbox checked={form.enableShortageManagement} label="setup.features.shortageManagement" onChange={(event) => setForm((current) => current ? { ...current, enableShortageManagement: event.target.checked } : current)} />
                <Checkbox checked={form.enableComponentsBom} label="setup.features.componentsBom" onChange={(event) => setForm((current) => current ? { ...current, enableComponentsBom: event.target.checked } : current)} />
                <Checkbox checked={form.enableUom} label="setup.features.uom" onChange={(event) => setForm((current) => current ? { ...current, enableUom: event.target.checked } : current)} />
                <Checkbox checked={form.enableUomConversion} label="setup.features.uomConversion" onChange={(event) => setForm((current) => current ? { ...current, enableUomConversion: event.target.checked } : current)} />
              </div>
            ) : null}

            {activeTab === "workflow" ? (
              <div className="hc-settings-form__checks">
                <Checkbox checked={form.requirePoBeforeReceipt} label="setup.workflow.requirePoBeforeReceipt" onChange={(event) => setForm((current) => current ? { ...current, requirePoBeforeReceipt: event.target.checked } : current)} />
                <Checkbox checked={form.allowDirectPurchaseReceipt} label="setup.workflow.allowDirectPurchaseReceipt" onChange={(event) => setForm((current) => current ? { ...current, allowDirectPurchaseReceipt: event.target.checked } : current)} />
                <Checkbox checked={form.allowPartialReceipt} label="setup.workflow.allowPartialReceipt" onChange={(event) => setForm((current) => current ? { ...current, allowPartialReceipt: event.target.checked } : current)} />
                <Checkbox checked={form.allowOverReceipt} label="setup.workflow.allowOverReceipt" onChange={(event) => setForm((current) => current ? { ...current, allowOverReceipt: event.target.checked } : current)} />
                <Field label="setup.workflow.overReceiptTolerancePercent"><Input min={0} type="number" value={form.overReceiptTolerancePercent} onChange={(event) => setForm((current) => current ? { ...current, overReceiptTolerancePercent: Number(event.target.value || 0) } : current)} /></Field>
                <Checkbox checked={form.enablePostingWorkflow} label="setup.workflow.enablePostingWorkflow" onChange={(event) => setForm((current) => current ? { ...current, enablePostingWorkflow: event.target.checked } : current)} />
                <Checkbox checked={form.lockPostedDocuments} label="setup.workflow.lockPostedDocuments" onChange={(event) => setForm((current) => current ? { ...current, lockPostedDocuments: event.target.checked } : current)} />
                <Checkbox checked={form.requireApprovalBeforePosting} label="setup.workflow.requireApprovalBeforePosting" onChange={(event) => setForm((current) => current ? { ...current, requireApprovalBeforePosting: event.target.checked } : current)} />
                <Checkbox checked={form.enableReversals} label="setup.workflow.enableReversals" onChange={(event) => setForm((current) => current ? { ...current, enableReversals: event.target.checked } : current)} />
                <Checkbox checked={form.requireReasonForCancelOrReversal} label="setup.workflow.requireReasonForCancelOrReversal" onChange={(event) => setForm((current) => current ? { ...current, requireReasonForCancelOrReversal: event.target.checked } : current)} />
              </div>
            ) : null}

            {activeTab === "stock" ? (
              canShowStock ? (
                <>
                  <div className="hc-settings-form__grid">
                    <Field label="setup.stock.defaultWarehouse">
                      <Select value={form.defaultWarehouseId ?? ""} onChange={(event) => setForm((current) => current ? { ...current, defaultWarehouseId: event.target.value || null } : current)}>
                        <option value="">setup.stock.selectWarehouse</option>
                        {warehouses.filter((warehouse) => warehouse.isActive).map((warehouse) => (
                          <option key={warehouse.id} value={warehouse.id}>{`${warehouse.code} - ${warehouse.name}`}</option>
                        ))}
                      </Select>
                    </Field>
                    <Field label="setup.stock.defaultWarehouseName">
                      <Input value={form.defaultWarehouseName ?? ""} onChange={(event) => setForm((current) => current ? { ...current, defaultWarehouseName: event.target.value || null } : current)} />
                    </Field>
                  </div>
                  <div className="hc-settings-form__checks">
                    <Checkbox checked={form.allowNegativeStock} label="setup.stock.allowNegativeStock" onChange={(event) => setForm((current) => current ? { ...current, allowNegativeStock: event.target.checked } : current)} />
                    <Checkbox checked={form.enableBatchTracking} label="setup.stock.enableBatchTracking" onChange={(event) => setForm((current) => current ? { ...current, enableBatchTracking: event.target.checked } : current)} />
                    <Checkbox checked={form.enableSerialTracking} label="setup.stock.enableSerialTracking" onChange={(event) => setForm((current) => current ? { ...current, enableSerialTracking: event.target.checked } : current)} />
                    <Checkbox checked={form.enableExpiryTracking} label="setup.stock.enableExpiryTracking" onChange={(event) => setForm((current) => current ? { ...current, enableExpiryTracking: event.target.checked } : current)} />
                    <Checkbox checked={form.enableStockTransfers} label="setup.stock.enableStockTransfers" onChange={(event) => setForm((current) => current ? { ...current, enableStockTransfers: event.target.checked } : current)} />
                    <Checkbox checked={form.enableStockAdjustments} label="setup.stock.enableStockAdjustments" onChange={(event) => setForm((current) => current ? { ...current, enableStockAdjustments: event.target.checked } : current)} />
                  </div>
                </>
              ) : (
                <EmptyState title="setup.stock.emptyTitle" description="setup.stock.emptyDescription" />
              )
            ) : null}

            {activeTab === "users" ? <UsersTab users={users} formatDate={formatDate} /> : null}
            {activeTab === "roles" ? <RolesTab roles={roles} /> : null}
            {activeTab === "invitations" ? <InvitationsTab invitations={invitations} formatDate={formatDate} /> : null}
            {activeTab === "security" ? security ? <SecurityTab security={security} setSecurity={setSecurity} /> : <EmptyState title="settings.errorTitle" description="settings.loadError" /> : null}
            {activeTab === "sessions" ? <SessionsTab sessions={sessions} formatDate={formatDate} /> : null}
            {activeTab === "audit-log" ? <AuditTab auditLogs={auditLogs} formatDate={formatDate} /> : null}

            {["company-profile", "features", "workflow", "stock", "localization", "document-numbering"].includes(activeTab) ? (
              <div className="hc-settings-form__actions">
                <Button isLoading={saving} onClick={() => void handleSave()}>settings.save</Button>
              </div>
            ) : null}

            {activeTab === "security" ? (
              <div className="hc-settings-form__actions">
                <Button isLoading={saving} onClick={() => void handleSecuritySave()}>settings.save</Button>
              </div>
            ) : null}
          </Card>
        </div>
      ) : null}
    </section>
  );
}

function UsersTab({ formatDate, users }: { formatDate: ReturnType<typeof useI18n>["formatDate"]; users: UserListItem[] }) {
  return (
    <DataTable
      hasData={users.length > 0}
      columns={<tr><th scope="col">settings.users.columns.user</th><th scope="col">table.roles</th><th scope="col">common.status</th><th scope="col">settings.users.columns.lastLogin</th></tr>}
      rows={users.map((user) => (
        <tr key={user.membershipId}>
          <td><div className="hc-table__cell-strong"><span className="hc-table__title">{user.fullName}</span><span className="hc-table__subtitle">{user.email}</span></div></td>
          <td>{user.roles.map((role) => role.name).join(", ") || "-"}</td>
          <td><div className="hc-table__meta-list"><Badge tone={user.isOwner ? "primary" : "neutral"}>{user.isOwner ? "settings.users.ownerBadge" : user.membershipStatus}</Badge></div></td>
          <td>{user.lastLoginAt ? formatDate(user.lastLoginAt, { day: "numeric", month: "short", year: "numeric" }) : "settings.users.never"}</td>
        </tr>
      ))}
      emptyState={<EmptyState title="settings.users.emptyTitle" description="settings.users.emptyDescription" />}
    />
  );
}

function RolesTab({ roles }: { roles: AuthRole[] }) {
  return (
    <DataTable
      hasData={roles.length > 0}
      columns={<tr><th scope="col">settings.roles.columns.role</th><th scope="col">settings.roles.columns.template</th><th scope="col">settings.roles.columns.permissions</th><th scope="col">common.status</th></tr>}
      rows={roles.map((role) => (
        <tr key={role.id}>
          <td>{role.name}</td>
          <td>{role.templateKey || "settings.roles.customTemplate"}</td>
          <td>{role.permissions.length}</td>
          <td><Badge tone={role.isActive ? "success" : "warning"}>{role.isActive ? "status.active" : "status.inactive"}</Badge></td>
        </tr>
      ))}
      emptyState={<EmptyState title="settings.roles.emptyTitle" description="settings.roles.emptyDescription" />}
    />
  );
}

function InvitationsTab({ formatDate, invitations }: { formatDate: ReturnType<typeof useI18n>["formatDate"]; invitations: Invitation[] }) {
  return (
    <DataTable
      hasData={invitations.length > 0}
      columns={<tr><th scope="col">auth.email</th><th scope="col">common.status</th><th scope="col">settings.invitations.columns.expiresAt</th></tr>}
      rows={invitations.map((invitation) => (
        <tr key={invitation.id}>
          <td>{invitation.email}</td>
          <td><Badge tone="warning">{`settings.invitations.status.${invitation.status.toLowerCase()}`}</Badge></td>
          <td>{formatDate(invitation.expiresAt, { day: "numeric", month: "short", year: "numeric" })}</td>
        </tr>
      ))}
      emptyState={<EmptyState title="settings.invitations.emptyTitle" description="settings.invitations.emptyDescription" />}
    />
  );
}

function SessionsTab({ formatDate, sessions }: { formatDate: ReturnType<typeof useI18n>["formatDate"]; sessions: SessionItem[] }) {
  return (
    <DataTable
      hasData={sessions.length > 0}
      columns={<tr><th scope="col">settings.sessions.columns.device</th><th scope="col">settings.sessions.columns.browser</th><th scope="col">settings.sessions.columns.expiresAt</th></tr>}
      rows={sessions.map((session) => (
        <tr key={session.id}>
          <td>{session.deviceName || "settings.sessions.unknownDevice"}</td>
          <td>{session.browser || "settings.notSet"}</td>
          <td>{formatDate(session.expiresAt, { day: "numeric", month: "short", year: "numeric" })}</td>
        </tr>
      ))}
      emptyState={<EmptyState title="settings.sessions.emptyTitle" description="settings.sessions.emptyDescription" />}
    />
  );
}

function AuditTab({ auditLogs, formatDate }: { auditLogs: AuditLogItem[]; formatDate: ReturnType<typeof useI18n>["formatDate"] }) {
  return (
    <DataTable
      hasData={auditLogs.length > 0}
      columns={<tr><th scope="col">settings.audit.columns.action</th><th scope="col">settings.audit.columns.module</th><th scope="col">settings.audit.columns.createdAt</th></tr>}
      rows={auditLogs.map((entry) => (
        <tr key={entry.id}>
          <td>{entry.action}</td>
          <td>{entry.module}</td>
          <td>{formatDate(entry.createdAt, { day: "numeric", month: "short", year: "numeric" })}</td>
        </tr>
      ))}
      emptyState={<EmptyState title="settings.audit.emptyTitle" description="settings.audit.emptyDescription" />}
    />
  );
}

function SecurityTab({
  security,
  setSecurity,
}: {
  security: SecuritySettings;
  setSecurity: Dispatch<SetStateAction<SecuritySettings | null>>;
}) {
  return (
    <>
      <div className="hc-settings-form__grid">
        <Field label="settings.security.fields.minimumPasswordLength">
          <Input min={6} type="number" value={security.minimumPasswordLength} onChange={(event) => setSecurity((current) => current ? { ...current, minimumPasswordLength: Number(event.target.value || 8) } : current)} />
        </Field>
        <Field label="settings.security.fields.sessionTimeoutMinutes">
          <Input min={15} type="number" value={security.sessionTimeoutMinutes} onChange={(event) => setSecurity((current) => current ? { ...current, sessionTimeoutMinutes: Number(event.target.value || 480) } : current)} />
        </Field>
        <Field label="settings.security.fields.inviteExpiryDays">
          <Input min={1} type="number" value={security.inviteExpiryDays} onChange={(event) => setSecurity((current) => current ? { ...current, inviteExpiryDays: Number(event.target.value || 7) } : current)} />
        </Field>
        <Field label="settings.security.fields.loginAttemptLimit">
          <Input min={1} type="number" value={security.loginAttemptLimit} onChange={(event) => setSecurity((current) => current ? { ...current, loginAttemptLimit: Number(event.target.value || 5) } : current)} />
        </Field>
        <Field label="settings.security.fields.auditRetentionDays">
          <Input min={30} type="number" value={security.auditRetentionDays} onChange={(event) => setSecurity((current) => current ? { ...current, auditRetentionDays: Number(event.target.value || 365) } : current)} />
        </Field>
        <Field label="settings.security.fields.allowedEmailDomains">
          <Input value={security.allowedEmailDomains ?? ""} onChange={(event) => setSecurity((current) => current ? { ...current, allowedEmailDomains: event.target.value || null } : current)} />
        </Field>
      </div>
      <div className="hc-settings-form__checks">
        <Checkbox checked={security.requireUppercase} label="settings.security.fields.requireUppercase" onChange={(event) => setSecurity((current) => current ? { ...current, requireUppercase: event.target.checked } : current)} />
        <Checkbox checked={security.requireLowercase} label="settings.security.fields.requireLowercase" onChange={(event) => setSecurity((current) => current ? { ...current, requireLowercase: event.target.checked } : current)} />
        <Checkbox checked={security.requireNumber} label="settings.security.fields.requireNumber" onChange={(event) => setSecurity((current) => current ? { ...current, requireNumber: event.target.checked } : current)} />
        <Checkbox checked={security.requireSymbol} label="settings.security.fields.requireSymbol" onChange={(event) => setSecurity((current) => current ? { ...current, requireSymbol: event.target.checked } : current)} />
        <Checkbox checked={security.forceTwoFactor} label="settings.security.fields.forceTwoFactor" onChange={(event) => setSecurity((current) => current ? { ...current, forceTwoFactor: event.target.checked } : current)} />
        <Checkbox checked={security.enableEmailOtp} label="settings.security.fields.enableEmailOtp" onChange={(event) => setSecurity((current) => current ? { ...current, enableEmailOtp: event.target.checked } : current)} />
      </div>
    </>
  );
}
