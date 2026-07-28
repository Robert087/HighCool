using ERP.Application.Common.Exceptions;
using ERP.Application.Inventory;
using ERP.Application.Inventory.Transfers;
using ERP.Domain.Common;
using ERP.Domain.Inventory;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Inventory.Transfers;

public sealed class InventoryTransferPostingService(
    AppDbContext dbContext,
    IInventoryTransferService queryService,
    IStockAvailabilityService stockAvailabilityService) : IInventoryTransferPostingService
{
    public async Task<InventoryTransferDto?> PostAsync(Guid id, string actor, CancellationToken cancellationToken)
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
            throw new InvalidOperationException("Only Draft inventory transfers can be posted.");
        }

        ValidateForPosting(entity);

        await stockAvailabilityService.EnsureStockOutAllowedAsync(
            entity.Lines
                .Select(line => new StockOutRequirement(
                    line.ItemId,
                    entity.SourceWarehouseId,
                    line.BaseQty,
                    $"Inventory transfer {entity.TransferNo} line {line.LineNo}"))
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

    public async Task<InventoryTransferDto?> CancelAsync(Guid id, string actor, CancellationToken cancellationToken)
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
            throw new InvalidOperationException("Only Posted inventory transfers can be canceled.");
        }

        ValidateForPosting(entity);

        await stockAvailabilityService.EnsureStockOutAllowedAsync(
            entity.Lines
                .Select(line => new StockOutRequirement(
                    line.ItemId,
                    entity.DestinationWarehouseId,
                    line.BaseQty,
                    $"Inventory transfer cancellation {entity.TransferNo} line {line.LineNo}"))
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

    private Task<InventoryTransfer?> LoadForPostingAsync(Guid id, CancellationToken cancellationToken)
    {
        return dbContext.InventoryTransfers
            .AsSplitQuery()
            .Include(transfer => transfer.SourceWarehouse)
            .Include(transfer => transfer.DestinationWarehouse)
            .Include(transfer => transfer.Lines.OrderBy(line => line.LineNo))
                .ThenInclude(line => line.Item)
            .Include(transfer => transfer.Lines)
                .ThenInclude(line => line.Uom)
            .SingleOrDefaultAsync(transfer => transfer.Id == id, cancellationToken);
    }

    private static void ValidateForPosting(InventoryTransfer entity)
    {
        if (entity.SourceWarehouse is null || !entity.SourceWarehouse.IsActive)
        {
            throw new InvalidOperationException("Source warehouse was not found.");
        }

        if (entity.DestinationWarehouse is null || !entity.DestinationWarehouse.IsActive)
        {
            throw new InvalidOperationException("Destination warehouse was not found.");
        }

        if (entity.SourceWarehouseId == entity.DestinationWarehouseId)
        {
            throw new InvalidOperationException("Source warehouse and destination warehouse must be different.");
        }

        if (entity.Lines.Count == 0)
        {
            throw new InvalidOperationException("At least one transfer line is required before posting.");
        }

        foreach (var line in entity.Lines)
        {
            if (line.Item is null || !line.Item.IsActive)
            {
                throw new InvalidOperationException($"Inventory transfer line {line.LineNo} item was not found.");
            }

            if (line.Uom is null || !line.Uom.IsActive)
            {
                throw new InvalidOperationException($"Inventory transfer line {line.LineNo} UOM was not found.");
            }

            if (line.Quantity <= 0m || line.BaseQty <= 0m)
            {
                throw new InvalidOperationException($"Inventory transfer line {line.LineNo} quantity must be greater than zero.");
            }
        }
    }

    private async Task CreateStockEntriesAsync(
        InventoryTransfer entity,
        bool isCancellation,
        string actor,
        CancellationToken cancellationToken)
    {
        var runningBalances = new Dictionary<(Guid ItemId, Guid WarehouseId), decimal>();

        foreach (var line in entity.Lines.OrderBy(line => line.LineNo))
        {
            if (isCancellation)
            {
                await AddStockEntryAsync(
                    runningBalances,
                    line,
                    entity.SourceWarehouseId,
                    StockTransactionType.InventoryTransferCancellationIn,
                    SourceDocumentType.InventoryTransferCancellation,
                    isIn: true,
                    operation: "cancel:source-in",
                    entity,
                    actor,
                    cancellationToken);

                await AddStockEntryAsync(
                    runningBalances,
                    line,
                    entity.DestinationWarehouseId,
                    StockTransactionType.InventoryTransferCancellationOut,
                    SourceDocumentType.InventoryTransferCancellation,
                    isIn: false,
                    operation: "cancel:destination-out",
                    entity,
                    actor,
                    cancellationToken);
            }
            else
            {
                await AddStockEntryAsync(
                    runningBalances,
                    line,
                    entity.SourceWarehouseId,
                    StockTransactionType.InventoryTransferOut,
                    SourceDocumentType.InventoryTransfer,
                    isIn: false,
                    operation: "post:source-out",
                    entity,
                    actor,
                    cancellationToken);

                await AddStockEntryAsync(
                    runningBalances,
                    line,
                    entity.DestinationWarehouseId,
                    StockTransactionType.InventoryTransferIn,
                    SourceDocumentType.InventoryTransfer,
                    isIn: true,
                    operation: "post:destination-in",
                    entity,
                    actor,
                    cancellationToken);
            }
        }
    }

    private async Task AddStockEntryAsync(
        IDictionary<(Guid ItemId, Guid WarehouseId), decimal> runningBalances,
        InventoryTransferLine line,
        Guid warehouseId,
        StockTransactionType transactionType,
        SourceDocumentType sourceDocType,
        bool isIn,
        string operation,
        InventoryTransfer entity,
        string actor,
        CancellationToken cancellationToken)
    {
        var key = (line.ItemId, warehouseId);
        if (!runningBalances.TryGetValue(key, out var runningBalance))
        {
            runningBalance = await dbContext.StockLedgerEntries
                .Where(entry => entry.ItemId == line.ItemId && entry.WarehouseId == warehouseId)
                .OrderByDescending(entry => entry.TransactionDate)
                .ThenByDescending(entry => entry.CreatedAt)
                .ThenByDescending(entry => entry.Id)
                .Select(entry => entry.RunningBalanceQty)
                .FirstOrDefaultAsync(cancellationToken);
        }

        runningBalance = isIn
            ? Round(runningBalance + line.BaseQty)
            : Round(runningBalance - line.BaseQty);
        runningBalances[key] = runningBalance;

        dbContext.StockLedgerEntries.Add(new StockLedgerEntry
        {
            ItemId = line.ItemId,
            WarehouseId = warehouseId,
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
            TransactionDate = entity.TransferDate,
            UnitCost = null,
            TotalCost = null,
            CreatedBy = actor
        });
    }

    private static decimal Round(decimal value)
    {
        return decimal.Round(value, 6, MidpointRounding.AwayFromZero);
    }

    private async Task<InventoryTransferDto?> ResolvePostConcurrencyAsync(Guid id, CancellationToken cancellationToken)
    {
        dbContext.ChangeTracker.Clear();

        var status = await dbContext.InventoryTransfers
            .AsNoTracking()
            .Where(entity => entity.Id == id)
            .Select(entity => (DocumentStatus?)entity.Status)
            .SingleOrDefaultAsync(cancellationToken);

        if (status == DocumentStatus.Posted)
        {
            return await queryService.GetAsync(id, cancellationToken);
        }

        throw new ConcurrencyConflictException("Inventory transfer posting conflicted with another request. Refresh and try again.");
    }

    private async Task<InventoryTransferDto?> ResolveCancelConcurrencyAsync(Guid id, CancellationToken cancellationToken)
    {
        dbContext.ChangeTracker.Clear();

        var status = await dbContext.InventoryTransfers
            .AsNoTracking()
            .Where(entity => entity.Id == id)
            .Select(entity => (DocumentStatus?)entity.Status)
            .SingleOrDefaultAsync(cancellationToken);

        if (status == DocumentStatus.Canceled)
        {
            return await queryService.GetAsync(id, cancellationToken);
        }

        throw new ConcurrencyConflictException("Inventory transfer cancellation conflicted with another request. Refresh and try again.");
    }

    private static string BuildLedgerOperationKey(Guid transferId, Guid lineId, string operation)
    {
        return $"inventory-transfer:{transferId}:{operation}:{lineId}";
    }
}
