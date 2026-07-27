using ERP.Application.Security;
using ERP.Domain.Identity;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Security;

public sealed class OrganizationFeatureService(
    AppDbContext dbContext,
    IRequestExecutionContext executionContext) : IOrganizationFeatureService
{
    private bool _organizationLoaded;
    private Organization? _organization;

    public async Task<bool> IsEnabledAsync(OrganizationFeature feature, CancellationToken cancellationToken)
    {
        var organization = await GetActiveOrganizationAsync(cancellationToken);

        return organization is not null && IsEnabled(organization, feature);
    }

    public async Task RequireEnabledAsync(OrganizationFeature feature, CancellationToken cancellationToken)
    {
        if (!await IsEnabledAsync(feature, cancellationToken))
        {
            throw new FeatureDisabledException(feature);
        }
    }

    private static bool IsEnabled(Organization organization, OrganizationFeature feature)
    {
        return feature switch
        {
            OrganizationFeature.Inventory => organization.EnableInventory,
            OrganizationFeature.Purchasing => organization.EnableProcurement,
            OrganizationFeature.Sales => organization.EnableSales,
            OrganizationFeature.Employees => organization.EnableEmployees,
            OrganizationFeature.Salaries => organization.EnableSalaries,
            OrganizationFeature.EmployeeAdvances => organization.EnableEmployeeAdvances,
            OrganizationFeature.Expenses => organization.EnableExpenses,
            OrganizationFeature.Reports => organization.EnableReports,
            OrganizationFeature.Notifications => organization.EnableNotifications,
            OrganizationFeature.PriceLists => organization.EnablePriceLists,
            OrganizationFeature.InventoryTransfers => organization.EnableInventory && organization.EnableStockTransfers,
            OrganizationFeature.InventoryAdjustments => organization.EnableInventory && organization.EnableStockAdjustments,
            OrganizationFeature.InventoryCounts => organization.EnableInventory && organization.EnableInventoryCounts,
            OrganizationFeature.InventoryIssues => organization.EnableInventory && organization.EnableInventoryIssues,
            OrganizationFeature.LowStockAlerts => organization.EnableInventory && organization.EnableLowStockAlerts,
            OrganizationFeature.PurchaseOrders => organization.EnableProcurement && organization.EnablePurchaseOrders,
            OrganizationFeature.PurchaseReceipts => organization.EnableProcurement && organization.EnablePurchaseReceipts,
            OrganizationFeature.Warehouses => organization.EnableInventory && organization.EnableWarehouses,
            OrganizationFeature.SupplierManagement => organization.EnableProcurement,
            OrganizationFeature.SupplierFinancials => organization.EnableProcurement,
            OrganizationFeature.ShortageManagement => organization.EnableInventory && organization.EnableProcurement && organization.EnableShortageManagement,
            OrganizationFeature.Uom => organization.EnableInventory && organization.EnableUom,
            OrganizationFeature.UomConversion => organization.EnableInventory && organization.EnableUom && organization.EnableUomConversion,
            OrganizationFeature.Reversals => organization.EnableReversals,
            _ => false
        };
    }

    private async Task<Organization?> GetActiveOrganizationAsync(CancellationToken cancellationToken)
    {
        if (_organizationLoaded)
        {
            return _organization;
        }

        _organizationLoaded = true;

        if (!executionContext.OrganizationId.HasValue)
        {
            return null;
        }

        _organization = await dbContext.Organizations
            .IgnoreQueryFilters()
            .AsNoTracking()
            .SingleOrDefaultAsync(entity => entity.Id == executionContext.OrganizationId.Value, cancellationToken);

        return _organization;
    }
}
