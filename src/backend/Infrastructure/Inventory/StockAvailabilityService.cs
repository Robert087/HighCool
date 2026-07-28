using ERP.Application.Inventory;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Inventory;

public sealed class StockAvailabilityService(AppDbContext dbContext) : IStockAvailabilityService
{
    public async Task EnsureStockOutAllowedAsync(
        IReadOnlyCollection<StockOutRequirement> requirements,
        CancellationToken cancellationToken)
    {
        if (requirements.Count == 0)
        {
            return;
        }

        var groupedRequirements = requirements
            .Where(requirement => requirement.BaseQty > 0m)
            .GroupBy(requirement => new { requirement.ItemId, requirement.WarehouseId })
            .Select(group => new
            {
                group.Key.ItemId,
                group.Key.WarehouseId,
                BaseQty = Round(group.Sum(requirement => requirement.BaseQty)),
                Context = string.Join("; ", group.Select(requirement => requirement.Context).Where(context => !string.IsNullOrWhiteSpace(context)).Distinct())
            })
            .ToArray();

        foreach (var requirement in groupedRequirements)
        {
            if (await AllowsNegativeStockAsync(requirement.ItemId, requirement.WarehouseId, cancellationToken))
            {
                continue;
            }

            var currentBalance = await dbContext.StockLedgerEntries
                .AsNoTracking()
                .Where(entity => entity.ItemId == requirement.ItemId && entity.WarehouseId == requirement.WarehouseId)
                .SumSignedBaseQtyAsync(dbContext, cancellationToken);

            var projectedBalance = Round(currentBalance - requirement.BaseQty);
            if (projectedBalance < 0m)
            {
                var context = string.IsNullOrWhiteSpace(requirement.Context)
                    ? "stock-out posting"
                    : requirement.Context;

                throw new InvalidOperationException(
                    $"Insufficient stock. Negative stock is not allowed. {context} would reduce item {requirement.ItemId} in warehouse {requirement.WarehouseId} below zero.");
            }
        }
    }

    private async Task<bool> AllowsNegativeStockAsync(Guid itemId, Guid warehouseId, CancellationToken cancellationToken)
    {
        var organizationIds = await dbContext.Items
            .AsNoTracking()
            .Where(entity => entity.Id == itemId)
            .Select(entity => entity.OrganizationId)
            .Concat(dbContext.Warehouses
                .AsNoTracking()
                .Where(entity => entity.Id == warehouseId)
                .Select(entity => entity.OrganizationId))
            .Distinct()
            .ToListAsync(cancellationToken);

        organizationIds = organizationIds
            .Where(organizationId => organizationId != Guid.Empty)
            .ToList();

        if (organizationIds.Count != 1)
        {
            return false;
        }

        return await dbContext.Organizations
            .AsNoTracking()
            .Where(entity => entity.Id == organizationIds[0])
            .Select(entity => entity.AllowNegativeStock)
            .SingleOrDefaultAsync(cancellationToken);
    }

    private static decimal Round(decimal value)
    {
        return decimal.Round(value, 6, MidpointRounding.AwayFromZero);
    }
}
