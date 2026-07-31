using ERP.Application.Common.Exceptions;
using ERP.Application.Common.Pagination;
using ERP.Application.Pricing;
using ERP.Application.Security;
using ERP.Domain.Pricing;
using FluentValidation;
using FluentValidation.Results;

namespace ERP.Api.Endpoints;

public static class PricingEndpoints
{
    public static IEndpointRouteBuilder MapPricingEndpoints(this IEndpointRouteBuilder app)
    {
        var priceLists = app.MapGroup("/api/pricing/price-lists").RequireAuthorization();
        priceLists.MapGet("/", ListPriceListsAsync)
            .AddEndpointFilter(new OrganizationSetupEndpointFilter(true, OrganizationFeatureKeys.Inventory, OrganizationFeatureKeys.PriceLists))
            .AddEndpointFilter(new PermissionEndpointFilter(Permissions.PricingPriceListView));
        priceLists.MapGet("/{id:guid}", GetPriceListAsync)
            .AddEndpointFilter(new OrganizationSetupEndpointFilter(true, OrganizationFeatureKeys.Inventory, OrganizationFeatureKeys.PriceLists))
            .AddEndpointFilter(new PermissionEndpointFilter(Permissions.PricingPriceListView));
        priceLists.MapPost("/", CreatePriceListAsync)
            .AddEndpointFilter(new OrganizationSetupEndpointFilter(true, OrganizationFeatureKeys.Inventory, OrganizationFeatureKeys.PriceLists))
            .AddEndpointFilter(new PermissionEndpointFilter(Permissions.PricingPriceListManage));
        priceLists.MapPut("/{id:guid}", UpdatePriceListAsync)
            .AddEndpointFilter(new OrganizationSetupEndpointFilter(true, OrganizationFeatureKeys.Inventory, OrganizationFeatureKeys.PriceLists))
            .AddEndpointFilter(new PermissionEndpointFilter(Permissions.PricingPriceListManage));
        priceLists.MapPost("/{id:guid}/activate", ActivatePriceListAsync)
            .AddEndpointFilter(new OrganizationSetupEndpointFilter(true, OrganizationFeatureKeys.Inventory, OrganizationFeatureKeys.PriceLists))
            .AddEndpointFilter(new PermissionEndpointFilter(Permissions.PricingPriceListManage));
        priceLists.MapPost("/{id:guid}/deactivate", DeactivatePriceListAsync)
            .AddEndpointFilter(new OrganizationSetupEndpointFilter(true, OrganizationFeatureKeys.Inventory, OrganizationFeatureKeys.PriceLists))
            .AddEndpointFilter(new PermissionEndpointFilter(Permissions.PricingPriceListManage));
        priceLists.MapDelete("/{id:guid}", DeletePriceListAsync)
            .AddEndpointFilter(new OrganizationSetupEndpointFilter(true, OrganizationFeatureKeys.Inventory, OrganizationFeatureKeys.PriceLists))
            .AddEndpointFilter(new PermissionEndpointFilter(Permissions.PricingPriceListManage));

        var itemPrices = app.MapGroup("/api/pricing/item-prices").RequireAuthorization();
        itemPrices.MapGet("/", ListItemPricesAsync)
            .AddEndpointFilter(new OrganizationSetupEndpointFilter(true, OrganizationFeatureKeys.Inventory, OrganizationFeatureKeys.PriceLists))
            .AddEndpointFilter(new PermissionEndpointFilter(Permissions.PricingItemPriceView));
        itemPrices.MapGet("/{id:guid}", GetItemPriceAsync)
            .AddEndpointFilter(new OrganizationSetupEndpointFilter(true, OrganizationFeatureKeys.Inventory, OrganizationFeatureKeys.PriceLists))
            .AddEndpointFilter(new PermissionEndpointFilter(Permissions.PricingItemPriceView));
        itemPrices.MapPost("/", CreateItemPriceAsync)
            .AddEndpointFilter(new OrganizationSetupEndpointFilter(true, OrganizationFeatureKeys.Inventory, OrganizationFeatureKeys.PriceLists))
            .AddEndpointFilter(new PermissionEndpointFilter(Permissions.PricingItemPriceManage));
        itemPrices.MapPut("/{id:guid}", UpdateItemPriceAsync)
            .AddEndpointFilter(new OrganizationSetupEndpointFilter(true, OrganizationFeatureKeys.Inventory, OrganizationFeatureKeys.PriceLists))
            .AddEndpointFilter(new PermissionEndpointFilter(Permissions.PricingItemPriceManage));
        itemPrices.MapPost("/{id:guid}/activate", ActivateItemPriceAsync)
            .AddEndpointFilter(new OrganizationSetupEndpointFilter(true, OrganizationFeatureKeys.Inventory, OrganizationFeatureKeys.PriceLists))
            .AddEndpointFilter(new PermissionEndpointFilter(Permissions.PricingItemPriceManage));
        itemPrices.MapPost("/{id:guid}/deactivate", DeactivateItemPriceAsync)
            .AddEndpointFilter(new OrganizationSetupEndpointFilter(true, OrganizationFeatureKeys.Inventory, OrganizationFeatureKeys.PriceLists))
            .AddEndpointFilter(new PermissionEndpointFilter(Permissions.PricingItemPriceManage));
        itemPrices.MapDelete("/{id:guid}", DeleteItemPriceAsync)
            .AddEndpointFilter(new OrganizationSetupEndpointFilter(true, OrganizationFeatureKeys.Inventory, OrganizationFeatureKeys.PriceLists))
            .AddEndpointFilter(new PermissionEndpointFilter(Permissions.PricingItemPriceManage));

        var pricing = app.MapGroup("/api/pricing").RequireAuthorization();
        pricing.MapGet("/resolve", ResolvePriceAsync)
            .AddEndpointFilter(new OrganizationSetupEndpointFilter(true, OrganizationFeatureKeys.Inventory, OrganizationFeatureKeys.PriceLists))
            .AddEndpointFilter(new PermissionEndpointFilter(Permissions.PricingItemPriceView));
        pricing.MapGet("/filter-options", GetFilterOptionsAsync)
            .AddEndpointFilter(new OrganizationSetupEndpointFilter(true, OrganizationFeatureKeys.Inventory, OrganizationFeatureKeys.PriceLists))
            .AddEndpointFilter(new PermissionEndpointFilter(Permissions.PricingItemPriceView));
        pricing.MapGet("/items/{itemId:guid}/uoms", GetItemUomOptionsAsync)
            .AddEndpointFilter(new OrganizationSetupEndpointFilter(true, OrganizationFeatureKeys.Inventory, OrganizationFeatureKeys.PriceLists))
            .AddEndpointFilter(new PermissionEndpointFilter(Permissions.PricingItemPriceView));

        return app;
    }

