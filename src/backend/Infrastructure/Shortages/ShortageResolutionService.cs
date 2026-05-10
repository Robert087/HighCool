using ERP.Application.Common.Exceptions;
using ERP.Application.Common.Pagination;
using ERP.Application.Shortages;
using ERP.Domain.Common;
using ERP.Domain.Shortages;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Shortages;

public sealed class ShortageResolutionService(AppDbContext dbContext) : IShortageResolutionService
{
    public async Task<PagedResult<ShortageResolutionListItemDto>> ListAsync(ShortageResolutionListQuery query, CancellationToken cancellationToken)
    {
        var pagination = new PaginationRequest(query.Page, query.PageSize);

        var resolutions = dbContext.ShortageResolutions
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            resolutions = resolutions.Where(entity =>
                entity.ResolutionNo.Contains(search) ||
                entity.Supplier!.Code.Contains(search) ||
                entity.Supplier.Name.Contains(search) ||
                (entity.Notes != null && entity.Notes.Contains(search)));
        }

        if (query.SupplierId.HasValue)
        {
            resolutions = resolutions.Where(entity => entity.SupplierId == query.SupplierId.Value);
        }

        if (query.ResolutionType.HasValue)
        {
            resolutions = resolutions.Where(entity => entity.ResolutionType == query.ResolutionType.Value);
        }

        if (query.Status.HasValue)
        {
            resolutions = resolutions.Where(entity => entity.Status == query.Status.Value);
        }

        if (query.FromDate.HasValue)
        {
            resolutions = resolutions.Where(entity => entity.ResolutionDate >= query.FromDate.Value);
        }

        if (query.ToDate.HasValue)
        {
            resolutions = resolutions.Where(entity => entity.ResolutionDate <= query.ToDate.Value);
        }

        resolutions = ApplyResolutionSorting(resolutions, query);

        var totalCount = await resolutions.CountAsync(cancellationToken);
        var items = await resolutions
            .Skip(pagination.Skip)
            .Take(pagination.NormalizedPageSize)
            .Select(entity => new ShortageResolutionListItemDto(
                entity.Id,
                entity.ResolutionNo,
                entity.SupplierId,
                entity.Supplier!.Code,
                entity.Supplier.Name,
                entity.ResolutionType,
                entity.ResolutionDate,
                entity.TotalQty,
                entity.TotalAmount,
                entity.Currency,
                entity.Status,
                entity.Allocations.Count,
                entity.ReversalDocumentId,
                entity.ReversedAt,
                entity.CreatedAt,
                entity.UpdatedAt))
            .ToListAsync(cancellationToken);

