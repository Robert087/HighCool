namespace ERP.Application.Security;

public interface IOrganizationFeatureService
{
    Task<bool> IsEnabledAsync(OrganizationFeature feature, CancellationToken cancellationToken);

    Task RequireEnabledAsync(OrganizationFeature feature, CancellationToken cancellationToken);
}

