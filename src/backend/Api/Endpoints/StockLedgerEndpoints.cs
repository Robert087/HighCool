using ERP.Application.Inventory;
using ERP.Application.Common.Pagination;
using ERP.Domain.Inventory;

namespace ERP.Api.Endpoints;

public static class StockLedgerEndpoints
{
    public static IEndpointRouteBuilder MapStockLedgerEndpoints(this IEndpointRouteBuilder app)
    {
        var ledger = app.MapGroup("/api/stock-ledger");
        ledger.MapGet("/", ListLedgerAsync);
        ledger.MapGet("/item/{itemId:guid}", ListLedgerByItemAsync);

        var balance = app.MapGroup("/api/stock-balance");
        balance.MapGet("/", ListBalanceAsync);
        balance.MapGet("/item/{itemId:guid}", ListBalanceByItemAsync);

        return app;
    }

    private static async Task<IResult> ListLedgerAsync(
        string? search,
        Guid? itemId,
        Guid? warehouseId,
        StockTransactionType? transactionType,
        DateTime? fromDate,
        DateTime? toDate,
        int? page,
        int? pageSize,
        string? sortBy,
        SortDirection? sortDirection,
        IStockLedgerQueryService service,
        CancellationToken cancellationToken)
    {
        var validationError = ValidateDateRange(fromDate, toDate);
        if (validationError is not null)
        {
            return Results.BadRequest(new { message = validationError });
        }

        try
        {
            var result = await service.ListAsync(
                new StockLedgerQuery(search, itemId, warehouseId, transactionType, fromDate, toDate, page ?? 1, pageSize ?? 20, sortBy, sortDirection ?? SortDirection.Desc),
                cancellationToken);

            return Results.Ok(result);
        }
        catch (InvalidOperationException exception)
        {
            return Results.BadRequest(new { message = exception.Message });
        }
    }

    private static Task<IResult> ListLedgerByItemAsync(
        Guid itemId,
        string? search,
        Guid? warehouseId,
        StockTransactionType? transactionType,
        DateTime? fromDate,
        DateTime? toDate,
        int? page,
        int? pageSize,
        string? sortBy,
        SortDirection? sortDirection,
        IStockLedgerQueryService service,
        CancellationToken cancellationToken)
    {
        return ListLedgerAsync(search, itemId, warehouseId, transactionType, fromDate, toDate, page, pageSize, sortBy, sortDirection, service, cancellationToken);
    }

    private static async Task<IResult> ListBalanceAsync(
        string? search,
        Guid? itemId,
        Guid? warehouseId,
        StockTransactionType? transactionType,
        DateTime? fromDate,
        DateTime? toDate,
        int? page,
        int? pageSize,
        string? sortBy,
        SortDirection? sortDirection,
        IStockBalanceService service,
        CancellationToken cancellationToken)
    {
        var validationError = ValidateDateRange(fromDate, toDate);
        if (validationError is not null)
        {
            return Results.BadRequest(new { message = validationError });
        }

        try
        {
            var result = await service.ListAsync(
                new StockBalanceQuery(search, itemId, warehouseId, transactionType, fromDate, toDate, page ?? 1, pageSize ?? 20, sortBy, sortDirection ?? SortDirection.Asc),
                cancellationToken);

            return Results.Ok(result);
        }
        catch (InvalidOperationException exception)
        {
            return Results.BadRequest(new { message = exception.Message });
        }
    }

    private static Task<IResult> ListBalanceByItemAsync(
        Guid itemId,
        string? search,
        Guid? warehouseId,
        StockTransactionType? transactionType,
        DateTime? fromDate,
        DateTime? toDate,
        int? page,
        int? pageSize,
        string? sortBy,
        SortDirection? sortDirection,
        IStockBalanceService service,
        CancellationToken cancellationToken)
    {
        return ListBalanceAsync(search, itemId, warehouseId, transactionType, fromDate, toDate, page, pageSize, sortBy, sortDirection, service, cancellationToken);
    }

    private static string? ValidateDateRange(DateTime? fromDate, DateTime? toDate)
    {
        if (fromDate.HasValue && toDate.HasValue && fromDate.Value > toDate.Value)
        {
            return "From date cannot be later than to date.";
        }

        return null;
    }
}