        return new PagedResult<ShortageResolutionListItemDto>(
            items,
            pagination.NormalizedPage,
            pagination.NormalizedPageSize,
            totalCount,
            CalculateTotalPages(totalCount, pagination.NormalizedPageSize),
            new
            {
                query.Search,
                query.SupplierId,
                query.ResolutionType,
                query.Status,
                query.FromDate,
                query.ToDate
            },
            new PagedSort(ResolveResolutionSortBy(query.SortBy), query.SortDirection));
    }

    public async Task<ShortageResolutionDto?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var resolution = await IncludeResolutionDetails()
            .SingleOrDefaultAsync(entity => entity.Id == id, cancellationToken);

        return resolution is null ? null : ToDto(resolution);
    }

    public async Task<ShortageResolutionDto> CreateDraftAsync(UpsertShortageResolutionRequest request, string actor, CancellationToken cancellationToken)
    {
        var resolutionNo = await ResolveResolutionNoAsync(request.ResolutionNo, null, cancellationToken);
        var shortageRows = await ValidateDraftRequestAsync(request, cancellationToken);

        var resolution = new ShortageResolution
        {
            ResolutionNo = resolutionNo,
            SupplierId = request.SupplierId,
            ResolutionType = request.ResolutionType!.Value,
            ResolutionDate = request.ResolutionDate!.Value,
            TotalQty = ResolveTotalQty(request),
            TotalAmount = ResolveTotalAmount(request),
            Currency = NormalizeOptionalText(request.Currency),
            Notes = NormalizeOptionalText(request.Notes),
            Status = DocumentStatus.Draft,
            CreatedBy = actor
        };

        dbContext.ShortageResolutions.Add(resolution);
        AddAllocations(resolution, request.Allocations, shortageRows, actor);
        await dbContext.SaveChangesAsync(cancellationToken);

        return await GetRequiredAsync(resolution.Id, cancellationToken);
    }

    public async Task<ShortageResolutionDto?> UpdateDraftAsync(Guid id, UpsertShortageResolutionRequest request, string actor, CancellationToken cancellationToken)
    {
        var resolution = await dbContext.ShortageResolutions
            .Include(entity => entity.Allocations)
            .SingleOrDefaultAsync(entity => entity.Id == id, cancellationToken);

        if (resolution is null)
        {
            return null;
        }

        EnsureDraftIsEditable(resolution);

        resolution.ResolutionNo = await ResolveResolutionNoAsync(request.ResolutionNo, id, cancellationToken);
        var shortageRows = await ValidateDraftRequestAsync(request, cancellationToken);

        resolution.SupplierId = request.SupplierId;
        resolution.ResolutionType = request.ResolutionType!.Value;
        resolution.ResolutionDate = request.ResolutionDate!.Value;
        resolution.TotalQty = ResolveTotalQty(request);
        resolution.TotalAmount = ResolveTotalAmount(request);
        resolution.Currency = NormalizeOptionalText(request.Currency);
        resolution.Notes = NormalizeOptionalText(request.Notes);
        resolution.UpdatedBy = actor;

        dbContext.ShortageResolutionAllocations.RemoveRange(resolution.Allocations);
        AddAllocations(resolution, request.Allocations, shortageRows, actor);

        await dbContext.SaveChangesAsync(cancellationToken);
        return await GetRequiredAsync(resolution.Id, cancellationToken);
    }

    public async Task<IReadOnlyList<ShortageResolutionAllocationDto>> GetAllocationsAsync(Guid id, CancellationToken cancellationToken)
    {
        var resolution = await IncludeResolutionDetails()
            .SingleOrDefaultAsync(entity => entity.Id == id, cancellationToken);

        return resolution?.Allocations
            .OrderBy(entity => entity.SequenceNo)
            .Select(ToAllocationDto)
            .ToList() ?? [];
    }

    public async Task<PagedResult<OpenShortageDto>> ListOpenShortagesAsync(OpenShortageQuery query, CancellationToken cancellationToken)
    {
        var pagination = new PaginationRequest(query.Page, query.PageSize);
        var shortages = BuildOpenShortagesQuery();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            shortages = shortages.Where(entity =>
                entity.PurchaseReceipt!.Supplier!.Code.Contains(search) ||
                entity.PurchaseReceipt.Supplier.Name.Contains(search) ||
                entity.PurchaseReceipt.ReceiptNo.Contains(search) ||
                entity.Item!.Code.Contains(search) ||
                entity.Item.Name.Contains(search) ||
                entity.ComponentItem!.Code.Contains(search) ||
                entity.ComponentItem.Name.Contains(search) ||
                (entity.ShortageReasonCode != null &&
                    (entity.ShortageReasonCode.Code.Contains(search) || entity.ShortageReasonCode.Name.Contains(search))));
        }

        if (query.SupplierId.HasValue)
        {
            shortages = shortages.Where(entity => entity.PurchaseReceipt!.SupplierId == query.SupplierId.Value);
        }

        if (query.ItemId.HasValue)
        {
            shortages = shortages.Where(entity => entity.ItemId == query.ItemId.Value);
        }

        if (query.ComponentItemId.HasValue)
        {
            shortages = shortages.Where(entity => entity.ComponentItemId == query.ComponentItemId.Value);
        }

        if (query.AffectsSupplierBalance.HasValue)
        {
            shortages = shortages.Where(entity => entity.AffectsSupplierBalance == query.AffectsSupplierBalance.Value);
        }

        if (query.Status.HasValue)
        {
            shortages = shortages.Where(entity => entity.Status == query.Status.Value);
        }

        if (query.FromDate.HasValue)
        {
            shortages = shortages.Where(entity => entity.PurchaseReceipt!.ReceiptDate >= query.FromDate.Value);
        }

        if (query.ToDate.HasValue)
        {
            shortages = shortages.Where(entity => entity.PurchaseReceipt!.ReceiptDate <= query.ToDate.Value);
        }

        shortages = ApplyOpenShortageSorting(shortages, query);

        var totalCount = await shortages.CountAsync(cancellationToken);
        var entities = await shortages
            .Skip(pagination.Skip)
            .Take(pagination.NormalizedPageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<OpenShortageDto>(
            entities.Select(ToOpenShortageDto).ToArray(),
            pagination.NormalizedPage,
            pagination.NormalizedPageSize,
            totalCount,
            CalculateTotalPages(totalCount, pagination.NormalizedPageSize),
            new
            {
                query.Search,
                query.SupplierId,
                query.ItemId,
                query.ComponentItemId,
                query.AffectsSupplierBalance,
                query.Status,
                query.FromDate,
                query.ToDate
            },
            new PagedSort(ResolveOpenShortageSortBy(query.SortBy), query.SortDirection));
    }

    public async Task<OpenShortageDto?> GetShortageAsync(Guid id, CancellationToken cancellationToken)
    {
        var shortage = await BuildOpenShortagesQuery(includeResolved: true)
            .SingleOrDefaultAsync(entity => entity.Id == id, cancellationToken);

        return shortage is null ? null : ToOpenShortageDto(shortage);
    }

    public async Task<IReadOnlyList<SuggestedShortageAllocationDto>> SuggestAllocationsAsync(
        SuggestShortageAllocationsQuery query,
        CancellationToken cancellationToken)
    {
        var shortages = await BuildOpenShortagesQuery()
            .Where(entity => entity.PurchaseReceipt!.SupplierId == query.SupplierId)
            .OrderBy(entity => entity.PurchaseReceipt!.ReceiptDate)
            .ThenBy(entity => entity.CreatedAt)
            .ToListAsync(cancellationToken);

        var suggestions = new List<SuggestedShortageAllocationDto>();
        var remainingQty = query.TotalQty;
        var sequence = 1;

        foreach (var shortage in shortages)
        {
            var openQty = GetEffectiveOpenQty(shortage);
            var openAmount = GetEffectiveOpenAmount(shortage);

            if (query.ResolutionType == ShortageResolutionType.Physical)
            {
                var suggestedQty = openQty;
                if (remainingQty.HasValue)
                {
                    if (remainingQty.Value <= 0m)
                    {
                        break;
                    }

                    suggestedQty = Math.Min(openQty, remainingQty.Value);
                    remainingQty -= suggestedQty;
                }

                if (suggestedQty <= 0m)
                {
                    continue;
                }

                suggestions.Add(new SuggestedShortageAllocationDto(
                    shortage.Id,
                    sequence++,
                    "FIFO",
                    suggestedQty,
                    null,
                    null,
                    openQty,
                    openAmount,
                    shortage.PurchaseReceipt!.ReceiptNo,
                    shortage.PurchaseReceipt.ReceiptDate,
                    shortage.Item!.Code,
                    shortage.ComponentItem!.Code));

                continue;
            }

            if (remainingQty.HasValue && remainingQty.Value <= 0m)
            {
                break;
            }

            var suggestedFinancialQty = openQty;
            if (remainingQty.HasValue)
            {
                suggestedFinancialQty = Math.Min(openQty, remainingQty.Value);
                remainingQty -= suggestedFinancialQty;
            }

            if (suggestedFinancialQty <= 0m)
            {
                continue;
            }

            var suggestedRate = shortage.ShortageValue.HasValue && shortage.ShortageQty > 0m
                ? Round(shortage.ShortageValue.Value / shortage.ShortageQty)
                : (decimal?)null;

            suggestions.Add(new SuggestedShortageAllocationDto(
                shortage.Id,
                sequence++,
                "FIFO",
                suggestedFinancialQty,
                suggestedRate.HasValue ? Round(suggestedFinancialQty * suggestedRate.Value) : null,
                suggestedRate,
                openQty,
                openAmount,
                shortage.PurchaseReceipt!.ReceiptNo,
                shortage.PurchaseReceipt.ReceiptDate,
                shortage.Item!.Code,
                shortage.ComponentItem!.Code));
        }

        return suggestions;
    }

    private IQueryable<ShortageResolution> IncludeResolutionDetails()
    {
        return dbContext.ShortageResolutions
            .AsNoTracking()
            .AsSplitQuery()
            .Include(entity => entity.Supplier)
            .Include(entity => entity.Allocations.OrderBy(allocation => allocation.SequenceNo))
                .ThenInclude(entity => entity.ShortageLedgerEntry!)
                    .ThenInclude(entity => entity.PurchaseReceipt)
                        .ThenInclude(entity => entity!.Supplier)
            .Include(entity => entity.Allocations)
                .ThenInclude(entity => entity.ShortageLedgerEntry!)
                    .ThenInclude(entity => entity.PurchaseOrder)
            .Include(entity => entity.Allocations)
                .ThenInclude(entity => entity.ShortageLedgerEntry!)
                    .ThenInclude(entity => entity.Item)
            .Include(entity => entity.Allocations)
                .ThenInclude(entity => entity.ShortageLedgerEntry!)
                    .ThenInclude(entity => entity.ComponentItem);
    }

    private IQueryable<ShortageLedgerEntry> BuildOpenShortagesQuery(bool includeResolved = false)
    {
        var query = dbContext.ShortageLedgerEntries
            .AsNoTracking()
            .AsSplitQuery()
            .Include(entity => entity.PurchaseReceipt)
                .ThenInclude(entity => entity!.Supplier)
            .Include(entity => entity.PurchaseOrder)
            .Include(entity => entity.Item)
            .Include(entity => entity.ComponentItem)
            .Include(entity => entity.ShortageReasonCode)
            .AsQueryable();

        if (!includeResolved)
        {
            query = query.Where(entity =>
                entity.Status != ShortageEntryStatus.Resolved &&
                entity.Status != ShortageEntryStatus.Canceled &&
                (entity.OpenQty > 0m ||
                 entity.ShortageQty > entity.ResolvedPhysicalQty + entity.ResolvedFinancialQtyEquivalent));
        }

        return query;
    }

    private static IQueryable<ShortageResolution> ApplyResolutionSorting(IQueryable<ShortageResolution> query, ShortageResolutionListQuery request)
    {
        var sortBy = ResolveResolutionSortBy(request.SortBy);
        var ascending = request.SortDirection == SortDirection.Asc;

        return (sortBy, ascending) switch
        {
            ("resolutionNo", true) => query.OrderBy(entity => entity.ResolutionNo).ThenBy(entity => entity.Id),
            ("resolutionNo", false) => query.OrderByDescending(entity => entity.ResolutionNo).ThenByDescending(entity => entity.Id),
            ("supplierName", true) => query.OrderBy(entity => entity.Supplier!.Name).ThenBy(entity => entity.ResolutionDate),
            ("supplierName", false) => query.OrderByDescending(entity => entity.Supplier!.Name).ThenByDescending(entity => entity.ResolutionDate),
            ("status", true) => query.OrderBy(entity => entity.Status).ThenBy(entity => entity.ResolutionDate),
            ("status", false) => query.OrderByDescending(entity => entity.Status).ThenByDescending(entity => entity.ResolutionDate),
            _ when ascending => query.OrderBy(entity => entity.ResolutionDate).ThenBy(entity => entity.CreatedAt),
            _ => query.OrderByDescending(entity => entity.ResolutionDate).ThenByDescending(entity => entity.CreatedAt)
        };
    }

    private static IQueryable<ShortageLedgerEntry> ApplyOpenShortageSorting(IQueryable<ShortageLedgerEntry> query, OpenShortageQuery request)
    {
        var sortBy = ResolveOpenShortageSortBy(request.SortBy);
        var ascending = request.SortDirection == SortDirection.Asc;

        return (sortBy, ascending) switch
        {
            ("supplierName", true) => query.OrderBy(entity => entity.PurchaseReceipt!.Supplier!.Name).ThenBy(entity => entity.PurchaseReceipt!.ReceiptDate),
            ("supplierName", false) => query.OrderByDescending(entity => entity.PurchaseReceipt!.Supplier!.Name).ThenByDescending(entity => entity.PurchaseReceipt!.ReceiptDate),
            ("itemCode", true) => query.OrderBy(entity => entity.Item!.Code).ThenBy(entity => entity.PurchaseReceipt!.ReceiptDate),
            ("itemCode", false) => query.OrderByDescending(entity => entity.Item!.Code).ThenByDescending(entity => entity.PurchaseReceipt!.ReceiptDate),
            ("componentItemCode", true) => query.OrderBy(entity => entity.ComponentItem!.Code).ThenBy(entity => entity.PurchaseReceipt!.ReceiptDate),
            ("componentItemCode", false) => query.OrderByDescending(entity => entity.ComponentItem!.Code).ThenByDescending(entity => entity.PurchaseReceipt!.ReceiptDate),
            ("openQty", true) => query.OrderBy(entity => entity.OpenQty).ThenBy(entity => entity.PurchaseReceipt!.ReceiptDate),
            ("openQty", false) => query.OrderByDescending(entity => entity.OpenQty).ThenByDescending(entity => entity.PurchaseReceipt!.ReceiptDate),
            _ when ascending => query.OrderBy(entity => entity.PurchaseReceipt!.ReceiptDate).ThenBy(entity => entity.CreatedAt),
            _ => query.OrderByDescending(entity => entity.PurchaseReceipt!.ReceiptDate).ThenByDescending(entity => entity.CreatedAt)
        };
    }

    private static string ResolveResolutionSortBy(string? sortBy)
    {
        return string.IsNullOrWhiteSpace(sortBy) ? "resolutionDate" : sortBy.Trim();
    }

    private static string ResolveOpenShortageSortBy(string? sortBy)
    {
        return string.IsNullOrWhiteSpace(sortBy) ? "receiptDate" : sortBy.Trim();
    }

    private static int CalculateTotalPages(int totalCount, int pageSize)
    {
        return totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize);
    }

    private async Task<Dictionary<Guid, ShortageLedgerEntry>> ValidateDraftRequestAsync(
        UpsertShortageResolutionRequest request,
        CancellationToken cancellationToken)
    {
        var supplierExists = await dbContext.Suppliers
            .AnyAsync(entity => entity.Id == request.SupplierId && entity.IsActive, cancellationToken);

        if (!supplierExists)
        {
            throw new InvalidOperationException("Supplier was not found.");
        }

        var shortageIds = request.Allocations
            .Select(entity => entity.ShortageLedgerId)
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToArray();

        if (shortageIds.Length == 0)
        {
            return [];
        }

        var shortageRows = await dbContext.ShortageLedgerEntries
            .AsNoTracking()
            .Include(entity => entity.PurchaseReceipt)
            .Include(entity => entity.Item)
            .Include(entity => entity.ComponentItem)
            .Where(entity => shortageIds.Contains(entity.Id))
            .ToDictionaryAsync(entity => entity.Id, cancellationToken);

        if (shortageRows.Count != shortageIds.Length)
        {
            throw new InvalidOperationException("One or more shortage rows were not found.");
        }

        foreach (var shortage in shortageRows.Values)
        {
            if (shortage.PurchaseReceipt?.SupplierId != request.SupplierId)
            {
                throw new InvalidOperationException("All selected shortage rows must belong to the selected supplier.");
            }

            if (shortage.Status == ShortageEntryStatus.Canceled)
            {
                throw new InvalidOperationException("Canceled shortage rows cannot be allocated.");
            }
        }

        return shortageRows;
    }

    private void AddAllocations(
        ShortageResolution resolution,
        IReadOnlyList<UpsertShortageResolutionAllocationRequest> allocations,
        IReadOnlyDictionary<Guid, ShortageLedgerEntry> shortageRows,
        string actor)
    {
        foreach (var request in allocations.OrderBy(entity => entity.SequenceNo))
        {
            if (!shortageRows.ContainsKey(request.ShortageLedgerId))
            {
                continue;
            }

            resolution.Allocations.Add(new ShortageResolutionAllocation
            {
                ResolutionId = resolution.Id,
                ShortageLedgerId = request.ShortageLedgerId,
                AllocationType = resolution.ResolutionType == ShortageResolutionType.Physical
                    ? ShortageAllocationType.Physical
                    : ShortageAllocationType.Financial,
                AllocatedQty = request.AllocatedQty,
                AllocatedAmount = resolution.ResolutionType == ShortageResolutionType.Financial &&
                                  request.AllocatedQty.HasValue &&
                                  request.ValuationRate.HasValue
                    ? Round(request.AllocatedQty.Value * request.ValuationRate.Value)
                    : null,
                ValuationRate = request.ValuationRate,
                FinancialQtyEquivalent = resolution.ResolutionType == ShortageResolutionType.Financial
                    ? request.AllocatedQty
                    : null,
                AllocationMethod = NormalizeOptionalText(request.AllocationMethod) ?? "Manual",
                SequenceNo = request.SequenceNo,
                CreatedBy = actor
            });
        }
    }

    private async Task<ShortageResolutionDto> GetRequiredAsync(Guid id, CancellationToken cancellationToken)
    {
        return await GetAsync(id, cancellationToken)
            ?? throw new InvalidOperationException("Shortage resolution was not found after saving.");
    }

    private async Task<string> ResolveResolutionNoAsync(string? requestedResolutionNo, Guid? currentId, CancellationToken cancellationToken)
    {
        var normalizedResolutionNo = NormalizeOptionalText(requestedResolutionNo);
        if (!string.IsNullOrWhiteSpace(normalizedResolutionNo))
        {
            var duplicateExists = await dbContext.ShortageResolutions
                .AnyAsync(
                    entity => entity.ResolutionNo == normalizedResolutionNo &&
                              (!currentId.HasValue || entity.Id != currentId.Value),
                    cancellationToken);

            if (duplicateExists)
            {
                throw new DuplicateEntityException("A shortage resolution with the same number already exists.");
            }

            return normalizedResolutionNo;
        }

        var today = DateTime.UtcNow;
        var prefix = $"SR-{today:yyyyMMdd}";
        var latestTodayNo = await dbContext.ShortageResolutions
            .Where(entity => entity.ResolutionNo.StartsWith(prefix))
            .OrderByDescending(entity => entity.ResolutionNo)
            .Select(entity => entity.ResolutionNo)
            .FirstOrDefaultAsync(cancellationToken);

        var nextSequence = 1;
        if (!string.IsNullOrWhiteSpace(latestTodayNo))
        {
            var suffix = latestTodayNo[(prefix.Length + 1)..];
            if (int.TryParse(suffix, out var parsedSequence))
            {
                nextSequence = parsedSequence + 1;
            }
        }

        return $"{prefix}-{nextSequence:0000}";
    }

    private static decimal? ResolveTotalQty(UpsertShortageResolutionRequest request)
    {
        if (request.Allocations.Count == 0)
        {
            return request.TotalQty;
        }

        if (request.ResolutionType is ShortageResolutionType.Physical or ShortageResolutionType.Financial)
        {
            return request.Allocations.Sum(entity => entity.AllocatedQty ?? 0m);
        }

        return request.TotalQty;
    }

    private static decimal? ResolveTotalAmount(UpsertShortageResolutionRequest request)
    {
        if (request.ResolutionType == ShortageResolutionType.Financial && request.Allocations.Count > 0)
        {
            return request.Allocations.Sum(entity =>
                entity.AllocatedQty.HasValue && entity.ValuationRate.HasValue
                    ? Round(entity.AllocatedQty.Value * entity.ValuationRate.Value)
                    : 0m);
        }

        return request.TotalAmount;
    }

    private static ShortageResolutionDto ToDto(ShortageResolution resolution)
    {
        return new ShortageResolutionDto(
            resolution.Id,
            resolution.ResolutionNo,
            resolution.SupplierId,
            resolution.Supplier?.Code ?? string.Empty,
            resolution.Supplier?.Name ?? string.Empty,
            resolution.ResolutionType,
            resolution.ResolutionDate,
            resolution.TotalQty,
            resolution.TotalAmount,
            resolution.Currency,
            resolution.Notes,
            resolution.Status,
            resolution.ReversalDocumentId,
            resolution.ReversedAt,
            resolution.ApprovedBy,
            resolution.CreatedAt,
            resolution.CreatedBy,
            resolution.UpdatedAt,
            resolution.UpdatedBy,
            resolution.Allocations.OrderBy(entity => entity.SequenceNo).Select(ToAllocationDto).ToList());
    }

    private static ShortageResolutionAllocationDto ToAllocationDto(ShortageResolutionAllocation allocation)
    {
        var shortage = allocation.ShortageLedgerEntry;

        return new ShortageResolutionAllocationDto(
            allocation.Id,
            allocation.ResolutionId,
            allocation.ShortageLedgerId,
            allocation.AllocationType.ToString(),
            allocation.SequenceNo,
            allocation.AllocationMethod,
            allocation.AllocatedQty,
            allocation.AllocatedAmount,
            allocation.ValuationRate,
            allocation.FinancialQtyEquivalent,
            shortage?.PurchaseReceipt?.SupplierId ?? Guid.Empty,
            shortage?.PurchaseReceipt?.Supplier?.Code ?? string.Empty,
            shortage?.PurchaseReceipt?.Supplier?.Name ?? string.Empty,
            shortage?.PurchaseReceipt?.ReceiptNo ?? string.Empty,
            shortage?.PurchaseReceipt?.ReceiptDate ?? DateTime.MinValue,
            shortage?.ItemId ?? Guid.Empty,
            shortage?.Item?.Code ?? string.Empty,
            shortage?.Item?.Name ?? string.Empty,
            shortage?.ComponentItemId ?? Guid.Empty,
            shortage?.ComponentItem?.Code ?? string.Empty,
            shortage?.ComponentItem?.Name ?? string.Empty,
            shortage?.ShortageQty ?? 0m,
            shortage?.ExpectedQty ?? 0m,
            shortage?.ActualQty ?? 0m,
            shortage?.ResolvedPhysicalQty ?? 0m,
            shortage is null ? 0m : GetFinalPhysicalComponentQty(shortage),
            shortage?.ResolvedFinancialQtyEquivalent ?? 0m,
            GetEffectiveResolvedQtyEquivalent(shortage),
            shortage is null ? 0m : GetEffectiveOpenQty(shortage),
            shortage is null ? null : GetEffectiveOpenAmount(shortage),
            shortage?.AffectsSupplierBalance ?? false,
            shortage is null ? string.Empty : GetEffectiveStatus(shortage).ToString(),
            allocation.CreatedAt,
            allocation.CreatedBy);
    }

    private static OpenShortageDto ToOpenShortageDto(ShortageLedgerEntry entity)
    {
        var openQty = GetEffectiveOpenQty(entity);
        var shortageValue = GetEffectiveShortageValue(entity);
        var openAmount = GetEffectiveOpenAmount(entity);
        var status = GetEffectiveStatus(entity);

        return new OpenShortageDto(
            entity.Id,
            entity.PurchaseReceipt!.SupplierId,
            entity.PurchaseReceipt.Supplier!.Code,
            entity.PurchaseReceipt.Supplier.Name,
            entity.PurchaseReceiptId,
            entity.PurchaseReceipt.ReceiptNo,
            entity.PurchaseReceipt.ReceiptDate,
            entity.PurchaseReceiptLineId,
            entity.PurchaseOrderId,
            entity.PurchaseOrder?.PoNo,
            entity.ItemId,
            entity.Item!.Code,
            entity.Item.Name,
            entity.ComponentItemId,
            entity.ComponentItem!.Code,
            entity.ComponentItem.Name,
            entity.ShortageQty,
            entity.ExpectedQty,
            entity.ActualQty,
            entity.ResolvedPhysicalQty,
            GetFinalPhysicalComponentQty(entity),
            entity.ResolvedFinancialQtyEquivalent,
            GetEffectiveResolvedQtyEquivalent(entity),
            openQty,
            shortageValue,
            entity.ResolvedAmount,
            openAmount,
            status,
            entity.AffectsSupplierBalance,
            entity.ShortageReasonCodeId,
            entity.ShortageReasonCode?.Code,
            entity.ShortageReasonCode?.Name,
            entity.ApprovalStatus,
            entity.CreatedAt,
            entity.UpdatedAt);
    }

    private static decimal GetEffectiveOpenQty(ShortageLedgerEntry entity)
    {
        if (entity.OpenQty > 0m || entity.Status is ShortageEntryStatus.Resolved or ShortageEntryStatus.Canceled)
        {
            return entity.OpenQty;
        }

        return ClampToZero(Round(entity.ShortageQty - GetEffectiveResolvedQtyEquivalent(entity)));
    }

    private static decimal GetFinalPhysicalComponentQty(ShortageLedgerEntry entity)
    {
        return Round(entity.ActualQty + entity.ResolvedPhysicalQty);
    }

    private static decimal? GetEffectiveShortageValue(ShortageLedgerEntry entity)
    {
        if (entity.ShortageValue.HasValue)
        {
            return entity.ShortageValue.Value;
        }

        return null;
    }

    private static decimal? GetEffectiveOpenAmount(ShortageLedgerEntry entity)
    {
        if (!entity.ShortageValue.HasValue)
        {
            return entity.OpenAmount;
        }

        var effectiveOpenQty = GetEffectiveOpenQty(entity);
        if (effectiveOpenQty == 0m)
        {
            return 0m;
        }

        if (entity.ShortageQty <= 0m)
        {
            return entity.OpenAmount;
        }

        var valuationRate = Round(entity.ShortageValue.Value / entity.ShortageQty);
        var computedOpenAmount = ClampToZero(Round(effectiveOpenQty * valuationRate));

        if (entity.OpenAmount.HasValue && entity.OpenAmount.Value > 0m)
        {
            return computedOpenAmount;
        }

        return computedOpenAmount;
    }

    private static ShortageEntryStatus GetEffectiveStatus(ShortageLedgerEntry entity)
    {
        if (entity.Status == ShortageEntryStatus.Canceled)
        {
            return entity.Status;
        }

        var openQty = GetEffectiveOpenQty(entity);
        if (openQty == 0m && entity.ShortageQty > 0m)
        {
            return ShortageEntryStatus.Resolved;
        }

        if (GetEffectiveResolvedQtyEquivalent(entity) > 0m)
        {
            return ShortageEntryStatus.PartiallyResolved;
        }

        return ShortageEntryStatus.Open;
    }

    private static decimal GetEffectiveResolvedQtyEquivalent(ShortageLedgerEntry? entity)
    {
        if (entity is null)
        {
            return 0m;
        }

        return Round(entity.ResolvedPhysicalQty + entity.ResolvedFinancialQtyEquivalent);
    }

    private static void EnsureDraftIsEditable(ShortageResolution resolution)
    {
        if (resolution.Status != DocumentStatus.Draft)
        {
            throw new InvalidOperationException("Only Draft shortage resolutions can be edited.");
        }
    }

    private static string? NormalizeOptionalText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static decimal Round(decimal value)
    {
        return decimal.Round(value, 6, MidpointRounding.AwayFromZero);
    }

    private static decimal ClampToZero(decimal value)
    {
        return Math.Abs(value) < 0.000001m ? 0m : value;
    }
}
