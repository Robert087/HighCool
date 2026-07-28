using ERP.Application.Common.Exceptions;
using ERP.Application.Common.Numbering;
using ERP.Application.Common.Pagination;
using ERP.Application.Inventory.Counts;
using ERP.Application.Purchasing.PurchaseReceipts;
using ERP.Domain.Common;
using ERP.Domain.Inventory;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Inventory.Counts;

public sealed class InventoryCountService(
    AppDbContext dbContext,
    IQuantityConversionService quantityConversionService,
    IDocumentNumberService documentNumberService) : IInventoryCountService
{
    private const string DocumentType = "InventoryCount";
    private const string DocumentPrefix = "CNT-";
    private const int DocumentPaddingLength = 6;

    public async Task<PagedResult<InventoryCountListItemDto>> ListAsync(
        InventoryCountListQuery query,
        CancellationToken cancellationToken)
    {
        var pagination = new PaginationRequest(query.Page, query.PageSize);
        var counts = dbContext.InventoryCounts
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var value = query.Search.Trim();
            counts = counts.Where(entity =>
                entity.CountNo.Contains(value) ||
                entity.Warehouse!.Code.Contains(value) ||
                entity.Warehouse.Name.Contains(value) ||
                (entity.Notes != null && entity.Notes.Contains(value)));
        }

        if (!string.IsNullOrWhiteSpace(query.CountNo))
        {
            var value = query.CountNo.Trim();
            counts = counts.Where(entity => entity.CountNo.Contains(value));
        }

        if (query.WarehouseId.HasValue)
        {
            counts = counts.Where(entity => entity.WarehouseId == query.WarehouseId.Value);
        }

        if (query.Status.HasValue)
        {
            counts = counts.Where(entity => entity.Status == query.Status.Value);
        }

        if (query.FromDate.HasValue)
        {
            counts = counts.Where(entity => entity.CountDate >= query.FromDate.Value);
        }

        if (query.ToDate.HasValue)
        {
            counts = counts.Where(entity => entity.CountDate <= query.ToDate.Value);
        }

        var totalCount = await counts.CountAsync(cancellationToken);
        var items = await ApplySorting(counts, query)
            .Skip(pagination.Skip)
            .Take(pagination.NormalizedPageSize)
            .Select(entity => new InventoryCountListItemDto(
                entity.Id,
                entity.CountNo,
                entity.CountDate,
                entity.SnapshotAt,
                entity.WarehouseId,
                entity.Warehouse!.Code,
                entity.Warehouse.Name,
                entity.Status,
                entity.Lines.Count,
                entity.CreatedBy,
                entity.CreatedAt,
                entity.UpdatedAt,
                entity.PostedAt,
                entity.PostedBy,
                entity.CanceledAt,
                entity.CanceledBy))
            .ToListAsync(cancellationToken);

        return new PagedResult<InventoryCountListItemDto>(
            items,
            pagination.NormalizedPage,
            pagination.NormalizedPageSize,
            totalCount,
            totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pagination.NormalizedPageSize),
            new
            {
                query.Search,
                query.CountNo,
                query.WarehouseId,
                query.Status,
                query.FromDate,
                query.ToDate
            },
            new PagedSort(ResolveSortBy(query.SortBy), query.SortDirection));
    }

    public async Task<InventoryCountDto?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var entity = await dbContext.InventoryCounts
            .AsNoTracking()
            .AsSplitQuery()
            .Include(count => count.Warehouse)
            .Include(count => count.Lines.OrderBy(line => line.LineNo))
                .ThenInclude(line => line.Item)
            .Include(count => count.Lines)
                .ThenInclude(line => line.Uom)
            .SingleOrDefaultAsync(count => count.Id == id, cancellationToken);

        return entity is null ? null : ToDto(entity);
    }

    public async Task<InventoryCountDto> CreateDraftAsync(
        UpsertInventoryCountRequest request,
        string actor,
        CancellationToken cancellationToken)
    {
        await ValidateRequestAsync(request, cancellationToken);

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var snapshotAt = DateTime.UtcNow;
        var entity = new InventoryCount
        {
            CountNo = await documentNumberService.GenerateAsync(
                new DocumentNumberRequest(
                    DocumentType,
                    DocumentPrefix,
                    DocumentPaddingLength,
                    await GetMinimumNextCountNumberAsync(cancellationToken)),
                actor,
                cancellationToken),
            CountDate = request.CountDate!.Value,
            SnapshotAt = snapshotAt,
            WarehouseId = request.WarehouseId,
            Notes = Normalize(request.Notes),
            Status = DocumentStatus.Draft,
            CreatedBy = actor
        };

        dbContext.InventoryCounts.Add(entity);
        await AddLinesAsync(entity, request.Lines, actor, cancellationToken);
        await RefreshLineSystemQuantitiesAsync(entity, snapshotAt, actor, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return await GetRequiredAsync(entity.Id, cancellationToken);
    }

    public async Task<InventoryCountDto?> UpdateDraftAsync(
        Guid id,
        UpsertInventoryCountRequest request,
        string actor,
        CancellationToken cancellationToken)
    {
        var entity = await dbContext.InventoryCounts
            .Include(count => count.Lines)
            .SingleOrDefaultAsync(count => count.Id == id, cancellationToken);

        if (entity is null)
        {
            return null;
        }

        EnsureEditable(entity);
        await ValidateRequestAsync(request, cancellationToken);

        var snapshotAt = DateTime.UtcNow;
        entity.CountDate = request.CountDate!.Value;
        entity.SnapshotAt = snapshotAt;
        entity.WarehouseId = request.WarehouseId;
        entity.Notes = Normalize(request.Notes);
        entity.UpdatedBy = actor;
        entity.Version++;

        dbContext.InventoryCountLines.RemoveRange(entity.Lines);
        entity.Lines.Clear();
        await AddLinesAsync(entity, request.Lines, actor, cancellationToken);
        await RefreshLineSystemQuantitiesAsync(entity, snapshotAt, actor, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return await GetRequiredAsync(entity.Id, cancellationToken);
    }

    public async Task<bool> DeleteDraftAsync(Guid id, CancellationToken cancellationToken)
    {
        var entity = await dbContext.InventoryCounts
            .Include(count => count.Lines)
            .SingleOrDefaultAsync(count => count.Id == id, cancellationToken);

        if (entity is null)
        {
            return false;
        }

        EnsureEditable(entity);
        dbContext.InventoryCounts.Remove(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<InventoryCountDto?> RefreshSystemQuantitiesAsync(
        Guid id,
        string actor,
        CancellationToken cancellationToken)
    {
        var entity = await dbContext.InventoryCounts
            .Include(count => count.Lines)
            .SingleOrDefaultAsync(count => count.Id == id, cancellationToken);

        if (entity is null)
        {
            return null;
        }

        EnsureEditable(entity);
        var snapshotAt = DateTime.UtcNow;
        entity.SnapshotAt = snapshotAt;
        entity.UpdatedBy = actor;
        entity.Version++;
        await RefreshLineSystemQuantitiesAsync(entity, snapshotAt, actor, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return await GetRequiredAsync(entity.Id, cancellationToken);
    }

    private async Task ValidateRequestAsync(
        UpsertInventoryCountRequest request,
        CancellationToken cancellationToken)
    {
        var warehouseExists = await dbContext.Warehouses
            .AnyAsync(entity => entity.Id == request.WarehouseId && entity.IsActive, cancellationToken);
        if (!warehouseExists)
        {
            throw new InvalidOperationException("Warehouse was not found.");
        }

        var itemIds = request.Lines.Select(line => line.ItemId).Distinct().ToArray();
        var itemCount = await dbContext.Items
            .CountAsync(entity => itemIds.Contains(entity.Id) && entity.IsActive, cancellationToken);
        if (itemCount != itemIds.Length)
        {
            throw new InvalidOperationException("One or more item references were not found.");
        }

        var uomIds = request.Lines.Select(line => line.UomId).Distinct().ToArray();
        var uomCount = await dbContext.Uoms
            .CountAsync(entity => uomIds.Contains(entity.Id) && entity.IsActive, cancellationToken);
        if (uomCount != uomIds.Length)
        {
            throw new InvalidOperationException("One or more UOM references were not found.");
        }

        if (request.Lines.GroupBy(line => line.LineNo).Any(group => group.Count() > 1))
        {
            throw new DuplicateEntityException("Line numbers must be unique inside the inventory count.");
        }

        if (request.Lines.GroupBy(line => line.ItemId).Any(group => group.Count() > 1))
        {
            throw new DuplicateEntityException("The same item cannot appear more than once inside the inventory count.");
        }
    }

    private async Task AddLinesAsync(
        InventoryCount entity,
        IReadOnlyList<UpsertInventoryCountLineRequest> lines,
        string actor,
        CancellationToken cancellationToken)
    {
        var itemIds = lines.Select(line => line.ItemId).Distinct().ToArray();
        var items = await dbContext.Items
            .AsNoTracking()
            .Where(item => itemIds.Contains(item.Id))
            .ToDictionaryAsync(item => item.Id, cancellationToken);

        foreach (var line in lines.OrderBy(line => line.LineNo))
        {
            var item = items[line.ItemId];
            decimal baseCountedQty;

            try
            {
                baseCountedQty = await quantityConversionService.ConvertAsync(
                    line.CountedQty,
                    line.UomId,
                    item.BaseUomId,
                    cancellationToken);
            }
            catch (InvalidOperationException exception) when (exception.Message.Contains("global UOM conversion", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Inventory count line {line.LineNo} requires a global UOM conversion from the count UOM to the item base UOM.");
            }

            if (baseCountedQty < 0m)
            {
                throw new InvalidOperationException($"Inventory count line {line.LineNo} counted quantity cannot be negative.");
            }

            entity.Lines.Add(new InventoryCountLine
            {
                LineNo = line.LineNo,
                ItemId = line.ItemId,
                UomId = line.UomId,
                SystemQty = 0m,
                CountedQty = Round(line.CountedQty),
                VarianceQty = Round(line.CountedQty),
                BaseSystemQty = 0m,
                BaseCountedQty = Round(baseCountedQty),
                BaseVarianceQty = Round(baseCountedQty),
                Notes = Normalize(line.Notes),
                CreatedBy = actor
            });
        }
    }

    private async Task RefreshLineSystemQuantitiesAsync(
        InventoryCount entity,
        DateTime snapshotAt,
        string actor,
        CancellationToken cancellationToken)
    {
        foreach (var line in entity.Lines.OrderBy(line => line.LineNo))
        {
            var baseSystemQty = await GetCurrentBaseStockAsync(line.ItemId, entity.WarehouseId, cancellationToken);
            var uomSystemQty = await ConvertFromBaseAsync(line.ItemId, baseSystemQty, line.UomId, cancellationToken);

            line.SystemQty = Round(uomSystemQty);
            line.BaseSystemQty = Round(baseSystemQty);
            line.VarianceQty = Round(line.CountedQty - line.SystemQty);
            line.BaseVarianceQty = Round(line.BaseCountedQty - line.BaseSystemQty);
            line.UpdatedBy = actor;
        }

        entity.SnapshotAt = snapshotAt;
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

    private async Task<decimal> ConvertFromBaseAsync(
        Guid itemId,
        decimal baseQty,
        Guid toUomId,
        CancellationToken cancellationToken)
    {
        if (baseQty == 0m)
        {
            return 0m;
        }

        var baseUomId = await dbContext.Items
            .AsNoTracking()
            .Where(entity => entity.Id == itemId)
            .Select(entity => entity.BaseUomId)
            .SingleAsync(cancellationToken);

        return await quantityConversionService.ConvertAsync(
            baseQty,
            baseUomId,
            toUomId,
            cancellationToken);
    }

    private async Task<int> GetMinimumNextCountNumberAsync(CancellationToken cancellationToken)
    {
        var existingNumbers = await dbContext.InventoryCounts
            .AsNoTracking()
            .Select(entity => entity.CountNo)
            .ToListAsync(cancellationToken);

        var maxValue = 0;
        foreach (var countNo in existingNumbers)
        {
            if (TryParseGeneratedCountNumber(countNo, out var value) && value > maxValue)
            {
                maxValue = value;
            }
        }

        return maxValue + 1;
    }

    private static bool TryParseGeneratedCountNumber(string countNo, out int value)
    {
        value = 0;
        if (!countNo.StartsWith(DocumentPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var suffix = countNo[DocumentPrefix.Length..];
        return suffix.Length == DocumentPaddingLength &&
               suffix.All(char.IsDigit) &&
               int.TryParse(suffix, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out value);
    }

    private async Task<InventoryCountDto> GetRequiredAsync(Guid id, CancellationToken cancellationToken)
    {
        return await GetAsync(id, cancellationToken)
            ?? throw new InvalidOperationException("Inventory count was not found after save.");
    }

    private static InventoryCountDto ToDto(InventoryCount entity)
    {
        return new InventoryCountDto(
            entity.Id,
            entity.CountNo,
            entity.CountDate,
            entity.SnapshotAt,
            entity.WarehouseId,
            entity.Warehouse?.Code ?? string.Empty,
            entity.Warehouse?.Name ?? string.Empty,
            entity.Notes,
            entity.Status,
            entity.Lines.OrderBy(line => line.LineNo).Select(line => new InventoryCountLineDto(
                line.Id,
                line.LineNo,
                line.ItemId,
                line.Item?.Code ?? string.Empty,
                line.Item?.Name ?? string.Empty,
                line.UomId,
                line.Uom?.Code ?? string.Empty,
                line.Uom?.Name ?? string.Empty,
                line.SystemQty,
                line.CountedQty,
                line.VarianceQty,
                line.BaseSystemQty,
                line.BaseCountedQty,
                line.BaseVarianceQty,
                line.Notes,
                line.CreatedAt,
                line.UpdatedAt)).ToList(),
            entity.CreatedAt,
            entity.CreatedBy,
            entity.UpdatedAt,
            entity.UpdatedBy,
            entity.PostedAt,
            entity.PostedBy,
            entity.CanceledAt,
            entity.CanceledBy);
    }

    private static void EnsureEditable(InventoryCount entity)
    {
        if (entity.Status != DocumentStatus.Draft)
        {
            throw new InvalidOperationException("Only Draft inventory counts can be edited.");
        }
    }

    private static IQueryable<InventoryCount> ApplySorting(
        IQueryable<InventoryCount> query,
        InventoryCountListQuery request)
    {
        var sortBy = ResolveSortBy(request.SortBy);
        var ascending = request.SortDirection == SortDirection.Asc;

        return (sortBy, ascending) switch
        {
            ("countNo", true) => query.OrderBy(entity => entity.CountNo).ThenBy(entity => entity.Id),
            ("countNo", false) => query.OrderByDescending(entity => entity.CountNo).ThenByDescending(entity => entity.Id),
            ("warehouseName", true) => query.OrderBy(entity => entity.Warehouse!.Name).ThenBy(entity => entity.CountDate),
            ("warehouseName", false) => query.OrderByDescending(entity => entity.Warehouse!.Name).ThenByDescending(entity => entity.CountDate),
            ("status", true) => query.OrderBy(entity => entity.Status).ThenBy(entity => entity.CountDate),
            ("status", false) => query.OrderByDescending(entity => entity.Status).ThenByDescending(entity => entity.CountDate),
            _ when ascending => query.OrderBy(entity => entity.CountDate).ThenBy(entity => entity.CreatedAt),
            _ => query.OrderByDescending(entity => entity.CountDate).ThenByDescending(entity => entity.CreatedAt)
        };
    }

    private static string ResolveSortBy(string? sortBy)
    {
        return string.IsNullOrWhiteSpace(sortBy) ? "countDate" : sortBy.Trim();
    }

    private static string? Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static decimal Round(decimal value)
    {
        return decimal.Round(value, 6, MidpointRounding.AwayFromZero);
    }
}
