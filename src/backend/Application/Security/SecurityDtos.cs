using ERP.Application.Common.Pagination;
using ERP.Domain.Identity;

namespace ERP.Application.Security;

public sealed record SignupRequest(
    string FullName,
    string Email,
    string Password,
    string OrganizationName);

public sealed record LoginRequest(
    string Email,
    string Password,
    bool RememberMe,
    string? DeviceName);

public sealed record SwitchOrganizationRequest(Guid OrganizationId, bool RememberMe);

public sealed record LogoutRequest(bool AllDevices);

public sealed record ForgotPasswordRequest(string Email);

public sealed record ResetPasswordRequest(string Token, string Password);

public sealed record RequestEmailVerificationRequest(string Email);

public sealed record ConfirmEmailVerificationRequest(string Token);

public sealed record InviteUserRequest(
    string Email,
    string? FullName,
    IReadOnlyList<Guid> RoleIds,
    Guid? ProfileId,
    AccessScopeMode BranchAccessMode,
    AccessScopeMode WarehouseAccessMode,
    IReadOnlyList<string> BranchCodes,
    IReadOnlyList<Guid> WarehouseIds);

public sealed record AcceptInvitationRequest(
    string Token,
    string FullName,
    string Password);

public sealed record UpdateMembershipRequest(
    MembershipStatus Status,
    IReadOnlyList<Guid> RoleIds,
    Guid? ProfileId,
    AccessScopeMode BranchAccessMode,
    AccessScopeMode WarehouseAccessMode,
    IReadOnlyList<string> BranchCodes,
    IReadOnlyList<Guid> WarehouseIds);

public sealed record TransferOwnershipRequest(Guid MembershipId);

public sealed record ChangeUserRolesRequest(IReadOnlyList<Guid> RoleIds);

public sealed record UpsertRoleRequest(
    string Name,
    bool IsActive,
    IReadOnlyList<string> Permissions);

public sealed record UserListQuery(
    string? Search,
    string? Status,
    Guid? RoleId,
    int Page,
    int PageSize,
    string? SortBy,
    SortDirection SortDirection);

public sealed record UpdateOrganizationRequest(
    string Name,
    string? Logo,
    string? Address,
    string? Phone,
    string? TaxId,
    string? CommercialRegistry,
    string DefaultCurrency,
    string Timezone,
    string DefaultLanguage,
    bool RtlEnabled,
    int FiscalYearStartMonth,
    string PurchaseOrderPrefix,
    string PurchaseReceiptPrefix,
    string PurchaseReturnPrefix,
    string PaymentPrefix,
    Guid? DefaultWarehouseId,
    bool AutoPostDrafts);

public sealed record UpdateSecuritySettingsRequest(
    int MinimumPasswordLength,
    bool RequireUppercase,
    bool RequireLowercase,
    bool RequireNumber,
    bool RequireSymbol,
    int SessionTimeoutMinutes,
    bool ForceTwoFactor,
    int InviteExpiryDays,
    string? AllowedEmailDomains,
    int LoginAttemptLimit,
    int AuditRetentionDays,
    bool EnableEmailOtp);

public sealed record UpsertProfileRequest(
    string? JobTitle,
    string? Department,
    string? Phone,
    string? DefaultBranchCode,
    Guid? DefaultWarehouseId,
    string LanguagePreference,
    string? DashboardPreference,
    string? SignaturePlaceholder,
    string? Avatar);

public sealed record AuditLogQuery(
    Guid? UserId,
    string? Module,
    string? Action,
    DateTime? FromDate,
    DateTime? ToDate,
    int Page,
    int PageSize);

public sealed record AuthResponse(
    string AccessToken,
    DateTime ExpiresAt,
    CurrentWorkspaceDto Workspace,
    string? EmailVerificationToken,
    string? PasswordResetToken);

public sealed record CurrentWorkspaceDto(
    Guid UserId,
    string FullName,
    string Email,
    bool EmailVerified,
    Guid OrganizationId,
    string OrganizationName,
    Guid MembershipId,
    bool RequiresTwoFactor,
    bool SetupCompleted,
    string? SetupStep,
    string? SetupVersion,
    IReadOnlyList<string> Permissions,
    IReadOnlyList<OrganizationOptionDto> Organizations,
    IReadOnlyList<RoleDto> Roles);

