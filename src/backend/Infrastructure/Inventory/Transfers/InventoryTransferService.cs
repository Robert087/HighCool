using ERP.Application.Common.Exceptions;
using ERP.Application.Common.Numbering;
using ERP.Application.Common.Pagination;
using ERP.Application.Inventory.Transfers;
using ERP.Application.Purchasing.PurchaseReceipts;
using ERP.Domain.Common;
using ERP.Domain.Inventory;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Inventory.Transfers;

public sealed class InventoryTransferService(
    AppDbContext dbContext,
    IQuantityConversionService quantityConversionService,
    IDocumentNumberService documentNumberService) : IInventoryTransferService
{
    private const string DocumentType = "InventoryTransfer";
    private const string DocumentPrefix = "TRF-";
    private const int DocumentPaddingLength = 6;

    public async Task<PagedResult<InventoryTransferListItemDto>> ListAsync(
        InventoryTransferListQuery query,
        CancellationToken cancellationToken)
    {
        var pagination = new PaginationRequest(query.Page, query.PageSize);
        var transfers = dbContext.InventoryTransfers
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var value = query.Search.Trim();
            transfers = transfers.Where(entity =>
                entity.TransferNo.Contains(value) ||
                entity.SourceWarehouse!.Code.Contains(value) ||
                entity.SourceWarehouse.Name.Contains(value) ||
                entity.DestinationWarehouse!.Code.Contains(value) ||
                entity.DestinationWarehouse.Name.Contains(value) ||
                (entity.Notes != null && entity.Notes.Contains(value)));
        }

        if (!string.IsNullOrWhiteSpace(query.TransferNo))
        {
            var value = query.TransferNo.Trim();
            transfers = transfers.Where(entity => entity.TransferNo.Contains(value));
        }

        if (query.SourceWarehouseId.HasValue)
        {
            transfers = transfers.Where(entity => entity.SourceWarehouseId == query.SourceWarehouseId.Value);
        }

        if (query.DestinationWarehouseId.HasValue)
        {
            transfers = transfers.Where(entity => entity.DestinationWarehouseId == query.DestinationWarehouseId.Value);
        }

        if (query.Status.HasValue)
        {
            transfers = transfers.Where(entity => entity.Status == query.Status.Value);
        }

        if (query.FromDate.HasValue)
        {
            transfers = transfers.Where(entity => entity.TransferDate >= query.FromDate.Value);
        }

        if (query.ToDate.HasValue)
        {
            transfers = transfers.Where(entity => entity.TransferDate <= query.ToDate.Value);
        }

        var totalCount = await transfers.CountAsync(cancellationToken);
        var items = await ApplySorting(transfers, query)
            .Skip(pagination.Skip)
            .Take(pagination.NormalizedPageSize)
            .Select(entity => new InventoryTransferListItemDto(
                entity.Id,
                entity.TransferNo,
                entity.TransferDate,
                entity.SourceWarehouseId,
                entity.SourceWarehouse!.Code,
                entity.SourceWarehouse.Name,
                entity.DestinationWarehouseId,
                entity.DestinationWarehouse!.Code,
                entity.DestinationWarehouse.Name,
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

        return new PagedResult<InventoryTransferListItemDto>(
            items,
            pagination.NormalizedPage,
            pagination.NormalizedPageSize,
            totalCount,
            totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pagination.NormalizedPageSize),
            new
            {
                query.Search,
                query.TransferNo,
                query.SourceWarehouseId,
                query.DestinationWarehouseId,
                query.Status,
                query.FromDate,
                query.ToDate
            },
            new PagedSort(ResolveSortBy(query.SortBy), query.SortDirection));
    }

    public async Task<InventoryTransferDto?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var entity = await dbContext.InventoryTransfers
            .AsNoTracking()
            .AsSplitQuery()
            .Include(transfer => transfer.SourceWarehouse)
            .Include(transfer => transfer.DestinationWarehouse)
            .Include(transfer => transfer.Lines.OrderBy(line => line.LineNo))
                .ThenInclude(line => line.Item)
            .Include(transfer => transfer.Lines)
                .ThenInclude(line => line.Uom)
            .SingleOrDefaultAsync(transfer => transfer.Id == id, cancellationToken);

        return entity is null ? null : ToDto(entity);
    }

    public async Task<InventoryTransferDto> CreateDraftAsync(
        UpsertInventoryTransferRequest request,
        string actor,
        CancellationToken cancellationToken)
    {
        await ValidateRequestAsync(request, cancellationToken);

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var entity = new InventoryTransfer
        {
            TransferNo = await documentNumberService.GenerateAsync(
                new DocumentNumberRequest(
                    DocumentType,
                    DocumentPrefix,
                    DocumentPaddingLength,
                    await GetMinimumNextTransferNumberAsync(cancellationToken)),
                actor,
                cancellationToken),
            TransferDate = request.TransferDate!.Value,
            SourceWarehouseId = request.SourceWarehouseId,
            DestinationWarehouseId = request.DestinationWarehouseId,
            Notes = Normalize(request.Notes),
            Status = DocumentStatus.Draft,
            CreatedBy = actor
        };

        dbContext.InventoryTransfers.Add(entity);
        await AddLinesAsync(entity, request.Lines, actor, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return await GetRequiredAsync(entity.Id, cancellationToken);
    }

    public async Task<InventoryTransferDto?> UpdateDraftAsync(
        Guid id,
        UpsertInventoryTransferRequest request,
        string actor,
        CancellationToken cancellationToken)
    {
        var entity = await dbContext.InventoryTransfers
            .Include(transfer => transfer.Lines)
            .SingleOrDefaultAsync(transfer => transfer.Id == id, cancellationToken);

        if (entity is null)
        {
            return null;
        }

        EnsureEditable(entity);
        await ValidateRequestAsync(request, cancellationToken);

        entity.TransferDate = request.TransferDate!.Value;
        entity.SourceWarehouseId = request.SourceWarehouseId;
        entity.DestinationWarehouseId = request.DestinationWarehouseId;
        entity.Notes = Normalize(request.Notes);
        entity.UpdatedBy = actor;
        entity.Version++;

        dbContext.InventoryTransferLines.RemoveRange(entity.Lines);
        entity.Lines.Clear();
        await AddLinesAsync(entity, request.Lines, actor, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return await GetRequiredAsync(entity.Id, cancellationToken);
    }

    public async Task<bool> DeleteDraftAsync(Guid id, CancellationToken cancellationToken)
    {
        var entity = await dbContext.InventoryTransfers
            .Include(transfer => transfer.Lines)
            .SingleOrDefaultAsync(transfer => transfer.Id == id, cancellationToken);

        if (entity is null)
        {
            return false;
        }

        EnsureEditable(entity);
        dbContext.InventoryTransfers.Remove(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task ValidateRequestAsync(
        UpsertInventoryTransferRequest request,
        CancellationToken cancellationToken)
    {
        if (request.SourceWarehouseId == request.DestinationWarehouseId)
        {
            throw new InvalidOperationException("Source warehouse and destination warehouse must be different.");
        }

        var warehouseIds = new[] { request.SourceWarehouseId, request.DestinationWarehouseId };
        var warehouseCount = await dbContext.Warehouses
            .CountAsync(entity => warehouseIds.Contains(entity.Id) && entity.IsActive, cancellationToken);
        if (warehouseCount != 2)
        {
            throw new InvalidOperationException("Source and destination warehouses must be active.");
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
            throw new DuplicateEntityException("Line numbers must be unique inside the inventory transfer.");
        }
    }

    private async Task AddLinesAsync(
        InventoryTransfer entity,
        IReadOnlyList<UpsertInventoryTransferLineRequest> lines,
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
                throw new InvalidOperationException($"Inventory transfer line {line.LineNo} requires a global UOM conversion from the transfer UOM to the item base UOM.");
            }

            if (baseQty <= 0m)
            {
                throw new InvalidOperationException($"Inventory transfer line {line.LineNo} base quantity must be greater than zero.");
            }

            entity.Lines.Add(new InventoryTransferLine
            {
                LineNo = line.LineNo,
                ItemId = line.ItemId,
                UomId = line.UomId,
                Quantity = Round(line.Quantity),
                BaseQty = Round(baseQty),
                Notes = Normalize(line.Notes),
                CreatedBy = actor
            });
        }
    }

    private async Task<int> GetMinimumNextTransferNumberAsync(CancellationToken cancellationToken)
    {
        var existingNumbers = await dbContext.InventoryTransfers
            .AsNoTracking()
            .Select(entity => entity.TransferNo)
            .ToListAsync(cancellationToken);

        var maxValue = 0;
        foreach (var transferNo in existingNumbers)
        {
            if (TryParseGeneratedTransferNumber(transferNo, out var value) && value > maxValue)
            {
                maxValue = value;
            }
        }

        return maxValue + 1;
    }

    private static bool TryParseGeneratedTransferNumber(string transferNo, out int value)
    {
        value = 0;
        if (!transferNo.StartsWith(DocumentPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var suffix = transferNo[DocumentPrefix.Length..];
        return suffix.Length == DocumentPaddingLength &&
               suffix.All(char.IsDigit) &&
               int.TryParse(suffix, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out value);
    }

    private async Task<InventoryTransferDto> GetRequiredAsync(Guid id, CancellationToken cancellationToken)
    {
        return await GetAsync(id, cancellationToken)
            ?? throw new InvalidOperationException("Inventory transfer was not found after save.");
    }

    private static InventoryTransferDto ToDto(InventoryTransfer entity)
    {
        return new InventoryTransferDto(
            entity.Id,
            entity.TransferNo,
            entity.TransferDate,
            entity.SourceWarehouseId,
            entity.SourceWarehouse?.Code ?? string.Empty,
            entity.SourceWarehouse?.Name ?? string.Empty,
            entity.DestinationWarehouseId,
            entity.DestinationWarehouse?.Code ?? string.Empty,
            entity.DestinationWarehouse?.Name ?? string.Empty,
            entity.Notes,
            entity.Status,
            entity.Lines.OrderBy(line => line.LineNo).Select(line => new InventoryTransferLineDto(
                line.Id,
                line.LineNo,
                line.ItemId,
                line.Item?.Code ?? string.Empty,
                line.Item?.Name ?? string.Empty,
                line.UomId,
                line.Uom?.Code ?? string.Empty,
                line.Uom?.Name ?? string.Empty,
                line.Quantity,
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

    private static void EnsureEditable(InventoryTransfer entity)
    {
        if (entity.Status != DocumentStatus.Draft)
        {
            throw new InvalidOperationException("Only Draft inventory transfers can be edited.");
        }
    }

    private static IQueryable<InventoryTransfer> ApplySorting(
        IQueryable<InventoryTransfer> query,
        InventoryTransferListQuery request)
    {
        var sortBy = ResolveSortBy(request.SortBy);
        var ascending = request.SortDirection == SortDirection.Asc;

        return (sortBy, ascending) switch
        {
            ("transferNo", true) => query.OrderBy(entity => entity.TransferNo).ThenBy(entity => entity.Id),
            ("transferNo", false) => query.OrderByDescending(entity => entity.TransferNo).ThenByDescending(entity => entity.Id),
            ("sourceWarehouseName", true) => query.OrderBy(entity => entity.SourceWarehouse!.Name).ThenBy(entity => entity.TransferDate),
            ("sourceWarehouseName", false) => query.OrderByDescending(entity => entity.SourceWarehouse!.Name).ThenByDescending(entity => entity.TransferDate),
            ("destinationWarehouseName", true) => query.OrderBy(entity => entity.DestinationWarehouse!.Name).ThenBy(entity => entity.TransferDate),
            ("destinationWarehouseName", false) => query.OrderByDescending(entity => entity.DestinationWarehouse!.Name).ThenByDescending(entity => entity.TransferDate),
            ("status", true) => query.OrderBy(entity => entity.Status).ThenBy(entity => entity.TransferDate),
            ("status", false) => query.OrderByDescending(entity => entity.Status).ThenByDescending(entity => entity.TransferDate),
            _ when ascending => query.OrderBy(entity => entity.TransferDate).ThenBy(entity => entity.CreatedAt),
            _ => query.OrderByDescending(entity => entity.TransferDate).ThenByDescending(entity => entity.CreatedAt)
        };
    }

    private static string ResolveSortBy(string? sortBy)
    {
        return string.IsNullOrWhiteSpace(sortBy) ? "transferDate" : sortBy.Trim();
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
