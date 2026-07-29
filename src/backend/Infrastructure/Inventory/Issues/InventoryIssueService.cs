using ERP.Application.Common.Exceptions;
using ERP.Application.Common.Numbering;
using ERP.Application.Common.Pagination;
using ERP.Application.Inventory.Issues;
using ERP.Application.Purchasing.PurchaseReceipts;
using ERP.Domain.Common;
using ERP.Domain.Inventory;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Inventory.Issues;

public sealed class InventoryIssueService(
    AppDbContext dbContext,
    IQuantityConversionService quantityConversionService,
    IDocumentNumberService documentNumberService) : IInventoryIssueService
{
    private const string DocumentType = "InventoryIssue";
    private const string DocumentPrefix = "ISS-";
    private const int DocumentPaddingLength = 6;

    public async Task<PagedResult<InventoryIssueListItemDto>> ListAsync(
        InventoryIssueListQuery query,
        CancellationToken cancellationToken)
    {
        var pagination = new PaginationRequest(query.Page, query.PageSize);
        var issues = dbContext.InventoryIssues
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var value = query.Search.Trim();
            issues = issues.Where(entity =>
                entity.IssueNo.Contains(value) ||
                entity.Warehouse!.Code.Contains(value) ||
                entity.Warehouse.Name.Contains(value) ||
                (entity.ReferenceNo != null && entity.ReferenceNo.Contains(value)) ||
                (entity.RequestedBy != null && entity.RequestedBy.Contains(value)) ||
                (entity.Notes != null && entity.Notes.Contains(value)));
        }

        if (!string.IsNullOrWhiteSpace(query.IssueNo))
        {
            var value = query.IssueNo.Trim();
            issues = issues.Where(entity => entity.IssueNo.Contains(value));
        }

        if (query.WarehouseId.HasValue)
        {
            issues = issues.Where(entity => entity.WarehouseId == query.WarehouseId.Value);
        }

        if (query.Reason.HasValue)
        {
            issues = issues.Where(entity => entity.Reason == query.Reason.Value);
        }

        if (query.Status.HasValue)
        {
            issues = issues.Where(entity => entity.Status == query.Status.Value);
        }

        if (query.FromDate.HasValue)
        {
            issues = issues.Where(entity => entity.IssueDate >= query.FromDate.Value);
        }

        if (query.ToDate.HasValue)
        {
            issues = issues.Where(entity => entity.IssueDate <= query.ToDate.Value);
        }

        var totalCount = await issues.CountAsync(cancellationToken);
        var items = await ApplySorting(issues, query)
            .Skip(pagination.Skip)
            .Take(pagination.NormalizedPageSize)
            .Select(entity => new InventoryIssueListItemDto(
                entity.Id,
                entity.IssueNo,
                entity.IssueDate,
                entity.WarehouseId,
                entity.Warehouse!.Code,
                entity.Warehouse.Name,
                entity.Reason,
                entity.ReferenceNo,
                entity.RequestedBy,
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

        return new PagedResult<InventoryIssueListItemDto>(
            items,
            pagination.NormalizedPage,
            pagination.NormalizedPageSize,
            totalCount,
            totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pagination.NormalizedPageSize),
            new
            {
                query.Search,
                query.IssueNo,
                query.WarehouseId,
                query.Reason,
                query.Status,
                query.FromDate,
                query.ToDate
            },
            new PagedSort(ResolveSortBy(query.SortBy), query.SortDirection));
    }

    public async Task<InventoryIssueDto?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var entity = await dbContext.InventoryIssues
            .AsNoTracking()
            .AsSplitQuery()
            .Include(issue => issue.Warehouse)
            .Include(issue => issue.Lines.OrderBy(line => line.LineNo))
                .ThenInclude(line => line.Item)
            .Include(issue => issue.Lines)
                .ThenInclude(line => line.Uom)
            .SingleOrDefaultAsync(issue => issue.Id == id, cancellationToken);

        return entity is null ? null : ToDto(entity);
    }

    public async Task<InventoryIssueDto> CreateDraftAsync(
        UpsertInventoryIssueRequest request,
        string actor,
        CancellationToken cancellationToken)
    {
        await ValidateRequestAsync(request, cancellationToken);

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var entity = new InventoryIssue
        {
            IssueNo = await documentNumberService.GenerateAsync(
                new DocumentNumberRequest(
                    DocumentType,
                    DocumentPrefix,
                    DocumentPaddingLength,
                    MinimumNextValue: 1),
                actor,
                cancellationToken),
            IssueDate = request.IssueDate!.Value,
            WarehouseId = request.WarehouseId,
            Reason = request.Reason!.Value,
            ReferenceNo = Normalize(request.ReferenceNo),
            RequestedBy = Normalize(request.RequestedBy),
            Notes = Normalize(request.Notes),
            Status = DocumentStatus.Draft,
            CreatedBy = actor
        };

        dbContext.InventoryIssues.Add(entity);
        await AddLinesAsync(entity, request.Lines, actor, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return await GetRequiredAsync(entity.Id, cancellationToken);
    }

    public async Task<InventoryIssueDto?> UpdateDraftAsync(
        Guid id,
        UpsertInventoryIssueRequest request,
        string actor,
        CancellationToken cancellationToken)
    {
        var entity = await dbContext.InventoryIssues
            .Include(issue => issue.Lines)
            .SingleOrDefaultAsync(issue => issue.Id == id, cancellationToken);

        if (entity is null)
        {
            return null;
        }

        EnsureEditable(entity);
        await ValidateRequestAsync(request, cancellationToken);

        entity.IssueDate = request.IssueDate!.Value;
        entity.WarehouseId = request.WarehouseId;
        entity.Reason = request.Reason!.Value;
        entity.ReferenceNo = Normalize(request.ReferenceNo);
        entity.RequestedBy = Normalize(request.RequestedBy);
        entity.Notes = Normalize(request.Notes);
        entity.UpdatedBy = actor;
        entity.Version++;

        dbContext.InventoryIssueLines.RemoveRange(entity.Lines);
        entity.Lines.Clear();
        await AddLinesAsync(entity, request.Lines, actor, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return await GetRequiredAsync(entity.Id, cancellationToken);
    }

    public async Task<bool> DeleteDraftAsync(Guid id, CancellationToken cancellationToken)
    {
        var entity = await dbContext.InventoryIssues
            .Include(issue => issue.Lines)
            .SingleOrDefaultAsync(issue => issue.Id == id, cancellationToken);

        if (entity is null)
        {
            return false;
        }

        EnsureEditable(entity);
        dbContext.InventoryIssues.Remove(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task ValidateRequestAsync(
        UpsertInventoryIssueRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Reason is null || !Enum.IsDefined(typeof(InventoryIssueReason), request.Reason.Value))
        {
            throw new InvalidOperationException("Issue reason is required.");
        }

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
            throw new DuplicateEntityException("Line numbers must be unique inside the inventory issue.");
        }

        if (request.Lines.GroupBy(line => line.ItemId).Any(group => group.Count() > 1))
        {
            throw new DuplicateEntityException("The same item cannot appear more than once inside the inventory issue.");
        }
    }

    private async Task AddLinesAsync(
        InventoryIssue entity,
        IReadOnlyList<UpsertInventoryIssueLineRequest> lines,
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
                throw new InvalidOperationException($"Inventory issue line {line.LineNo} requires a global UOM conversion from the issue UOM to the item base UOM.");
            }

            if (baseQty <= 0m)
            {
                throw new InvalidOperationException($"Inventory issue line {line.LineNo} base quantity must be greater than zero.");
            }

            entity.Lines.Add(new InventoryIssueLine
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

    private async Task<InventoryIssueDto> GetRequiredAsync(Guid id, CancellationToken cancellationToken)
    {
        return await GetAsync(id, cancellationToken)
            ?? throw new InvalidOperationException("Inventory issue was not found after save.");
    }

    private static InventoryIssueDto ToDto(InventoryIssue entity)
    {
        return new InventoryIssueDto(
            entity.Id,
            entity.IssueNo,
            entity.IssueDate,
            entity.WarehouseId,
            entity.Warehouse?.Code ?? string.Empty,
            entity.Warehouse?.Name ?? string.Empty,
            entity.Reason,
            entity.ReferenceNo,
            entity.RequestedBy,
            entity.Notes,
            entity.Status,
            entity.Lines.OrderBy(line => line.LineNo).Select(line => new InventoryIssueLineDto(
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

    private static void EnsureEditable(InventoryIssue entity)
    {
        if (entity.Status != DocumentStatus.Draft)
        {
            throw new InvalidOperationException("Only Draft inventory issues can be edited.");
        }
    }

    private static IQueryable<InventoryIssue> ApplySorting(
        IQueryable<InventoryIssue> query,
        InventoryIssueListQuery request)
    {
        var sortBy = ResolveSortBy(request.SortBy);
        var ascending = request.SortDirection == SortDirection.Asc;

        return (sortBy, ascending) switch
        {
            ("issueNo", true) => query.OrderBy(entity => entity.IssueNo).ThenBy(entity => entity.Id),
            ("issueNo", false) => query.OrderByDescending(entity => entity.IssueNo).ThenByDescending(entity => entity.Id),
            ("warehouseName", true) => query.OrderBy(entity => entity.Warehouse!.Name).ThenBy(entity => entity.IssueDate),
            ("warehouseName", false) => query.OrderByDescending(entity => entity.Warehouse!.Name).ThenByDescending(entity => entity.IssueDate),
            ("reason", true) => query.OrderBy(entity => entity.Reason).ThenBy(entity => entity.IssueDate),
            ("reason", false) => query.OrderByDescending(entity => entity.Reason).ThenByDescending(entity => entity.IssueDate),
            ("status", true) => query.OrderBy(entity => entity.Status).ThenBy(entity => entity.IssueDate),
            ("status", false) => query.OrderByDescending(entity => entity.Status).ThenByDescending(entity => entity.IssueDate),
            _ when ascending => query.OrderBy(entity => entity.IssueDate).ThenBy(entity => entity.CreatedAt),
            _ => query.OrderByDescending(entity => entity.IssueDate).ThenByDescending(entity => entity.CreatedAt)
        };
    }

    private static string ResolveSortBy(string? sortBy)
    {
        return string.IsNullOrWhiteSpace(sortBy) ? "issueDate" : sortBy.Trim();
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
