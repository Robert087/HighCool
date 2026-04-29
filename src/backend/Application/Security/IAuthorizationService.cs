namespace ERP.Application.Security;

public interface IAuthorizationService
{
    Task EnsureAuthenticatedAsync(CancellationToken cancellationToken);

    Task EnsureOrganizationAccessAsync(CancellationToken cancellationToken);

    Task EnsurePermissionAsync(string permission, CancellationToken cancellationToken);

    Task EnsureWarehouseAccessAsync(Guid warehouseId, CancellationToken cancellationToken);
}
