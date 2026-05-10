using ERP.Application.Common.Pagination;
using ERP.Application.Security;

namespace ERP.Api.Endpoints;

public static class OrganizationSecurityEndpoints
{
    public static IEndpointRouteBuilder MapOrganizationSecurityEndpoints(this IEndpointRouteBuilder app)
    {
        var settings = app.MapGroup("/api/settings").RequireAuthorization();

        settings.MapGet("/organization/setup-status", GetSetupStatusAsync);
        settings.MapGet("/organization/setup", GetSetupAsync);
        settings.MapPut("/organization/setup", SaveSetupAsync);
        settings.MapPost("/organization/setup/complete", CompleteSetupAsync);
        settings.MapGet("/organization", GetOrganizationAsync);
        settings.MapPut("/organization", UpdateOrganizationAsync);
        settings.MapGet("/security", GetSecurityAsync);
        settings.MapGet("/features", GetFeaturesAsync);
        settings.MapPut("/security", UpdateSecurityAsync);
        settings.MapGet("/users", ListUsersAsync);
        settings.MapPost("/users/{membershipId:guid}/suspend", SuspendUserAsync);
        settings.MapPost("/users/{membershipId:guid}/activate", ActivateUserAsync);
        settings.MapPut("/users/{membershipId:guid}/roles", ChangeUserRolesAsync);
        settings.MapPut("/users/{membershipId:guid}", UpdateMembershipAsync);
        settings.MapPost("/users/transfer-ownership", TransferOwnershipAsync);
        settings.MapGet("/roles", ListRolesAsync);
        settings.MapGet("/permissions/matrix", GetPermissionMatrixAsync);
        settings.MapPost("/roles", CreateRoleAsync);
        settings.MapPut("/roles/{roleId:guid}", UpdateRoleAsync);
        settings.MapPost("/roles/{roleId:guid}/clone", CloneRoleAsync);
        settings.MapPost("/roles/{roleId:guid}/activate", ActivateRoleAsync);
        settings.MapPost("/roles/{roleId:guid}/deactivate", DeactivateRoleAsync);
        settings.MapGet("/invitations", ListInvitationsAsync);
        settings.MapPost("/invitations", InviteUserAsync);
        settings.MapPost("/invitations/{invitationId:guid}/revoke", RevokeInvitationAsync);
        settings.MapGet("/sessions", ListSessionsAsync);
        settings.MapGet("/audit-log", ListAuditLogsAsync);
        settings.MapGet("/profiles", ListProfilesAsync);
        settings.MapPost("/profiles", CreateProfileAsync);
        settings.MapPut("/profiles/{profileId:guid}", UpdateProfileAsync);

        return app;
    }

    private static Task<IResult> GetOrganizationAsync(IOrganizationAdministrationService service, CancellationToken cancellationToken)
        => ExecuteAsync(() => service.GetOrganizationAsync(cancellationToken));

    private static Task<IResult> GetSetupStatusAsync(IOrganizationAdministrationService service, CancellationToken cancellationToken)
        => ExecuteAsync(() => service.GetSetupStatusAsync(cancellationToken));

    private static Task<IResult> GetSetupAsync(IOrganizationAdministrationService service, CancellationToken cancellationToken)
        => ExecuteAsync(() => service.GetSetupAsync(cancellationToken));

    private static Task<IResult> SaveSetupAsync(SaveOrganizationSetupRequest request, IOrganizationAdministrationService service, CancellationToken cancellationToken)
        => ExecuteAsync(() => service.SaveSetupAsync(request, cancellationToken));

    private static Task<IResult> CompleteSetupAsync(IOrganizationAdministrationService service, CancellationToken cancellationToken)
        => ExecuteAsync(() => service.CompleteSetupAsync(cancellationToken));

    private static Task<IResult> UpdateOrganizationAsync(UpdateOrganizationRequest request, IOrganizationAdministrationService service, CancellationToken cancellationToken)
        => ExecuteAsync(() => service.UpdateOrganizationAsync(request, cancellationToken));

