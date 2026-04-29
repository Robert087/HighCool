using ERP.Application.Common.Pagination;

namespace ERP.Application.Security;

public interface IOrganizationAdministrationService
{
    Task<OrganizationSettingsDto> GetOrganizationAsync(CancellationToken cancellationToken);

    Task<OrganizationSetupStatusDto> GetSetupStatusAsync(CancellationToken cancellationToken);

    Task<OrganizationSetupDto> GetSetupAsync(CancellationToken cancellationToken);

    Task<OrganizationSetupDto> SaveSetupAsync(SaveOrganizationSetupRequest request, CancellationToken cancellationToken);

    Task<OrganizationSetupStatusDto> CompleteSetupAsync(CancellationToken cancellationToken);

    Task<OrganizationSettingsDto> UpdateOrganizationAsync(UpdateOrganizationRequest request, CancellationToken cancellationToken);

    Task<SecuritySettingsDto> GetSecuritySettingsAsync(CancellationToken cancellationToken);

    Task<FeatureConfigurationDto> GetFeatureConfigurationAsync(CancellationToken cancellationToken);

    Task<SecuritySettingsDto> UpdateSecuritySettingsAsync(UpdateSecuritySettingsRequest request, CancellationToken cancellationToken);

    Task<PagedResult<UserAccessListItemDto>> ListUsersAsync(UserListQuery query, CancellationToken cancellationToken);

    Task<UserListItemDto?> UpdateMembershipAsync(Guid membershipId, UpdateMembershipRequest request, CancellationToken cancellationToken);

    Task<UserListItemDto?> SuspendUserAsync(Guid membershipId, CancellationToken cancellationToken);

    Task<UserListItemDto?> ActivateUserAsync(Guid membershipId, CancellationToken cancellationToken);

    Task<UserListItemDto?> ChangeUserRolesAsync(Guid membershipId, ChangeUserRolesRequest request, CancellationToken cancellationToken);

    Task TransferOwnershipAsync(TransferOwnershipRequest request, CancellationToken cancellationToken);

    Task<IReadOnlyList<RoleDto>> ListRolesAsync(CancellationToken cancellationToken);

    Task<PermissionMatrixDto> GetPermissionMatrixAsync(CancellationToken cancellationToken);

    Task<RoleDto> CreateRoleAsync(UpsertRoleRequest request, CancellationToken cancellationToken);

    Task<RoleDto?> UpdateRoleAsync(Guid roleId, UpsertRoleRequest request, CancellationToken cancellationToken);

    Task<RoleDto?> CloneRoleAsync(Guid roleId, CancellationToken cancellationToken);

    Task<bool> SetRoleActiveAsync(Guid roleId, bool isActive, CancellationToken cancellationToken);

    Task<IReadOnlyList<InvitationDto>> ListInvitationsAsync(CancellationToken cancellationToken);

    Task<InvitationDto> InviteUserAsync(InviteUserRequest request, CancellationToken cancellationToken);

    Task<bool> RevokeInvitationAsync(Guid invitationId, CancellationToken cancellationToken);

    Task<IReadOnlyList<SessionDto>> ListSessionsAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<AuditLogDto>> ListAuditLogsAsync(AuditLogQuery query, CancellationToken cancellationToken);

    Task<IReadOnlyList<ProfileDto>> ListProfilesAsync(CancellationToken cancellationToken);

    Task<ProfileDto> CreateProfileAsync(UpsertProfileRequest request, CancellationToken cancellationToken);

    Task<ProfileDto?> UpdateProfileAsync(Guid profileId, UpsertProfileRequest request, CancellationToken cancellationToken);
}