public sealed record OrganizationOptionDto(Guid OrganizationId, string Name, bool IsOwner);

public sealed record UserListItemDto(
    Guid MembershipId,
    Guid UserId,
    string FullName,
    string Email,
    bool EmailVerified,
    UserAccountStatus UserStatus,
    MembershipStatus MembershipStatus,
    bool IsOwner,
    IReadOnlyList<RoleDto> Roles,
    Guid? ProfileId,
    DateTime CreatedAt,
    DateTime? LastLoginAt);

public sealed record UserAccessListItemDto(
    Guid? MembershipId,
    Guid? InvitationId,
    Guid? UserId,
    string? FullName,
    string Email,
    bool EmailVerified,
    UserAccountStatus? UserStatus,
    MembershipStatus? MembershipStatus,
    InvitationStatus? InvitationStatus,
    string AccessStatus,
    bool IsOwner,
    IReadOnlyList<RoleDto> Roles,
    Guid? ProfileId,
    DateTime CreatedAt,
    DateTime? LastLoginAt);

public sealed record RoleDto(
    Guid Id,
    string Name,
    bool IsSystemRole,
    bool IsProtected,
    bool IsActive,
    string? TemplateKey,
    IReadOnlyList<string> Permissions);

public sealed record PermissionMatrixDto(
    IReadOnlyList<PermissionMatrixActionDto> Actions,
    IReadOnlyList<PermissionMatrixRowDto> Rows);

public sealed record PermissionMatrixActionDto(string Key, string LabelKey);

public sealed record PermissionMatrixRowDto(
    string Key,
    string LabelKey,
    IReadOnlyDictionary<string, IReadOnlyList<string>> Permissions);

public sealed record InvitationDto(
    Guid Id,
    string Email,
    string? FullName,
    InvitationStatus Status,
    DateTime ExpiresAt,
    DateTime CreatedAt,
    IReadOnlyList<Guid> RoleIds);

public sealed record SessionDto(
    Guid Id,
    DateTime CreatedAt,
    DateTime ExpiresAt,
    bool IsActive,
    bool RememberMe,
    string? DeviceName,
    string? Browser,
    string? IpAddress);

public sealed record AuditLogDto(
    Guid Id,
    Guid? UserId,
    string Action,
    string Module,
    string ResourceType,
    string? ResourceId,
    string? BeforeData,
    string? AfterData,
    string? IpAddress,
    string? UserAgent,
    DateTime CreatedAt);

public sealed record ProfileDto(
    Guid Id,
    string? JobTitle,
    string? Department,
    string? Phone,
    string? DefaultBranchCode,
    Guid? DefaultWarehouseId,
    string LanguagePreference,
    string? DashboardPreference,
    string? SignaturePlaceholder,
    string? Avatar);

public sealed record OrganizationSettingsDto(
    Guid Id,
    string Name,
    string? Logo,
    string? Address,
    string? Phone,
    string? TaxId,
    string? CommercialRegistry,
    string DefaultCurrency,
    string Timezone,
    string DefaultLanguage,
    bool RtlEnabled,
    int FiscalYearStartMonth,
    string PurchaseOrderPrefix,
    string PurchaseReceiptPrefix,
    string PurchaseReturnPrefix,
    string PaymentPrefix,
    Guid? DefaultWarehouseId,
    bool AutoPostDrafts);

public sealed record SecuritySettingsDto(
    int MinimumPasswordLength,
    bool RequireUppercase,
    bool RequireLowercase,
    bool RequireNumber,
    bool RequireSymbol,
    int SessionTimeoutMinutes,
    bool ForceTwoFactor,
    int InviteExpiryDays,
    string? AllowedEmailDomains,
    int LoginAttemptLimit,
    int AuditRetentionDays,
    bool EnableEmailOtp);