    private static async Task<IResult> ListPriceListsAsync(
        string? search,
        string? code,
        string? name,
        PriceListType? type,
        string? currency,
        bool? isActive,
        bool? isDefault,
        int? page,
        int? pageSize,
        string? sortBy,
        SortDirection? sortDirection,
        IPricingService service,
        CancellationToken cancellationToken)
    {
        var result = await service.ListPriceListsAsync(
            new PriceListListQuery(search, code, name, type, currency, isActive, isDefault, page ?? 1, pageSize ?? 20, sortBy, sortDirection ?? SortDirection.Asc),
            cancellationToken);

        return Results.Ok(result);
    }

    private static async Task<IResult> GetPriceListAsync(
        Guid id,
        IPricingService service,
        CancellationToken cancellationToken)
    {
        var result = await service.GetPriceListAsync(id, cancellationToken);
        return result is null ? Results.NotFound() : Results.Ok(result);
    }

    private static async Task<IResult> CreatePriceListAsync(
        UpsertPriceListRequest request,
        IValidator<UpsertPriceListRequest> validator,
        IPricingService service,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        return await HandleValidatedRequestAsync(
            request,
            validator,
            () => service.CreatePriceListAsync(request, GetActor(context), cancellationToken));
    }

    private static async Task<IResult> UpdatePriceListAsync(
        Guid id,
        UpdatePriceListRequest request,
        IValidator<UpdatePriceListRequest> validator,
        IPricingService service,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        return await HandleValidatedRequestAsync(
            request,
            validator,
            async () =>
            {
                var result = await service.UpdatePriceListAsync(id, request, GetActor(context), cancellationToken);
                return result is null ? Results.NotFound() : Results.Ok(result);
            });
    }

