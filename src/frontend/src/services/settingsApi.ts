import { requestJson, type PaginatedResult, type PaginationParams } from "./api";

export interface OrganizationSettings {
  id: string;
  name: string;
  logo: string | null;
  address: string | null;
  phone: string | null;
  taxId: string | null;
  commercialRegistry: string | null;
  defaultCurrency: string;
  timezone: string;
  defaultLanguage: string;
  rtlEnabled: boolean;
  fiscalYearStartMonth: number;
  purchaseOrderPrefix: string;
  purchaseReceiptPrefix: string;
  purchaseReturnPrefix: string;
  paymentPrefix: string;
  defaultWarehouseId: string | null;
  autoPostDrafts: boolean;
}

export interface SecuritySettings {
  minimumPasswordLength: number;
  requireUppercase: boolean;
  requireLowercase: boolean;
  requireNumber: boolean;
  requireSymbol: boolean;
  sessionTimeoutMinutes: number;
  forceTwoFactor: boolean;
  inviteExpiryDays: number;
  allowedEmailDomains: string | null;
  loginAttemptLimit: number;
  auditRetentionDays: number;
  enableEmailOtp: boolean;
}

export interface AuthRole {
  id: string;
  name: string;
  isSystemRole: boolean;
  isProtected: boolean;
  isActive: boolean;
  templateKey: string | null;
  permissions: string[];
}

export interface UserListItem {
  membershipId: string | null;
  invitationId: string | null;
  userId: string | null;
  fullName: string | null;
  email: string;
  emailVerified: boolean;
  userStatus: string | null;
  membershipStatus: string | null;
  invitationStatus: string | null;
  accessStatus: "Active" | "Invited" | "Suspended" | "Disabled";
  isOwner: boolean;
  roles: AuthRole[];
  profileId: string | null;
  createdAt: string;
  lastLoginAt: string | null;
}

export interface Invitation {
  id: string;
  email: string;
  fullName: string | null;
  status: string;
  expiresAt: string;
  createdAt: string;
  roleIds: string[];
}

export interface PermissionMatrixAction {
  key: PermissionActionKey;
  labelKey: string;
}

export type PermissionActionKey = "view" | "create" | "edit" | "delete" | "post" | "cancelReverse" | "manage";

export interface PermissionMatrixRow {
  key: string;
  labelKey: string;
  permissions: Partial<Record<PermissionActionKey, string[]>>;
}

export interface PermissionMatrix {
  actions: PermissionMatrixAction[];
  rows: PermissionMatrixRow[];
}

export interface SessionItem {
  id: string;
  createdAt: string;
  expiresAt: string;
  isActive: boolean;
  rememberMe: boolean;
  deviceName: string | null;
  browser: string | null;
  ipAddress: string | null;
}

export interface AuditLogItem {
  id: string;
  userId: string | null;
  action: string;
  module: string;
  resourceType: string;
  resourceId: string | null;
  beforeData: string | null;
  afterData: string | null;
  ipAddress: string | null;
  userAgent: string | null;
  createdAt: string;
}

export interface Profile {
  id: string;
  jobTitle: string | null;
  department: string | null;
  phone: string | null;
  defaultBranchCode: string | null;
  defaultWarehouseId: string | null;
  languagePreference: string;
  dashboardPreference: string | null;
  signaturePlaceholder: string | null;
  avatar: string | null;
}

export interface FeatureConfiguration {
  workspaceEnabled: boolean;
  procurementEnabled: boolean;
  inventoryEnabled: boolean;
  suppliersEnabled: boolean;
  supplierFinancialsEnabled: boolean;
  settingsEnabled: boolean;
  offlineDraftsOnly: boolean;
  emailOtpEnabled: boolean;
  autoPostDrafts: boolean;
  enabledModules: string[];
  disabledModules: string[];
}

export interface OrganizationSetupStatus {
  setupCompleted: boolean;
  setupCompletedAt: string | null;
  setupCompletedBy: string | null;
  setupStep: string | null;
  setupVersion: string | null;
}

export interface WarehouseLookup {
  id: string;
  code: string;
  name: string;
  isActive: boolean;
}

export interface OrganizationFeatureSettings {
  enableProcurement: boolean;
  enablePurchaseOrders: boolean;
  enablePurchaseReceipts: boolean;
  enableInventory: boolean;
  enableWarehouses: boolean;
  enableMultipleWarehouses: boolean;
  enableSupplierManagement: boolean;
  enableSupplierFinancials: boolean;
  enableShortageManagement: boolean;
  enableComponentsBom: boolean;
  enableUom: boolean;
  enableUomConversion: boolean;
}

export interface OrganizationWorkflowSettings {
  requirePoBeforeReceipt: boolean;
  allowDirectPurchaseReceipt: boolean;
  allowPartialReceipt: boolean;
  allowOverReceipt: boolean;
  overReceiptTolerancePercent: number;
  enablePostingWorkflow: boolean;
  lockPostedDocuments: boolean;
  requireApprovalBeforePosting: boolean;
  enableReversals: boolean;
  requireReasonForCancelOrReversal: boolean;
}

export interface OrganizationStockSettings {
  defaultWarehouseId: string | null;
  allowNegativeStock: boolean;
  enableBatchTracking: boolean;
  enableSerialTracking: boolean;
  enableExpiryTracking: boolean;
  enableStockTransfers: boolean;
  enableStockAdjustments: boolean;
}

