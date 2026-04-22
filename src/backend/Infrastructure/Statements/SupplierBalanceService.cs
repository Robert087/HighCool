using ERP.Application.Statements;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Statements;

public sealed class SupplierBalanceService(AppDbContext dbContext) : ISupplierBalanceService
{
    public async Task<SupplierStatementSummaryDto?> GetSummaryAsync(SupplierStatementSummaryQuery query, CancellationToken cancellationToken)
    {
        var supplier = await dbContext.Suppliers
            .AsNoTracking()
            .SingleOrDefaultAsync(entity => entity.Id == query.SupplierId, cancellationToken);

        if (supplier is null)
        {
            return null;
        }

        var currentBalance = await dbContext.SupplierStatementEntries
            .AsNoTracking()
            .Where(entity => entity.SupplierId == query.SupplierId)
            .OrderByDescending(entity => entity.EntryDate)
            .ThenByDescending(entity => entity.CreatedAt)
            .ThenByDescending(entity => entity.Id)
            .Select(entity => entity.RunningBalance)
            .FirstOrDefaultAsync(cancellationToken);

        var filteredQuery = dbContext.SupplierStatementEntries
            .AsNoTracking()
            .Where(entity => entity.SupplierId == query.SupplierId);

        if (query.EffectType.HasValue)
        {
            filteredQuery = filteredQuery.Where(entity => entity.EffectType == query.EffectType.Value);
        }

        if (query.SourceDocType.HasValue)
        {
            filteredQuery = filteredQuery.Where(entity => entity.SourceDocType == query.SourceDocType.Value);
        }

        if (query.FromDate.HasValue)
        {
            filteredQuery = filteredQuery.Where(entity => entity.EntryDate >= query.FromDate.Value);
        }

        if (query.ToDate.HasValue)
        {
            filteredQuery = filteredQuery.Where(entity => entity.EntryDate <= query.ToDate.Value);
        }

        var entries = await filteredQuery
            .OrderBy(entity => entity.EntryDate)
            .ThenBy(entity => entity.CreatedAt)
            .ThenBy(entity => entity.Id)
            .ToListAsync(cancellationToken);

        var openingBalance = 0m;
        if (query.FromDate.HasValue)
        {
            openingBalance = await dbContext.SupplierStatementEntries
                .AsNoTracking()
                .Where(entity => entity.SupplierId == query.SupplierId && entity.EntryDate < query.FromDate.Value)
                .OrderByDescending(entity => entity.EntryDate)
                .ThenByDescending(entity => entity.CreatedAt)
                .ThenByDescending(entity => entity.Id)
                .Select(entity => entity.RunningBalance)
                .FirstOrDefaultAsync(cancellationToken);
        }

        var closingBalance = entries.Count == 0
            ? openingBalance
            : entries[^1].RunningBalance;

        return new SupplierStatementSummaryDto(
            supplier.Id,
            supplier.Code,
            supplier.Name,
            query.FromDate,
            query.ToDate,
            currentBalance,
            openingBalance,
            closingBalance,
            entries.Sum(entity => entity.Debit),
            entries.Sum(entity => entity.Credit),
            entries.Count);
    }
}
