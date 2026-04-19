using ERP.Application.Common.Exceptions;
using ERP.Application.MasterData.ItemComponents;
using ERP.Application.MasterData.Items;
using ERP.Application.MasterData.ItemUomConversions;
using FluentValidation;
using FluentValidation.Results;

namespace ERP.Api.Endpoints;

public static class ItemMasterDataEndpoints
{
    public static IEndpointRouteBuilder MapItemMasterDataEndpoints(this IEndpointRouteBuilder app)
    {
        var items = app.MapGroup("/api/items");
        items.MapGet("/", ListItemsAsync);
        items.MapGet("/{id:guid}", GetItemAsync);
        items.MapPost("/", CreateItemAsync);
        items.MapPut("/{id:guid}", UpdateItemAsync);
        items.MapPost("/{id:guid}/deactivate", DeactivateItemAsync);

        var components = app.MapGroup("/api/item-components");
        components.MapGet("/", ListItemComponentsAsync);
        components.MapGet("/{id:guid}", GetItemComponentAsync);
        components.MapPost("/", CreateItemComponentAsync);
        components.MapPut("/{id:guid}", UpdateItemComponentAsync);
        components.MapDelete("/{id:guid}", DeleteItemComponentAsync);

        var conversions = app.MapGroup("/api/item-uom-conversions");
        conversions.MapGet("/", ListItemUomConversionsAsync);
        conversions.MapGet("/{id:guid}", GetItemUomConversionAsync);
        conversions.MapPost("/", CreateItemUomConversionAsync);
        conversions.MapPut("/{id:guid}", UpdateItemUomConversionAsync);
        conversions.MapPost("/{id:guid}/deactivate", DeactivateItemUomConversionAsync);

        return app;
    }

    private static async Task<IResult> ListItemsAsync(
        string? search,
        bool? isActive,
        IItemService service,
        CancellationToken cancellationToken)
    {
        var result = await service.ListAsync(new ItemListQuery(search, isActive), cancellationToken);
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

    private static async Task<IResult> ListItemComponentsAsync(
        Guid? parentItemId,
        Guid? componentItemId,
        string? search,
        IItemComponentService service,
        CancellationToken cancellationToken)
    {
        var result = await service.ListAsync(
            new ItemComponentListQuery(parentItemId, componentItemId, search),
            cancellationToken);

        return Results.Ok(result);
    }

    private static async Task<IResult> GetItemComponentAsync(
        Guid id,
        IItemComponentService service,
        CancellationToken cancellationToken)
    {
        var result = await service.GetAsync(id, cancellationToken);
        return result is null ? Results.NotFound() : Results.Ok(result);
    }

    private static async Task<IResult> CreateItemComponentAsync(
        UpsertItemComponentRequest request,
        IValidator<UpsertItemComponentRequest> validator,
        IItemComponentService service,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        return await HandleValidatedCreateAsync(
            request,
            validator,
            () => service.CreateAsync(request, GetActor(context), cancellationToken));
    }

    private static async Task<IResult> UpdateItemComponentAsync(
        Guid id,
        UpsertItemComponentRequest request,
        IValidator<UpsertItemComponentRequest> validator,
        IItemComponentService service,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        return await HandleValidatedUpdateAsync(
            request,
            validator,
            () => service.UpdateAsync(id, request, GetActor(context), cancellationToken));
    }

    private static async Task<IResult> DeleteItemComponentAsync(
        Guid id,
        IItemComponentService service,
        CancellationToken cancellationToken)
    {
        var result = await service.DeleteAsync(id, cancellationToken);
        return result ? Results.NoContent() : Results.NotFound();
    }

    private static async Task<IResult> ListItemUomConversionsAsync(
        Guid? itemId,
        bool? isActive,
        string? search,
        IItemUomConversionService service,
        CancellationToken cancellationToken)
    {
        var result = await service.ListAsync(
            new ItemUomConversionListQuery(itemId, isActive, search),
            cancellationToken);

        return Results.Ok(result);
    }

    private static async Task<IResult> GetItemUomConversionAsync(
        Guid id,
        IItemUomConversionService service,
        CancellationToken cancellationToken)
    {
        var result = await service.GetAsync(id, cancellationToken);
        return result is null ? Results.NotFound() : Results.Ok(result);
    }

    private static async Task<IResult> CreateItemUomConversionAsync(
        UpsertItemUomConversionRequest request,
        IValidator<UpsertItemUomConversionRequest> validator,
        IItemUomConversionService service,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        return await HandleValidatedCreateAsync(
            request,
            validator,
            () => service.CreateAsync(request, GetActor(context), cancellationToken));
    }

    private static async Task<IResult> UpdateItemUomConversionAsync(
        Guid id,
        UpsertItemUomConversionRequest request,
        IValidator<UpsertItemUomConversionRequest> validator,
        IItemUomConversionService service,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        return await HandleValidatedUpdateAsync(
            request,
            validator,
            () => service.UpdateAsync(id, request, GetActor(context), cancellationToken));
    }

    private static async Task<IResult> DeactivateItemUomConversionAsync(
        Guid id,
        IItemUomConversionService service,
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