    private static async Task<IResult> ActivatePriceListAsync(
        Guid id,
        VersionRequest request,
        IValidator<VersionRequest> validator,
        IPricingService service,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        return await HandleValidatedRequestAsync(
            request,
            validator,
            async () =>
            {
                var result = await service.ActivatePriceListAsync(id, request.Version, GetActor(context), cancellationToken);
                return result is null ? Results.NotFound() : Results.Ok(result);
            });
    }

    private static async Task<IResult> DeactivatePriceListAsync(
        Guid id,
        VersionRequest request,
        IValidator<VersionRequest> validator,
        IPricingService service,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        return await HandleValidatedRequestAsync(
            request,
            validator,
            async () =>
            {
                var result = await service.DeactivatePriceListAsync(id, request.Version, GetActor(context), cancellationToken);
                return result is null ? Results.NotFound() : Results.Ok(result);
            });
    }

    private static async Task<IResult> DeletePriceListAsync(
        Guid id,
        int? version,
        IPricingService service,
        CancellationToken cancellationToken)
    {
        if (!version.HasValue || version.Value < 0)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["version"] = ["Version is required."] });
        }

        try
        {
            var deleted = await service.DeletePriceListAsync(id, version.Value, cancellationToken);
            return deleted ? Results.NoContent() : Results.NotFound();
        }
        catch (ConcurrencyConflictException exception)
        {
            return Results.Conflict(new { message = exception.Message });
        }
        catch (InvalidOperationException exception)
        {
            return Results.Conflict(new { message = exception.Message });
        }
    }

    private static async Task<IResult> ListItemPricesAsync(
        string? search,
        Guid? priceListId,
        PriceListType? priceListType,
        Guid? itemId,
        Guid? categoryId,
        Guid? uomId,
        string? currency,
        bool? isActive,
        DateTime? effectiveOn,
        DateTime? validFrom,
        DateTime? validTo,
        int? page,
        int? pageSize,
        string? sortBy,
        SortDirection? sortDirection,
        IPricingService service,
        CancellationToken cancellationToken)
    {
        var result = await service.ListItemPricesAsync(
            new ItemPriceListQuery(search, priceListId, priceListType, itemId, categoryId, uomId, currency, isActive, effectiveOn, validFrom, validTo, page ?? 1, pageSize ?? 20, sortBy, sortDirection ?? SortDirection.Asc),
            cancellationToken);

        return Results.Ok(result);
    }

    private static async Task<IResult> GetItemPriceAsync(
        Guid id,
        IPricingService service,
        CancellationToken cancellationToken)
    {
        var result = await service.GetItemPriceAsync(id, cancellationToken);
        return result is null ? Results.NotFound() : Results.Ok(result);
    }

    private static async Task<IResult> CreateItemPriceAsync(
        UpsertItemPriceRequest request,
        IValidator<UpsertItemPriceRequest> validator,
        IPricingService service,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        return await HandleValidatedRequestAsync(
            request,
            validator,
            () => service.CreateItemPriceAsync(request, GetActor(context), cancellationToken));
    }

    private static async Task<IResult> UpdateItemPriceAsync(
        Guid id,
        UpdateItemPriceRequest request,
        IValidator<UpdateItemPriceRequest> validator,
        IPricingService service,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        return await HandleValidatedRequestAsync(
            request,
            validator,
            async () =>
            {
                var result = await service.UpdateItemPriceAsync(id, request, GetActor(context), cancellationToken);
                return result is null ? Results.NotFound() : Results.Ok(result);
            });
    }

    private static async Task<IResult> ActivateItemPriceAsync(
        Guid id,
        VersionRequest request,
        IValidator<VersionRequest> validator,
        IPricingService service,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        return await HandleValidatedRequestAsync(
            request,
            validator,
            async () =>
            {
                var result = await service.ActivateItemPriceAsync(id, request.Version, GetActor(context), cancellationToken);
                return result is null ? Results.NotFound() : Results.Ok(result);
            });
    }

    private static async Task<IResult> DeactivateItemPriceAsync(
        Guid id,
        VersionRequest request,
        IValidator<VersionRequest> validator,
        IPricingService service,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        return await HandleValidatedRequestAsync(
            request,
            validator,
            async () =>
            {
                var result = await service.DeactivateItemPriceAsync(id, request.Version, GetActor(context), cancellationToken);
                return result is null ? Results.NotFound() : Results.Ok(result);
            });
    }

    private static async Task<IResult> DeleteItemPriceAsync(
        Guid id,
        int? version,
        IPricingService service,
        CancellationToken cancellationToken)
    {
        if (!version.HasValue || version.Value < 0)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["version"] = ["Version is required."] });
        }

        try
        {
            var deleted = await service.DeleteItemPriceAsync(id, version.Value, cancellationToken);
            return deleted ? Results.NoContent() : Results.NotFound();
        }
        catch (ConcurrencyConflictException exception)
        {
            return Results.Conflict(new { message = exception.Message });
        }
    }

    private static async Task<IResult> ResolvePriceAsync(
        Guid priceListId,
        Guid itemId,
        Guid uomId,
        decimal quantity,
        DateTime? effectiveDate,
        IPricingService service,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await service.ResolvePriceAsync(
                new PriceResolutionQuery(priceListId, itemId, uomId, quantity, effectiveDate),
                cancellationToken);

            return result is null ? Results.NotFound(new { message = "No matching item price was found." }) : Results.Ok(result);
        }
        catch (InvalidOperationException exception)
        {
            return Results.BadRequest(new { message = exception.Message });
        }
    }

    private static async Task<IResult> GetFilterOptionsAsync(
        IPricingService service,
        CancellationToken cancellationToken)
    {
        return Results.Ok(await service.GetFilterOptionsAsync(cancellationToken));
    }

    private static async Task<IResult> GetItemUomOptionsAsync(
        Guid itemId,
        IPricingService service,
        CancellationToken cancellationToken)
    {
        var result = await service.GetItemUomOptionsAsync(itemId, cancellationToken);
        return result is null ? Results.NotFound() : Results.Ok(result);
    }

    private static async Task<IResult> HandleValidatedRequestAsync<TRequest, TResult>(
        TRequest request,
        IValidator<TRequest> validator,
        Func<Task<TResult>> handler)
    {
        var validationResult = await validator.ValidateAsync(request);
        if (!validationResult.IsValid)
        {
            return Results.ValidationProblem(ToErrors(validationResult));
        }

        try
        {
            var result = await handler();
            return result is IResult typedResult ? typedResult : Results.Ok(result);
        }
        catch (DuplicateEntityException exception)
        {
            return Results.Conflict(new { message = exception.Message });
        }
        catch (ConcurrencyConflictException exception)
        {
            return Results.Conflict(new { message = exception.Message });
        }
        catch (InvalidOperationException exception)
        {
            return Results.BadRequest(new { message = exception.Message });
        }
    }

    private static Dictionary<string, string[]> ToErrors(ValidationResult validationResult)
    {
        return validationResult.Errors
            .GroupBy(error => error.PropertyName)
            .ToDictionary(
                group => group.Key,
                group => group.Select(error => error.ErrorMessage).ToArray());
    }

    private static string GetActor(HttpContext context)
    {
        return context.User.Identity?.Name ?? "system";
    }
}
