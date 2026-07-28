using ERP.Application.Common.Exceptions;
using ERP.Application.Inventory;
using ERP.Application.Inventory.Counts;
using ERP.Application.Purchasing.PurchaseReceipts;
using ERP.Domain.Common;
using ERP.Domain.Inventory;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Inventory.Counts;

public sealed class InventoryCountPostingService(
    AppDbContext dbContext,
    IInventoryCountService queryService,
    IQuantityConversionService quantityConversionService,
    IStockAvailabilityService stockAvailabilityService) : IInventoryCountPostingService
{
    public async Task<InventoryCountDto?> PostAsync(Guid id, string actor, CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var entity = await LoadForPostingAsync(id, cancellationToken);
        if (entity is null)
        {
            return null;
        }

        if (entity.Status == DocumentStatus.Posted)
        {
            await transaction.CommitAsync(cancellationToken);
            return await queryService.GetAsync(id, cancellationToken);
        }

        if (entity.Status != DocumentStatus.Draft)
        {
            throw new InvalidOperationException("Only Draft inventory counts can be posted.");
        }

        ValidateForPosting(entity);

        try
        {
            await RefreshPostingQuantitiesAsync(entity, actor, cancellationToken);
            await CreatePostingStockEntriesAsync(entity, actor, cancellationToken);

            entity.Status = DocumentStatus.Posted;
            entity.PostedAt = DateTime.UtcNow;
            entity.PostedBy = actor;
            entity.UpdatedBy = actor;
            entity.Version++;

            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return await ResolvePostConcurrencyAsync(id, cancellationToken);
        }
        catch (DbUpdateException exception) when (PersistenceExceptionClassifier.IsUniqueConstraintViolation(exception))
        {
            await transaction.RollbackAsync(cancellationToken);
            return await ResolvePostConcurrencyAsync(id, cancellationToken);
        }

        return await queryService.GetAsync(entity.Id, cancellationToken);
    }

    public async Task<InventoryCountDto?> CancelAsync(Guid id, string actor, CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var entity = await LoadForPostingAsync(id, cancellationToken);
        if (entity is null)
        {
            return null;
        }

        if (entity.Status == DocumentStatus.Canceled)
        {
            await transaction.CommitAsync(cancellationToken);
            return await queryService.GetAsync(id, cancellationToken);
        }

        if (entity.Status != DocumentStatus.Posted)
        {
            throw new InvalidOperationException("Only Posted inventory counts can be canceled.");
        }

        ValidateForPosting(entity);

        await stockAvailabilityService.EnsureStockOutAllowedAsync(
            entity.Lines
                .Where(line => line.BaseVarianceQty > 0m)
                .Select(line => new StockOutRequirement(
                    line.ItemId,
                    entity.WarehouseId,
                    line.BaseVarianceQty,
                    $"Inventory count cancellation {entity.CountNo} line {line.LineNo}"))
                .ToArray(),
            cancellationToken);

        try
        {
            await CreateCancellationStockEntriesAsync(entity, actor, cancellationToken);

            entity.Status = DocumentStatus.Canceled;
            entity.CanceledAt = DateTime.UtcNow;
            entity.CanceledBy = actor;
            entity.UpdatedBy = actor;
            entity.Version++;

            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return await ResolveCancelConcurrencyAsync(id, cancellationToken);
        }
        catch (DbUpdateException exception) when (PersistenceExceptionClassifier.IsUniqueConstraintViolation(exception))
        {
            await transaction.RollbackAsync(cancellationToken);
            return await ResolveCancelConcurrencyAsync(id, cancellationToken);
        }

        return await queryService.GetAsync(entity.Id, cancellationToken);
    }

    private Task<InventoryCount?> LoadForPostingAsync(Guid id, CancellationToken cancellationToken)
    {
        return dbContext.InventoryCounts
            .AsSplitQuery()
            .Include(count => count.Warehouse)
            .Include(count => count.Lines.OrderBy(line => line.LineNo))
                .ThenInclude(line => line.Item)
            .Include(count => count.Lines)
                .ThenInclude(line => line.Uom)
            .SingleOrDefaultAsync(count => count.Id == id, cancellationToken);
    }

    private static void ValidateForPosting(InventoryCount entity)
    {
        if (entity.Warehouse is null || !entity.Warehouse.IsActive)
        {
            throw new InvalidOperationException("Warehouse was not found.");
        }

        if (entity.Lines.Count == 0)
        {
            throw new InvalidOperationException("At least one count line is required before posting.");
        }

        if (entity.Lines.GroupBy(line => line.ItemId).Any(group => group.Count() > 1))
        {
            throw new DuplicateEntityException("The same item cannot appear more than once inside the inventory count.");
        }

        foreach (var line in entity.Lines)
        {
            if (line.Item is null || !line.Item.IsActive)
            {
                throw new InvalidOperationException($"Inventory count line {line.LineNo} item was not found.");
            }

            if (line.Uom is null || !line.Uom.IsActive)
            {
                throw new InvalidOperationException($"Inventory count line {line.LineNo} UOM was not found.");
            }

            if (line.CountedQty < 0m || line.BaseCountedQty < 0m)
            {
                throw new InvalidOperationException($"Inventory count line {line.LineNo} counted quantity cannot be negative.");
            }
        }
    }

    private async Task RefreshPostingQuantitiesAsync(
        InventoryCount entity,
        string actor,
        CancellationToken cancellationToken)
    {
        var snapshotAt = DateTime.UtcNow;

        foreach (var line in entity.Lines.OrderBy(line => line.LineNo))
        {
            var baseSystemQty = await GetCurrentBaseStockAsync(line.ItemId, entity.WarehouseId, cancellationToken);
            var baseCountedQty = await ConvertToBaseAsync(line, cancellationToken);
            var systemQty = await ConvertFromBaseAsync(line, baseSystemQty, cancellationToken);

            line.SystemQty = Round(systemQty);
            line.CountedQty = Round(line.CountedQty);
            line.VarianceQty = Round(line.CountedQty - line.SystemQty);
            line.BaseSystemQty = Round(baseSystemQty);
            line.BaseCountedQty = Round(baseCountedQty);
            line.BaseVarianceQty = Round(baseCountedQty - baseSystemQty);
            line.UpdatedBy = actor;
        }

        entity.SnapshotAt = snapshotAt;
    }

    private async Task CreatePostingStockEntriesAsync(
        InventoryCount entity,
        string actor,
        CancellationToken cancellationToken)
    {
        var runningBalances = new Dictionary<(Guid ItemId, Guid WarehouseId), decimal>();

        foreach (var line in entity.Lines.OrderBy(line => line.LineNo).Where(line => line.BaseVarianceQty != 0m))
        {
            var isIncrease = line.BaseVarianceQty > 0m;
            await AddStockEntryAsync(
                runningBalances,
                line,
                isIncrease
                    ? StockTransactionType.InventoryCountIncrease
                    : StockTransactionType.InventoryCountDecrease,
                SourceDocumentType.InventoryCount,
                isIn: isIncrease,
                operation: isIncrease ? "post:increase" : "post:decrease",
                entity,
                actor,
                cancellationToken);
        }
    }

    private async Task CreateCancellationStockEntriesAsync(
        InventoryCount entity,
        string actor,
        CancellationToken cancellationToken)
    {
        var runningBalances = new Dictionary<(Guid ItemId, Guid WarehouseId), decimal>();

        foreach (var line in entity.Lines.OrderBy(line => line.LineNo).Where(line => line.BaseVarianceQty != 0m))
        {
            var reversesIncrease = line.BaseVarianceQty > 0m;
            await AddStockEntryAsync(
                runningBalances,
                line,
                reversesIncrease
                    ? StockTransactionType.InventoryCountCancellationOut
                    : StockTransactionType.InventoryCountCancellationIn,
                SourceDocumentType.InventoryCountCancellation,
                isIn: !reversesIncrease,
                operation: "cancel",
                entity,
                actor,
                cancellationToken);
        }
    }

    private async Task AddStockEntryAsync(
        IDictionary<(Guid ItemId, Guid WarehouseId), decimal> runningBalances,
        InventoryCountLine line,
        StockTransactionType transactionType,
        SourceDocumentType sourceDocType,
        bool isIn,
        string operation,
        InventoryCount entity,
        string actor,
        CancellationToken cancellationToken)
    {
        var key = (line.ItemId, entity.WarehouseId);
        if (!runningBalances.TryGetValue(key, out var runningBalance))
        {
            runningBalance = await GetCurrentBaseStockAsync(line.ItemId, entity.WarehouseId, cancellationToken);
        }

        var baseQty = Math.Abs(line.BaseVarianceQty);
        var quantity = Math.Abs(line.VarianceQty);

        runningBalance = isIn
            ? Round(runningBalance + baseQty)
            : Round(runningBalance - baseQty);
        runningBalances[key] = runningBalance;

        dbContext.StockLedgerEntries.Add(new StockLedgerEntry
        {
            ItemId = line.ItemId,
            WarehouseId = entity.WarehouseId,
            TransactionType = transactionType,
            SourceDocType = sourceDocType,
            SourceDocId = entity.Id,
            SourceLineId = line.Id,
            LedgerOperationKey = BuildLedgerOperationKey(entity.Id, line.Id, operation),
            QtyIn = isIn ? quantity : 0m,
            QtyOut = isIn ? 0m : quantity,
            UomId = line.UomId,
            BaseQty = baseQty,
            RunningBalanceQty = runningBalance,
            TransactionDate = entity.CountDate,
            UnitCost = null,
            TotalCost = null,
            CreatedBy = actor
        });
    }

    private async Task<decimal> ConvertToBaseAsync(InventoryCountLine line, CancellationToken cancellationToken)
    {
        try
        {
            return Round(await quantityConversionService.ConvertAsync(
                line.CountedQty,
                line.UomId,
                line.Item!.BaseUomId,
                cancellationToken));
        }
        catch (InvalidOperationException exception) when (exception.Message.Contains("global UOM conversion", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Inventory count line {line.LineNo} requires a global UOM conversion from the count UOM to the item base UOM.");
        }
    }

    private async Task<decimal> ConvertFromBaseAsync(
        InventoryCountLine line,
        decimal baseQty,
        CancellationToken cancellationToken)
    {
        if (baseQty == 0m)
        {
            return 0m;
        }

        try
        {
            return Round(await quantityConversionService.ConvertAsync(
                baseQty,
                line.Item!.BaseUomId,
                line.UomId,
                cancellationToken));
        }
        catch (InvalidOperationException exception) when (exception.Message.Contains("global UOM conversion", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Inventory count line {line.LineNo} requires a global UOM conversion from the item base UOM to the count UOM.");
        }
    }

    private async Task<decimal> GetCurrentBaseStockAsync(
        Guid itemId,
        Guid warehouseId,
        CancellationToken cancellationToken)
    {
        return Round(await dbContext.StockLedgerEntries
            .AsNoTracking()
            .Where(entry => entry.ItemId == itemId && entry.WarehouseId == warehouseId)
            .SumSignedBaseQtyAsync(dbContext, cancellationToken));
    }

    private async Task<InventoryCountDto?> ResolvePostConcurrencyAsync(Guid id, CancellationToken cancellationToken)
    {
        dbContext.ChangeTracker.Clear();

        var status = await dbContext.InventoryCounts
            .AsNoTracking()
            .Where(entity => entity.Id == id)
            .Select(entity => (DocumentStatus?)entity.Status)
            .SingleOrDefaultAsync(cancellationToken);

        if (status == DocumentStatus.Posted)
        {
            return await queryService.GetAsync(id, cancellationToken);
        }

        throw new ConcurrencyConflictException("Inventory count posting conflicted with another request. Refresh and try again.");
    }

    private async Task<InventoryCountDto?> ResolveCancelConcurrencyAsync(Guid id, CancellationToken cancellationToken)
    {
        dbContext.ChangeTracker.Clear();

        var status = await dbContext.InventoryCounts
            .AsNoTracking()
            .Where(entity => entity.Id == id)
            .Select(entity => (DocumentStatus?)entity.Status)
            .SingleOrDefaultAsync(cancellationToken);

        if (status == DocumentStatus.Canceled)
        {
            return await queryService.GetAsync(id, cancellationToken);
        }

        throw new ConcurrencyConflictException("Inventory count cancellation conflicted with another request. Refresh and try again.");
    }

    private static string BuildLedgerOperationKey(Guid countId, Guid lineId, string operation)
    {
        return $"inventory-count:{countId}:{operation}:{lineId}";
    }

    private static decimal Round(decimal value)
    {
        return decimal.Round(value, 6, MidpointRounding.AwayFromZero);
    }
}
