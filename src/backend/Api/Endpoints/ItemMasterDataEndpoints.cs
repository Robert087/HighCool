using ERP.Application.Common.Exceptions;
using ERP.Application.Common.Pagination;
using ERP.Application.MasterData.ItemCategories;
using ERP.Application.MasterData.Items;
using ERP.Application.MasterData.UomConversions;
using ERP.Application.Security;
using FluentValidation;
using FluentValidation.Results;

namespace ERP.Api.Endpoints;

public static class ItemMasterDataEndpoints
{
    public static IEndpointRouteBuilder MapItemMasterDataEndpoints(this IEndpointRouteBuilder app)
    {
        var items = app.MapGroup("/api/items").RequireAuthorization();
        items.MapGet("/", ListItemsAsync).AddEndpointFilter(new OrganizationSetupEndpointFilter(true, OrganizationFeatureKeys.Inventory)).AddEndpointFilter(new PermissionEndpointFilter(Permissions.ItemsView));
        items.MapGet("/{id:guid}", GetItemAsync).AddEndpointFilter(new OrganizationSetupEndpointFilter(true, OrganizationFeatureKeys.Inventory)).AddEndpointFilter(new PermissionEndpointFilter(Permissions.ItemsView));
        items.MapPost("/", CreateItemAsync).AddEndpointFilter(new OrganizationSetupEndpointFilter(true, OrganizationFeatureKeys.Inventory)).AddEndpointFilter(new PermissionEndpointFilter(Permissions.ItemsCreate));
        items.MapPut("/{id:guid}", UpdateItemAsync).AddEndpointFilter(new OrganizationSetupEndpointFilter(true, OrganizationFeatureKeys.Inventory)).AddEndpointFilter(new PermissionEndpointFilter(Permissions.ItemsEdit));
        items.MapPost("/{id:guid}/deactivate", DeactivateItemAsync).AddEndpointFilter(new OrganizationSetupEndpointFilter(true, OrganizationFeatureKeys.Inventory)).AddEndpointFilter(new PermissionEndpointFilter(Permissions.ItemsEdit));

        var categories = app.MapGroup("/api/item-categories").RequireAuthorization();
        categories.MapGet("/", ListItemCategoriesAsync).AddEndpointFilter(new OrganizationSetupEndpointFilter(true, OrganizationFeatureKeys.Inventory)).AddEndpointFilter(new PermissionEndpointFilter(Permissions.ItemsView));
        categories.MapGet("/{id:guid}", GetItemCategoryAsync).AddEndpointFilter(new OrganizationSetupEndpointFilter(true, OrganizationFeatureKeys.Inventory)).AddEndpointFilter(new PermissionEndpointFilter(Permissions.ItemsView));
        categories.MapPost("/", CreateItemCategoryAsync).AddEndpointFilter(new OrganizationSetupEndpointFilter(true, OrganizationFeatureKeys.Inventory)).AddEndpointFilter(new PermissionEndpointFilter(Permissions.ItemsCreate));
        categories.MapPut("/{id:guid}", UpdateItemCategoryAsync).AddEndpointFilter(new OrganizationSetupEndpointFilter(true, OrganizationFeatureKeys.Inventory)).AddEndpointFilter(new PermissionEndpointFilter(Permissions.ItemsEdit));
        categories.MapPost("/{id:guid}/activate", ActivateItemCategoryAsync).AddEndpointFilter(new OrganizationSetupEndpointFilter(true, OrganizationFeatureKeys.Inventory)).AddEndpointFilter(new PermissionEndpointFilter(Permissions.ItemsEdit));
        categories.MapPost("/{id:guid}/deactivate", DeactivateItemCategoryAsync).AddEndpointFilter(new OrganizationSetupEndpointFilter(true, OrganizationFeatureKeys.Inventory)).AddEndpointFilter(new PermissionEndpointFilter(Permissions.ItemsEdit));

        var conversions = app.MapGroup("/api/uom-conversions").RequireAuthorization();
        conversions.MapGet("/", ListUomConversionsAsync).AddEndpointFilter(new OrganizationSetupEndpointFilter(true, OrganizationFeatureKeys.Uom, OrganizationFeatureKeys.UomConversion)).AddEndpointFilter(new PermissionEndpointFilter(Permissions.UomsManage));
        conversions.MapGet("/{id:guid}", GetUomConversionAsync).AddEndpointFilter(new OrganizationSetupEndpointFilter(true, OrganizationFeatureKeys.Uom, OrganizationFeatureKeys.UomConversion)).AddEndpointFilter(new PermissionEndpointFilter(Permissions.UomsManage));
        conversions.MapPost("/", CreateUomConversionAsync).AddEndpointFilter(new OrganizationSetupEndpointFilter(true, OrganizationFeatureKeys.Uom, OrganizationFeatureKeys.UomConversion)).AddEndpointFilter(new PermissionEndpointFilter(Permissions.UomsManage));
        conversions.MapPut("/{id:guid}", UpdateUomConversionAsync).AddEndpointFilter(new OrganizationSetupEndpointFilter(true, OrganizationFeatureKeys.Uom, OrganizationFeatureKeys.UomConversion)).AddEndpointFilter(new PermissionEndpointFilter(Permissions.UomsManage));
        conversions.MapPost("/{id:guid}/deactivate", DeactivateUomConversionAsync).AddEndpointFilter(new OrganizationSetupEndpointFilter(true, OrganizationFeatureKeys.Uom, OrganizationFeatureKeys.UomConversion)).AddEndpointFilter(new PermissionEndpointFilter(Permissions.UomsManage));

        return app;
    }

