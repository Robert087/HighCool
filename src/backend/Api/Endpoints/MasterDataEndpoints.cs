using ERP.Application.Common.Exceptions;
using ERP.Application.MasterData.Customers;
using ERP.Application.MasterData.Suppliers;
using ERP.Application.MasterData.Uoms;
using ERP.Application.MasterData.Warehouses;
using ERP.Application.Security;
using FluentValidation;
using FluentValidation.Results;

namespace ERP.Api.Endpoints;

public static class MasterDataEndpoints
{
    public static IEndpointRouteBuilder MapMasterDataEndpoints(this IEndpointRouteBuilder app)
    {
        var customers = app.MapGroup("/api/customers").RequireAuthorization();
        customers.MapGet("/", ListCustomersAsync).AddEndpointFilter(new OrganizationSetupEndpointFilter(true, OrganizationFeatureKeys.SupplierManagement)).AddEndpointFilter(new PermissionEndpointFilter(Permissions.CustomersView));
        customers.MapGet("/{id:guid}", GetCustomerAsync).AddEndpointFilter(new OrganizationSetupEndpointFilter(true, OrganizationFeatureKeys.SupplierManagement)).AddEndpointFilter(new PermissionEndpointFilter(Permissions.CustomersView));
        customers.MapPost("/", CreateCustomerAsync).AddEndpointFilter(new OrganizationSetupEndpointFilter(true, OrganizationFeatureKeys.SupplierManagement)).AddEndpointFilter(new PermissionEndpointFilter(Permissions.CustomersCreate));
        customers.MapPut("/{id:guid}", UpdateCustomerAsync).AddEndpointFilter(new OrganizationSetupEndpointFilter(true, OrganizationFeatureKeys.SupplierManagement)).AddEndpointFilter(new PermissionEndpointFilter(Permissions.CustomersEdit));
        customers.MapPost("/{id:guid}/activate", ActivateCustomerAsync).AddEndpointFilter(new OrganizationSetupEndpointFilter(true, OrganizationFeatureKeys.SupplierManagement)).AddEndpointFilter(new PermissionEndpointFilter(Permissions.CustomersEdit));
        customers.MapPost("/{id:guid}/deactivate", DeactivateCustomerAsync).AddEndpointFilter(new OrganizationSetupEndpointFilter(true, OrganizationFeatureKeys.SupplierManagement)).AddEndpointFilter(new PermissionEndpointFilter(Permissions.CustomersEdit));

        var suppliers = app.MapGroup("/api/suppliers").RequireAuthorization();
        suppliers.MapGet("/", ListSuppliersAsync).AddEndpointFilter(new OrganizationSetupEndpointFilter(true, OrganizationFeatureKeys.SupplierManagement)).AddEndpointFilter(new PermissionEndpointFilter(Permissions.SuppliersView));
        suppliers.MapGet("/{id:guid}", GetSupplierAsync).AddEndpointFilter(new OrganizationSetupEndpointFilter(true, OrganizationFeatureKeys.SupplierManagement)).AddEndpointFilter(new PermissionEndpointFilter(Permissions.SuppliersView));
        suppliers.MapPost("/", CreateSupplierAsync).AddEndpointFilter(new OrganizationSetupEndpointFilter(true, OrganizationFeatureKeys.SupplierManagement)).AddEndpointFilter(new PermissionEndpointFilter(Permissions.SuppliersCreate));
        suppliers.MapPut("/{id:guid}", UpdateSupplierAsync).AddEndpointFilter(new OrganizationSetupEndpointFilter(true, OrganizationFeatureKeys.SupplierManagement)).AddEndpointFilter(new PermissionEndpointFilter(Permissions.SuppliersEdit));
        suppliers.MapPost("/{id:guid}/deactivate", DeactivateSupplierAsync).AddEndpointFilter(new OrganizationSetupEndpointFilter(true, OrganizationFeatureKeys.SupplierManagement)).AddEndpointFilter(new PermissionEndpointFilter(Permissions.SuppliersEdit));

        var warehouses = app.MapGroup("/api/warehouses").RequireAuthorization();
        warehouses.MapGet("/", ListWarehousesAsync).AddEndpointFilter(new OrganizationSetupEndpointFilter(true, OrganizationFeatureKeys.Inventory, OrganizationFeatureKeys.Warehouses)).AddEndpointFilter(new PermissionEndpointFilter(Permissions.InventoryWarehouseManage));
        warehouses.MapGet("/{id:guid}", GetWarehouseAsync).AddEndpointFilter(new OrganizationSetupEndpointFilter(true, OrganizationFeatureKeys.Inventory, OrganizationFeatureKeys.Warehouses)).AddEndpointFilter(new PermissionEndpointFilter(Permissions.InventoryWarehouseManage));
        warehouses.MapPost("/", CreateWarehouseAsync).AddEndpointFilter(new OrganizationSetupEndpointFilter(true, OrganizationFeatureKeys.Inventory, OrganizationFeatureKeys.Warehouses)).AddEndpointFilter(new PermissionEndpointFilter(Permissions.InventoryWarehouseManage));
        warehouses.MapPut("/{id:guid}", UpdateWarehouseAsync).AddEndpointFilter(new OrganizationSetupEndpointFilter(true, OrganizationFeatureKeys.Inventory, OrganizationFeatureKeys.Warehouses)).AddEndpointFilter(new PermissionEndpointFilter(Permissions.InventoryWarehouseManage));
        warehouses.MapPost("/{id:guid}/deactivate", DeactivateWarehouseAsync).AddEndpointFilter(new OrganizationSetupEndpointFilter(true, OrganizationFeatureKeys.Inventory, OrganizationFeatureKeys.Warehouses)).AddEndpointFilter(new PermissionEndpointFilter(Permissions.InventoryWarehouseManage));

        var uoms = app.MapGroup("/api/uoms").RequireAuthorization();
        uoms.MapGet("/", ListUomsAsync).AddEndpointFilter(new OrganizationSetupEndpointFilter(true, OrganizationFeatureKeys.Uom)).AddEndpointFilter(new PermissionEndpointFilter(Permissions.UomsManage));
        uoms.MapGet("/{id:guid}", GetUomAsync).AddEndpointFilter(new OrganizationSetupEndpointFilter(true, OrganizationFeatureKeys.Uom)).AddEndpointFilter(new PermissionEndpointFilter(Permissions.UomsManage));
        uoms.MapPost("/", CreateUomAsync).AddEndpointFilter(new OrganizationSetupEndpointFilter(true, OrganizationFeatureKeys.Uom)).AddEndpointFilter(new PermissionEndpointFilter(Permissions.UomsManage));
        uoms.MapPut("/{id:guid}", UpdateUomAsync).AddEndpointFilter(new OrganizationSetupEndpointFilter(true, OrganizationFeatureKeys.Uom)).AddEndpointFilter(new PermissionEndpointFilter(Permissions.UomsManage));
        uoms.MapPost("/{id:guid}/deactivate", DeactivateUomAsync).AddEndpointFilter(new OrganizationSetupEndpointFilter(true, OrganizationFeatureKeys.Uom)).AddEndpointFilter(new PermissionEndpointFilter(Permissions.UomsManage));

        return app;
    }

