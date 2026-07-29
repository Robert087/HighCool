using ERP.Application.Common.Exceptions;
using ERP.Application.Inventory;
using ERP.Application.Inventory.Issues;
using ERP.Application.Purchasing.PurchaseReceipts;
using ERP.Domain.Common;
using ERP.Domain.Inventory;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Inventory.Issues;

public sealed class InventoryIssuePostingService(
    AppDbContext dbContext,
    IInventoryIssueService queryService,
    IQuantityConversionService quantityConversionService,
    IStockAvailabilityService stockAvailabilityService) : IInventoryIssuePostingService
{
    public async Task<InventoryIssueDto?> PostAsync(Guid id, string actor, CancellationToken cancellationToken)
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
            throw new InvalidOperationException("Only Draft inventory issues can be posted.");
        }

        ValidateForPosting(entity);
        await RefreshPostingQuantitiesAsync(entity, actor, cancellationToken);
        await stockAvailabilityService.EnsureStockOutAllowedAsync(
            entity.Lines
                .Where(line => line.BaseQty > 0m)
                .Select(line => new StockOutRequirement(
                    line.ItemId,
                    entity.WarehouseId,
                    line.BaseQty,
                    $"Inventory issue {entity.IssueNo} line {line.LineNo}"))
                .ToArray(),
            cancellationToken);

        try
        {
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

    public async Task<InventoryIssueDto?> CancelAsync(Guid id, string actor, CancellationToken cancellationToken)
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
            throw new InvalidOperationException("Only Posted inventory issues can be canceled.");
        }

        ValidateForCancellation(entity);

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

    private Task<InventoryIssue?> LoadForPostingAsync(Guid id, CancellationToken cancellationToken)
    {
        return dbContext.InventoryIssues
            .AsSplitQuery()
            .Include(issue => issue.Warehouse)
            .Include(issue => issue.Lines.OrderBy(line => line.LineNo))
                .ThenInclude(line => line.Item)
            .Include(issue => issue.Lines)
                .ThenInclude(line => line.Uom)
            .SingleOrDefaultAsync(issue => issue.Id == id, cancellationToken);
    }

    private static void ValidateForPosting(InventoryIssue entity)
    {
        if (entity.Warehouse is null || !entity.Warehouse.IsActive)
        {
            throw new InvalidOperationException("Warehouse was not found.");
        }

        if (!Enum.IsDefined(typeof(InventoryIssueReason), entity.Reason))
        {
            throw new InvalidOperationException("Issue reason is required.");
        }

        if (entity.Lines.Count == 0)
        {
            throw new InvalidOperationException("At least one issue line is required before posting.");
        }

        if (entity.Lines.GroupBy(line => line.ItemId).Any(group => group.Count() > 1))
        {
            throw new DuplicateEntityException("The same item cannot appear more than once inside the inventory issue.");
        }

        foreach (var line in entity.Lines)
        {
            if (line.Item is null || !line.Item.IsActive)
            {
                throw new InvalidOperationException($"Inventory issue line {line.LineNo} item was not found.");
            }

            if (line.Uom is null || !line.Uom.IsActive)
            {
                throw new InvalidOperationException($"Inventory issue line {line.LineNo} UOM was not found.");
            }

            if (line.Quantity <= 0m)
            {
                throw new InvalidOperationException($"Inventory issue line {line.LineNo} quantity must be greater than zero.");
            }
        }
    }

    private static void ValidateForCancellation(InventoryIssue entity)
    {
        if (entity.Warehouse is null)
        {
            throw new InvalidOperationException("Warehouse was not found.");
        }

        if (!Enum.IsDefined(typeof(InventoryIssueReason), entity.Reason))
        {
            throw new InvalidOperationException("Issue reason is required.");
        }

        if (entity.Lines.Count == 0)
        {
            throw new InvalidOperationException("At least one issue line is required before cancellation.");
        }

        if (entity.Lines.GroupBy(line => line.ItemId).Any(group => group.Count() > 1))
        {
            throw new DuplicateEntityException("The same item cannot appear more than once inside the inventory issue.");
        }

        foreach (var line in entity.Lines)
        {
            if (line.Item is null)
            {
                throw new InvalidOperationException($"Inventory issue line {line.LineNo} item was not found.");
            }

            if (line.Uom is null)
            {
                throw new InvalidOperationException($"Inventory issue line {line.LineNo} UOM was not found.");
            }

            if (line.Quantity <= 0m)
            {
                throw new InvalidOperationException($"Inventory issue line {line.LineNo} quantity must be greater than zero.");
            }

            if (line.BaseQty <= 0m)
            {
                throw new InvalidOperationException($"Inventory issue line {line.LineNo} base quantity must be greater than zero.");
            }
        }
    }

    private async Task RefreshPostingQuantitiesAsync(
        InventoryIssue entity,
        string actor,
        CancellationToken cancellationToken)
    {
        foreach (var line in entity.Lines.OrderBy(line => line.LineNo))
        {
            var baseQty = await ConvertToBaseAsync(line, cancellationToken);
            if (baseQty <= 0m)
            {
                throw new InvalidOperationException($"Inventory issue line {line.LineNo} base quantity must be greater than zero.");
            }

            line.Quantity = Round(line.Quantity);
            line.BaseQty = Round(baseQty);
            line.UpdatedBy = actor;
        }
    }

    private async Task CreatePostingStockEntriesAsync(
        InventoryIssue entity,
        string actor,
        CancellationToken cancellationToken)
    {
        var runningBalances = new Dictionary<(Guid ItemId, Guid WarehouseId), decimal>();

        foreach (var line in entity.Lines.OrderBy(line => line.LineNo).Where(line => line.BaseQty > 0m))
        {
            await AddStockEntryAsync(
                runningBalances,
                line,
                StockTransactionType.InventoryIssue,
                SourceDocumentType.InventoryIssue,
                isIn: false,
                operation: "post",
                entity,
                actor,
                cancellationToken);
        }
    }

    private async Task CreateCancellationStockEntriesAsync(
        InventoryIssue entity,
        string actor,
        CancellationToken cancellationToken)
    {
        var runningBalances = new Dictionary<(Guid ItemId, Guid WarehouseId), decimal>();

        foreach (var line in entity.Lines.OrderBy(line => line.LineNo).Where(line => line.BaseQty > 0m))
        {
            await AddStockEntryAsync(
                runningBalances,
                line,
                StockTransactionType.InventoryIssueCancellation,
                SourceDocumentType.InventoryIssueCancellation,
                isIn: true,
                operation: "cancel",
                entity,
                actor,
                cancellationToken);
        }
    }

    private async Task AddStockEntryAsync(
        IDictionary<(Guid ItemId, Guid WarehouseId), decimal> runningBalances,
        InventoryIssueLine line,
        StockTransactionType transactionType,
        SourceDocumentType sourceDocType,
        bool isIn,
        string operation,
        InventoryIssue entity,
        string actor,
        CancellationToken cancellationToken)
    {
        var key = (line.ItemId, entity.WarehouseId);
        if (!runningBalances.TryGetValue(key, out var runningBalance))
        {
            runningBalance = await GetCurrentBaseStockAsync(line.ItemId, entity.WarehouseId, cancellationToken);
        }

        runningBalance = isIn
            ? Round(runningBalance + line.BaseQty)
            : Round(runningBalance - line.BaseQty);
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
            QtyIn = isIn ? line.Quantity : 0m,
            QtyOut = isIn ? 0m : line.Quantity,
            UomId = line.UomId,
            BaseQty = line.BaseQty,
            RunningBalanceQty = runningBalance,
            TransactionDate = entity.IssueDate,
            UnitCost = null,
            TotalCost = null,
            CreatedBy = actor
        });
    }

    private async Task<decimal> ConvertToBaseAsync(InventoryIssueLine line, CancellationToken cancellationToken)
    {
        try
        {
            return Round(await quantityConversionService.ConvertAsync(
                line.Quantity,
                line.UomId,
                line.Item!.BaseUomId,
                cancellationToken));
        }
        catch (InvalidOperationException exception) when (exception.Message.Contains("global UOM conversion", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Inventory issue line {line.LineNo} requires a global UOM conversion from the issue UOM to the item base UOM.");
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

    private async Task<InventoryIssueDto?> ResolvePostConcurrencyAsync(Guid id, CancellationToken cancellationToken)
    {
        dbContext.ChangeTracker.Clear();

        var status = await dbContext.InventoryIssues
            .AsNoTracking()
            .Where(entity => entity.Id == id)
            .Select(entity => (DocumentStatus?)entity.Status)
            .SingleOrDefaultAsync(cancellationToken);

        if (status == DocumentStatus.Posted)
        {
            return await queryService.GetAsync(id, cancellationToken);
        }

        throw new ConcurrencyConflictException("Inventory issue posting conflicted with another request. Refresh and try again.");
    }

    private async Task<InventoryIssueDto?> ResolveCancelConcurrencyAsync(Guid id, CancellationToken cancellationToken)
    {
        dbContext.ChangeTracker.Clear();

        var status = await dbContext.InventoryIssues
            .AsNoTracking()
            .Where(entity => entity.Id == id)
            .Select(entity => (DocumentStatus?)entity.Status)
            .SingleOrDefaultAsync(cancellationToken);

        if (status == DocumentStatus.Canceled)
        {
            return await queryService.GetAsync(id, cancellationToken);
        }

        throw new ConcurrencyConflictException("Inventory issue cancellation conflicted with another request. Refresh and try again.");
    }

    private static string BuildLedgerOperationKey(Guid issueId, Guid lineId, string operation)
    {
        return $"inventory-issue:{issueId}:{operation}:{lineId}";
    }

    private static decimal Round(decimal value)
    {
        return decimal.Round(value, 6, MidpointRounding.AwayFromZero);
    }
}