    private static async Task<IResult> ListItemsAsync(
        string? search,
        bool? isActive,
        bool? isSellable,
        Guid? categoryId,
        Guid? baseUomId,
        int? page,
        int? pageSize,
        string? sortBy,
        SortDirection? sortDirection,
        IItemService service,
        CancellationToken cancellationToken)
    {
        var result = await service.ListAsync(
            new ItemListQuery(
                search,
                isActive,
                isSellable,
                categoryId,
                baseUomId,
                page ?? 1,
                pageSize ?? 20,
                sortBy,
                sortDirection ?? SortDirection.Asc),
            cancellationToken);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetItemAsync(Guid id, IItemService service, CancellationToken cancellationToken)
    {
        var result = await service.GetAsync(id, cancellationToken);
        return result is null ? Results.NotFound() : Results.Ok(result);
    }

    private static async Task<IResult> CreateItemAsync(
        UpsertItemRequest request,
        IValidator<UpsertItemRequest> validator,
        IItemService service,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        return await HandleValidatedCreateAsync(
            request,
            validator,
            () => service.CreateAsync(request, GetActor(context), cancellationToken));
    }

    private static async Task<IResult> UpdateItemAsync(
        Guid id,
        UpsertItemRequest request,
        IValidator<UpsertItemRequest> validator,
        IItemService service,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        return await HandleValidatedUpdateAsync(
            request,
            validator,
            () => service.UpdateAsync(id, request, GetActor(context), cancellationToken));
    }

    private static async Task<IResult> DeactivateItemAsync(
        Guid id,
        IItemService service,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var result = await service.DeactivateAsync(id, GetActor(context), cancellationToken);
        return result ? Results.NoContent() : Results.NotFound();
    }

    private static async Task<IResult> ListItemCategoriesAsync(
        string? search,
        bool? isActive,
        int? page,
        int? pageSize,
        string? sortBy,
        SortDirection? sortDirection,
        IItemCategoryService service,
        CancellationToken cancellationToken)
    {
        var result = await service.ListAsync(
            new ItemCategoryListQuery(
                search,
                isActive,
                page ?? 1,
                pageSize ?? 20,
                sortBy,
                sortDirection ?? SortDirection.Asc),
            cancellationToken);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetItemCategoryAsync(
        Guid id,
        IItemCategoryService service,
        CancellationToken cancellationToken)
    {
        var result = await service.GetAsync(id, cancellationToken);
        return result is null ? Results.NotFound() : Results.Ok(result);
    }

    private static async Task<IResult> CreateItemCategoryAsync(
        UpsertItemCategoryRequest request,
        IValidator<UpsertItemCategoryRequest> validator,
        IItemCategoryService service,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        return await HandleValidatedCreateAsync(
            request,
            validator,
            () => service.CreateAsync(request, GetActor(context), cancellationToken));
    }

    private static async Task<IResult> UpdateItemCategoryAsync(
        Guid id,
        UpsertItemCategoryRequest request,
        IValidator<UpsertItemCategoryRequest> validator,
        IItemCategoryService service,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        return await HandleValidatedUpdateAsync(
            request,
            validator,
            () => service.UpdateAsync(id, request, GetActor(context), cancellationToken));
    }

    private static async Task<IResult> ActivateItemCategoryAsync(
        Guid id,
        IItemCategoryService service,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var result = await service.ActivateAsync(id, GetActor(context), cancellationToken);
        return result ? Results.NoContent() : Results.NotFound();
    }

    private static async Task<IResult> DeactivateItemCategoryAsync(
        Guid id,
        IItemCategoryService service,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var result = await service.DeactivateAsync(id, GetActor(context), cancellationToken);
        return result ? Results.NoContent() : Results.NotFound();
    }

    private static async Task<IResult> ListUomConversionsAsync(
        bool? isActive,
        string? search,
        Guid? fromUomId,
        Guid? toUomId,
        int? page,
        int? pageSize,
        string? sortBy,
        SortDirection? sortDirection,
        IUomConversionService service,
        CancellationToken cancellationToken)
    {
        var result = await service.ListAsync(
            new UomConversionListQuery(
                isActive,
                search,
                fromUomId,
                toUomId,
                page ?? 1,
                pageSize ?? 20,
                sortBy,
                sortDirection ?? SortDirection.Asc),
            cancellationToken);

        return Results.Ok(result);
    }

    private static async Task<IResult> GetUomConversionAsync(
        Guid id,
        IUomConversionService service,
        CancellationToken cancellationToken)
    {
        var result = await service.GetAsync(id, cancellationToken);
        return result is null ? Results.NotFound() : Results.Ok(result);
    }

    private static async Task<IResult> CreateUomConversionAsync(
        UpsertUomConversionRequest request,
        IValidator<UpsertUomConversionRequest> validator,
        IUomConversionService service,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        return await HandleValidatedCreateAsync(
            request,
            validator,
            () => service.CreateAsync(request, GetActor(context), cancellationToken));
    }

    private static async Task<IResult> UpdateUomConversionAsync(
        Guid id,
        UpsertUomConversionRequest request,
        IValidator<UpsertUomConversionRequest> validator,
        IUomConversionService service,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        return await HandleValidatedUpdateAsync(
            request,
            validator,
            () => service.UpdateAsync(id, request, GetActor(context), cancellationToken));
    }

    private static async Task<IResult> DeactivateUomConversionAsync(
        Guid id,
        IUomConversionService service,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var result = await service.DeactivateAsync(id, GetActor(context), cancellationToken);
        return result ? Results.NoContent() : Results.NotFound();
    }

    private static async Task<IResult> HandleValidatedCreateAsync<TRequest, TResult>(
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
            return Results.Created(string.Empty, result);
        }
        catch (DuplicateEntityException exception)
        {
            return Results.Conflict(new { message = exception.Message });
        }
        catch (InvalidOperationException exception)
        {
            return Results.BadRequest(new { message = exception.Message });
        }
    }

    private static async Task<IResult> HandleValidatedUpdateAsync<TRequest, TResult>(
        TRequest request,
        IValidator<TRequest> validator,
        Func<Task<TResult?>> handler)
        where TResult : class
    {
        var validationResult = await validator.ValidateAsync(request);

        if (!validationResult.IsValid)
        {
            return Results.ValidationProblem(ToErrors(validationResult));
        }

        try
        {
            var result = await handler();
            return result is null ? Results.NotFound() : Results.Ok(result);
        }
        catch (DuplicateEntityException exception)
        {
            return Results.Conflict(new { message = exception.Message });
        }
        catch (InvalidOperationException exception)
        {
            return Results.BadRequest(new { message = exception.Message });
        }
    }

    private static string GetActor(HttpContext context)
    {
        return context.User.Identity?.Name ?? "system";
    }

    private static Dictionary<string, string[]> ToErrors(ValidationResult validationResult)
    {
        return validationResult.Errors
            .GroupBy(error => error.PropertyName)
            .ToDictionary(
                group => group.Key,
                group => group.Select(error => error.ErrorMessage).ToArray());
    }
}