export interface OrganizationSetup {
  organization: OrganizationSettings;
  features: OrganizationFeatureSettings;
  workflow: OrganizationWorkflowSettings;
  stock: OrganizationStockSettings;
  security: SecuritySettings;
  status: OrganizationSetupStatus;
  warehouses: WarehouseLookup[];
}

export interface SaveOrganizationSetupRequest extends OrganizationSettings, OrganizationFeatureSettings, OrganizationWorkflowSettings, OrganizationStockSettings {
  defaultWarehouseName?: string | null;
  setupStep?: string | null;
  setupVersion?: string | null;
}

export function getOrganizationSettings() {
  return requestJson<OrganizationSettings>("/api/settings/organization");
}

export function updateOrganizationSettings(input: OrganizationSettings) {
  return requestJson<OrganizationSettings>("/api/settings/organization", {
    method: "PUT",
    body: JSON.stringify(input),
  });
}

export function getSecuritySettings() {
  return requestJson<SecuritySettings>("/api/settings/security");
}

export function updateSecuritySettings(input: SecuritySettings) {
  return requestJson<SecuritySettings>("/api/settings/security", {
    method: "PUT",
    body: JSON.stringify(input),
  });
}

export interface UserListRequest extends PaginationParams {
  search: string;
  status: string;
  roleId: string;
}

function buildUsersUrl(request: UserListRequest) {
  const url = new URL("/api/settings/users", window.location.origin);

  if (request.search.trim()) {
    url.searchParams.set("search", request.search.trim());
  }

  if (request.status) {
    url.searchParams.set("status", request.status);
  }

  if (request.roleId) {
    url.searchParams.set("roleId", request.roleId);
  }

  url.searchParams.set("page", String(request.page));
  url.searchParams.set("pageSize", String(request.pageSize));
  url.searchParams.set("sortBy", request.sortBy ?? "name");
  url.searchParams.set("sortDirection", request.sortDirection ?? "Asc");

  return `${url.pathname}${url.search}`;
}

export function listUsers(request: UserListRequest = {
  search: "",
  status: "",
  roleId: "",
  page: 1,
  pageSize: 20,
  sortBy: "name",
  sortDirection: "Asc",
}) {
  return requestJson<PaginatedResult<UserListItem>>(buildUsersUrl(request));
}

export function listRoles() {
  return requestJson<AuthRole[]>("/api/settings/roles");
}

export function getPermissionMatrix() {
  return requestJson<PermissionMatrix>("/api/settings/permissions/matrix");
}

export function inviteUser(input: { email: string; fullName?: string | null; roleIds: string[] }) {
  return requestJson<Invitation>("/api/settings/invitations", {
    method: "POST",
    body: JSON.stringify({
      email: input.email,
      fullName: input.fullName || null,
      roleIds: input.roleIds,
      profileId: null,
      branchAccessMode: "All",
      warehouseAccessMode: "All",
      branchCodes: [],
      warehouseIds: [],
    }),
  });
}

export function suspendUser(membershipId: string) {
  return requestJson<UserListItem>(`/api/settings/users/${membershipId}/suspend`, {
    method: "POST",
  });
}

export function activateUser(membershipId: string) {
  return requestJson<UserListItem>(`/api/settings/users/${membershipId}/activate`, {
    method: "POST",
  });
}

export function changeUserRoles(membershipId: string, roleIds: string[]) {
  return requestJson<UserListItem>(`/api/settings/users/${membershipId}/roles`, {
    method: "PUT",
    body: JSON.stringify({ roleIds }),
  });
}

export function updateRolePermissions(role: AuthRole, permissions: string[]) {
  return requestJson<AuthRole>(`/api/settings/roles/${role.id}`, {
    method: "PUT",
    body: JSON.stringify({
      name: role.name,
      isActive: role.isActive,
      permissions,
    }),
  });
}

export function listInvitations() {
  return requestJson<Invitation[]>("/api/settings/invitations");
}

export function listSessions() {
  return requestJson<SessionItem[]>("/api/settings/sessions");
}

export function listAuditLog(params?: {
  page?: number;
  pageSize?: number;
  userId?: string;
  module?: string;
  action?: string;
  fromDate?: string;
  toDate?: string;
}) {
  const search = new URLSearchParams();
  search.set("page", String(params?.page ?? 1));
  search.set("pageSize", String(params?.pageSize ?? 20));

  if (params?.userId) {
    search.set("userId", params.userId);
  }

  if (params?.module) {
    search.set("module", params.module);
  }

  if (params?.action) {
    search.set("action", params.action);
  }

  if (params?.fromDate) {
    search.set("fromDate", params.fromDate);
  }

  if (params?.toDate) {
    search.set("toDate", params.toDate);
  }

  return requestJson<AuditLogItem[]>(`/api/settings/audit-log?${search.toString()}`);
}

export function listProfiles() {
  return requestJson<Profile[]>("/api/settings/profiles");
}

export function getFeatureConfiguration() {
  return requestJson<FeatureConfiguration>("/api/settings/features");
}

export function getOrganizationSetupStatus() {
  return requestJson<OrganizationSetupStatus>("/api/settings/organization/setup-status");
}

export function getOrganizationSetup() {
  return requestJson<OrganizationSetup>("/api/settings/organization/setup");
}

export function saveOrganizationSetup(input: SaveOrganizationSetupRequest) {
  return requestJson<OrganizationSetup>("/api/settings/organization/setup", {
    method: "PUT",
    body: JSON.stringify(input),
  });
}

export function completeOrganizationSetup() {
  return requestJson<OrganizationSetupStatus>("/api/settings/organization/setup/complete", {
    method: "POST",
  });
}
