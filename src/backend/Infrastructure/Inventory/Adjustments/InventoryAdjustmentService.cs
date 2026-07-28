using ERP.Application.Common.Exceptions;
using ERP.Application.Common.Numbering;
using ERP.Application.Common.Pagination;
using ERP.Application.Inventory.Adjustments;
using ERP.Application.Purchasing.PurchaseReceipts;
using ERP.Domain.Common;
using ERP.Domain.Inventory;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Inventory.Adjustments;

public sealed class InventoryAdjustmentService(
    AppDbContext dbContext,
    IQuantityConversionService quantityConversionService,
    IDocumentNumberService documentNumberService) : IInventoryAdjustmentService
{
    private const string DocumentType = "InventoryAdjustment";
    private const string DocumentPrefix = "ADJ-";
    private const int DocumentPaddingLength = 6;

    public async Task<PagedResult<InventoryAdjustmentListItemDto>> ListAsync(
        InventoryAdjustmentListQuery query,
        CancellationToken cancellationToken)
    {
        var pagination = new PaginationRequest(query.Page, query.PageSize);
        var adjustments = dbContext.InventoryAdjustments
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var value = query.Search.Trim();
            adjustments = adjustments.Where(entity =>
                entity.AdjustmentNo.Contains(value) ||
                entity.Reason.Contains(value) ||
                entity.Warehouse!.Code.Contains(value) ||
                entity.Warehouse.Name.Contains(value) ||
                (entity.Notes != null && entity.Notes.Contains(value)));
        }

        if (!string.IsNullOrWhiteSpace(query.AdjustmentNo))
        {
            var value = query.AdjustmentNo.Trim();
            adjustments = adjustments.Where(entity => entity.AdjustmentNo.Contains(value));
        }

        if (query.WarehouseId.HasValue)
        {
            adjustments = adjustments.Where(entity => entity.WarehouseId == query.WarehouseId.Value);
        }

        if (query.Status.HasValue)
        {
            adjustments = adjustments.Where(entity => entity.Status == query.Status.Value);
        }

        if (query.FromDate.HasValue)
        {
            adjustments = adjustments.Where(entity => entity.AdjustmentDate >= query.FromDate.Value);
        }

        if (query.ToDate.HasValue)
        {
            adjustments = adjustments.Where(entity => entity.AdjustmentDate <= query.ToDate.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Reason))
        {
            var value = query.Reason.Trim();
            adjustments = adjustments.Where(entity => entity.Reason.Contains(value));
        }

        var totalCount = await adjustments.CountAsync(cancellationToken);
        var items = await ApplySorting(adjustments, query)
            .Skip(pagination.Skip)
            .Take(pagination.NormalizedPageSize)
            .Select(entity => new InventoryAdjustmentListItemDto(
                entity.Id,
                entity.AdjustmentNo,
                entity.AdjustmentDate,
                entity.WarehouseId,
                entity.Warehouse!.Code,
                entity.Warehouse.Name,
                entity.Status,
                entity.Reason,
                entity.Lines.Count,
                entity.CreatedBy,
                entity.CreatedAt,
                entity.UpdatedAt,
                entity.PostedAt,
                entity.PostedBy,
                entity.CanceledAt,
                entity.CanceledBy))
            .ToListAsync(cancellationToken);

        return new PagedResult<InventoryAdjustmentListItemDto>(
            items,
            pagination.NormalizedPage,
            pagination.NormalizedPageSize,
            totalCount,
            totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pagination.NormalizedPageSize),
            new
            {
                query.Search,
                query.AdjustmentNo,
                query.WarehouseId,
                query.Status,
                query.FromDate,
                query.ToDate,
                query.Reason
            },
            new PagedSort(ResolveSortBy(query.SortBy), query.SortDirection));
    }

    public async Task<InventoryAdjustmentDto?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var entity = await dbContext.InventoryAdjustments
            .AsNoTracking()
            .AsSplitQuery()
            .Include(adjustment => adjustment.Warehouse)
            .Include(adjustment => adjustment.Lines.OrderBy(line => line.LineNo))
                .ThenInclude(line => line.Item)
            .Include(adjustment => adjustment.Lines)
                .ThenInclude(line => line.Uom)
            .SingleOrDefaultAsync(adjustment => adjustment.Id == id, cancellationToken);

        return entity is null ? null : ToDto(entity);
    }

    public async Task<InventoryAdjustmentDto> CreateDraftAsync(
        UpsertInventoryAdjustmentRequest request,
        string actor,
        CancellationToken cancellationToken)
    {
        await ValidateRequestAsync(request, null, cancellationToken);

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var entity = new InventoryAdjustment
        {
            AdjustmentNo = await documentNumberService.GenerateAsync(
                new DocumentNumberRequest(
                    DocumentType,
                    DocumentPrefix,
                    DocumentPaddingLength,
                    await GetMinimumNextAdjustmentNumberAsync(cancellationToken)),
                actor,
                cancellationToken),
            AdjustmentDate = request.AdjustmentDate!.Value,
            WarehouseId = request.WarehouseId,
            Reason = request.Reason.Trim(),
            Notes = Normalize(request.Notes),
            Status = DocumentStatus.Draft,
            CreatedBy = actor
        };

        dbContext.InventoryAdjustments.Add(entity);
        await AddLinesAsync(entity, request.Lines, actor, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return await GetRequiredAsync(entity.Id, cancellationToken);
    }

    public async Task<InventoryAdjustmentDto?> UpdateDraftAsync(
        Guid id,
        UpsertInventoryAdjustmentRequest request,
        string actor,
        CancellationToken cancellationToken)
    {
        var entity = await dbContext.InventoryAdjustments
            .Include(adjustment => adjustment.Lines)
            .SingleOrDefaultAsync(adjustment => adjustment.Id == id, cancellationToken);

        if (entity is null)
        {
            return null;
        }

        EnsureEditable(entity);
        await ValidateRequestAsync(request, id, cancellationToken);

        entity.AdjustmentDate = request.AdjustmentDate!.Value;
        entity.WarehouseId = request.WarehouseId;
        entity.Reason = request.Reason.Trim();
        entity.Notes = Normalize(request.Notes);
        entity.UpdatedBy = actor;
        entity.Version++;

        dbContext.InventoryAdjustmentLines.RemoveRange(entity.Lines);
        entity.Lines.Clear();
        await AddLinesAsync(entity, request.Lines, actor, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return await GetRequiredAsync(entity.Id, cancellationToken);
    }

    public async Task<bool> DeleteDraftAsync(Guid id, CancellationToken cancellationToken)
    {
        var entity = await dbContext.InventoryAdjustments
            .Include(adjustment => adjustment.Lines)
            .SingleOrDefaultAsync(adjustment => adjustment.Id == id, cancellationToken);

        if (entity is null)
        {
            return false;
        }

        EnsureEditable(entity);
        dbContext.InventoryAdjustments.Remove(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task ValidateRequestAsync(
        UpsertInventoryAdjustmentRequest request,
        Guid? currentId,
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
            throw new DuplicateEntityException("Line numbers must be unique inside the inventory adjustment.");
        }

        _ = currentId;
    }

    private async Task AddLinesAsync(
        InventoryAdjustment entity,
        IReadOnlyList<UpsertInventoryAdjustmentLineRequest> lines,
        string actor,
        CancellationToken cancellationToken)
    {
        var items = await dbContext.Items
            .AsNoTracking()
            .Where(item => lines.Select(line => line.ItemId).Contains(item.Id))
            .ToDictionaryAsync(item => item.Id, cancellationToken);

        foreach (var line in lines.OrderBy(line => line.LineNo))
        {
            var item = items[line.ItemId];
            decimal baseQty;

            try
            {
                baseQty = await quantityConversionService.ConvertAsync(
                    line.Quantity,
                    line.UomId,
                    item.BaseUomId,
                    cancellationToken);
            }
            catch (InvalidOperationException exception) when (exception.Message.Contains("global UOM conversion", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Inventory adjustment line {line.LineNo} requires a global UOM conversion from the adjustment UOM to the item base UOM.");
            }

            if (baseQty <= 0m)
            {
                throw new InvalidOperationException($"Inventory adjustment line {line.LineNo} base quantity must be greater than zero.");
            }

            entity.Lines.Add(new InventoryAdjustmentLine
            {
                LineNo = line.LineNo,
                ItemId = line.ItemId,
                UomId = line.UomId,
                Quantity = Round(line.Quantity),
                AdjustmentType = line.AdjustmentType,
                BaseQty = Round(baseQty),
                Notes = Normalize(line.Notes),
                CreatedBy = actor
            });
        }
    }

    private async Task<int> GetMinimumNextAdjustmentNumberAsync(CancellationToken cancellationToken)
    {
        var existingNumbers = await dbContext.InventoryAdjustments
            .AsNoTracking()
            .Select(entity => entity.AdjustmentNo)
            .ToListAsync(cancellationToken);

        var maxValue = 0;
        foreach (var adjustmentNo in existingNumbers)
        {
            if (TryParseGeneratedAdjustmentNumber(adjustmentNo, out var value) && value > maxValue)
            {
                maxValue = value;
            }
        }

        return maxValue + 1;
    }

    private static bool TryParseGeneratedAdjustmentNumber(string adjustmentNo, out int value)
    {
        value = 0;
        if (!adjustmentNo.StartsWith(DocumentPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var suffix = adjustmentNo[DocumentPrefix.Length..];
        return suffix.Length == DocumentPaddingLength &&
               suffix.All(char.IsDigit) &&
               int.TryParse(suffix, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out value);
    }

    private async Task<InventoryAdjustmentDto> GetRequiredAsync(Guid id, CancellationToken cancellationToken)
    {
        return await GetAsync(id, cancellationToken)
            ?? throw new InvalidOperationException("Inventory adjustment was not found after save.");
    }

    private static InventoryAdjustmentDto ToDto(InventoryAdjustment entity)
    {
        return new InventoryAdjustmentDto(
            entity.Id,
            entity.AdjustmentNo,
            entity.AdjustmentDate,
            entity.WarehouseId,
            entity.Warehouse?.Code ?? string.Empty,
            entity.Warehouse?.Name ?? string.Empty,
            entity.Reason,
            entity.Notes,
            entity.Status,
            entity.Lines.OrderBy(line => line.LineNo).Select(line => new InventoryAdjustmentLineDto(
                line.Id,
                line.LineNo,
                line.ItemId,
                line.Item?.Code ?? string.Empty,
                line.Item?.Name ?? string.Empty,
                line.UomId,
                line.Uom?.Code ?? string.Empty,
                line.Uom?.Name ?? string.Empty,
                line.Quantity,
                line.AdjustmentType,
                line.BaseQty,
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

    private static void EnsureEditable(InventoryAdjustment entity)
    {
        if (entity.Status != DocumentStatus.Draft)
        {
            throw new InvalidOperationException("Only Draft inventory adjustments can be edited.");
        }
    }

    private static IQueryable<InventoryAdjustment> ApplySorting(
        IQueryable<InventoryAdjustment> query,
        InventoryAdjustmentListQuery request)
    {
        var sortBy = ResolveSortBy(request.SortBy);
        var ascending = request.SortDirection == SortDirection.Asc;

        return (sortBy, ascending) switch
        {
            ("adjustmentNo", true) => query.OrderBy(entity => entity.AdjustmentNo).ThenBy(entity => entity.Id),
            ("adjustmentNo", false) => query.OrderByDescending(entity => entity.AdjustmentNo).ThenByDescending(entity => entity.Id),
            ("warehouseName", true) => query.OrderBy(entity => entity.Warehouse!.Name).ThenBy(entity => entity.AdjustmentDate),
            ("warehouseName", false) => query.OrderByDescending(entity => entity.Warehouse!.Name).ThenByDescending(entity => entity.AdjustmentDate),
            ("status", true) => query.OrderBy(entity => entity.Status).ThenBy(entity => entity.AdjustmentDate),
            ("status", false) => query.OrderByDescending(entity => entity.Status).ThenByDescending(entity => entity.AdjustmentDate),
            _ when ascending => query.OrderBy(entity => entity.AdjustmentDate).ThenBy(entity => entity.CreatedAt),
            _ => query.OrderByDescending(entity => entity.AdjustmentDate).ThenByDescending(entity => entity.CreatedAt)
        };
    }

    private static string ResolveSortBy(string? sortBy)
    {
        return string.IsNullOrWhiteSpace(sortBy) ? "adjustmentDate" : sortBy.Trim();
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