    private static async Task<IResult> ListCustomersAsync(
        string? search,
        bool? isActive,
        ICustomerService service,
        CancellationToken cancellationToken)
    {
        var result = await service.ListAsync(new CustomerListQuery(search, isActive), cancellationToken);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetCustomerAsync(
        Guid id,
        ICustomerService service,
        CancellationToken cancellationToken)
    {
        var result = await service.GetAsync(id, cancellationToken);
        return result is null ? Results.NotFound() : Results.Ok(result);
    }

    private static async Task<IResult> CreateCustomerAsync(
        CreateCustomerRequest request,
        IValidator<CreateCustomerRequest> validator,
        ICustomerService service,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        return await HandleValidatedRequestAsync(
            request,
            validator,
            () => service.CreateAsync(request, GetActor(context), cancellationToken));
    }

    private static async Task<IResult> UpdateCustomerAsync(
        Guid id,
        UpdateCustomerRequest request,
        IValidator<UpdateCustomerRequest> validator,
        ICustomerService service,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var validationResult = await validator.ValidateAsync(request, cancellationToken);

        if (!validationResult.IsValid)
        {
            return Results.ValidationProblem(ToErrors(validationResult));
        }

        try
        {
            var result = await service.UpdateAsync(id, request, GetActor(context), cancellationToken);
            return result is null ? Results.NotFound() : Results.Ok(result);
        }
        catch (DuplicateEntityException exception)
        {
            return Results.Conflict(new { message = exception.Message });
        }
    }

    private static async Task<IResult> ActivateCustomerAsync(
        Guid id,
        ICustomerService service,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var activated = await service.ActivateAsync(id, GetActor(context), cancellationToken);
        return activated ? Results.NoContent() : Results.NotFound();
    }

    private static async Task<IResult> DeactivateCustomerAsync(
        Guid id,
        ICustomerService service,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var deactivated = await service.DeactivateAsync(id, GetActor(context), cancellationToken);
        return deactivated ? Results.NoContent() : Results.NotFound();
    }

    private static async Task<IResult> ListSuppliersAsync(
        string? search,
        bool? isActive,
        ISupplierService service,
        CancellationToken cancellationToken)
    {
        var result = await service.ListAsync(new SupplierListQuery(search, isActive), cancellationToken);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetSupplierAsync(
        Guid id,
        ISupplierService service,
        CancellationToken cancellationToken)
    {
        var result = await service.GetAsync(id, cancellationToken);
        return result is null ? Results.NotFound() : Results.Ok(result);
    }

    private static async Task<IResult> CreateSupplierAsync(
        UpsertSupplierRequest request,
        IValidator<UpsertSupplierRequest> validator,
        ISupplierService service,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        return await HandleValidatedRequestAsync(
            request,
            validator,
            () => service.CreateAsync(request, GetActor(context), cancellationToken));
    }

    private static async Task<IResult> UpdateSupplierAsync(
        Guid id,
        UpsertSupplierRequest request,
        IValidator<UpsertSupplierRequest> validator,
        ISupplierService service,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var validationResult = await validator.ValidateAsync(request, cancellationToken);

        if (!validationResult.IsValid)
        {
            return Results.ValidationProblem(ToErrors(validationResult));
        }

        try
        {
            var result = await service.UpdateAsync(id, request, GetActor(context), cancellationToken);
            return result is null ? Results.NotFound() : Results.Ok(result);
        }
        catch (DuplicateEntityException exception)
        {
            return Results.Conflict(new { message = exception.Message });
        }
    }

    private static async Task<IResult> DeactivateSupplierAsync(
        Guid id,
        ISupplierService service,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var deactivated = await service.DeactivateAsync(id, GetActor(context), cancellationToken);
        return deactivated ? Results.NoContent() : Results.NotFound();
    }

    private static async Task<IResult> ListWarehousesAsync(
        string? search,
        bool? isActive,
        IWarehouseService service,
        CancellationToken cancellationToken)
    {
        var result = await service.ListAsync(new WarehouseListQuery(search, isActive), cancellationToken);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetWarehouseAsync(
        Guid id,
        IWarehouseService service,
        CancellationToken cancellationToken)
    {
        var result = await service.GetAsync(id, cancellationToken);
        return result is null ? Results.NotFound() : Results.Ok(result);
    }

    private static async Task<IResult> CreateWarehouseAsync(
        UpsertWarehouseRequest request,
        IValidator<UpsertWarehouseRequest> validator,
        IWarehouseService service,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        return await HandleValidatedRequestAsync(
            request,
            validator,
            () => service.CreateAsync(request, GetActor(context), cancellationToken));
    }

    private static async Task<IResult> UpdateWarehouseAsync(
        Guid id,
        UpsertWarehouseRequest request,
        IValidator<UpsertWarehouseRequest> validator,
        IWarehouseService service,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var validationResult = await validator.ValidateAsync(request, cancellationToken);

        if (!validationResult.IsValid)
        {
            return Results.ValidationProblem(ToErrors(validationResult));
        }

        try
        {
            var result = await service.UpdateAsync(id, request, GetActor(context), cancellationToken);
            return result is null ? Results.NotFound() : Results.Ok(result);
        }
        catch (DuplicateEntityException exception)
        {
            return Results.Conflict(new { message = exception.Message });
        }
    }

    private static async Task<IResult> DeactivateWarehouseAsync(
        Guid id,
        IWarehouseService service,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var deactivated = await service.DeactivateAsync(id, GetActor(context), cancellationToken);
        return deactivated ? Results.NoContent() : Results.NotFound();
    }

    private static async Task<IResult> ListUomsAsync(
        string? search,
        bool? isActive,
        IUomService service,
        CancellationToken cancellationToken)
    {
        var result = await service.ListAsync(new UomListQuery(search, isActive), cancellationToken);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetUomAsync(
        Guid id,
        IUomService service,
        CancellationToken cancellationToken)
    {
        var result = await service.GetAsync(id, cancellationToken);
        return result is null ? Results.NotFound() : Results.Ok(result);
    }

    private static async Task<IResult> CreateUomAsync(
        UpsertUomRequest request,
        IValidator<UpsertUomRequest> validator,
        IUomService service,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        return await HandleValidatedRequestAsync(
            request,
            validator,
            () => service.CreateAsync(request, GetActor(context), cancellationToken));
    }

    private static async Task<IResult> UpdateUomAsync(
        Guid id,
        UpsertUomRequest request,
        IValidator<UpsertUomRequest> validator,
        IUomService service,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var validationResult = await validator.ValidateAsync(request, cancellationToken);

        if (!validationResult.IsValid)
        {
            return Results.ValidationProblem(ToErrors(validationResult));
        }

        try
        {
            var result = await service.UpdateAsync(id, request, GetActor(context), cancellationToken);
            return result is null ? Results.NotFound() : Results.Ok(result);
        }
        catch (DuplicateEntityException exception)
        {
            return Results.Conflict(new { message = exception.Message });
        }
    }

    private static async Task<IResult> DeactivateUomAsync(
        Guid id,
        IUomService service,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var deactivated = await service.DeactivateAsync(id, GetActor(context), cancellationToken);
        return deactivated ? Results.NoContent() : Results.NotFound();
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
            return Results.Created(string.Empty, result);
        }
        catch (DuplicateEntityException exception)
        {
            return Results.Conflict(new { message = exception.Message });
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