    private static Task<IResult> GetSecurityAsync(IOrganizationAdministrationService service, CancellationToken cancellationToken)
        => ExecuteAsync(() => service.GetSecuritySettingsAsync(cancellationToken));

    private static Task<IResult> GetFeaturesAsync(IOrganizationAdministrationService service, CancellationToken cancellationToken)
        => ExecuteAsync(() => service.GetFeatureConfigurationAsync(cancellationToken));

    private static Task<IResult> UpdateSecurityAsync(UpdateSecuritySettingsRequest request, IOrganizationAdministrationService service, CancellationToken cancellationToken)
        => ExecuteAsync(() => service.UpdateSecuritySettingsAsync(request, cancellationToken));

    private static Task<IResult> ListUsersAsync(
        string? search,
        string? status,
        Guid? roleId,
        int? page,
        int? pageSize,
        string? sortBy,
        SortDirection? sortDirection,
        IOrganizationAdministrationService service,
        CancellationToken cancellationToken)
        => ExecuteAsync(() => service.ListUsersAsync(new UserListQuery(search, status, roleId, page ?? 1, pageSize ?? 20, sortBy, sortDirection ?? SortDirection.Asc), cancellationToken));

    private static Task<IResult> SuspendUserAsync(Guid membershipId, IOrganizationAdministrationService service, CancellationToken cancellationToken)
        => ExecuteAsync(async () =>
        {
            var result = await service.SuspendUserAsync(membershipId, cancellationToken);
            return result is null ? Results.NotFound() : Results.Ok(result);
        });

    private static Task<IResult> ActivateUserAsync(Guid membershipId, IOrganizationAdministrationService service, CancellationToken cancellationToken)
        => ExecuteAsync(async () =>
        {
            var result = await service.ActivateUserAsync(membershipId, cancellationToken);
            return result is null ? Results.NotFound() : Results.Ok(result);
        });

    private static Task<IResult> ChangeUserRolesAsync(Guid membershipId, ChangeUserRolesRequest request, IOrganizationAdministrationService service, CancellationToken cancellationToken)
        => ExecuteAsync(async () =>
        {
            var result = await service.ChangeUserRolesAsync(membershipId, request, cancellationToken);
            return result is null ? Results.NotFound() : Results.Ok(result);
        });

    private static Task<IResult> UpdateMembershipAsync(Guid membershipId, UpdateMembershipRequest request, IOrganizationAdministrationService service, CancellationToken cancellationToken)
        => ExecuteAsync(async () =>
        {
            var result = await service.UpdateMembershipAsync(membershipId, request, cancellationToken);
            return result is null ? Results.NotFound() : Results.Ok(result);
        });

    private static Task<IResult> TransferOwnershipAsync(TransferOwnershipRequest request, IOrganizationAdministrationService service, CancellationToken cancellationToken)
        => ExecuteAsync(async () =>
        {
            await service.TransferOwnershipAsync(request, cancellationToken);
            return Results.NoContent();
        });

    private static Task<IResult> ListRolesAsync(IOrganizationAdministrationService service, CancellationToken cancellationToken)
        => ExecuteAsync(() => service.ListRolesAsync(cancellationToken));

    private static Task<IResult> GetPermissionMatrixAsync(IOrganizationAdministrationService service, CancellationToken cancellationToken)
        => ExecuteAsync(() => service.GetPermissionMatrixAsync(cancellationToken));

    private static Task<IResult> CreateRoleAsync(UpsertRoleRequest request, IOrganizationAdministrationService service, CancellationToken cancellationToken)
        => ExecuteAsync(() => service.CreateRoleAsync(request, cancellationToken));

    private static Task<IResult> UpdateRoleAsync(Guid roleId, UpsertRoleRequest request, IOrganizationAdministrationService service, CancellationToken cancellationToken)
        => ExecuteAsync(async () =>
        {
            var result = await service.UpdateRoleAsync(roleId, request, cancellationToken);
            return result is null ? Results.NotFound() : Results.Ok(result);
        });

