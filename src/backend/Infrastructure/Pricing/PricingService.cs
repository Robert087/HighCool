using System.Data;
using ERP.Application.Common.Exceptions;
using ERP.Application.Common.Pagination;
using ERP.Application.Pricing;
using ERP.Application.Security;
using ERP.Domain.Identity;
using ERP.Domain.MasterData;
using ERP.Domain.Pricing;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Pricing;

public sealed class PricingService(
    AppDbContext dbContext,
    IRequestExecutionContext executionContext) : IPricingService
{
    public async Task<PagedResult<PriceListDto>> ListPriceListsAsync(
        PriceListListQuery query,
        CancellationToken cancellationToken)
    {
        var pagination = new PaginationRequest(query.Page, query.PageSize);
        var priceLists = dbContext.PriceLists.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            priceLists = priceLists.Where(entity =>
                entity.Code.Contains(search) ||
                entity.Name.Contains(search) ||
                entity.Currency.Contains(search) ||
                (entity.Description != null && entity.Description.Contains(search)));
        }

        if (!string.IsNullOrWhiteSpace(query.Code))
        {
            var code = NormalizeCode(query.Code);
            priceLists = priceLists.Where(entity => entity.Code.Contains(code));
        }

        if (!string.IsNullOrWhiteSpace(query.Name))
        {
            var name = query.Name.Trim();
            priceLists = priceLists.Where(entity => entity.Name.Contains(name));
        }

        if (query.Type.HasValue)
        {
            priceLists = priceLists.Where(entity => entity.Type == query.Type.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Currency))
        {
            var currency = NormalizeCurrency(query.Currency);
            priceLists = priceLists.Where(entity => entity.Currency == currency);
        }

        if (query.IsActive.HasValue)
        {
            priceLists = priceLists.Where(entity => entity.IsActive == query.IsActive.Value);
        }

        if (query.IsDefault.HasValue)
        {
            priceLists = priceLists.Where(entity => entity.IsDefault == query.IsDefault.Value);
        }

        var totalCount = await priceLists.CountAsync(cancellationToken);
        var rows = await ApplyPriceListSorting(priceLists, query)
            .Skip(pagination.Skip)
            .Take(pagination.NormalizedPageSize)
            .Select(entity => new PriceListDto(
                entity.Id,
                entity.Code,
                entity.Name,
                entity.Type,
                entity.Currency,
                entity.IsDefault,
                entity.IsActive,
                entity.Description,
                entity.ItemPrices.Count,
                entity.Version,
                entity.CreatedAt,
                entity.UpdatedAt))
            .ToListAsync(cancellationToken);

        return new PagedResult<PriceListDto>(
            rows,
            pagination.NormalizedPage,
            pagination.NormalizedPageSize,
            totalCount,
            CalculateTotalPages(totalCount, pagination.NormalizedPageSize),
            new { query.Search, query.Code, query.Name, query.Type, query.Currency, query.IsActive, query.IsDefault },
            new PagedSort(ResolvePriceListSortBy(query.SortBy), query.SortDirection));
    }

    public Task<PriceListDto?> GetPriceListAsync(Guid id, CancellationToken cancellationToken)
    {
        return dbContext.PriceLists
            .AsNoTracking()
            .Where(entity => entity.Id == id)
            .Select(entity => new PriceListDto(
                entity.Id,
                entity.Code,
                entity.Name,
                entity.Type,
                entity.Currency,
                entity.IsDefault,
                entity.IsActive,
                entity.Description,
                entity.ItemPrices.Count,
                entity.Version,
                entity.CreatedAt,
                entity.UpdatedAt))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<PriceListDto> CreatePriceListAsync(
        UpsertPriceListRequest request,
        string actor,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        var code = NormalizeCode(request.Code);
        await EnsurePriceListCodeIsUniqueAsync(code, null, cancellationToken);
        EnsurePriceListDefaultState(request.IsActive, request.IsDefault);

        if (request.IsDefault)
        {
            await ClearPreviousDefaultAsync(request.Type, null, actor, cancellationToken);
        }

        var priceList = new PriceList
        {
            Code = code,
            Name = request.Name.Trim(),
            Type = request.Type,
            Currency = NormalizeCurrency(request.Currency),
            IsDefault = request.IsDefault,
            IsActive = request.IsActive,
            Description = NormalizeOptional(request.Description),
            CreatedBy = actor
        };

        dbContext.PriceLists.Add(priceList);
        await SaveChangesHandlingConcurrencyAsync("Price list creation conflicted with another request. Refresh and try again.", cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return await GetRequiredPriceListAsync(priceList.Id, cancellationToken);
    }

    public async Task<PriceListDto?> UpdatePriceListAsync(
        Guid id,
        UpdatePriceListRequest request,
        string actor,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        var priceList = await dbContext.PriceLists.SingleOrDefaultAsync(entity => entity.Id == id, cancellationToken);
        if (priceList is null)
        {
            return null;
        }

        dbContext.Entry(priceList).Property(entity => entity.Version).OriginalValue = request.Version;

        var code = NormalizeCode(request.Code);
        await EnsurePriceListCodeIsUniqueAsync(code, id, cancellationToken);
        EnsurePriceListDefaultState(request.IsActive, request.IsDefault);

        if (request.IsDefault)
        {
            await ClearPreviousDefaultAsync(request.Type, id, actor, cancellationToken);
        }

        priceList.Code = code;
        priceList.Name = request.Name.Trim();
        priceList.Type = request.Type;
        priceList.Currency = NormalizeCurrency(request.Currency);
        priceList.IsDefault = request.IsDefault;
        priceList.IsActive = request.IsActive;
        priceList.Description = NormalizeOptional(request.Description);
        priceList.UpdatedBy = actor;
        priceList.Version++;

        await SyncItemPriceCurrencyAsync(priceList.Id, priceList.Currency, actor, cancellationToken);
        await SaveChangesHandlingConcurrencyAsync("Price list update conflicted with another request. Refresh and try again.", cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return await GetRequiredPriceListAsync(id, cancellationToken);
    }

    public async Task<PriceListDto?> ActivatePriceListAsync(
        Guid id,
        int version,
        string actor,
        CancellationToken cancellationToken)
    {
        var priceList = await dbContext.PriceLists.SingleOrDefaultAsync(entity => entity.Id == id, cancellationToken);
        if (priceList is null)
        {
            return null;
        }

        dbContext.Entry(priceList).Property(entity => entity.Version).OriginalValue = version;
        if (!priceList.IsActive)
        {
            priceList.IsActive = true;
            priceList.UpdatedBy = actor;
            priceList.Version++;
            await SaveChangesHandlingConcurrencyAsync("Price list activation conflicted with another request. Refresh and try again.", cancellationToken);
        }

        return await GetRequiredPriceListAsync(id, cancellationToken);
    }

    public async Task<PriceListDto?> DeactivatePriceListAsync(
        Guid id,
        int version,
        string actor,
        CancellationToken cancellationToken)
    {
        var priceList = await dbContext.PriceLists.SingleOrDefaultAsync(entity => entity.Id == id, cancellationToken);
        if (priceList is null)
        {
            return null;
        }

        dbContext.Entry(priceList).Property(entity => entity.Version).OriginalValue = version;
        if (priceList.IsActive || priceList.IsDefault)
        {
            priceList.IsActive = false;
            priceList.IsDefault = false;
            priceList.UpdatedBy = actor;
            priceList.Version++;
            await SaveChangesHandlingConcurrencyAsync("Price list deactivation conflicted with another request. Refresh and try again.", cancellationToken);
        }

        return await GetRequiredPriceListAsync(id, cancellationToken);
    }

    public async Task<bool> DeletePriceListAsync(Guid id, int version, CancellationToken cancellationToken)
    {
        var priceList = await dbContext.PriceLists
            .Include(entity => entity.ItemPrices)
            .SingleOrDefaultAsync(entity => entity.Id == id, cancellationToken);

        if (priceList is null)
        {
            return false;
        }

        dbContext.Entry(priceList).Property(entity => entity.Version).OriginalValue = version;

        if (priceList.ItemPrices.Count > 0)
        {
            throw new InvalidOperationException("Price lists with item prices cannot be deleted. Deactivate the price list instead.");
        }

        dbContext.PriceLists.Remove(priceList);
        await SaveChangesHandlingConcurrencyAsync("Price list deletion conflicted with another request. Refresh and try again.", cancellationToken);
        return true;
    }

    public async Task<PagedResult<ItemPriceDto>> ListItemPricesAsync(
        ItemPriceListQuery query,
        CancellationToken cancellationToken)
    {
        var pagination = new PaginationRequest(query.Page, query.PageSize);
        var itemPrices = IncludeItemPriceReferences(dbContext.ItemPrices.AsNoTracking());

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            itemPrices = itemPrices.Where(entity =>
                entity.PriceList!.Code.Contains(search) ||
                entity.PriceList.Name.Contains(search) ||
                entity.Item!.Code.Contains(search) ||
                entity.Item.Name.Contains(search) ||
                entity.Uom!.Code.Contains(search) ||
                entity.Currency.Contains(search) ||
                (entity.Notes != null && entity.Notes.Contains(search)));
        }

        if (query.PriceListId.HasValue)
        {
            itemPrices = itemPrices.Where(entity => entity.PriceListId == query.PriceListId.Value);
        }

        if (query.PriceListType.HasValue)
        {
            itemPrices = itemPrices.Where(entity => entity.PriceList!.Type == query.PriceListType.Value);
        }

        if (query.ItemId.HasValue)
        {
            itemPrices = itemPrices.Where(entity => entity.ItemId == query.ItemId.Value);
        }

        if (query.CategoryId.HasValue)
        {
            itemPrices = itemPrices.Where(entity => entity.Item!.CategoryId == query.CategoryId.Value);
        }

        if (query.UomId.HasValue)
        {
            itemPrices = itemPrices.Where(entity => entity.UomId == query.UomId.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Currency))
        {
            var currency = NormalizeCurrency(query.Currency);
            itemPrices = itemPrices.Where(entity => entity.Currency == currency);
        }

        if (query.IsActive.HasValue)
        {
            itemPrices = itemPrices.Where(entity => entity.IsActive == query.IsActive.Value);
        }

        if (query.EffectiveOn.HasValue)
        {
            var effectiveOn = NormalizeDate(query.EffectiveOn.Value);
            itemPrices = itemPrices.Where(entity =>
                entity.IsActive &&
                entity.PriceList!.IsActive &&
                entity.ValidFrom <= effectiveOn &&
                (!entity.ValidTo.HasValue || entity.ValidTo.Value >= effectiveOn));
        }

        if (query.ValidFrom.HasValue)
        {
            var validFrom = NormalizeDate(query.ValidFrom.Value);
            itemPrices = itemPrices.Where(entity => entity.ValidFrom >= validFrom);
        }

        if (query.ValidTo.HasValue)
        {
            var validTo = NormalizeDate(query.ValidTo.Value);
            itemPrices = itemPrices.Where(entity => entity.ValidTo.HasValue && entity.ValidTo.Value <= validTo);
        }

        var today = DateTime.UtcNow.Date;
        var totalCount = await itemPrices.CountAsync(cancellationToken);
        var rows = await ApplyItemPriceSorting(itemPrices, query, dbContext.Database.IsSqlite())
            .Skip(pagination.Skip)
            .Take(pagination.NormalizedPageSize)
            .Select(entity => ToItemPriceDto(entity, today))
            .ToListAsync(cancellationToken);

        return new PagedResult<ItemPriceDto>(
            rows,
            pagination.NormalizedPage,
            pagination.NormalizedPageSize,
            totalCount,
            CalculateTotalPages(totalCount, pagination.NormalizedPageSize),
            new
            {
                query.Search,
                query.PriceListId,
                query.PriceListType,
                query.ItemId,
                query.CategoryId,
                query.UomId,
                query.Currency,
                query.IsActive,
                query.EffectiveOn,
                query.ValidFrom,
                query.ValidTo
            },
            new PagedSort(ResolveItemPriceSortBy(query.SortBy), query.SortDirection));
    }

    public Task<ItemPriceDto?> GetItemPriceAsync(Guid id, CancellationToken cancellationToken)
    {
        var today = DateTime.UtcNow.Date;
        return dbContext.ItemPrices
            .AsNoTracking()
            .Include(entity => entity.PriceList)
            .Include(entity => entity.Item)
                .ThenInclude(entity => entity!.Category)
            .Include(entity => entity.Uom)
            .Where(entity => entity.Id == id)
            .Select(entity => ToItemPriceDto(entity, today))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<ItemPriceDto> CreateItemPriceAsync(
        UpsertItemPriceRequest request,
        string actor,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        var priceList = await ValidateItemPriceReferencesAsync(request.PriceListId, request.ItemId, request.UomId, request.Currency, request.IsActive, cancellationToken);
        var validFrom = NormalizeDate(request.ValidFrom!.Value);
        DateTime? validTo = request.ValidTo.HasValue ? NormalizeDate(request.ValidTo.Value) : null;

        await EnsureNoOverlapAsync(null, request.PriceListId, request.ItemId, request.UomId, request.MinimumQuantity, validFrom, validTo, request.IsActive, cancellationToken);

        var itemPrice = new ItemPrice
        {
            PriceListId = request.PriceListId,
            ItemId = request.ItemId,
            UomId = request.UomId,
            Currency = priceList.Currency,
            Rate = request.Rate,
            MinimumQuantity = request.MinimumQuantity,
            ValidFrom = validFrom,
            ValidTo = validTo,
            IsActive = request.IsActive,
            Notes = NormalizeOptional(request.Notes),
            CreatedBy = actor
        };

        dbContext.ItemPrices.Add(itemPrice);
        await SaveChangesHandlingConcurrencyAsync("Item price creation conflicted with another request. Refresh and try again.", cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return await GetRequiredItemPriceAsync(itemPrice.Id, cancellationToken);
    }

    public async Task<ItemPriceDto?> UpdateItemPriceAsync(
        Guid id,
        UpdateItemPriceRequest request,
        string actor,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        var itemPrice = await dbContext.ItemPrices.SingleOrDefaultAsync(entity => entity.Id == id, cancellationToken);
        if (itemPrice is null)
        {
            return null;
        }

        dbContext.Entry(itemPrice).Property(entity => entity.Version).OriginalValue = request.Version;

        var priceList = await ValidateItemPriceReferencesAsync(request.PriceListId, request.ItemId, request.UomId, request.Currency, request.IsActive, cancellationToken);
        var validFrom = NormalizeDate(request.ValidFrom!.Value);
        DateTime? validTo = request.ValidTo.HasValue ? NormalizeDate(request.ValidTo.Value) : null;

        await EnsureNoOverlapAsync(id, request.PriceListId, request.ItemId, request.UomId, request.MinimumQuantity, validFrom, validTo, request.IsActive, cancellationToken);

        itemPrice.PriceListId = request.PriceListId;
        itemPrice.ItemId = request.ItemId;
        itemPrice.UomId = request.UomId;
        itemPrice.Currency = priceList.Currency;
        itemPrice.Rate = request.Rate;
        itemPrice.MinimumQuantity = request.MinimumQuantity;
        itemPrice.ValidFrom = validFrom;
        itemPrice.ValidTo = validTo;
        itemPrice.IsActive = request.IsActive;
        itemPrice.Notes = NormalizeOptional(request.Notes);
        itemPrice.UpdatedBy = actor;
        itemPrice.Version++;

        await SaveChangesHandlingConcurrencyAsync("Item price update conflicted with another request. Refresh and try again.", cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return await GetRequiredItemPriceAsync(id, cancellationToken);
    }

    public async Task<ItemPriceDto?> ActivateItemPriceAsync(
        Guid id,
        int version,
        string actor,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        var itemPrice = await dbContext.ItemPrices.SingleOrDefaultAsync(entity => entity.Id == id, cancellationToken);
        if (itemPrice is null)
        {
            return null;
        }

        dbContext.Entry(itemPrice).Property(entity => entity.Version).OriginalValue = version;
        await ValidateItemPriceReferencesAsync(itemPrice.PriceListId, itemPrice.ItemId, itemPrice.UomId, itemPrice.Currency, true, cancellationToken);
        await EnsureNoOverlapAsync(id, itemPrice.PriceListId, itemPrice.ItemId, itemPrice.UomId, itemPrice.MinimumQuantity, itemPrice.ValidFrom, itemPrice.ValidTo, true, cancellationToken);

        if (!itemPrice.IsActive)
        {
            itemPrice.IsActive = true;
            itemPrice.UpdatedBy = actor;
            itemPrice.Version++;
            await SaveChangesHandlingConcurrencyAsync("Item price activation conflicted with another request. Refresh and try again.", cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return await GetRequiredItemPriceAsync(id, cancellationToken);
    }

    public async Task<ItemPriceDto?> DeactivateItemPriceAsync(
        Guid id,
        int version,
        string actor,
        CancellationToken cancellationToken)
    {
        var itemPrice = await dbContext.ItemPrices.SingleOrDefaultAsync(entity => entity.Id == id, cancellationToken);
        if (itemPrice is null)
        {
            return null;
        }

        dbContext.Entry(itemPrice).Property(entity => entity.Version).OriginalValue = version;
        if (itemPrice.IsActive)
        {
            itemPrice.IsActive = false;
            itemPrice.UpdatedBy = actor;
            itemPrice.Version++;
            await SaveChangesHandlingConcurrencyAsync("Item price deactivation conflicted with another request. Refresh and try again.", cancellationToken);
        }

        return await GetRequiredItemPriceAsync(id, cancellationToken);
    }

    public async Task<bool> DeleteItemPriceAsync(Guid id, int version, CancellationToken cancellationToken)
    {
        var itemPrice = await dbContext.ItemPrices.SingleOrDefaultAsync(entity => entity.Id == id, cancellationToken);
        if (itemPrice is null)
        {
            return false;
        }

        dbContext.Entry(itemPrice).Property(entity => entity.Version).OriginalValue = version;
        dbContext.ItemPrices.Remove(itemPrice);
        await SaveChangesHandlingConcurrencyAsync("Item price deletion conflicted with another request. Refresh and try again.", cancellationToken);
        return true;
    }

    public Task<PriceResolutionDto?> ResolvePriceAsync(
        PriceResolutionQuery query,
        CancellationToken cancellationToken)
    {
        if (query.Quantity <= 0m)
        {
            throw new InvalidOperationException("Quantity must be greater than zero.");
        }

        var effectiveDate = NormalizeDate(query.EffectiveDate ?? DateTime.UtcNow);

        var prices = dbContext.ItemPrices
            .AsNoTracking()
            .Where(entity =>
                entity.PriceListId == query.PriceListId &&
                entity.PriceList!.IsActive &&
                entity.ItemId == query.ItemId &&
                entity.UomId == query.UomId &&
                entity.IsActive &&
                entity.ValidFrom <= effectiveDate &&
                (!entity.ValidTo.HasValue || entity.ValidTo.Value >= effectiveDate) &&
                entity.MinimumQuantity <= query.Quantity);

        prices = dbContext.Database.IsSqlite()
            ? prices
                .OrderByDescending(entity => (double)entity.MinimumQuantity)
                .ThenByDescending(entity => entity.ValidFrom)
                .ThenBy(entity => entity.Id)
            : prices
                .OrderByDescending(entity => entity.MinimumQuantity)
                .ThenByDescending(entity => entity.ValidFrom)
                .ThenBy(entity => entity.Id);

        return prices
            .Select(entity => new PriceResolutionDto(
                entity.Id,
                entity.PriceListId,
                entity.ItemId,
                entity.UomId,
                entity.Currency,
                entity.Rate,
                entity.MinimumQuantity,
                entity.ValidFrom,
                entity.ValidTo))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<PricingFilterOptionsDto> GetFilterOptionsAsync(CancellationToken cancellationToken)
    {
        var organizationId = executionContext.OrganizationId;
        var defaultCurrency = organizationId.HasValue
            ? await dbContext.Organizations
                .AsNoTracking()
                .Where(entity => entity.Id == organizationId.Value)
                .Select(entity => entity.DefaultCurrency)
                .SingleOrDefaultAsync(cancellationToken)
            : null;

        var priceLists = await dbContext.PriceLists
            .AsNoTracking()
            .Where(entity => entity.IsActive)
            .OrderBy(entity => entity.Type)
            .ThenBy(entity => entity.Code)
            .Select(entity => new PricingOptionDto(entity.Id, entity.Code, entity.Name, entity.Currency))
            .ToListAsync(cancellationToken);

        var items = await dbContext.Items
            .AsNoTracking()
            .Where(entity => entity.IsActive)
            .OrderBy(entity => entity.Code)
            .Select(entity => new PricingOptionDto(entity.Id, entity.Code, entity.Name, null))
            .ToListAsync(cancellationToken);

        var uoms = await dbContext.Uoms
            .AsNoTracking()
            .Where(entity => entity.IsActive)
            .OrderBy(entity => entity.Code)
            .Select(entity => new PricingOptionDto(entity.Id, entity.Code, entity.Name, null))
            .ToListAsync(cancellationToken);

        var categories = await dbContext.ItemCategories
            .AsNoTracking()
            .Where(entity => entity.IsActive)
            .OrderBy(entity => entity.Code)
            .Select(entity => new PricingOptionDto(entity.Id, entity.Code, entity.Name, null))
            .ToListAsync(cancellationToken);

        var currencies = await dbContext.PriceLists
            .AsNoTracking()
            .Select(entity => entity.Currency)
            .Distinct()
            .ToListAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(defaultCurrency))
        {
            currencies.Add(NormalizeCurrency(defaultCurrency));
        }

        return new PricingFilterOptionsDto(
            priceLists,
            items,
            uoms,
            categories,
            currencies.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(currency => currency).ToArray());
    }

    public async Task<ItemPricingUomOptionsDto?> GetItemUomOptionsAsync(Guid itemId, CancellationToken cancellationToken)
    {
        var item = await dbContext.Items
            .AsNoTracking()
            .Where(entity => entity.Id == itemId)
            .Select(entity => new { entity.Id, entity.BaseUomId })
            .SingleOrDefaultAsync(cancellationToken);

        if (item is null)
        {
            return null;
        }

        var uoms = await dbContext.Uoms
            .AsNoTracking()
            .Where(entity =>
                entity.IsActive &&
                (entity.Id == item.BaseUomId ||
                 dbContext.UomConversions.Any(conversion =>
                     conversion.FromUomId == entity.Id &&
                     conversion.ToUomId == item.BaseUomId &&
                     conversion.IsActive)))
            .OrderBy(entity => entity.Code)
            .Select(entity => new PricingOptionDto(entity.Id, entity.Code, entity.Name, null))
            .ToListAsync(cancellationToken);

        return new ItemPricingUomOptionsDto(item.Id, uoms);
    }

    private async Task<PriceList> ValidateItemPriceReferencesAsync(
        Guid priceListId,
        Guid itemId,
        Guid uomId,
        string? clientCurrency,
        bool activePrice,
        CancellationToken cancellationToken)
    {
        var priceList = await dbContext.PriceLists.SingleOrDefaultAsync(entity => entity.Id == priceListId, cancellationToken);
        if (priceList is null)
        {
            throw new InvalidOperationException("Price list was not found.");
        }

        if (activePrice && !priceList.IsActive)
        {
            throw new InvalidOperationException("Active item prices require an active price list.");
        }

        if (!string.IsNullOrWhiteSpace(clientCurrency) &&
            !string.Equals(NormalizeCurrency(clientCurrency), priceList.Currency, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Item price currency must match the selected price list currency.");
        }

        var item = await dbContext.Items.AsNoTracking().SingleOrDefaultAsync(entity => entity.Id == itemId, cancellationToken);
        if (item is null)
        {
            throw new InvalidOperationException("Item was not found.");
        }

        if (!item.IsActive)
        {
            throw new InvalidOperationException("Item price requires an active item.");
        }

        var uom = await dbContext.Uoms.AsNoTracking().SingleOrDefaultAsync(entity => entity.Id == uomId, cancellationToken);
        if (uom is null)
        {
            throw new InvalidOperationException("UOM was not found.");
        }

        if (!uom.IsActive)
        {
            throw new InvalidOperationException("Item price requires an active UOM.");
        }

        if (uomId != item.BaseUomId)
        {
            var conversionExists = await dbContext.UomConversions.AnyAsync(
                entity => entity.FromUomId == uomId &&
                          entity.ToUomId == item.BaseUomId &&
                          entity.IsActive,
                cancellationToken);

            if (!conversionExists)
            {
                throw new InvalidOperationException("Selected UOM is not valid for this item.");
            }
        }

        return priceList;
    }

    private async Task EnsureNoOverlapAsync(
        Guid? currentId,
        Guid priceListId,
        Guid itemId,
        Guid uomId,
        decimal minimumQuantity,
        DateTime validFrom,
        DateTime? validTo,
        bool isActive,
        CancellationToken cancellationToken)
    {
        if (!isActive)
        {
            return;
        }

        var requestedEnd = validTo ?? DateTime.MaxValue.Date;
        var overlaps = await dbContext.ItemPrices.AnyAsync(
            entity => entity.Id != currentId &&
                      entity.PriceListId == priceListId &&
                      entity.ItemId == itemId &&
                      entity.UomId == uomId &&
                      entity.MinimumQuantity == minimumQuantity &&
                      entity.IsActive &&
                      entity.ValidFrom <= requestedEnd &&
                      validFrom <= (entity.ValidTo ?? DateTime.MaxValue.Date),
            cancellationToken);

        if (overlaps)
        {
            throw new DuplicateEntityException("An overlapping active item price already exists for the same price list, item, UOM, and minimum quantity.");
        }
    }

    private async Task ClearPreviousDefaultAsync(
        PriceListType type,
        Guid? currentId,
        string actor,
        CancellationToken cancellationToken)
    {
        var previousDefaults = await dbContext.PriceLists
            .Where(entity => entity.Type == type && entity.IsDefault && entity.Id != currentId)
            .ToListAsync(cancellationToken);

        foreach (var previousDefault in previousDefaults)
        {
            previousDefault.IsDefault = false;
            previousDefault.UpdatedBy = actor;
            previousDefault.Version++;
        }
    }

    private async Task SyncItemPriceCurrencyAsync(Guid priceListId, string currency, string actor, CancellationToken cancellationToken)
    {
        var itemPrices = await dbContext.ItemPrices
            .Where(entity => entity.PriceListId == priceListId && entity.Currency != currency)
            .ToListAsync(cancellationToken);

        foreach (var itemPrice in itemPrices)
        {
            itemPrice.Currency = currency;
            itemPrice.UpdatedBy = actor;
            itemPrice.Version++;
        }
    }

    private async Task EnsurePriceListCodeIsUniqueAsync(string code, Guid? currentId, CancellationToken cancellationToken)
    {
        var exists = await dbContext.PriceLists.AnyAsync(
            entity => entity.Code == code && entity.Id != currentId,
            cancellationToken);

        if (exists)
        {
            throw new DuplicateEntityException($"Price list code '{code}' already exists.");
        }
    }

    private static void EnsurePriceListDefaultState(bool isActive, bool isDefault)
    {
        if (isDefault && !isActive)
        {
            throw new InvalidOperationException("Inactive price lists cannot be marked as default.");
        }
    }

    private async Task SaveChangesHandlingConcurrencyAsync(string concurrencyMessage, CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConcurrencyConflictException(concurrencyMessage);
        }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
        {
            throw new DuplicateEntityException("A duplicate pricing record already exists.");
        }
        catch (DbUpdateException)
        {
            throw new ConcurrencyConflictException(concurrencyMessage);
        }
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException exception)
    {
        var message = exception.InnerException?.Message ?? exception.Message;
        return message.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase) ||
               message.Contains("duplicate", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<PriceListDto> GetRequiredPriceListAsync(Guid id, CancellationToken cancellationToken)
    {
        return await GetPriceListAsync(id, cancellationToken) ??
               throw new InvalidOperationException("Price list was not found after save.");
    }

    private async Task<ItemPriceDto> GetRequiredItemPriceAsync(Guid id, CancellationToken cancellationToken)
    {
        return await GetItemPriceAsync(id, cancellationToken) ??
               throw new InvalidOperationException("Item price was not found after save.");
    }

    private static IQueryable<PriceList> ApplyPriceListSorting(IQueryable<PriceList> query, PriceListListQuery request)
    {
        var sortBy = ResolvePriceListSortBy(request.SortBy);
        var ascending = request.SortDirection == SortDirection.Asc;

        return (sortBy, ascending) switch
        {
            ("code", true) => query.OrderBy(entity => entity.Code).ThenBy(entity => entity.Id),
            ("code", false) => query.OrderByDescending(entity => entity.Code).ThenBy(entity => entity.Id),
            ("type", true) => query.OrderBy(entity => entity.Type).ThenBy(entity => entity.Code).ThenBy(entity => entity.Id),
            ("type", false) => query.OrderByDescending(entity => entity.Type).ThenBy(entity => entity.Code).ThenBy(entity => entity.Id),
            ("currency", true) => query.OrderBy(entity => entity.Currency).ThenBy(entity => entity.Code).ThenBy(entity => entity.Id),
            ("currency", false) => query.OrderByDescending(entity => entity.Currency).ThenBy(entity => entity.Code).ThenBy(entity => entity.Id),
            ("isActive", true) => query.OrderBy(entity => entity.IsActive).ThenBy(entity => entity.Code).ThenBy(entity => entity.Id),
            ("isActive", false) => query.OrderByDescending(entity => entity.IsActive).ThenBy(entity => entity.Code).ThenBy(entity => entity.Id),
            ("isDefault", true) => query.OrderBy(entity => entity.IsDefault).ThenBy(entity => entity.Code).ThenBy(entity => entity.Id),
            ("isDefault", false) => query.OrderByDescending(entity => entity.IsDefault).ThenBy(entity => entity.Code).ThenBy(entity => entity.Id),
            ("updatedAt", true) => query.OrderBy(entity => entity.UpdatedAt ?? entity.CreatedAt).ThenBy(entity => entity.Code).ThenBy(entity => entity.Id),
            ("updatedAt", false) => query.OrderByDescending(entity => entity.UpdatedAt ?? entity.CreatedAt).ThenBy(entity => entity.Code).ThenBy(entity => entity.Id),
            _ when ascending => query.OrderBy(entity => entity.Name).ThenBy(entity => entity.Code).ThenBy(entity => entity.Id),
            _ => query.OrderByDescending(entity => entity.Name).ThenBy(entity => entity.Code).ThenBy(entity => entity.Id)
        };
    }

    private static IQueryable<ItemPrice> ApplyItemPriceSorting(IQueryable<ItemPrice> query, ItemPriceListQuery request, bool isSqlite)
    {
        var sortBy = ResolveItemPriceSortBy(request.SortBy);
        var ascending = request.SortDirection == SortDirection.Asc;

        return (sortBy, ascending) switch
        {
            ("priceList", true) => query.OrderBy(entity => entity.PriceList!.Code).ThenBy(entity => entity.Item!.Code).ThenBy(entity => entity.Id),
            ("priceList", false) => query.OrderByDescending(entity => entity.PriceList!.Code).ThenBy(entity => entity.Item!.Code).ThenBy(entity => entity.Id),
            ("item", true) => query.OrderBy(entity => entity.Item!.Code).ThenBy(entity => entity.Uom!.Code).ThenBy(entity => entity.ValidFrom).ThenBy(entity => entity.Id),
            ("item", false) => query.OrderByDescending(entity => entity.Item!.Code).ThenBy(entity => entity.Uom!.Code).ThenBy(entity => entity.ValidFrom).ThenBy(entity => entity.Id),
            ("uom", true) => query.OrderBy(entity => entity.Uom!.Code).ThenBy(entity => entity.Item!.Code).ThenBy(entity => entity.Id),
            ("uom", false) => query.OrderByDescending(entity => entity.Uom!.Code).ThenBy(entity => entity.Item!.Code).ThenBy(entity => entity.Id),
            ("rate", true) when isSqlite => query.OrderBy(entity => (double)entity.Rate).ThenBy(entity => entity.Id),
            ("rate", false) when isSqlite => query.OrderByDescending(entity => (double)entity.Rate).ThenBy(entity => entity.Id),
            ("rate", true) => query.OrderBy(entity => entity.Rate).ThenBy(entity => entity.Id),
            ("rate", false) => query.OrderByDescending(entity => entity.Rate).ThenBy(entity => entity.Id),
            ("minimumQuantity", true) when isSqlite => query.OrderBy(entity => (double)entity.MinimumQuantity).ThenBy(entity => entity.Id),
            ("minimumQuantity", false) when isSqlite => query.OrderByDescending(entity => (double)entity.MinimumQuantity).ThenBy(entity => entity.Id),
            ("minimumQuantity", true) => query.OrderBy(entity => entity.MinimumQuantity).ThenBy(entity => entity.Id),
            ("minimumQuantity", false) => query.OrderByDescending(entity => entity.MinimumQuantity).ThenBy(entity => entity.Id),
            ("validFrom", true) => query.OrderBy(entity => entity.ValidFrom).ThenBy(entity => entity.Id),
            ("validFrom", false) => query.OrderByDescending(entity => entity.ValidFrom).ThenBy(entity => entity.Id),
            ("validTo", true) => query.OrderBy(entity => entity.ValidTo).ThenBy(entity => entity.Id),
            ("validTo", false) => query.OrderByDescending(entity => entity.ValidTo).ThenBy(entity => entity.Id),
            ("isActive", true) => query.OrderBy(entity => entity.IsActive).ThenBy(entity => entity.Item!.Code).ThenBy(entity => entity.Id),
            ("isActive", false) => query.OrderByDescending(entity => entity.IsActive).ThenBy(entity => entity.Item!.Code).ThenBy(entity => entity.Id),
            ("updatedAt", true) => query.OrderBy(entity => entity.UpdatedAt ?? entity.CreatedAt).ThenBy(entity => entity.Item!.Code).ThenBy(entity => entity.Id),
            ("updatedAt", false) => query.OrderByDescending(entity => entity.UpdatedAt ?? entity.CreatedAt).ThenBy(entity => entity.Item!.Code).ThenBy(entity => entity.Id),
            _ when ascending => query.OrderBy(entity => entity.PriceList!.Code).ThenBy(entity => entity.Item!.Code).ThenBy(entity => entity.Uom!.Code).ThenBy(entity => entity.Id),
            _ => query.OrderByDescending(entity => entity.PriceList!.Code).ThenBy(entity => entity.Item!.Code).ThenBy(entity => entity.Uom!.Code).ThenBy(entity => entity.Id)
        };
    }

    private static IQueryable<ItemPrice> IncludeItemPriceReferences(IQueryable<ItemPrice> query)
    {
        return query
            .Include(entity => entity.PriceList)
            .Include(entity => entity.Item)
                .ThenInclude(entity => entity!.Category)
            .Include(entity => entity.Uom);
    }

    private static string ResolvePriceListSortBy(string? sortBy)
    {
        return sortBy?.Trim() switch
        {
            "code" => "code",
            "type" => "type",
            "currency" => "currency",
            "isActive" => "isActive",
            "isDefault" => "isDefault",
            "updatedAt" => "updatedAt",
            _ => "name"
        };
    }

    private static string ResolveItemPriceSortBy(string? sortBy)
    {
        return sortBy?.Trim() switch
        {
            "priceList" => "priceList",
            "item" => "item",
            "uom" => "uom",
            "rate" => "rate",
            "minimumQuantity" => "minimumQuantity",
            "validFrom" => "validFrom",
            "validTo" => "validTo",
            "isActive" => "isActive",
            "updatedAt" => "updatedAt",
            _ => "priceList"
        };
    }

    private static ItemPriceDto ToItemPriceDto(ItemPrice entity, DateTime today)
    {
        return new ItemPriceDto(
            entity.Id,
            entity.PriceListId,
            entity.PriceList!.Code,
            entity.PriceList.Name,
            entity.PriceList.Type,
            entity.ItemId,
            entity.Item!.Code,
            entity.Item.Name,
            entity.Item.CategoryId,
            entity.Item.Category != null ? entity.Item.Category.Code : null,
            entity.Item.Category != null ? entity.Item.Category.Name : null,
            entity.UomId,
            entity.Uom!.Code,
            entity.Uom.Name,
            entity.Currency,
            entity.Rate,
            entity.MinimumQuantity,
            entity.ValidFrom,
            entity.ValidTo,
            entity.IsActive,
            entity.IsActive &&
                entity.PriceList.IsActive &&
                entity.ValidFrom <= today &&
                (!entity.ValidTo.HasValue || entity.ValidTo.Value >= today),
            entity.Notes,
            entity.Version,
            entity.CreatedAt,
            entity.UpdatedAt);
    }

    private static string NormalizeCode(string code)
    {
        return code.Trim().ToUpperInvariant();
    }

    private static string NormalizeCurrency(string currency)
    {
        return currency.Trim().ToUpperInvariant();
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static DateTime NormalizeDate(DateTime value)
    {
        return value.Date;
    }

    private static int CalculateTotalPages(int totalCount, int pageSize)
    {
        return totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize);
    }
}