public sealed record FeatureConfigurationDto(
    bool WorkspaceEnabled,
    bool ProcurementEnabled,
    bool InventoryEnabled,
    bool SuppliersEnabled,
    bool SupplierFinancialsEnabled,
    bool SettingsEnabled,
    bool OfflineDraftsOnly,
    bool EmailOtpEnabled,
    bool AutoPostDrafts,
    IReadOnlyList<string> EnabledModules,
    IReadOnlyList<string> DisabledModules);

public sealed record OrganizationSetupStatusDto(
    bool SetupCompleted,
    DateTime? SetupCompletedAt,
    string? SetupCompletedBy,
    string? SetupStep,
    string? SetupVersion);

public sealed record OrganizationFeatureSettingsDto(
    bool EnableProcurement,
    bool EnablePurchaseOrders,
    bool EnablePurchaseReceipts,
    bool EnableInventory,
    bool EnableWarehouses,
    bool EnableMultipleWarehouses,
    bool EnableSupplierManagement,
    bool EnableSupplierFinancials,
    bool EnableShortageManagement,
    bool EnableComponentsBom,
    bool EnableUom,
    bool EnableUomConversion);

public sealed record OrganizationWorkflowSettingsDto(
    bool RequirePoBeforeReceipt,
    bool AllowDirectPurchaseReceipt,
    bool AllowPartialReceipt,
    bool AllowOverReceipt,
    decimal OverReceiptTolerancePercent,
    bool EnablePostingWorkflow,
    bool LockPostedDocuments,
    bool RequireApprovalBeforePosting,
    bool EnableReversals,
    bool RequireReasonForCancelOrReversal);

public sealed record OrganizationStockSettingsDto(
    Guid? DefaultWarehouseId,
    bool AllowNegativeStock,
    bool EnableBatchTracking,
    bool EnableSerialTracking,
    bool EnableExpiryTracking,
    bool EnableStockTransfers,
    bool EnableStockAdjustments);

public sealed record WarehouseLookupDto(Guid Id, string Code, string Name, bool IsActive);

public sealed record OrganizationSetupDto(
    OrganizationSettingsDto Organization,
    OrganizationFeatureSettingsDto Features,
    OrganizationWorkflowSettingsDto Workflow,
    OrganizationStockSettingsDto Stock,
    SecuritySettingsDto Security,
    OrganizationSetupStatusDto Status,
    IReadOnlyList<WarehouseLookupDto> Warehouses);

public sealed record SaveOrganizationSetupRequest(
    string Name,
    string? Logo,
    string? Address,
    string? Phone,
    string? TaxId,
    string? CommercialRegistry,
    string DefaultCurrency,
    string Timezone,
    string DefaultLanguage,
    bool RtlEnabled,
    int FiscalYearStartMonth,
    string PurchaseOrderPrefix,
    string PurchaseReceiptPrefix,
    string PurchaseReturnPrefix,
    string PaymentPrefix,
    bool EnableProcurement,
    bool EnablePurchaseOrders,
    bool EnablePurchaseReceipts,
    bool EnableInventory,
    bool EnableWarehouses,
    bool EnableMultipleWarehouses,
    bool EnableSupplierManagement,
    bool EnableSupplierFinancials,
    bool EnableShortageManagement,
    bool EnableComponentsBom,
    bool EnableUom,
    bool EnableUomConversion,
    bool RequirePoBeforeReceipt,
    bool AllowDirectPurchaseReceipt,
    bool AllowPartialReceipt,
    bool AllowOverReceipt,
    decimal OverReceiptTolerancePercent,
    bool EnablePostingWorkflow,
    bool LockPostedDocuments,
    bool RequireApprovalBeforePosting,
    bool EnableReversals,
    bool RequireReasonForCancelOrReversal,
    Guid? DefaultWarehouseId,
    string? DefaultWarehouseName,
    bool AllowNegativeStock,
    bool EnableBatchTracking,
    bool EnableSerialTracking,
    bool EnableExpiryTracking,
    bool EnableStockTransfers,
    bool EnableStockAdjustments,
    bool AutoPostDrafts,
    string? SetupStep,
    string? SetupVersion);
