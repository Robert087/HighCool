using ERP.Application.TestData;
using Microsoft.Extensions.DependencyInjection;

namespace ERP.Infrastructure.TestData;

public static class OrganizationTestDataServiceCollectionExtensions
{
    public static IServiceCollection AddOrganizationTestDataTools(this IServiceCollection services)
    {
        services.AddScoped<IOrganizationTestDataService, OrganizationTestDataService>();
        return services;
    }
}
