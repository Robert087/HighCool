using ERP.Application.Security;
using ERP.Domain.Identity;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Security;

public sealed class AuthorizationService(
    AppDbContext dbContext,
    IRequestExecutionContext executionContext) : IAuthorizationService
{
    private readonly AppDbContext _dbContext = dbContext;
    private readonly IRequestExecutionContext _executionContext = executionContext;

    public Task EnsureAuthenticatedAsync(CancellationToken cancellationToken)
    {
        if (!_executionContext.IsAuthenticated || !_executionContext.UserId.HasValue)
        {
            throw new UnauthorizedAccessException("Authentication is required.");
        }

        return Task.CompletedTask;
    }

    public async Task EnsureOrganizationAccessAsync(CancellationToken cancellationToken)
    {
        await EnsureAuthenticatedAsync(cancellationToken);

        if (!_executionContext.OrganizationId.HasValue || !_executionContext.MembershipId.HasValue)
        {
            throw new UnauthorizedAccessException("Organization access is required.");
        }

        var membership = await _dbContext.OrganizationMemberships
            .IgnoreQueryFilters()
            .AsNoTracking()
            .SingleOrDefaultAsync(entity =>
                entity.Id == _executionContext.MembershipId.Value &&
                entity.OrganizationId == _executionContext.OrganizationId.Value &&
                entity.UserId == _executionContext.UserId!.Value,
                cancellationToken);

        if (membership is null || membership.Status is MembershipStatus.Suspended or MembershipStatus.Disabled or MembershipStatus.Deleted)
        {
            throw new UnauthorizedAccessException("You do not have an active membership in this organization.");
        }
    }

    public async Task EnsurePermissionAsync(string permission, CancellationToken cancellationToken)
    {
        await EnsureOrganizationAccessAsync(cancellationToken);

        var membership = await _dbContext.OrganizationMemberships
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Include(entity => entity.Roles)
            .ThenInclude(entity => entity.Role)
            .ThenInclude(entity => entity!.Permissions)
            .SingleAsync(entity => entity.Id == _executionContext.MembershipId!.Value, cancellationToken);

        if (membership.IsOwner)
        {
            return;
        }

        var assignedPermissions = membership.Roles
            .Where(entity => entity.Role != null && entity.Role.IsActive)
            .SelectMany(entity => entity.Role!.Permissions.Select(permissionEntity => permissionEntity.PermissionKey));

        var expandedPermissions = Permissions.Expand(assignedPermissions);

        if (!expandedPermissions.Contains(permission))
        {
            throw new UnauthorizedAccessException("You do not have permission to perform this action.");
        }
    }

    public async Task EnsureWarehouseAccessAsync(Guid warehouseId, CancellationToken cancellationToken)
    {
        await EnsureOrganizationAccessAsync(cancellationToken);

        var membership = await _dbContext.OrganizationMemberships
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Include(entity => entity.WarehouseAccesses)
            .SingleAsync(entity => entity.Id == _executionContext.MembershipId!.Value, cancellationToken);

        if (membership.IsOwner || membership.WarehouseAccessMode == AccessScopeMode.All)
        {
            return;
        }

        if (!membership.WarehouseAccesses.Any(entity => entity.WarehouseId == warehouseId))
        {
            throw new UnauthorizedAccessException("You do not have warehouse access for this action.");
        }
    }
}
