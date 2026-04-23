using ERP.Application.Shortages;
using ERP.Application.Statements;
using ERP.Domain.Common;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Shortages;

public sealed class ShortageResolutionPostingService(
    AppDbContext dbContext,
    IShortageResolutionService shortageResolutionService,
    IShortageResolutionValidationService validationService,
    IShortageResolutionAllocationService allocationService,
    ISupplierStatementPostingService supplierStatementPostingService) : IShortageResolutionPostingService
{
    public async Task<ShortageResolutionDto?> PostAsync(Guid id, string actor, CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var resolution = await dbContext.ShortageResolutions
            .AsSplitQuery()
            .Include(entity => entity.Supplier)
            .Include(entity => entity.Allocations.OrderBy(allocation => allocation.SequenceNo))
                .ThenInclude(entity => entity.ShortageLedgerEntry!)
                    .ThenInclude(entity => entity.PurchaseReceipt)
                        .ThenInclude(entity => entity!.Supplier)
            .Include(entity => entity.Allocations)
                .ThenInclude(entity => entity.ShortageLedgerEntry!)
                    .ThenInclude(entity => entity.Item)
            .Include(entity => entity.Allocations)
                .ThenInclude(entity => entity.ShortageLedgerEntry!)
                    .ThenInclude(entity => entity.ComponentItem)
            .SingleOrDefaultAsync(entity => entity.Id == id, cancellationToken);

        if (resolution is null)
        {
            return null;
        }

        if (resolution.Status == DocumentStatus.Posted)
        {
            await transaction.CommitAsync(cancellationToken);
            return await shortageResolutionService.GetAsync(id, cancellationToken);
        }

        if (resolution.Status != DocumentStatus.Draft)
        {
            throw new InvalidOperationException("Only Draft shortage resolutions can be posted.");
        }

        var stockEffectsExist = await dbContext.StockLedgerEntries
            .AnyAsync(entity => entity.SourceDocId == resolution.Id && entity.SourceDocType == Domain.Inventory.SourceDocumentType.ShortageResolution, cancellationToken);

        if (stockEffectsExist)
        {
            throw new InvalidOperationException("Stock posting effects already exist for this shortage resolution.");
        }

        var statementEffectsExist = await dbContext.SupplierStatementEntries
            .AnyAsync(
                entity => entity.SourceDocId == resolution.Id &&
                          (entity.SourceDocType == Domain.Statements.SupplierStatementSourceDocumentType.ShortageFinancialResolution ||
                           entity.SourceDocType == Domain.Statements.SupplierStatementSourceDocumentType.ShortageResolution),
                cancellationToken);

        if (statementEffectsExist)
        {
            throw new InvalidOperationException("Supplier statement effects already exist for this shortage resolution.");
        }

        await validationService.ValidateDraftAsync(resolution, cancellationToken);
        await allocationService.ApplyAsync(resolution, actor, cancellationToken);
        await supplierStatementPostingService.CreateFinancialShortageResolutionEntriesAsync(resolution, actor, cancellationToken);

        resolution.TotalQty = resolution.ResolutionType == Domain.Shortages.ShortageResolutionType.Physical
            ? resolution.Allocations.Sum(entity => entity.AllocatedQty ?? 0m)
            : resolution.ResolutionType == Domain.Shortages.ShortageResolutionType.Financial
                ? resolution.Allocations.Sum(entity => entity.FinancialQtyEquivalent ?? 0m)
                : resolution.TotalQty;
        resolution.TotalAmount = resolution.ResolutionType == Domain.Shortages.ShortageResolutionType.Financial
            ? resolution.Allocations.Sum(entity => entity.AllocatedAmount ?? 0m)
            : resolution.TotalAmount;
        resolution.Status = DocumentStatus.Posted;
        resolution.ApprovedBy ??= actor;
        resolution.UpdatedBy = actor;

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return await shortageResolutionService.GetAsync(id, cancellationToken);
    }
}
