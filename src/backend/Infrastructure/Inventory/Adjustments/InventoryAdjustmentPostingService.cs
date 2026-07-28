using ERP.Application.Common.Exceptions;
using ERP.Application.Inventory;
using ERP.Application.Inventory.Adjustments;
using ERP.Domain.Common;
using ERP.Domain.Inventory;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Inventory.Adjustments;

public sealed class InventoryAdjustmentPostingService(
    AppDbContext dbContext,
    IInventoryAdjustmentService queryService,
    IStockAvailabilityService stockAvailabilityService) : IInventoryAdjustmentPostingService
{
    public async Task<InventoryAdjustmentDto?> PostAsync(Guid id, string actor, CancellationToken cancellationToken)
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
            throw new InvalidOperationException("Only Draft inventory adjustments can be posted.");
        }

        ValidateForPosting(entity);

        await stockAvailabilityService.EnsureStockOutAllowedAsync(
            entity.Lines
                .Where(line => line.AdjustmentType == InventoryAdjustmentType.Decrease)
                .Select(line => new StockOutRequirement(line.ItemId, entity.WarehouseId, line.BaseQty, $"Inventory adjustment {entity.AdjustmentNo} line {line.LineNo}"))
                .ToArray(),
            cancellationToken);

        try
        {
            await CreateStockEntriesAsync(entity, isCancellation: false, actor, cancellationToken);

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

    public async Task<InventoryAdjustmentDto?> CancelAsync(Guid id, string actor, CancellationToken cancellationToken)
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
            throw new InvalidOperationException("Only Posted inventory adjustments can be canceled.");
        }

        ValidateForPosting(entity);

        await stockAvailabilityService.EnsureStockOutAllowedAsync(
            entity.Lines
                .Where(line => line.AdjustmentType == InventoryAdjustmentType.Increase)
                .Select(line => new StockOutRequirement(line.ItemId, entity.WarehouseId, line.BaseQty, $"Inventory adjustment cancellation {entity.AdjustmentNo} line {line.LineNo}"))
                .ToArray(),
            cancellationToken);

        try
        {
            await CreateStockEntriesAsync(entity, isCancellation: true, actor, cancellationToken);

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

    private Task<InventoryAdjustment?> LoadForPostingAsync(Guid id, CancellationToken cancellationToken)
    {
        return dbContext.InventoryAdjustments
            .AsSplitQuery()
            .Include(adjustment => adjustment.Warehouse)
            .Include(adjustment => adjustment.Lines.OrderBy(line => line.LineNo))
                .ThenInclude(line => line.Item)
            .Include(adjustment => adjustment.Lines)
                .ThenInclude(line => line.Uom)
            .SingleOrDefaultAsync(adjustment => adjustment.Id == id, cancellationToken);
    }

    private static void ValidateForPosting(InventoryAdjustment entity)
    {
        if (entity.Warehouse is null || !entity.Warehouse.IsActive)
        {
            throw new InvalidOperationException("Warehouse was not found.");
        }

        if (entity.Lines.Count == 0)
        {
            throw new InvalidOperationException("At least one adjustment line is required before posting.");
        }

        foreach (var line in entity.Lines)
        {
            if (line.Item is null || !line.Item.IsActive)
            {
                throw new InvalidOperationException($"Inventory adjustment line {line.LineNo} item was not found.");
            }

            if (line.Uom is null || !line.Uom.IsActive)
            {
                throw new InvalidOperationException($"Inventory adjustment line {line.LineNo} UOM was not found.");
            }

            if (line.Quantity <= 0m || line.BaseQty <= 0m)
            {
                throw new InvalidOperationException($"Inventory adjustment line {line.LineNo} quantity must be greater than zero.");
            }
        }
    }

    private async Task CreateStockEntriesAsync(
        InventoryAdjustment entity,
        bool isCancellation,
        string actor,
        CancellationToken cancellationToken)
    {
        var runningBalances = new Dictionary<(Guid ItemId, Guid WarehouseId), decimal>();

        foreach (var line in entity.Lines.OrderBy(line => line.LineNo))
        {
            var key = (line.ItemId, entity.WarehouseId);
            if (!runningBalances.TryGetValue(key, out var runningBalance))
            {
                runningBalance = await dbContext.StockLedgerEntries
                    .Where(entry => entry.ItemId == line.ItemId && entry.WarehouseId == entity.WarehouseId)
                    .OrderByDescending(entry => entry.TransactionDate)
                    .ThenByDescending(entry => entry.CreatedAt)
                    .ThenByDescending(entry => entry.Id)
                    .Select(entry => entry.RunningBalanceQty)
                    .FirstOrDefaultAsync(cancellationToken);
            }

            var effectiveIncrease = isCancellation
                ? line.AdjustmentType == InventoryAdjustmentType.Decrease
                : line.AdjustmentType == InventoryAdjustmentType.Increase;

            runningBalance = effectiveIncrease
                ? Round(runningBalance + line.BaseQty)
                : Round(runningBalance - line.BaseQty);
            runningBalances[key] = runningBalance;

            dbContext.StockLedgerEntries.Add(new StockLedgerEntry
            {
                ItemId = line.ItemId,
                WarehouseId = entity.WarehouseId,
                TransactionType = isCancellation
                    ? StockTransactionType.InventoryAdjustmentCancellation
                    : line.AdjustmentType == InventoryAdjustmentType.Increase
                        ? StockTransactionType.InventoryAdjustmentIncrease
                        : StockTransactionType.InventoryAdjustmentDecrease,
                SourceDocType = isCancellation
                    ? SourceDocumentType.InventoryAdjustmentCancellation
                    : SourceDocumentType.InventoryAdjustment,
                SourceDocId = entity.Id,
                SourceLineId = line.Id,
                LedgerOperationKey = BuildLedgerOperationKey(entity.Id, line.Id, isCancellation),
                QtyIn = effectiveIncrease ? line.Quantity : 0m,
                QtyOut = effectiveIncrease ? 0m : line.Quantity,
                UomId = line.UomId,
                BaseQty = line.BaseQty,
                RunningBalanceQty = runningBalance,
                TransactionDate = entity.AdjustmentDate,
                UnitCost = null,
                TotalCost = null,
                CreatedBy = actor
            });
        }
    }

    private static decimal Round(decimal value)
    {
        return decimal.Round(value, 6, MidpointRounding.AwayFromZero);
    }

    private async Task<InventoryAdjustmentDto?> ResolvePostConcurrencyAsync(Guid id, CancellationToken cancellationToken)
    {
        dbContext.ChangeTracker.Clear();

        var status = await dbContext.InventoryAdjustments
            .AsNoTracking()
            .Where(entity => entity.Id == id)
            .Select(entity => (DocumentStatus?)entity.Status)
            .SingleOrDefaultAsync(cancellationToken);

        if (status == DocumentStatus.Posted)
        {
            return await queryService.GetAsync(id, cancellationToken);
        }

        throw new ConcurrencyConflictException("Inventory adjustment posting conflicted with another request. Refresh and try again.");
    }

    private async Task<InventoryAdjustmentDto?> ResolveCancelConcurrencyAsync(Guid id, CancellationToken cancellationToken)
    {
        dbContext.ChangeTracker.Clear();

        var status = await dbContext.InventoryAdjustments
            .AsNoTracking()
            .Where(entity => entity.Id == id)
            .Select(entity => (DocumentStatus?)entity.Status)
            .SingleOrDefaultAsync(cancellationToken);

        if (status == DocumentStatus.Canceled)
        {
            return await queryService.GetAsync(id, cancellationToken);
        }

        throw new ConcurrencyConflictException("Inventory adjustment cancellation conflicted with another request. Refresh and try again.");
    }

    private static string BuildLedgerOperationKey(Guid adjustmentId, Guid lineId, bool isCancellation)
    {
        return isCancellation
            ? $"inventory-adjustment:{adjustmentId}:cancel:{lineId}"
            : $"inventory-adjustment:{adjustmentId}:post:{lineId}";
    }
}