    private static Task<IResult> CloneRoleAsync(Guid roleId, IOrganizationAdministrationService service, CancellationToken cancellationToken)
        => ExecuteAsync(async () =>
        {
            var result = await service.CloneRoleAsync(roleId, cancellationToken);
            return result is null ? Results.NotFound() : Results.Ok(result);
        });

    private static Task<IResult> ActivateRoleAsync(Guid roleId, IOrganizationAdministrationService service, CancellationToken cancellationToken)
        => ExecuteAsync(async () =>
        {
            var changed = await service.SetRoleActiveAsync(roleId, true, cancellationToken);
            return changed ? Results.NoContent() : Results.NotFound();
        });

    private static Task<IResult> DeactivateRoleAsync(Guid roleId, IOrganizationAdministrationService service, CancellationToken cancellationToken)
        => ExecuteAsync(async () =>
        {
            var changed = await service.SetRoleActiveAsync(roleId, false, cancellationToken);
            return changed ? Results.NoContent() : Results.NotFound();
        });

    private static Task<IResult> ListInvitationsAsync(IOrganizationAdministrationService service, CancellationToken cancellationToken)
        => ExecuteAsync(() => service.ListInvitationsAsync(cancellationToken));

    private static Task<IResult> InviteUserAsync(InviteUserRequest request, IOrganizationAdministrationService service, CancellationToken cancellationToken)
        => ExecuteAsync(() => service.InviteUserAsync(request, cancellationToken));

    private static Task<IResult> RevokeInvitationAsync(Guid invitationId, IOrganizationAdministrationService service, CancellationToken cancellationToken)
        => ExecuteAsync(async () =>
        {
            var changed = await service.RevokeInvitationAsync(invitationId, cancellationToken);
            return changed ? Results.NoContent() : Results.NotFound();
        });

    private static Task<IResult> ListSessionsAsync(IOrganizationAdministrationService service, CancellationToken cancellationToken)
        => ExecuteAsync(() => service.ListSessionsAsync(cancellationToken));

    private static Task<IResult> ListAuditLogsAsync(Guid? userId, string? module, string? action, DateTime? fromDate, DateTime? toDate, int page, int pageSize, IOrganizationAdministrationService service, CancellationToken cancellationToken)
        => ExecuteAsync(() => service.ListAuditLogsAsync(new AuditLogQuery(userId, module, action, fromDate, toDate, page, pageSize), cancellationToken));

    private static Task<IResult> ListProfilesAsync(IOrganizationAdministrationService service, CancellationToken cancellationToken)
        => ExecuteAsync(() => service.ListProfilesAsync(cancellationToken));

    private static Task<IResult> CreateProfileAsync(UpsertProfileRequest request, IOrganizationAdministrationService service, CancellationToken cancellationToken)
        => ExecuteAsync(() => service.CreateProfileAsync(request, cancellationToken));

    private static Task<IResult> UpdateProfileAsync(Guid profileId, UpsertProfileRequest request, IOrganizationAdministrationService service, CancellationToken cancellationToken)
        => ExecuteAsync(async () =>
        {
            var result = await service.UpdateProfileAsync(profileId, request, cancellationToken);
            return result is null ? Results.NotFound() : Results.Ok(result);
        });

    private static async Task<IResult> ExecuteAsync<T>(Func<Task<T>> action)
    {
        try
        {
            return Results.Ok(await action());
        }
        catch (UnauthorizedAccessException exception)
        {
            return Results.Json(new { message = exception.Message }, statusCode: StatusCodes.Status403Forbidden);
        }
        catch (InvalidOperationException exception)
        {
            return Results.BadRequest(new { message = exception.Message });
        }
    }

    private static async Task<IResult> ExecuteAsync(Func<Task<IResult>> action)
    {
        try
        {
            return await action();
        }
        catch (UnauthorizedAccessException exception)
        {
            return Results.Json(new { message = exception.Message }, statusCode: StatusCodes.Status403Forbidden);
        }
        catch (InvalidOperationException exception)
        {
            return Results.BadRequest(new { message = exception.Message });
        }
    }
}
