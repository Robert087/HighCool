using ERP.Domain.Inventory;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Inventory;

internal static class StockLedgerAggregation
{
    public static async Task<decimal> SumSignedBaseQtyAsync(
        this IQueryable<StockLedgerEntry> query,
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (dbContext.Database.IsSqlite())
        {
            var total = await query.SumAsync(
                entity => entity.QtyIn > 0m ? (double)entity.BaseQty : -(double)entity.BaseQty,
                cancellationToken);

            return (decimal)total;
        }

        return await query.SumAsync(
            entity => entity.QtyIn > 0m ? entity.BaseQty : -entity.BaseQty,
            cancellationToken);
    }
}
