using ERP.Application.Security;
using ERP.Application.Common.Pagination;
using ERP.Domain.Identity;
using ERP.Domain.MasterData;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Security;

public sealed class OrganizationAdministrationService(
    AppDbContext dbContext,
    IRequestExecutionContext executionContext,
    IAuthorizationService authorizationService,
    IAuditLogService auditLogService) : IOrganizationAdministrationService
{
    private readonly AppDbContext _dbContext = dbContext;
    private readonly IRequestExecutionContext _executionContext = executionContext;
    private readonly IAuthorizationService _authorizationService = authorizationService;
    private readonly IAuditLogService _auditLogService = auditLogService;

    public async Task<OrganizationSettingsDto> GetOrganizationAsync(CancellationToken cancellationToken)
    {
        await _authorizationService.EnsurePermissionAsync(Permissions.SettingsOrganizationManage, cancellationToken);
        var organization = await LoadOrganizationAsync(cancellationToken);
        return ToOrganizationDto(organization);
    }

    public async Task<OrganizationSetupStatusDto> GetSetupStatusAsync(CancellationToken cancellationToken)
    {
        await _authorizationService.EnsureOrganizationAccessAsync(cancellationToken);
        var organization = await LoadOrganizationAsync(cancellationToken);
        return ToSetupStatusDto(organization);
    }

    public async Task<OrganizationSetupDto> GetSetupAsync(CancellationToken cancellationToken)
    {
        await _authorizationService.EnsureOrganizationAccessAsync(cancellationToken);
        var organization = await LoadOrganizationAsync(cancellationToken);
        var security = await LoadSecuritySettingsAsync(cancellationToken);
        var warehouses = await LoadWarehouseLookupsAsync(cancellationToken);

        return ToSetupDto(organization, security, warehouses);
    }

    public async Task<OrganizationSetupDto> SaveSetupAsync(SaveOrganizationSetupRequest request, CancellationToken cancellationToken)
    {
        await _authorizationService.EnsureOrganizationAccessAsync(cancellationToken);

        var organization = await LoadOrganizationAsync(cancellationToken);
        var security = await LoadSecuritySettingsAsync(cancellationToken);
        var before = ToSetupDto(organization, security, await LoadWarehouseLookupsAsync(cancellationToken));
        var hadSetupStartedAudit = await _dbContext.AuditLogEntries
            .AsNoTracking()
            .AnyAsync(entity =>
                entity.OrganizationId == organization.Id &&
                entity.Action == "setup_started",
                cancellationToken);

        ValidateSetupDependencies(request);

        organization.Name = request.Name.Trim();
        organization.Logo = NormalizeOptional(request.Logo);
        organization.Address = NormalizeOptional(request.Address);
        organization.Phone = NormalizeOptional(request.Phone);
        organization.TaxId = NormalizeOptional(request.TaxId);
        organization.CommercialRegistry = NormalizeOptional(request.CommercialRegistry);
        organization.DefaultCurrency = request.DefaultCurrency.Trim();
        organization.Timezone = request.Timezone.Trim();
        organization.DefaultLanguage = request.DefaultLanguage.Trim();
        organization.RtlEnabled = request.RtlEnabled;
        organization.FiscalYearStartMonth = request.FiscalYearStartMonth;
        organization.PurchaseOrderPrefix = request.PurchaseOrderPrefix.Trim();
        organization.PurchaseReceiptPrefix = request.PurchaseReceiptPrefix.Trim();
        organization.PurchaseReturnPrefix = request.PurchaseReturnPrefix.Trim();
        organization.PaymentPrefix = request.PaymentPrefix.Trim();
        organization.EnableProcurement = request.EnableProcurement;
        organization.EnablePurchaseOrders = request.EnablePurchaseOrders;
        organization.EnablePurchaseReceipts = request.EnablePurchaseReceipts;
        organization.EnableInventory = request.EnableInventory;
        organization.EnableWarehouses = request.EnableWarehouses;
        organization.EnableMultipleWarehouses = request.EnableMultipleWarehouses;
        organization.EnableSupplierManagement = request.EnableSupplierManagement;
        organization.EnableSupplierFinancials = request.EnableSupplierFinancials;
        organization.EnableShortageManagement = request.EnableShortageManagement;
        organization.EnableComponentsBom = request.EnableComponentsBom;
        organization.EnableUom = request.EnableUom;
        organization.EnableUomConversion = request.EnableUomConversion;
        organization.RequirePoBeforeReceipt = request.RequirePoBeforeReceipt;
        organization.AllowDirectPurchaseReceipt = request.AllowDirectPurchaseReceipt;
        organization.AllowPartialReceipt = request.AllowPartialReceipt;
        organization.AllowOverReceipt = request.AllowOverReceipt;
        organization.OverReceiptTolerancePercent = request.AllowOverReceipt ? request.OverReceiptTolerancePercent : 0m;
        organization.EnablePostingWorkflow = request.EnablePostingWorkflow;
        organization.LockPostedDocuments = request.LockPostedDocuments;
        organization.RequireApprovalBeforePosting = request.RequireApprovalBeforePosting;
        organization.EnableReversals = request.EnableReversals;
        organization.RequireReasonForCancelOrReversal = request.RequireReasonForCancelOrReversal;
        organization.AllowNegativeStock = request.AllowNegativeStock;
        organization.EnableBatchTracking = request.EnableBatchTracking;
        organization.EnableSerialTracking = request.EnableSerialTracking;
        organization.EnableExpiryTracking = request.EnableExpiryTracking;
        organization.EnableStockTransfers = request.EnableStockTransfers;
        organization.EnableStockAdjustments = request.EnableStockAdjustments;
        organization.AutoPostDrafts = request.AutoPostDrafts;
        organization.SetupStep = NormalizeOptional(request.SetupStep);
        organization.SetupVersion = NormalizeOptional(request.SetupVersion) ?? "v1";
        organization.UpdatedBy = _executionContext.Actor;

        if (!organization.EnableWarehouses)
        {
            organization.DefaultWarehouseId = null;
        }
        else
        {
            organization.DefaultWarehouseId = await ResolveDefaultWarehouseIdAsync(organization, request.DefaultWarehouseId, request.DefaultWarehouseName, cancellationToken);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        var warehouses = await LoadWarehouseLookupsAsync(cancellationToken);
        var after = ToSetupDto(organization, security, warehouses);

        if (!hadSetupStartedAudit)
        {
            await _auditLogService.WriteAsync("setup_started", "settings", nameof(Organization), organization.Id.ToString(), null, new { organization.SetupStep, organization.SetupVersion }, null, null, cancellationToken);
        }

        await _auditLogService.WriteAsync("setup_saved", "settings", nameof(Organization), organization.Id.ToString(), before, after, null, null, cancellationToken);
        await WriteFeatureChangeAuditEntriesAsync(before.Features, after.Features, organization.Id, cancellationToken);
        await _auditLogService.WriteAsync("organization_setup_profile_updated", "settings", nameof(Organization), organization.Id.ToString(), before.Organization, after.Organization, null, null, cancellationToken);

        return after;
    }

    public async Task<OrganizationSetupStatusDto> CompleteSetupAsync(CancellationToken cancellationToken)
    {
        await _authorizationService.EnsureOrganizationAccessAsync(cancellationToken);
        var organization = await LoadOrganizationAsync(cancellationToken);
        ValidateSetupForCompletion(organization);

        organization.SetupCompleted = true;
        organization.SetupCompletedAt = DateTime.UtcNow;
        organization.SetupCompletedBy = _executionContext.Actor;
        organization.SetupStep = "completed";
        organization.SetupVersion ??= "v1";
        organization.UpdatedBy = _executionContext.Actor;

        await _dbContext.SaveChangesAsync(cancellationToken);
        await _auditLogService.WriteAsync("setup_completed", "settings", nameof(Organization), organization.Id.ToString(), null, ToSetupStatusDto(organization), null, null, cancellationToken);

        return ToSetupStatusDto(organization);
    }

    public async Task<OrganizationSettingsDto> UpdateOrganizationAsync(UpdateOrganizationRequest request, CancellationToken cancellationToken)
    {
        await _authorizationService.EnsurePermissionAsync(Permissions.SettingsOrganizationManage, cancellationToken);
        var organization = await LoadOrganizationAsync(cancellationToken);
        var before = ToOrganizationDto(organization);

        organization.Name = request.Name.Trim();
        organization.Logo = NormalizeOptional(request.Logo);
        organization.Address = NormalizeOptional(request.Address);
        organization.Phone = NormalizeOptional(request.Phone);
        organization.TaxId = NormalizeOptional(request.TaxId);
        organization.CommercialRegistry = NormalizeOptional(request.CommercialRegistry);
        organization.DefaultCurrency = request.DefaultCurrency.Trim();
        organization.Timezone = request.Timezone.Trim();
        organization.DefaultLanguage = request.DefaultLanguage.Trim();
        organization.RtlEnabled = request.RtlEnabled;
        organization.FiscalYearStartMonth = request.FiscalYearStartMonth;
        organization.PurchaseOrderPrefix = request.PurchaseOrderPrefix.Trim();
        organization.PurchaseReceiptPrefix = request.PurchaseReceiptPrefix.Trim();
        organization.PurchaseReturnPrefix = request.PurchaseReturnPrefix.Trim();
        organization.PaymentPrefix = request.PaymentPrefix.Trim();
        organization.DefaultWarehouseId = request.DefaultWarehouseId;
        organization.AutoPostDrafts = request.AutoPostDrafts;
        organization.UpdatedBy = _executionContext.Actor;

        await _dbContext.SaveChangesAsync(cancellationToken);
        var after = ToOrganizationDto(organization);
        await _auditLogService.WriteAsync("organization_updated", "settings", nameof(Organization), organization.Id.ToString(), before, after, null, null, cancellationToken);
        return after;
    }

    public async Task<SecuritySettingsDto> GetSecuritySettingsAsync(CancellationToken cancellationToken)
    {
        await _authorizationService.EnsurePermissionAsync(Permissions.SettingsSecurityManage, cancellationToken);
        var settings = await LoadSecuritySettingsAsync(cancellationToken);
        return ToSecurityDto(settings);
    }

    public async Task<FeatureConfigurationDto> GetFeatureConfigurationAsync(CancellationToken cancellationToken)
    {
        await _authorizationService.EnsureOrganizationAccessAsync(cancellationToken);
        var organization = await LoadOrganizationAsync(cancellationToken);
        var security = await LoadSecuritySettingsAsync(cancellationToken);
        return BuildFeatureConfigurationDto(organization, security);
    }

    public async Task<SecuritySettingsDto> UpdateSecuritySettingsAsync(UpdateSecuritySettingsRequest request, CancellationToken cancellationToken)
    {
        await _authorizationService.EnsurePermissionAsync(Permissions.SettingsSecurityManage, cancellationToken);
        var settings = await LoadSecuritySettingsAsync(cancellationToken);
        var before = ToSecurityDto(settings);

        settings.MinimumPasswordLength = request.MinimumPasswordLength;
        settings.RequireUppercase = request.RequireUppercase;
        settings.RequireLowercase = request.RequireLowercase;
        settings.RequireNumber = request.RequireNumber;
        settings.RequireSymbol = request.RequireSymbol;
        settings.SessionTimeoutMinutes = request.SessionTimeoutMinutes;
        settings.ForceTwoFactor = request.ForceTwoFactor;
        settings.InviteExpiryDays = request.InviteExpiryDays;
        settings.AllowedEmailDomains = NormalizeOptional(request.AllowedEmailDomains);
        settings.LoginAttemptLimit = request.LoginAttemptLimit;
        settings.AuditRetentionDays = request.AuditRetentionDays;
        settings.EnableEmailOtp = request.EnableEmailOtp;
        settings.UpdatedBy = _executionContext.Actor;

        await _dbContext.SaveChangesAsync(cancellationToken);
        var after = ToSecurityDto(settings);
        await _auditLogService.WriteAsync("security_settings_updated", "settings", nameof(OrganizationSecuritySettings), settings.Id.ToString(), before, after, null, null, cancellationToken);
        return after;
    }

    public async Task<PagedResult<UserAccessListItemDto>> ListUsersAsync(UserListQuery query, CancellationToken cancellationToken)
    {
        await _authorizationService.EnsurePermissionAsync(Permissions.SettingsUsersManage, cancellationToken);
        var pagination = new PaginationRequest(query.Page, query.PageSize);
        var normalizedStatus = NormalizeOptional(query.Status);

        var membershipsQuery = _dbContext.OrganizationMemberships
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(entity => entity.OrganizationId == _executionContext.OrganizationId)
            .Include(entity => entity.User)
            .Include(entity => entity.Roles)
            .ThenInclude(entity => entity.Role)
            .ThenInclude(entity => entity!.Permissions)
            .AsQueryable();

        var invitationsQuery = _dbContext.UserInvitations
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(entity =>
                entity.OrganizationId == _executionContext.OrganizationId &&
                entity.Status == InvitationStatus.Pending)
            .Include(entity => entity.Roles)
            .ThenInclude(entity => entity.Role)
            .ThenInclude(entity => entity!.Permissions)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            membershipsQuery = membershipsQuery.Where(entity =>
                entity.User!.FullName.Contains(search) ||
                entity.User.Email.Contains(search));
            invitationsQuery = invitationsQuery.Where(entity =>
                entity.Email.Contains(search) ||
                (entity.FullName != null && entity.FullName.Contains(search)));
        }

        if (query.RoleId.HasValue)
        {
            membershipsQuery = membershipsQuery.Where(entity => entity.Roles.Any(role => role.RoleId == query.RoleId.Value));
            invitationsQuery = invitationsQuery.Where(entity => entity.Roles.Any(role => role.RoleId == query.RoleId.Value));
        }

        membershipsQuery = ApplyUserStatusFilter(membershipsQuery, normalizedStatus);
        invitationsQuery = string.Equals(normalizedStatus, "Invited", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(normalizedStatus)
            ? invitationsQuery
            : invitationsQuery.Where(_ => false);

        var memberItems = (await membershipsQuery.ToListAsync(cancellationToken))
            .Select(ToUserAccessListItemDto);
        var invitationItems = (await invitationsQuery.ToListAsync(cancellationToken))
            .Select(ToUserAccessListItemDto);
        var rows = ApplyUserSorting(memberItems.Concat(invitationItems), query).ToArray();
        var pageItems = rows
            .Skip(pagination.Skip)
            .Take(pagination.NormalizedPageSize)
            .ToArray();

        return new PagedResult<UserAccessListItemDto>(
            pageItems,
            pagination.NormalizedPage,
            pagination.NormalizedPageSize,
            rows.Length,
            CalculateTotalPages(rows.Length, pagination.NormalizedPageSize),
            new
            {
                query.Search,
                Status = normalizedStatus,
                query.RoleId
            },
            new PagedSort(ResolveUserSortBy(query.SortBy), query.SortDirection));
    }

    public async Task<UserListItemDto?> UpdateMembershipAsync(Guid membershipId, UpdateMembershipRequest request, CancellationToken cancellationToken)
    {
        await _authorizationService.EnsurePermissionAsync(Permissions.SettingsUsersManage, cancellationToken);
        var membership = await _dbContext.OrganizationMemberships
            .IgnoreQueryFilters()
            .Include(entity => entity.User)
            .Include(entity => entity.Roles)
            .ThenInclude(entity => entity.Role)
            .ThenInclude(entity => entity!.Permissions)
            .Include(entity => entity.WarehouseAccesses)
            .Include(entity => entity.BranchAccesses)
            .SingleOrDefaultAsync(entity =>
                entity.Id == membershipId &&
                entity.OrganizationId == _executionContext.OrganizationId,
                cancellationToken);

        if (membership is null)
        {
            return null;
        }

        if (_executionContext.MembershipId == membership.Id)
        {
            throw new InvalidOperationException("You cannot change your own critical membership access from this screen.");
        }

        var before = ToUserListItemDto(membership);
        await EnsureNotRemovingLastOwnerAsync(membership, request.Status, request.RoleIds, cancellationToken);
        await EnsureRolesExistAsync(request.RoleIds, cancellationToken);

        membership.Status = request.Status;
        membership.ProfileId = request.ProfileId;
        membership.BranchAccessMode = request.BranchAccessMode;
        membership.WarehouseAccessMode = request.WarehouseAccessMode;
        membership.UpdatedBy = _executionContext.Actor;

        _dbContext.MembershipRoles.RemoveRange(membership.Roles);
        _dbContext.MembershipWarehouseAccesses.RemoveRange(membership.WarehouseAccesses);
        _dbContext.MembershipBranchAccesses.RemoveRange(membership.BranchAccesses);

        foreach (var roleId in request.RoleIds.Distinct())
        {
            _dbContext.MembershipRoles.Add(new MembershipRole
            {
                OrganizationId = membership.OrganizationId,
                MembershipId = membership.Id,
                RoleId = roleId,
                CreatedBy = _executionContext.Actor
            });
        }

        foreach (var warehouseId in request.WarehouseIds.Distinct())
        {
            _dbContext.MembershipWarehouseAccesses.Add(new MembershipWarehouseAccess
            {
                OrganizationId = membership.OrganizationId,
                MembershipId = membership.Id,
                WarehouseId = warehouseId,
                CreatedBy = _executionContext.Actor
            });
        }

        foreach (var branchCode in request.BranchCodes.Where(code => !string.IsNullOrWhiteSpace(code)).Select(code => code.Trim()).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            _dbContext.MembershipBranchAccesses.Add(new MembershipBranchAccess
            {
                OrganizationId = membership.OrganizationId,
                MembershipId = membership.Id,
                BranchCode = branchCode,
                CreatedBy = _executionContext.Actor
            });
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        var after = await ReloadMembershipDtoAsync(membership.Id, cancellationToken);
        await _auditLogService.WriteAsync("membership_updated", "settings", nameof(OrganizationMembership), membership.Id.ToString(), before, after, null, null, cancellationToken);

        return after;
    }

    public async Task<UserListItemDto?> SuspendUserAsync(Guid membershipId, CancellationToken cancellationToken)
    {
        return await SetMembershipStatusAsync(membershipId, MembershipStatus.Suspended, "user_suspended", cancellationToken);
    }

    public async Task<UserListItemDto?> ActivateUserAsync(Guid membershipId, CancellationToken cancellationToken)
    {
        return await SetMembershipStatusAsync(membershipId, MembershipStatus.Active, "user_activated", cancellationToken);
    }

    public async Task<UserListItemDto?> ChangeUserRolesAsync(Guid membershipId, ChangeUserRolesRequest request, CancellationToken cancellationToken)
    {
        await _authorizationService.EnsurePermissionAsync(Permissions.SettingsUsersManage, cancellationToken);
        if (request.RoleIds.Count == 0)
        {
            throw new InvalidOperationException("Role is required.");
        }

        var membership = await _dbContext.OrganizationMemberships
            .IgnoreQueryFilters()
            .Include(entity => entity.User)
            .Include(entity => entity.Roles)
            .ThenInclude(entity => entity.Role)
            .ThenInclude(entity => entity!.Permissions)
            .SingleOrDefaultAsync(entity =>
                entity.Id == membershipId &&
                entity.OrganizationId == _executionContext.OrganizationId,
                cancellationToken);

        if (membership is null)
        {
            return null;
        }

        var roleIds = request.RoleIds.Distinct().ToArray();
        await EnsureRolesExistAsync(roleIds, cancellationToken);
        await EnsureNotRemovingLastOwnerAsync(membership, membership.Status, roleIds, cancellationToken);

        if (_executionContext.MembershipId == membership.Id)
        {
            throw new InvalidOperationException("You cannot change your own critical membership access from this screen.");
        }

        var before = ToUserListItemDto(membership);
        _dbContext.MembershipRoles.RemoveRange(membership.Roles);
        foreach (var roleId in roleIds)
        {
            _dbContext.MembershipRoles.Add(new MembershipRole
            {
                OrganizationId = membership.OrganizationId,
                MembershipId = membership.Id,
                RoleId = roleId,
                CreatedBy = _executionContext.Actor
            });
        }

        membership.UpdatedBy = _executionContext.Actor;
        await _dbContext.SaveChangesAsync(cancellationToken);
        var after = await ReloadMembershipDtoAsync(membership.Id, cancellationToken);
        await _auditLogService.WriteAsync("user_role_changed", "settings", nameof(OrganizationMembership), membership.Id.ToString(), before, after, null, null, cancellationToken);
        return after;
    }

    public async Task TransferOwnershipAsync(TransferOwnershipRequest request, CancellationToken cancellationToken)
    {
        await _authorizationService.EnsurePermissionAsync(Permissions.SettingsUsersManage, cancellationToken);

        var target = await _dbContext.OrganizationMemberships
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(entity => entity.Id == request.MembershipId && entity.OrganizationId == _executionContext.OrganizationId, cancellationToken)
            ?? throw new InvalidOperationException("Target membership was not found.");

        var current = await _dbContext.OrganizationMemberships
            .IgnoreQueryFilters()
            .SingleAsync(entity => entity.Id == _executionContext.MembershipId, cancellationToken);

        current.IsOwner = false;
        target.IsOwner = true;
        current.UpdatedBy = _executionContext.Actor;
        target.UpdatedBy = _executionContext.Actor;

        await _dbContext.SaveChangesAsync(cancellationToken);
        await _auditLogService.WriteAsync("ownership_transferred", "settings", nameof(OrganizationMembership), target.Id.ToString(), null, new { from = current.Id, to = target.Id }, null, null, cancellationToken);
    }

    public async Task<IReadOnlyList<RoleDto>> ListRolesAsync(CancellationToken cancellationToken)
    {
        await _authorizationService.EnsurePermissionAsync(Permissions.SettingsRolesManage, cancellationToken);
        var roles = await _dbContext.Roles
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(entity => entity.OrganizationId == _executionContext.OrganizationId)
            .Include(entity => entity.Permissions)
            .OrderByDescending(entity => entity.IsSystemRole)
            .ThenBy(entity => entity.Name)
            .ToListAsync(cancellationToken);
        return roles.Select(IdentityService.ToRoleDto).ToArray();
    }

    public async Task<PermissionMatrixDto> GetPermissionMatrixAsync(CancellationToken cancellationToken)
    {
        await _authorizationService.EnsurePermissionAsync(Permissions.SettingsRolesManage, cancellationToken);
        return BuildPermissionMatrix();
    }

    public async Task<RoleDto> CreateRoleAsync(UpsertRoleRequest request, CancellationToken cancellationToken)
    {
        await _authorizationService.EnsurePermissionAsync(Permissions.SettingsRolesManage, cancellationToken);
        ValidateRoleRequest(request);
        await EnsureRoleNameUniqueAsync(request.Name, null, cancellationToken);
        ValidatePermissions(request.Permissions);

        var role = new Role
        {
            OrganizationId = _executionContext.OrganizationId!.Value,
            Name = request.Name.Trim(),
            IsActive = request.IsActive,
            IsSystemRole = false,
            IsProtected = false,
            CreatedBy = _executionContext.Actor
        };
        _dbContext.Roles.Add(role);
        await _dbContext.SaveChangesAsync(cancellationToken);

        foreach (var permission in Permissions.Expand(request.Permissions.Distinct()))
        {
            _dbContext.RolePermissions.Add(new RolePermission
            {
                OrganizationId = role.OrganizationId,
                RoleId = role.Id,
                PermissionKey = permission,
                CreatedBy = _executionContext.Actor
            });
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        await _auditLogService.WriteAsync("role_created", "settings", nameof(Role), role.Id.ToString(), null, new { role.Name }, null, null, cancellationToken);

        role = await _dbContext.Roles.IgnoreQueryFilters().Include(entity => entity.Permissions).SingleAsync(entity => entity.Id == role.Id, cancellationToken);
        return IdentityService.ToRoleDto(role);
    }

    public async Task<RoleDto?> UpdateRoleAsync(Guid roleId, UpsertRoleRequest request, CancellationToken cancellationToken)
    {
        await _authorizationService.EnsurePermissionAsync(Permissions.SettingsRolesManage, cancellationToken);
        var role = await _dbContext.Roles
            .IgnoreQueryFilters()
            .Include(entity => entity.Permissions)
            .SingleOrDefaultAsync(entity => entity.Id == roleId && entity.OrganizationId == _executionContext.OrganizationId, cancellationToken);

        if (role is null)
        {
            return null;
        }

        ValidateRoleRequest(request);
        await EnsureRoleNameUniqueAsync(request.Name, roleId, cancellationToken);
        ValidatePermissions(request.Permissions);
        ValidateProtectedRoleUpdate(role, request);

        var before = IdentityService.ToRoleDto(role);
        role.Name = request.Name.Trim();
        role.IsActive = request.IsActive;
        role.UpdatedBy = _executionContext.Actor;
        _dbContext.RolePermissions.RemoveRange(role.Permissions);

        foreach (var permission in Permissions.Expand(request.Permissions.Distinct()))
        {
            _dbContext.RolePermissions.Add(new RolePermission
            {
                OrganizationId = role.OrganizationId,
                RoleId = role.Id,
                PermissionKey = permission,
                CreatedBy = _executionContext.Actor
            });
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        role = await _dbContext.Roles.IgnoreQueryFilters().Include(entity => entity.Permissions).SingleAsync(entity => entity.Id == roleId, cancellationToken);
        var after = IdentityService.ToRoleDto(role);
        await _auditLogService.WriteAsync("role_updated", "settings", nameof(Role), role.Id.ToString(), before, after, null, null, cancellationToken);
        return after;
    }

    public async Task<RoleDto?> CloneRoleAsync(Guid roleId, CancellationToken cancellationToken)
    {
        await _authorizationService.EnsurePermissionAsync(Permissions.SettingsRolesManage, cancellationToken);
        var role = await _dbContext.Roles
            .IgnoreQueryFilters()
            .Include(entity => entity.Permissions)
            .SingleOrDefaultAsync(entity => entity.Id == roleId && entity.OrganizationId == _executionContext.OrganizationId, cancellationToken);
        if (role is null)
        {
            return null;
        }

        var clone = new Role
        {
            OrganizationId = role.OrganizationId,
            Name = $"{role.Name} Copy",
            IsSystemRole = false,
            IsProtected = false,
            IsActive = role.IsActive,
            CreatedBy = _executionContext.Actor
        };
        _dbContext.Roles.Add(clone);
        await _dbContext.SaveChangesAsync(cancellationToken);

        foreach (var permission in role.Permissions.Select(entity => entity.PermissionKey))
        {
            _dbContext.RolePermissions.Add(new RolePermission
            {
                OrganizationId = role.OrganizationId,
                RoleId = clone.Id,
                PermissionKey = permission,
                CreatedBy = _executionContext.Actor
            });
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        clone = await _dbContext.Roles.IgnoreQueryFilters().Include(entity => entity.Permissions).SingleAsync(entity => entity.Id == clone.Id, cancellationToken);
        await _auditLogService.WriteAsync("role_cloned", "settings", nameof(Role), clone.Id.ToString(), null, new { sourceRoleId = role.Id, clone.Name }, null, null, cancellationToken);
        return IdentityService.ToRoleDto(clone);
    }

    public async Task<bool> SetRoleActiveAsync(Guid roleId, bool isActive, CancellationToken cancellationToken)
    {
        await _authorizationService.EnsurePermissionAsync(Permissions.SettingsRolesManage, cancellationToken);
        var role = await _dbContext.Roles
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(entity => entity.Id == roleId && entity.OrganizationId == _executionContext.OrganizationId, cancellationToken);
        if (role is null)
        {
            return false;
        }

        if (role.TemplateKey == "owner" && !isActive)
        {
            throw new InvalidOperationException("Owner must keep full access.");
        }

        if (!isActive)
        {
            var isAssigned = await _dbContext.MembershipRoles
                .IgnoreQueryFilters()
                .AnyAsync(entity => entity.RoleId == roleId, cancellationToken);

            if (isAssigned)
            {
                throw new InvalidOperationException("Cannot deactivate a role that is assigned to users.");
            }
        }

        role.IsActive = isActive;
        role.UpdatedBy = _executionContext.Actor;
        await _dbContext.SaveChangesAsync(cancellationToken);
        await _auditLogService.WriteAsync(isActive ? "role_activated" : "role_deactivated", "settings", nameof(Role), role.Id.ToString(), null, new { role.Name, role.IsActive }, null, null, cancellationToken);
        return true;
    }

    public async Task<IReadOnlyList<InvitationDto>> ListInvitationsAsync(CancellationToken cancellationToken)
    {
        await _authorizationService.EnsurePermissionAsync(Permissions.SettingsInvitationsManage, cancellationToken);
        var invitations = await _dbContext.UserInvitations
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(entity => entity.OrganizationId == _executionContext.OrganizationId)
            .Include(entity => entity.Roles)
            .OrderByDescending(entity => entity.CreatedAt)
            .ToListAsync(cancellationToken);
        return invitations.Select(ToInvitationDto).ToArray();
    }

    public async Task<InvitationDto> InviteUserAsync(InviteUserRequest request, CancellationToken cancellationToken)
    {
        await _authorizationService.EnsurePermissionAsync(Permissions.SettingsInvitationsManage, cancellationToken);
        ValidateInvitationRequest(request);
        await EnsureRolesExistAsync(request.RoleIds, cancellationToken);

        var settings = await LoadSecuritySettingsAsync(cancellationToken);
        var email = request.Email.Trim().ToLowerInvariant();
        ValidateAllowedDomains(email, settings.AllowedEmailDomains);

        var rawToken = SecurityTokenTools.CreateToken();
        var invitation = new UserInvitation
        {
            OrganizationId = _executionContext.OrganizationId!.Value,
            Email = email,
            FullName = NormalizeOptional(request.FullName),
            ProfileId = request.ProfileId,
            Status = InvitationStatus.Pending,
            TokenHash = SecurityTokenTools.ComputeHash(rawToken),
            ExpiresAt = DateTime.UtcNow.AddDays(settings.InviteExpiryDays <= 0 ? 7 : settings.InviteExpiryDays),
            BranchAccessMode = request.BranchAccessMode,
            WarehouseAccessMode = request.WarehouseAccessMode,
            CreatedBy = _executionContext.Actor
        };
        _dbContext.UserInvitations.Add(invitation);
        await _dbContext.SaveChangesAsync(cancellationToken);

        foreach (var roleId in request.RoleIds.Distinct())
        {
            _dbContext.UserInvitationRoles.Add(new UserInvitationRole
            {
                OrganizationId = invitation.OrganizationId,
                InvitationId = invitation.Id,
                RoleId = roleId,
                CreatedBy = _executionContext.Actor
            });
        }

        foreach (var warehouseId in request.WarehouseIds.Distinct())
        {
            _dbContext.UserInvitationWarehouseAccesses.Add(new UserInvitationWarehouseAccess
            {
                OrganizationId = invitation.OrganizationId,
                InvitationId = invitation.Id,
                WarehouseId = warehouseId,
                CreatedBy = _executionContext.Actor
            });
        }

        foreach (var branchCode in request.BranchCodes.Where(code => !string.IsNullOrWhiteSpace(code)).Select(code => code.Trim()).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            _dbContext.UserInvitationBranchAccesses.Add(new UserInvitationBranchAccess
            {
                OrganizationId = invitation.OrganizationId,
                InvitationId = invitation.Id,
                BranchCode = branchCode,
                CreatedBy = _executionContext.Actor
            });
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        await _auditLogService.WriteAsync("user_invited", "settings", nameof(UserInvitation), invitation.Id.ToString(), null, new { invitation.Email, token = rawToken }, null, null, cancellationToken);
        invitation = await _dbContext.UserInvitations.IgnoreQueryFilters().Include(entity => entity.Roles).SingleAsync(entity => entity.Id == invitation.Id, cancellationToken);
        return ToInvitationDto(invitation);
    }

    public async Task<bool> RevokeInvitationAsync(Guid invitationId, CancellationToken cancellationToken)
    {
        await _authorizationService.EnsurePermissionAsync(Permissions.SettingsInvitationsManage, cancellationToken);
        var invitation = await _dbContext.UserInvitations
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(entity => entity.Id == invitationId && entity.OrganizationId == _executionContext.OrganizationId, cancellationToken);
        if (invitation is null)
        {
            return false;
        }

        invitation.Status = InvitationStatus.Revoked;
        invitation.RevokedAt = DateTime.UtcNow;
        invitation.RevokedBy = _executionContext.Actor;
        await _dbContext.SaveChangesAsync(cancellationToken);
        await _auditLogService.WriteAsync("invitation_revoked", "settings", nameof(UserInvitation), invitation.Id.ToString(), null, new { invitation.Email }, null, null, cancellationToken);
        return true;
    }

    public async Task<IReadOnlyList<SessionDto>> ListSessionsAsync(CancellationToken cancellationToken)
    {
        await _authorizationService.EnsurePermissionAsync(Permissions.SettingsSessionsManage, cancellationToken);
        var sessions = await _dbContext.UserSessions
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(entity => entity.OrganizationId == _executionContext.OrganizationId)
            .OrderByDescending(entity => entity.CreatedAt)
            .Select(entity => new SessionDto(
                entity.Id,
                entity.CreatedAt,
                entity.ExpiresAt,
                entity.IsActive,
                entity.RememberMe,
                entity.DeviceName,
                entity.Browser,
                entity.IpAddress))
            .ToListAsync(cancellationToken);
        return sessions;
    }

    public async Task<IReadOnlyList<AuditLogDto>> ListAuditLogsAsync(AuditLogQuery query, CancellationToken cancellationToken)
    {
        await _authorizationService.EnsurePermissionAsync(Permissions.AuditLogView, cancellationToken);
        var logs = _dbContext.AuditLogEntries.AsNoTracking().Where(entity => entity.OrganizationId == _executionContext.OrganizationId);

        if (query.UserId.HasValue)
        {
            logs = logs.Where(entity => entity.UserId == query.UserId.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Module))
        {
            logs = logs.Where(entity => entity.Module == query.Module);
        }

        if (!string.IsNullOrWhiteSpace(query.Action))
        {
            logs = logs.Where(entity => entity.Action == query.Action);
        }

        if (query.FromDate.HasValue)
        {
            logs = logs.Where(entity => entity.CreatedAt >= query.FromDate.Value);
        }

        if (query.ToDate.HasValue)
        {
            logs = logs.Where(entity => entity.CreatedAt <= query.ToDate.Value);
        }

        var page = query.Page <= 0 ? 1 : query.Page;
        var pageSize = query.PageSize <= 0 ? 50 : query.PageSize;

        return await logs
            .OrderByDescending(entity => entity.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(entity => new AuditLogDto(
                entity.Id,
                entity.UserId,
                entity.Action,
                entity.Module,
                entity.ResourceType,
                entity.ResourceId,
                entity.BeforeData,
                entity.AfterData,
                entity.IpAddress,
                entity.UserAgent,
                entity.CreatedAt))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ProfileDto>> ListProfilesAsync(CancellationToken cancellationToken)
    {
        await _authorizationService.EnsurePermissionAsync(Permissions.SettingsProfilesManage, cancellationToken);
        return await _dbContext.UserProfiles
            .AsNoTracking()
            .OrderBy(entity => entity.JobTitle)
            .ThenBy(entity => entity.CreatedAt)
            .Select(entity => ToProfileDto(entity))
            .ToListAsync(cancellationToken);
    }

    public async Task<ProfileDto> CreateProfileAsync(UpsertProfileRequest request, CancellationToken cancellationToken)
    {
        await _authorizationService.EnsurePermissionAsync(Permissions.SettingsProfilesManage, cancellationToken);
        var profile = new UserProfile
        {
            OrganizationId = _executionContext.OrganizationId!.Value,
            JobTitle = NormalizeOptional(request.JobTitle),
            Department = NormalizeOptional(request.Department),
            Phone = NormalizeOptional(request.Phone),
            DefaultBranchCode = NormalizeOptional(request.DefaultBranchCode),
            DefaultWarehouseId = request.DefaultWarehouseId,
            LanguagePreference = request.LanguagePreference.Trim(),
            DashboardPreference = NormalizeOptional(request.DashboardPreference),
            SignaturePlaceholder = NormalizeOptional(request.SignaturePlaceholder),
            Avatar = NormalizeOptional(request.Avatar),
            CreatedBy = _executionContext.Actor
        };
        _dbContext.UserProfiles.Add(profile);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return ToProfileDto(profile);
    }

    public async Task<ProfileDto?> UpdateProfileAsync(Guid profileId, UpsertProfileRequest request, CancellationToken cancellationToken)
    {
        await _authorizationService.EnsurePermissionAsync(Permissions.SettingsProfilesManage, cancellationToken);
        var profile = await _dbContext.UserProfiles.SingleOrDefaultAsync(entity => entity.Id == profileId, cancellationToken);
        if (profile is null)
        {
            return null;
        }

        profile.JobTitle = NormalizeOptional(request.JobTitle);
        profile.Department = NormalizeOptional(request.Department);
        profile.Phone = NormalizeOptional(request.Phone);
        profile.DefaultBranchCode = NormalizeOptional(request.DefaultBranchCode);
        profile.DefaultWarehouseId = request.DefaultWarehouseId;
        profile.LanguagePreference = request.LanguagePreference.Trim();
        profile.DashboardPreference = NormalizeOptional(request.DashboardPreference);
        profile.SignaturePlaceholder = NormalizeOptional(request.SignaturePlaceholder);
        profile.Avatar = NormalizeOptional(request.Avatar);
        profile.UpdatedBy = _executionContext.Actor;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return ToProfileDto(profile);
    }

    private async Task<Guid?> ResolveDefaultWarehouseIdAsync(
        Organization organization,
        Guid? defaultWarehouseId,
        string? defaultWarehouseName,
        CancellationToken cancellationToken)
    {
        if (defaultWarehouseId.HasValue)
        {
            var warehouseExists = await _dbContext.Warehouses
                .IgnoreQueryFilters()
                .AnyAsync(entity =>
                    entity.Id == defaultWarehouseId.Value &&
                    entity.IsActive,
                    cancellationToken);

            if (!warehouseExists)
            {
                throw new InvalidOperationException("Default warehouse was not found.");
            }

            return defaultWarehouseId;
        }

        if (string.IsNullOrWhiteSpace(defaultWarehouseName))
        {
            return organization.DefaultWarehouseId;
        }

        var normalizedName = defaultWarehouseName.Trim();
        var existingWarehouse = await _dbContext.Warehouses
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(entity => entity.Name == normalizedName || entity.Code == normalizedName, cancellationToken);

        if (existingWarehouse is not null)
        {
            return existingWarehouse.Id;
        }

        var warehouse = new Warehouse
        {
            Code = $"WH-{DateTime.UtcNow:HHmmss}",
            Name = normalizedName,
            IsActive = true,
            CreatedBy = _executionContext.Actor
        };
        _dbContext.Warehouses.Add(warehouse);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return warehouse.Id;
    }

    private static void ValidateSetupDependencies(SaveOrganizationSetupRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new InvalidOperationException("Organization name is required.");
        }

        if (string.IsNullOrWhiteSpace(request.DefaultCurrency))
        {
            throw new InvalidOperationException("Currency is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Timezone))
        {
            throw new InvalidOperationException("Timezone is required.");
        }

        if (request.FiscalYearStartMonth is < 1 or > 12)
        {
            throw new InvalidOperationException("Fiscal year start month must be between 1 and 12.");
        }

        if (request.EnablePurchaseReceipts && !request.EnableProcurement)
        {
            throw new InvalidOperationException("Purchase Receipts requires Procurement.");
        }

        if (request.EnableShortageManagement && !request.EnableProcurement)
        {
            throw new InvalidOperationException("Shortage Management requires Procurement.");
        }

        if (request.EnableSupplierFinancials && !request.EnableSupplierManagement)
        {
            throw new InvalidOperationException("Supplier Financials requires Suppliers.");
        }

        if (request.EnableStockAdjustments && !request.EnableInventory)
        {
            throw new InvalidOperationException("Stock Adjustments requires Inventory.");
        }

        if (request.EnableUomConversion && !request.EnableUom)
        {
            throw new InvalidOperationException("UOM Conversion requires UOM.");
        }

        if (request.EnableMultipleWarehouses && !request.EnableWarehouses)
        {
            throw new InvalidOperationException("Multiple Warehouses requires Warehouses.");
        }

        if (request.EnableStockTransfers && !request.EnableWarehouses)
        {
            throw new InvalidOperationException("Stock Transfers requires Warehouses.");
        }

        if (request.EnableShortageManagement && !request.EnablePurchaseReceipts)
        {
            throw new InvalidOperationException("Shortage Management requires Purchase Receipts.");
        }

        if (request.RequirePoBeforeReceipt && (!request.EnablePurchaseOrders || !request.EnablePurchaseReceipts))
        {
            throw new InvalidOperationException("Require PO before Receipt requires Purchase Orders and Purchase Receipts.");
        }

        if (request.EnableInventory && !request.EnableUom)
        {
            throw new InvalidOperationException("Inventory requires UOM.");
        }

        if (!request.AllowOverReceipt && request.OverReceiptTolerancePercent > 0m)
        {
            throw new InvalidOperationException("Over Receipt Tolerance can only be used when over receipt is enabled.");
        }
    }

    private static void ValidateSetupForCompletion(Organization organization)
    {
        if (string.IsNullOrWhiteSpace(organization.Name))
        {
            throw new InvalidOperationException("Organization name is required before setup can be completed.");
        }

        if (organization.EnableWarehouses && !organization.DefaultWarehouseId.HasValue)
        {
            throw new InvalidOperationException("Default warehouse is required when warehouses are enabled.");
        }
    }

    private async Task<Organization> LoadOrganizationAsync(CancellationToken cancellationToken)
    {
        return await _dbContext.Organizations
            .IgnoreQueryFilters()
            .SingleAsync(entity => entity.Id == _executionContext.OrganizationId, cancellationToken);
    }

    private async Task<IReadOnlyList<WarehouseLookupDto>> LoadWarehouseLookupsAsync(CancellationToken cancellationToken)
    {
        return await _dbContext.Warehouses
            .IgnoreQueryFilters()
            .AsNoTracking()
            .OrderBy(entity => entity.Name)
            .Select(entity => new WarehouseLookupDto(entity.Id, entity.Code, entity.Name, entity.IsActive))
            .ToListAsync(cancellationToken);
    }

    private async Task<OrganizationSecuritySettings> LoadSecuritySettingsAsync(CancellationToken cancellationToken)
    {
        return await _dbContext.OrganizationSecuritySettings
            .IgnoreQueryFilters()
            .SingleAsync(entity => entity.OrganizationId == _executionContext.OrganizationId, cancellationToken);
    }

    private async Task EnsureRoleNameUniqueAsync(string name, Guid? currentRoleId, CancellationToken cancellationToken)
    {
        var normalizedName = name.Trim();
        var exists = await _dbContext.Roles
            .IgnoreQueryFilters()
            .AnyAsync(entity =>
                entity.OrganizationId == _executionContext.OrganizationId &&
                entity.Name == normalizedName &&
                entity.Id != currentRoleId,
                cancellationToken);

        if (exists)
        {
            throw new InvalidOperationException("A role with this name already exists.");
        }
    }

    private static void ValidatePermissions(IEnumerable<string> permissions)
    {
        var invalidPermission = permissions.FirstOrDefault(permission => !Permissions.All.Contains(permission, StringComparer.OrdinalIgnoreCase));
        if (invalidPermission is not null)
        {
            throw new InvalidOperationException($"Unknown permission '{invalidPermission}'.");
        }
    }

    private async Task EnsureRolesExistAsync(IEnumerable<Guid> roleIds, CancellationToken cancellationToken)
    {
        var distinctRoleIds = roleIds.Distinct().ToArray();
        var existingRoleCount = await _dbContext.Roles
            .IgnoreQueryFilters()
            .CountAsync(entity => entity.OrganizationId == _executionContext.OrganizationId && distinctRoleIds.Contains(entity.Id), cancellationToken);

        if (existingRoleCount != distinctRoleIds.Length)
        {
            throw new InvalidOperationException("One or more roles are invalid for this organization.");
        }
    }

    private async Task<UserListItemDto?> SetMembershipStatusAsync(
        Guid membershipId,
        MembershipStatus status,
        string auditAction,
        CancellationToken cancellationToken)
    {
        await _authorizationService.EnsurePermissionAsync(Permissions.SettingsUsersManage, cancellationToken);
        var membership = await _dbContext.OrganizationMemberships
            .IgnoreQueryFilters()
            .Include(entity => entity.User)
            .Include(entity => entity.Roles)
            .ThenInclude(entity => entity.Role)
            .ThenInclude(entity => entity!.Permissions)
            .SingleOrDefaultAsync(entity =>
                entity.Id == membershipId &&
                entity.OrganizationId == _executionContext.OrganizationId,
                cancellationToken);

        if (membership is null)
        {
            return null;
        }

        var currentRoleIds = membership.Roles.Select(entity => entity.RoleId).Distinct().ToArray();
        await EnsureNotRemovingLastOwnerAsync(membership, status, currentRoleIds, cancellationToken);

        if (_executionContext.MembershipId == membership.Id)
        {
            throw new InvalidOperationException("You cannot change your own critical membership access from this screen.");
        }

        var before = ToUserListItemDto(membership);

        membership.Status = status;
        membership.UpdatedBy = _executionContext.Actor;
        await _dbContext.SaveChangesAsync(cancellationToken);

        var after = await ReloadMembershipDtoAsync(membership.Id, cancellationToken);
        await _auditLogService.WriteAsync(auditAction, "settings", nameof(OrganizationMembership), membership.Id.ToString(), before, after, null, null, cancellationToken);
        return after;
    }

    private static IQueryable<OrganizationMembership> ApplyUserStatusFilter(IQueryable<OrganizationMembership> query, string? status)
    {
        return status?.Trim().ToLowerInvariant() switch
        {
            "active" => query.Where(entity =>
                entity.Status == MembershipStatus.Active &&
                entity.User!.Status == UserAccountStatus.Active &&
                !entity.User.IsDeleted),
            "suspended" => query.Where(entity =>
                entity.Status == MembershipStatus.Suspended ||
                entity.User!.Status == UserAccountStatus.Suspended),
            "disabled" => query.Where(entity =>
                entity.Status == MembershipStatus.Disabled ||
                entity.Status == MembershipStatus.Deleted ||
                entity.User!.Status == UserAccountStatus.Disabled ||
                entity.User.Status == UserAccountStatus.Deleted ||
                entity.User.IsDeleted),
            "invited" => query.Where(_ => false),
            _ => query
        };
    }

    private static IEnumerable<UserAccessListItemDto> ApplyUserSorting(IEnumerable<UserAccessListItemDto> query, UserListQuery request)
    {
        var ascending = request.SortDirection == SortDirection.Asc;
        return ResolveUserSortBy(request.SortBy) switch
        {
            "email" => ascending ? query.OrderBy(entity => entity.Email) : query.OrderByDescending(entity => entity.Email),
            "status" => ascending ? query.OrderBy(entity => entity.AccessStatus).ThenBy(entity => entity.Email) : query.OrderByDescending(entity => entity.AccessStatus).ThenBy(entity => entity.Email),
            "lastLoginAt" => ascending ? query.OrderBy(entity => entity.LastLoginAt) : query.OrderByDescending(entity => entity.LastLoginAt),
            "createdAt" => ascending ? query.OrderBy(entity => entity.CreatedAt) : query.OrderByDescending(entity => entity.CreatedAt),
            _ => ascending ? query.OrderBy(entity => entity.FullName ?? entity.Email) : query.OrderByDescending(entity => entity.FullName ?? entity.Email)
        };
    }

    private static string ResolveUserSortBy(string? sortBy)
    {
        return sortBy?.Trim() switch
        {
            "email" => "email",
            "status" => "status",
            "lastLoginAt" => "lastLoginAt",
            "createdAt" => "createdAt",
            _ => "name"
        };
    }

    private static int CalculateTotalPages(int totalCount, int pageSize)
    {
        return totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize);
    }

    private static void ValidateInvitationRequest(InviteUserRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
        {
            throw new InvalidOperationException("Email is required.");
        }

        if (!request.Email.Contains('@', StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Email must be valid.");
        }

        if (request.RoleIds.Count == 0)
        {
            throw new InvalidOperationException("Role is required.");
        }
    }

    private static void ValidateRoleRequest(UpsertRoleRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new InvalidOperationException("Role name is required.");
        }
    }

    private static void ValidateProtectedRoleUpdate(Role role, UpsertRoleRequest request)
    {
        if (role.TemplateKey != "owner")
        {
            return;
        }

        var expandedPermissions = Permissions.Expand(request.Permissions);
        if (!request.IsActive ||
            !string.Equals(request.Name.Trim(), "Owner", StringComparison.OrdinalIgnoreCase) ||
            !Permissions.All.All(permission => expandedPermissions.Contains(permission)))
        {
            throw new InvalidOperationException("Owner must keep full access.");
        }
    }

    private async Task EnsureNotRemovingLastOwnerAsync(OrganizationMembership membership, MembershipStatus newStatus, IReadOnlyList<Guid> roleIds, CancellationToken cancellationToken)
    {
        if (!membership.IsOwner)
        {
            return;
        }

        var ownerCount = await _dbContext.OrganizationMemberships
            .IgnoreQueryFilters()
            .CountAsync(entity =>
                entity.OrganizationId == membership.OrganizationId &&
                entity.IsOwner &&
                entity.Status == MembershipStatus.Active,
                cancellationToken);

        if (ownerCount <= 1 && newStatus != MembershipStatus.Active)
        {
            throw new InvalidOperationException("At least one Owner must remain active.");
        }

        if (ownerCount <= 1 && !await RequestedRolesContainOwnerRoleAsync(roleIds, cancellationToken))
        {
            throw new InvalidOperationException("At least one Owner must remain active.");
        }
    }

    private async Task<bool> RequestedRolesContainOwnerRoleAsync(IEnumerable<Guid> roleIds, CancellationToken cancellationToken)
    {
        return await _dbContext.Roles
            .IgnoreQueryFilters()
            .AnyAsync(entity =>
                entity.OrganizationId == _executionContext.OrganizationId &&
                roleIds.Contains(entity.Id) &&
                entity.TemplateKey == "owner",
                cancellationToken);
    }

    private async Task<UserListItemDto> ReloadMembershipDtoAsync(Guid membershipId, CancellationToken cancellationToken)
    {
        var membership = await _dbContext.OrganizationMemberships
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Include(entity => entity.User)
            .Include(entity => entity.Roles)
            .ThenInclude(entity => entity.Role)
            .ThenInclude(entity => entity!.Permissions)
            .SingleAsync(entity => entity.Id == membershipId, cancellationToken);
        return ToUserListItemDto(membership);
    }

    private static UserListItemDto ToUserListItemDto(OrganizationMembership membership)
    {
        var user = membership.User ?? throw new InvalidOperationException("Membership user was not loaded.");
        return new UserListItemDto(
            membership.Id,
            user.Id,
            user.FullName,
            user.Email,
            user.EmailVerified,
            user.Status,
            membership.Status,
            membership.IsOwner,
            membership.Roles.Where(entity => entity.Role is not null).Select(entity => IdentityService.ToRoleDto(entity.Role!)).ToArray(),
            membership.ProfileId,
            membership.CreatedAt,
            user.LastLoginAt);
    }

    private static UserAccessListItemDto ToUserAccessListItemDto(OrganizationMembership membership)
    {
        var user = membership.User ?? throw new InvalidOperationException("Membership user was not loaded.");
        var accessStatus = ResolveAccessStatus(user, membership);
        return new UserAccessListItemDto(
            membership.Id,
            null,
            user.Id,
            user.FullName,
            user.Email,
            user.EmailVerified,
            user.Status,
            membership.Status,
            null,
            accessStatus,
            membership.IsOwner,
            membership.Roles.Where(entity => entity.Role is not null).Select(entity => IdentityService.ToRoleDto(entity.Role!)).ToArray(),
            membership.ProfileId,
            membership.CreatedAt,
            user.LastLoginAt);
    }

    private static UserAccessListItemDto ToUserAccessListItemDto(UserInvitation invitation)
    {
        return new UserAccessListItemDto(
            null,
            invitation.Id,
            null,
            invitation.FullName,
            invitation.Email,
            false,
            null,
            null,
            invitation.Status,
            "Invited",
            false,
            invitation.Roles.Where(entity => entity.Role is not null).Select(entity => IdentityService.ToRoleDto(entity.Role!)).ToArray(),
            invitation.ProfileId,
            invitation.CreatedAt,
            null);
    }

    private static string ResolveAccessStatus(UserAccount user, OrganizationMembership membership)
    {
        if (membership.Status == MembershipStatus.Suspended || user.Status == UserAccountStatus.Suspended)
        {
            return "Suspended";
        }

        if (membership.Status is MembershipStatus.Disabled or MembershipStatus.Deleted ||
            user.Status is UserAccountStatus.Disabled or UserAccountStatus.Deleted ||
            user.IsDeleted)
        {
            return "Disabled";
        }

        return "Active";
    }

    private static InvitationDto ToInvitationDto(UserInvitation invitation)
    {
        return new InvitationDto(
            invitation.Id,
            invitation.Email,
            invitation.FullName,
            invitation.Status,
            invitation.ExpiresAt,
            invitation.CreatedAt,
            invitation.Roles.Select(entity => entity.RoleId).ToArray());
    }

    private static OrganizationSettingsDto ToOrganizationDto(Organization organization)
    {
        return new OrganizationSettingsDto(
            organization.Id,
            organization.Name,
            organization.Logo,
            organization.Address,
            organization.Phone,
            organization.TaxId,
            organization.CommercialRegistry,
            organization.DefaultCurrency,
            organization.Timezone,
            organization.DefaultLanguage,
            organization.RtlEnabled,
            organization.FiscalYearStartMonth,
            organization.PurchaseOrderPrefix,
            organization.PurchaseReceiptPrefix,
            organization.PurchaseReturnPrefix,
            organization.PaymentPrefix,
            organization.DefaultWarehouseId,
            organization.AutoPostDrafts);
    }

    private static OrganizationSetupStatusDto ToSetupStatusDto(Organization organization)
    {
        return new OrganizationSetupStatusDto(
            organization.SetupCompleted,
            organization.SetupCompletedAt,
            organization.SetupCompletedBy,
            organization.SetupStep,
            organization.SetupVersion);
    }

    private static OrganizationFeatureSettingsDto ToFeatureSettingsDto(Organization organization)
    {
        return new OrganizationFeatureSettingsDto(
            organization.EnableProcurement,
            organization.EnablePurchaseOrders,
            organization.EnablePurchaseReceipts,
            organization.EnableInventory,
            organization.EnableWarehouses,
            organization.EnableMultipleWarehouses,
            organization.EnableSupplierManagement,
            organization.EnableSupplierFinancials,
            organization.EnableShortageManagement,
            organization.EnableComponentsBom,
            organization.EnableUom,
            organization.EnableUomConversion);
    }

    private static OrganizationWorkflowSettingsDto ToWorkflowSettingsDto(Organization organization)
    {
        return new OrganizationWorkflowSettingsDto(
            organization.RequirePoBeforeReceipt,
            organization.AllowDirectPurchaseReceipt,
            organization.AllowPartialReceipt,
            organization.AllowOverReceipt,
            organization.OverReceiptTolerancePercent,
            organization.EnablePostingWorkflow,
            organization.LockPostedDocuments,
            organization.RequireApprovalBeforePosting,
            organization.EnableReversals,
            organization.RequireReasonForCancelOrReversal);
    }

    private static OrganizationStockSettingsDto ToStockSettingsDto(Organization organization)
    {
        return new OrganizationStockSettingsDto(
            organization.DefaultWarehouseId,
            organization.AllowNegativeStock,
            organization.EnableBatchTracking,
            organization.EnableSerialTracking,
            organization.EnableExpiryTracking,
            organization.EnableStockTransfers,
            organization.EnableStockAdjustments);
    }

    private static OrganizationSetupDto ToSetupDto(
        Organization organization,
        OrganizationSecuritySettings security,
        IReadOnlyList<WarehouseLookupDto> warehouses)
    {
        return new OrganizationSetupDto(
            ToOrganizationDto(organization),
            ToFeatureSettingsDto(organization),
            ToWorkflowSettingsDto(organization),
            ToStockSettingsDto(organization),
            ToSecurityDto(security),
            ToSetupStatusDto(organization),
            warehouses);
    }

    private static PermissionMatrixDto BuildPermissionMatrix()
    {
        var actions = new[]
        {
            new PermissionMatrixActionDto("view", "settings.permissions.actions.view"),
            new PermissionMatrixActionDto("create", "settings.permissions.actions.create"),
            new PermissionMatrixActionDto("edit", "settings.permissions.actions.edit"),
            new PermissionMatrixActionDto("delete", "settings.permissions.actions.delete"),
            new PermissionMatrixActionDto("post", "settings.permissions.actions.post"),
            new PermissionMatrixActionDto("cancelReverse", "settings.permissions.actions.cancelReverse"),
            new PermissionMatrixActionDto("manage", "settings.permissions.actions.manage")
        };

        var rows = new[]
        {
            MatrixRow("procurement", "settings.permissions.rows.procurement", new Dictionary<string, IReadOnlyList<string>>
            {
                ["view"] =
                [
                    Permissions.ProcurementPurchaseOrderView,
                    Permissions.ProcurementPurchaseReceiptView,
                    Permissions.ProcurementPurchaseReturnView
                ]
            }),
            MatrixRow("purchaseOrders", "settings.permissions.rows.purchaseOrders", new Dictionary<string, IReadOnlyList<string>>
            {
                ["view"] = [Permissions.ProcurementPurchaseOrderView],
                ["create"] = [Permissions.ProcurementPurchaseOrderCreate],
                ["edit"] = [Permissions.ProcurementPurchaseOrderEdit],
                ["delete"] = [Permissions.ProcurementPurchaseOrderDelete],
                ["post"] = [Permissions.ProcurementPurchaseOrderPost],
                ["cancelReverse"] = [Permissions.ProcurementPurchaseOrderCancel]
            }),
            MatrixRow("purchaseReceipts", "settings.permissions.rows.purchaseReceipts", new Dictionary<string, IReadOnlyList<string>>
            {
                ["view"] = [Permissions.ProcurementPurchaseReceiptView],
                ["create"] = [Permissions.ProcurementPurchaseReceiptCreate],
                ["edit"] = [Permissions.ProcurementPurchaseReceiptEdit],
                ["delete"] = [Permissions.ProcurementPurchaseReceiptDelete],
                ["post"] = [Permissions.ProcurementPurchaseReceiptPost],
                ["cancelReverse"] = [Permissions.ProcurementPurchaseReceiptCancel]
            }),
            MatrixRow("suppliers", "settings.permissions.rows.suppliers", new Dictionary<string, IReadOnlyList<string>>
            {
                ["view"] = [Permissions.SuppliersView],
                ["create"] = [Permissions.SuppliersCreate],
                ["edit"] = [Permissions.SuppliersEdit],
                ["delete"] = [Permissions.SuppliersDelete]
            }),
            MatrixRow("supplierFinancials", "settings.permissions.rows.supplierFinancials", new Dictionary<string, IReadOnlyList<string>>
            {
                ["view"] = [Permissions.SupplierFinancialsPayablesView],
                ["cancelReverse"] = [Permissions.SupplierFinancialsReversalsCreate]
            }),
            MatrixRow("payments", "settings.permissions.rows.payments", new Dictionary<string, IReadOnlyList<string>>
            {
                ["view"] = [Permissions.SupplierFinancialsPayablesView],
                ["create"] = [Permissions.SupplierFinancialsPaymentsCreate],
                ["edit"] = [Permissions.SupplierFinancialsPaymentsCreate],
                ["post"] = [Permissions.SupplierFinancialsPaymentsPost],
                ["cancelReverse"] = [Permissions.SupplierFinancialsReversalsCreate]
            }),
            MatrixRow("inventory", "settings.permissions.rows.inventory", new Dictionary<string, IReadOnlyList<string>>
            {
                ["view"] = [Permissions.InventoryStockLedgerView],
                ["create"] = [Permissions.InventoryAdjustmentCreate],
                ["post"] = [Permissions.InventoryAdjustmentPost],
                ["manage"] = [Permissions.InventoryWarehouseManage]
            }),
            MatrixRow("shortages", "settings.permissions.rows.shortages", new Dictionary<string, IReadOnlyList<string>>
            {
                ["view"] = [Permissions.ShortageView],
                ["create"] = [Permissions.ShortageResolutionCreate],
                ["post"] = [Permissions.ShortageResolutionPost]
            }),
            MatrixRow("settingsUsers", "settings.permissions.rows.settingsUsers", new Dictionary<string, IReadOnlyList<string>>
            {
                ["manage"] =
                [
                    Permissions.SettingsUsersManage,
                    Permissions.SettingsRolesManage
                ]
            })
        };

        return new PermissionMatrixDto(actions, rows);
    }

    private static PermissionMatrixRowDto MatrixRow(
        string key,
        string labelKey,
        IReadOnlyDictionary<string, IReadOnlyList<string>> permissions)
    {
        return new PermissionMatrixRowDto(key, labelKey, permissions);
    }

    private static FeatureConfigurationDto BuildFeatureConfigurationDto(Organization organization, OrganizationSecuritySettings security)
    {
        var enabledModules = new List<string> { "workspace", "settings" };
        var disabledModules = new List<string>();

        AddModuleState("procurement", organization.EnableProcurement && (organization.EnablePurchaseOrders || organization.EnablePurchaseReceipts), enabledModules, disabledModules);
        AddModuleState("inventory", organization.EnableInventory, enabledModules, disabledModules);
        AddModuleState("suppliers", organization.EnableSupplierManagement, enabledModules, disabledModules);
        AddModuleState("supplier-financials", organization.EnableSupplierFinancials, enabledModules, disabledModules);

        return new FeatureConfigurationDto(
            WorkspaceEnabled: true,
            ProcurementEnabled: organization.EnableProcurement && (organization.EnablePurchaseOrders || organization.EnablePurchaseReceipts),
            InventoryEnabled: organization.EnableInventory,
            SuppliersEnabled: organization.EnableSupplierManagement,
            SupplierFinancialsEnabled: organization.EnableSupplierFinancials,
            SettingsEnabled: true,
            OfflineDraftsOnly: true,
            EmailOtpEnabled: security.EnableEmailOtp,
            AutoPostDrafts: organization.AutoPostDrafts,
            EnabledModules: enabledModules,
            DisabledModules: disabledModules);
    }

    private static void AddModuleState(string key, bool enabled, List<string> enabledModules, List<string> disabledModules)
    {
        if (enabled)
        {
            enabledModules.Add(key);
            return;
        }

        disabledModules.Add(key);
    }

    private async Task WriteFeatureChangeAuditEntriesAsync(
        OrganizationFeatureSettingsDto before,
        OrganizationFeatureSettingsDto after,
        Guid organizationId,
        CancellationToken cancellationToken)
    {
        foreach (var change in GetFeatureChanges(before, after))
        {
            await _auditLogService.WriteAsync(
                change.enabled ? "feature_enabled" : "feature_disabled",
                "settings",
                nameof(Organization),
                organizationId.ToString(),
                null,
                new { Feature = change.name },
                null,
                null,
                cancellationToken);
        }
    }

    private static IEnumerable<(string name, bool enabled)> GetFeatureChanges(OrganizationFeatureSettingsDto before, OrganizationFeatureSettingsDto after)
    {
        var beforeValues = new Dictionary<string, bool>
        {
            ["procurement"] = before.EnableProcurement,
            ["purchase_orders"] = before.EnablePurchaseOrders,
            ["purchase_receipts"] = before.EnablePurchaseReceipts,
            ["inventory"] = before.EnableInventory,
            ["warehouses"] = before.EnableWarehouses,
            ["multiple_warehouses"] = before.EnableMultipleWarehouses,
            ["supplier_management"] = before.EnableSupplierManagement,
            ["supplier_financials"] = before.EnableSupplierFinancials,
            ["shortage_management"] = before.EnableShortageManagement,
            ["components_bom"] = before.EnableComponentsBom,
            ["uom"] = before.EnableUom,
            ["uom_conversion"] = before.EnableUomConversion,
        };

        var afterValues = new Dictionary<string, bool>
        {
            ["procurement"] = after.EnableProcurement,
            ["purchase_orders"] = after.EnablePurchaseOrders,
            ["purchase_receipts"] = after.EnablePurchaseReceipts,
            ["inventory"] = after.EnableInventory,
            ["warehouses"] = after.EnableWarehouses,
            ["multiple_warehouses"] = after.EnableMultipleWarehouses,
            ["supplier_management"] = after.EnableSupplierManagement,
            ["supplier_financials"] = after.EnableSupplierFinancials,
            ["shortage_management"] = after.EnableShortageManagement,
            ["components_bom"] = after.EnableComponentsBom,
            ["uom"] = after.EnableUom,
            ["uom_conversion"] = after.EnableUomConversion,
        };

        return beforeValues.Keys
            .Where(key => beforeValues[key] != afterValues[key])
            .Select(key => (key, afterValues[key]));
    }

    private static SecuritySettingsDto ToSecurityDto(OrganizationSecuritySettings settings)
    {
        return new SecuritySettingsDto(
            settings.MinimumPasswordLength,
            settings.RequireUppercase,
            settings.RequireLowercase,
            settings.RequireNumber,
            settings.RequireSymbol,
            settings.SessionTimeoutMinutes,
            settings.ForceTwoFactor,
            settings.InviteExpiryDays,
            settings.AllowedEmailDomains,
            settings.LoginAttemptLimit,
            settings.AuditRetentionDays,
            settings.EnableEmailOtp);
    }

    private static ProfileDto ToProfileDto(UserProfile profile)
    {
        return new ProfileDto(
            profile.Id,
            profile.JobTitle,
            profile.Department,
            profile.Phone,
            profile.DefaultBranchCode,
            profile.DefaultWarehouseId,
            profile.LanguagePreference,
            profile.DashboardPreference,
            profile.SignaturePlaceholder,
            profile.Avatar);
    }

    private static void ValidateAllowedDomains(string email, string? allowedDomains)
    {
        if (string.IsNullOrWhiteSpace(allowedDomains))
        {
            return;
        }

        var domain = email.Split('@').LastOrDefault() ?? string.Empty;
        var allowed = allowedDomains.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (!allowed.Contains(domain, StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("This email domain is not allowed for invitations.");
        }
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
