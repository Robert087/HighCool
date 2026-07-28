using ERP.Application.Inventory.Adjustments;
using ERP.Application.Purchasing.PurchaseReceipts;
using ERP.Domain.Common;
using ERP.Domain.Inventory;
using ERP.Domain.MasterData;
using ERP.Infrastructure.Common.Numbering;
using ERP.Infrastructure.Inventory;
using ERP.Infrastructure.Inventory.Adjustments;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Purchasing.PurchaseReceipts;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ERP.Application.Tests;

public sealed class InventoryAdjustmentWorkflowTests
{
    [Fact]
    public async Task CreateDraft_ShouldGenerateSequentialNumbersAndKeepNumberImmutableOnUpdate()
    {
        await using var dbContext = CreateDbContext();
        var references = await SeedReferencesAsync(dbContext);
        var service = CreateDocumentService(dbContext);

        var first = await service.CreateDraftAsync(
            Request(
                "IGNORED-1",
                references,
                [
                    Line(1, references.Item.Id, references.PieceUom.Id, 1m, InventoryAdjustmentType.Increase)
                ]),
            "tester",
            CancellationToken.None);
        var second = await service.CreateDraftAsync(
            Request(
                "IGNORED-2",
                references,
                [
                    Line(1, references.Item.Id, references.PieceUom.Id, 1m, InventoryAdjustmentType.Increase)
                ]),
            "tester",
            CancellationToken.None);

        var updated = await service.UpdateDraftAsync(
            first.Id,
            Request(
                "ADJ-HACK",
                references,
                [
                    Line(1, references.Item.Id, references.BoxUom.Id, 1m, InventoryAdjustmentType.Increase)
                ]),
            "tester",
            CancellationToken.None);

        Assert.Equal("ADJ-000001", first.AdjustmentNo);
        Assert.Equal("ADJ-000002", second.AdjustmentNo);
        Assert.NotNull(updated);
        Assert.Equal("ADJ-000001", updated!.AdjustmentNo);
        Assert.Equal(3, await dbContext.DocumentNumberSequences.Select(entity => entity.NextValue).SingleAsync());
    }

    [Fact]
    public async Task CreateDraft_ShouldInitializeSequenceFromExistingGeneratedNumbers()
    {
        await using var dbContext = CreateDbContext();
        var references = await SeedReferencesAsync(dbContext);
        dbContext.InventoryAdjustments.AddRange(
            new InventoryAdjustment
            {
                AdjustmentNo = "ADJ-000007",
                AdjustmentDate = DateTime.UtcNow.Date,
                WarehouseId = references.Warehouse.Id,
                Reason = "Legacy generated number",
                Status = DocumentStatus.Draft,
                CreatedBy = "seed"
            },
            new InventoryAdjustment
            {
                AdjustmentNo = "ADJ-20260728012852999",
                AdjustmentDate = DateTime.UtcNow.Date,
                WarehouseId = references.Warehouse.Id,
                Reason = "Legacy timestamp number",
                Status = DocumentStatus.Draft,
                CreatedBy = "seed"
            });
        await dbContext.SaveChangesAsync();
        var service = CreateDocumentService(dbContext);

        var created = await service.CreateDraftAsync(
            Request(
                null,
                references,
                [
                    Line(1, references.Item.Id, references.PieceUom.Id, 1m, InventoryAdjustmentType.Increase)
                ]),
            "tester",
            CancellationToken.None);

        Assert.Equal("ADJ-000008", created.AdjustmentNo);
        Assert.Contains("ADJ-20260728012852999", await dbContext.InventoryAdjustments.Select(entity => entity.AdjustmentNo).ToListAsync());
    }

    [Fact]
    public async Task CreateDraftsConcurrently_ShouldGenerateUniqueSequentialNumbers()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"highcool-adjustment-concurrent-create-{Guid.NewGuid():N}.db");
        try
        {
            var initContext = TestOrganizationContext.CreateExecutionContext();
            await using (var dbContext = CreateDbContext(databasePath, initContext))
            {
                await dbContext.Database.EnsureCreatedAsync();
                await TestOrganizationContext.EnsureOrganizationAsync(dbContext, initContext);
                await SeedReferencesAsync(dbContext);
            }

            var organizationId = initContext.OrganizationId!.Value;
            var tasks = Enumerable.Range(0, 6)
                .Select(_ => Task.Run(async () =>
                {
                    var executionContext = TestOrganizationContext.CreateExecutionContext(organizationId);
                    await using var dbContext = CreateDbContext(databasePath, executionContext);
                    var references = await LoadReferencesAsync(dbContext);
                    var service = CreateDocumentService(dbContext);

                    var created = await service.CreateDraftAsync(
                        Request(
                            null,
                            references,
                            [
                                Line(1, references.Item.Id, references.PieceUom.Id, 1m, InventoryAdjustmentType.Increase)
                            ]),
                        "tester",
                        CancellationToken.None);
                    return created.AdjustmentNo;
                }))
                .ToArray();

            var numbers = await Task.WhenAll(tasks);

            Assert.Equal(6, numbers.Distinct(StringComparer.OrdinalIgnoreCase).Count());
            Assert.Equal<string>(
                ["ADJ-000001", "ADJ-000002", "ADJ-000003", "ADJ-000004", "ADJ-000005", "ADJ-000006"],
                numbers.Order(StringComparer.Ordinal).ToArray());
        }
        finally
        {
            SqliteTestDatabase.DeleteSqliteFileSet(databasePath);
        }
    }

    [Fact]
    public async Task CreateUpdateAndDeleteDraft_ShouldPersistServerCalculatedBaseQty()
    {
        await using var dbContext = CreateDbContext();
        var references = await SeedReferencesAsync(dbContext);
        var service = CreateDocumentService(dbContext);

        var created = await service.CreateDraftAsync(
            Request(
                "ADJ-DRAFT-1",
                references,
                [
                    Line(1, references.Item.Id, references.BoxUom.Id, 2m, InventoryAdjustmentType.Increase)
                ]),
            "tester",
            CancellationToken.None);

        Assert.Equal(DocumentStatus.Draft, created.Status);
        Assert.Equal(20m, created.Lines.Single().BaseQty);

        var updated = await service.UpdateDraftAsync(
            created.Id,
            Request(
                "ADJ-DRAFT-1",
                references,
                [
                    Line(1, references.Item.Id, references.PieceUom.Id, 5m, InventoryAdjustmentType.Decrease)
                ]),
            "tester",
            CancellationToken.None);

        Assert.NotNull(updated);
        Assert.Equal(5m, updated!.Lines.Single().BaseQty);

        Assert.True(await service.DeleteDraftAsync(created.Id, CancellationToken.None));
        Assert.Equal(0, await dbContext.InventoryAdjustments.CountAsync());
    }

    [Fact]
    public async Task PostIncrease_ShouldCreateAppendOnlyStockLedgerEntryAndBeIdempotent()
    {
        await using var dbContext = CreateDbContext();
        var references = await SeedReferencesAsync(dbContext);
        var documentService = CreateDocumentService(dbContext);
        var postingService = CreatePostingService(dbContext);
        var created = await documentService.CreateDraftAsync(
            Request(
                "ADJ-IN-1",
                references,
                [
                    Line(1, references.Item.Id, references.BoxUom.Id, 2m, InventoryAdjustmentType.Increase)
                ]),
            "tester",
            CancellationToken.None);

        var posted = await postingService.PostAsync(created.Id, "poster", CancellationToken.None);
        var secondPost = await postingService.PostAsync(created.Id, "poster", CancellationToken.None);

        Assert.NotNull(posted);
        Assert.NotNull(secondPost);
        Assert.Equal(DocumentStatus.Posted, posted!.Status);
        Assert.Equal(DocumentStatus.Posted, secondPost!.Status);

        var entry = await dbContext.StockLedgerEntries.SingleAsync();
        Assert.Equal(StockTransactionType.InventoryAdjustmentIncrease, entry.TransactionType);
        Assert.Equal(SourceDocumentType.InventoryAdjustment, entry.SourceDocType);
        Assert.Equal(created.Id, entry.SourceDocId);
        Assert.Equal(2m, entry.QtyIn);
        Assert.Equal(0m, entry.QtyOut);
        Assert.Equal(20m, entry.BaseQty);
        Assert.Equal(20m, entry.RunningBalanceQty);
    }

    [Fact]
    public async Task PostDecrease_ShouldRejectAggregatedDuplicateLinesWhenNegativeStockIsDisabled()
    {
        await using var dbContext = CreateDbContext();
        var references = await SeedReferencesAsync(dbContext);
        await SeedBalanceAsync(dbContext, references, 8m);
        var documentService = CreateDocumentService(dbContext);
        var postingService = CreatePostingService(dbContext);
        var created = await documentService.CreateDraftAsync(
            Request(
                "ADJ-OUT-AGG",
                references,
                [
                    Line(1, references.Item.Id, references.PieceUom.Id, 5m, InventoryAdjustmentType.Decrease),
                    Line(2, references.Item.Id, references.PieceUom.Id, 4m, InventoryAdjustmentType.Decrease)
                ]),
            "tester",
            CancellationToken.None);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            postingService.PostAsync(created.Id, "poster", CancellationToken.None));

        Assert.Contains("Insufficient stock", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("line 1; Inventory adjustment", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("line 2", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(DocumentStatus.Draft, await dbContext.InventoryAdjustments.Where(entity => entity.Id == created.Id).Select(entity => entity.Status).SingleAsync());
        Assert.Equal(1, await dbContext.StockLedgerEntries.CountAsync());
    }

    [Fact]
    public async Task PostDecrease_ShouldAllowNegativeStockWhenOrganizationAllowsIt()
    {
        await using var dbContext = CreateDbContext();
        await SetAllowNegativeStockAsync(dbContext, true);
        var references = await SeedReferencesAsync(dbContext);
        var documentService = CreateDocumentService(dbContext);
        var postingService = CreatePostingService(dbContext);
        var created = await documentService.CreateDraftAsync(
            Request(
                "ADJ-NEG-OK",
                references,
                [
                    Line(1, references.Item.Id, references.PieceUom.Id, 3m, InventoryAdjustmentType.Decrease)
                ]),
            "tester",
            CancellationToken.None);

        var posted = await postingService.PostAsync(created.Id, "poster", CancellationToken.None);

        Assert.NotNull(posted);
        Assert.Equal(DocumentStatus.Posted, posted!.Status);
        var entry = await dbContext.StockLedgerEntries.SingleAsync();
        Assert.Equal(StockTransactionType.InventoryAdjustmentDecrease, entry.TransactionType);
        Assert.Equal(0m, entry.QtyIn);
        Assert.Equal(3m, entry.QtyOut);
        Assert.Equal(-3m, entry.RunningBalanceQty);
    }

    [Fact]
    public async Task CancelPostedIncrease_ShouldCreateReversingLedgerEntryWithoutEditingOriginal()
    {
        await using var dbContext = CreateDbContext();
        var references = await SeedReferencesAsync(dbContext);
        var documentService = CreateDocumentService(dbContext);
        var postingService = CreatePostingService(dbContext);
        var created = await documentService.CreateDraftAsync(
            Request(
                "ADJ-CANCEL-1",
                references,
                [
                    Line(1, references.Item.Id, references.PieceUom.Id, 6m, InventoryAdjustmentType.Increase)
                ]),
            "tester",
            CancellationToken.None);
        await postingService.PostAsync(created.Id, "poster", CancellationToken.None);

        var canceled = await postingService.CancelAsync(created.Id, "cancel-user", CancellationToken.None);
        var secondCancel = await postingService.CancelAsync(created.Id, "cancel-user", CancellationToken.None);

        Assert.NotNull(canceled);
        Assert.NotNull(secondCancel);
        Assert.Equal(DocumentStatus.Canceled, canceled!.Status);
        Assert.Equal(DocumentStatus.Canceled, secondCancel!.Status);

        var entries = await dbContext.StockLedgerEntries
            .OrderBy(entity => entity.CreatedAt)
            .ToListAsync();

        Assert.Equal(2, entries.Count);
        Assert.Equal(StockTransactionType.InventoryAdjustmentIncrease, entries[0].TransactionType);
        Assert.Equal(6m, entries[0].QtyIn);
        Assert.Equal(0m, entries[0].QtyOut);
        Assert.Equal(6m, entries[0].RunningBalanceQty);
        Assert.Equal(StockTransactionType.InventoryAdjustmentCancellation, entries[1].TransactionType);
        Assert.Equal(SourceDocumentType.InventoryAdjustmentCancellation, entries[1].SourceDocType);
        Assert.Equal(0m, entries[1].QtyIn);
        Assert.Equal(6m, entries[1].QtyOut);
        Assert.Equal(0m, entries[1].RunningBalanceQty);
    }

    [Fact]
    public async Task PostDecrease_ShouldAllowExactZeroStockAndRemainAtomic()
    {
        await using var dbContext = CreateDbContext();
        var references = await SeedReferencesAsync(dbContext);
        await SeedBalanceAsync(dbContext, references, 5m);
        var documentService = CreateDocumentService(dbContext);
        var postingService = CreatePostingService(dbContext);
        var created = await documentService.CreateDraftAsync(
            Request(
                "ADJ-ZERO-OK",
                references,
                [
                    Line(1, references.Item.Id, references.PieceUom.Id, 5m, InventoryAdjustmentType.Decrease)
                ]),
            "tester",
            CancellationToken.None);

        var posted = await postingService.PostAsync(created.Id, "poster", CancellationToken.None);

        Assert.NotNull(posted);
        Assert.Equal(DocumentStatus.Posted, posted!.Status);

        var adjustmentEntry = await dbContext.StockLedgerEntries
            .Where(entry => entry.SourceDocId == created.Id)
            .SingleAsync();

        Assert.Equal(StockTransactionType.InventoryAdjustmentDecrease, adjustmentEntry.TransactionType);
        Assert.Equal(0m, adjustmentEntry.RunningBalanceQty);
        Assert.Equal(2, await dbContext.StockLedgerEntries.CountAsync());
    }

    [Fact]
    public async Task PostDecrease_ShouldAggregateMixedUomBeforeNegativeStockValidation()
    {
        await using var dbContext = CreateDbContext();
        var references = await SeedReferencesAsync(dbContext);
        await SeedBalanceAsync(dbContext, references, 24m);
        var documentService = CreateDocumentService(dbContext);
        var postingService = CreatePostingService(dbContext);
        var created = await documentService.CreateDraftAsync(
            Request(
                "ADJ-MIXED-UOM",
                references,
                [
                    Line(1, references.Item.Id, references.BoxUom.Id, 1m, InventoryAdjustmentType.Decrease),
                    Line(2, references.Item.Id, references.PieceUom.Id, 15m, InventoryAdjustmentType.Decrease)
                ]),
            "tester",
            CancellationToken.None);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            postingService.PostAsync(created.Id, "poster", CancellationToken.None));

        Assert.Contains("Insufficient stock", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(DocumentStatus.Draft, await dbContext.InventoryAdjustments.Where(entity => entity.Id == created.Id).Select(entity => entity.Status).SingleAsync());
        Assert.Equal(1, await dbContext.StockLedgerEntries.CountAsync());
    }

    [Fact]
    public async Task PostMixedIncreaseAndDecreaseSameItem_ShouldNotUseUnpostedIncreaseForAvailability()
    {
        await using var dbContext = CreateDbContext();
        var references = await SeedReferencesAsync(dbContext);
        var documentService = CreateDocumentService(dbContext);
        var postingService = CreatePostingService(dbContext);
        var created = await documentService.CreateDraftAsync(
            Request(
                "ADJ-MIXED-SAME-ITEM",
                references,
                [
                    Line(1, references.Item.Id, references.PieceUom.Id, 10m, InventoryAdjustmentType.Increase),
                    Line(2, references.Item.Id, references.PieceUom.Id, 5m, InventoryAdjustmentType.Decrease)
                ]),
            "tester",
            CancellationToken.None);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            postingService.PostAsync(created.Id, "poster", CancellationToken.None));

        Assert.Contains("Insufficient stock", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(DocumentStatus.Draft, await dbContext.InventoryAdjustments.Where(entity => entity.Id == created.Id).Select(entity => entity.Status).SingleAsync());
        Assert.Equal(0, await dbContext.StockLedgerEntries.CountAsync());
    }

    [Fact]
    public async Task CancelPostedDecrease_ShouldCreateIncreaseReversalAndBlockPostAfterCancel()
    {
        await using var dbContext = CreateDbContext();
        var references = await SeedReferencesAsync(dbContext);
        await SeedBalanceAsync(dbContext, references, 10m);
        var documentService = CreateDocumentService(dbContext);
        var postingService = CreatePostingService(dbContext);
        var created = await documentService.CreateDraftAsync(
            Request(
                "ADJ-CANCEL-OUT",
                references,
                [
                    Line(1, references.Item.Id, references.PieceUom.Id, 4m, InventoryAdjustmentType.Decrease)
                ]),
            "tester",
            CancellationToken.None);

        await postingService.PostAsync(created.Id, "poster", CancellationToken.None);
        var canceled = await postingService.CancelAsync(created.Id, "cancel-user", CancellationToken.None);
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            postingService.PostAsync(created.Id, "poster", CancellationToken.None));

        Assert.NotNull(canceled);
        Assert.Equal(DocumentStatus.Canceled, canceled!.Status);
        Assert.Contains("Only Draft", exception.Message, StringComparison.OrdinalIgnoreCase);

        var entries = await dbContext.StockLedgerEntries
            .Where(entry => entry.SourceDocId == created.Id)
            .OrderBy(entry => entry.CreatedAt)
            .ToListAsync();

        Assert.Equal(2, entries.Count);
        Assert.Equal(StockTransactionType.InventoryAdjustmentDecrease, entries[0].TransactionType);
        Assert.Equal(4m, entries[0].QtyOut);
        Assert.Equal(6m, entries[0].RunningBalanceQty);
        Assert.Equal(StockTransactionType.InventoryAdjustmentCancellation, entries[1].TransactionType);
        Assert.Equal(4m, entries[1].QtyIn);
        Assert.Equal(0m, entries[1].QtyOut);
        Assert.Equal(10m, entries[1].RunningBalanceQty);
    }

    [Fact]
    public async Task CancelPostedIncrease_ShouldRespectNegativeStockRulesAndRollback()
    {
        await using var dbContext = CreateDbContext();
        var references = await SeedReferencesAsync(dbContext);
        var documentService = CreateDocumentService(dbContext);
        var postingService = CreatePostingService(dbContext);
        var created = await documentService.CreateDraftAsync(
            Request(
                "ADJ-CANCEL-BLOCKED",
                references,
                [
                    Line(1, references.Item.Id, references.PieceUom.Id, 6m, InventoryAdjustmentType.Increase)
                ]),
            "tester",
            CancellationToken.None);

        await postingService.PostAsync(created.Id, "poster", CancellationToken.None);
        await SeedStockOutAsync(dbContext, references, 6m);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            postingService.CancelAsync(created.Id, "cancel-user", CancellationToken.None));

        Assert.Contains("Insufficient stock", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(DocumentStatus.Posted, await dbContext.InventoryAdjustments.Where(entity => entity.Id == created.Id).Select(entity => entity.Status).SingleAsync());
        Assert.Equal(1, await dbContext.StockLedgerEntries.CountAsync(entry => entry.SourceDocId == created.Id));
    }

    [Fact]
    public async Task UpdateDeleteAndCancelDraft_ShouldRejectInvalidLifecycleTransitions()
    {
        await using var dbContext = CreateDbContext();
        var references = await SeedReferencesAsync(dbContext);
        var documentService = CreateDocumentService(dbContext);
        var postingService = CreatePostingService(dbContext);
        var created = await documentService.CreateDraftAsync(
            Request(
                "ADJ-LIFECYCLE",
                references,
                [
                    Line(1, references.Item.Id, references.PieceUom.Id, 2m, InventoryAdjustmentType.Increase)
                ]),
            "tester",
            CancellationToken.None);

        var cancelDraftException = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            postingService.CancelAsync(created.Id, "cancel-user", CancellationToken.None));

        await postingService.PostAsync(created.Id, "poster", CancellationToken.None);

        var updatePostedException = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            documentService.UpdateDraftAsync(
                created.Id,
                Request(
                    "ADJ-LIFECYCLE",
                    references,
                    [
                        Line(1, references.Item.Id, references.PieceUom.Id, 3m, InventoryAdjustmentType.Increase)
                    ]),
                "tester",
                CancellationToken.None));
        var deletePostedException = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            documentService.DeleteDraftAsync(created.Id, CancellationToken.None));

        Assert.Contains("Only Posted", cancelDraftException.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Only Draft", updatePostedException.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Only Draft", deletePostedException.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(DocumentStatus.Posted, await dbContext.InventoryAdjustments.Where(entity => entity.Id == created.Id).Select(entity => entity.Status).SingleAsync());
    }

    [Fact]
    public async Task PostConcurrently_ShouldCreateOneOriginalLedgerSet()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"highcool-adjustment-concurrent-post-{Guid.NewGuid():N}.db");
        try
        {
            Guid organizationId;
            Guid adjustmentId;
            await using (var dbContext = CreateInitializedDbContext(databasePath, out var executionContext))
            {
                organizationId = executionContext.OrganizationId!.Value;
                var references = await SeedReferencesAsync(dbContext);
                var created = await CreateDocumentService(dbContext).CreateDraftAsync(
                    Request(
                        null,
                        references,
                        [
                            Line(1, references.Item.Id, references.PieceUom.Id, 2m, InventoryAdjustmentType.Increase)
                        ]),
                    "tester",
                    CancellationToken.None);
                adjustmentId = created.Id;
            }

            var tasks = Enumerable.Range(0, 6)
                .Select(_ => Task.Run(async () =>
                {
                    await using var dbContext = CreateDbContext(databasePath, TestOrganizationContext.CreateExecutionContext(organizationId));
                    return await CreatePostingService(dbContext).PostAsync(adjustmentId, "poster", CancellationToken.None);
                }))
                .ToArray();

            var results = await Task.WhenAll(tasks);

            await using var verifyContext = CreateDbContext(databasePath, TestOrganizationContext.CreateExecutionContext(organizationId));
            Assert.All(results, result => Assert.Equal(DocumentStatus.Posted, result!.Status));
            Assert.Equal(DocumentStatus.Posted, await verifyContext.InventoryAdjustments.Where(entity => entity.Id == adjustmentId).Select(entity => entity.Status).SingleAsync());
            Assert.Equal(1, await verifyContext.StockLedgerEntries.CountAsync(entry => entry.SourceDocId == adjustmentId && entry.TransactionType == StockTransactionType.InventoryAdjustmentIncrease));
            Assert.Equal(1, await verifyContext.StockLedgerEntries.Select(entry => entry.LedgerOperationKey).Distinct().CountAsync());
        }
        finally
        {
            SqliteTestDatabase.DeleteSqliteFileSet(databasePath);
        }
    }

    [Fact]
    public async Task CancelConcurrently_ShouldCreateOneCancellationLedgerSet()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"highcool-adjustment-concurrent-cancel-{Guid.NewGuid():N}.db");
        try
        {
            Guid organizationId;
            Guid adjustmentId;
            await using (var dbContext = CreateInitializedDbContext(databasePath, out var executionContext))
            {
                organizationId = executionContext.OrganizationId!.Value;
                var references = await SeedReferencesAsync(dbContext);
                var created = await CreateDocumentService(dbContext).CreateDraftAsync(
                    Request(
                        null,
                        references,
                        [
                            Line(1, references.Item.Id, references.PieceUom.Id, 2m, InventoryAdjustmentType.Increase)
                        ]),
                    "tester",
                    CancellationToken.None);
                adjustmentId = created.Id;
                await CreatePostingService(dbContext).PostAsync(adjustmentId, "poster", CancellationToken.None);
            }

            var tasks = Enumerable.Range(0, 6)
                .Select(_ => Task.Run(async () =>
                {
                    await using var dbContext = CreateDbContext(databasePath, TestOrganizationContext.CreateExecutionContext(organizationId));
                    return await CreatePostingService(dbContext).CancelAsync(adjustmentId, "cancel-user", CancellationToken.None);
                }))
                .ToArray();

            var results = await Task.WhenAll(tasks);

            await using var verifyContext = CreateDbContext(databasePath, TestOrganizationContext.CreateExecutionContext(organizationId));
            Assert.All(results, result => Assert.Equal(DocumentStatus.Canceled, result!.Status));
            Assert.Equal(DocumentStatus.Canceled, await verifyContext.InventoryAdjustments.Where(entity => entity.Id == adjustmentId).Select(entity => entity.Status).SingleAsync());
            Assert.Equal(1, await verifyContext.StockLedgerEntries.CountAsync(entry => entry.SourceDocId == adjustmentId && entry.TransactionType == StockTransactionType.InventoryAdjustmentIncrease));
            Assert.Equal(1, await verifyContext.StockLedgerEntries.CountAsync(entry => entry.SourceDocId == adjustmentId && entry.TransactionType == StockTransactionType.InventoryAdjustmentCancellation));
            Assert.Equal(2, await verifyContext.StockLedgerEntries.Select(entry => entry.LedgerOperationKey).Distinct().CountAsync());
        }
        finally
        {
            SqliteTestDatabase.DeleteSqliteFileSet(databasePath);
        }
    }

    [Fact]
    public async Task LedgerOperationKey_ShouldBeUniquelyEnforced()
    {
        await using var dbContext = CreateDbContext();
        var references = await SeedReferencesAsync(dbContext);
        var operationKey = $"inventory-adjustment:{Guid.NewGuid()}:post:{Guid.NewGuid()}";

        dbContext.StockLedgerEntries.Add(CreateLedgerEntry(references, operationKey));
        await dbContext.SaveChangesAsync();

        dbContext.StockLedgerEntries.Add(CreateLedgerEntry(references, operationKey));
        var exception = await Assert.ThrowsAsync<DbUpdateException>(() => dbContext.SaveChangesAsync());

        Assert.True(PersistenceExceptionClassifier.IsUniqueConstraintViolation(exception));
    }

    private static IInventoryAdjustmentService CreateDocumentService(AppDbContext dbContext)
    {
        var organizationId = dbContext.Organizations
            .IgnoreQueryFilters()
            .Select(entity => entity.Id)
            .Single();
        var executionContext = TestOrganizationContext.CreateExecutionContext(organizationId);

        return new InventoryAdjustmentService(
            dbContext,
            new QuantityConversionService(dbContext),
            new DocumentNumberService(dbContext, executionContext));
    }

    private static IInventoryAdjustmentPostingService CreatePostingService(AppDbContext dbContext)
    {
        var documentService = CreateDocumentService(dbContext);
        return new InventoryAdjustmentPostingService(dbContext, documentService, new StockAvailabilityService(dbContext));
    }

    private static AppDbContext CreateDbContext()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"highcool-adjustment-tests-{Guid.NewGuid():N}.db");
        var dbContext = CreateInitializedDbContext(databasePath, out _);
        return dbContext;
    }

    private static AppDbContext CreateInitializedDbContext(
        string databasePath,
        out TestRequestExecutionContext executionContext)
    {
        executionContext = TestOrganizationContext.CreateExecutionContext();
        var dbContext = CreateDbContext(databasePath, executionContext);
        dbContext.Database.EnsureCreated();
        TestOrganizationContext.EnsureOrganizationAsync(dbContext, executionContext).GetAwaiter().GetResult();
        return dbContext;
    }

    private static AppDbContext CreateDbContext(string databasePath, TestRequestExecutionContext executionContext)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(SqliteTestDatabase.CreateConnectionString(databasePath))
            .Options;

        return new AppDbContext(options, executionContext);
    }

    private static async Task<AdjustmentReferences> SeedReferencesAsync(AppDbContext dbContext)
    {
        var pieceUom = new Uom
        {
            Code = "PCS",
            Name = "Pieces",
            Precision = 0,
            AllowsFraction = false,
            IsActive = true,
            CreatedBy = "seed"
        };

        var boxUom = new Uom
        {
            Code = "BOX",
            Name = "Box",
            Precision = 0,
            AllowsFraction = false,
            IsActive = true,
            CreatedBy = "seed"
        };

        var warehouse = new Warehouse
        {
            Code = "MAIN",
            Name = "Main Warehouse",
            IsActive = true,
            CreatedBy = "seed"
        };

        dbContext.Uoms.AddRange(pieceUom, boxUom);
        dbContext.Warehouses.Add(warehouse);
        await dbContext.SaveChangesAsync();

        dbContext.UomConversions.Add(new UomConversion
        {
            FromUomId = boxUom.Id,
            ToUomId = pieceUom.Id,
            Factor = 10m,
            RoundingMode = RoundingMode.None,
            IsActive = true,
            CreatedBy = "seed"
        });

        var item = new Item
        {
            Code = "ITM-ADJ",
            Name = "Adjustment Item",
            BaseUomId = pieceUom.Id,
            IsActive = true,
            IsSellable = true,
            HasComponents = false,
            CreatedBy = "seed"
        };

        dbContext.Items.Add(item);
        await dbContext.SaveChangesAsync();

        return new AdjustmentReferences(warehouse, pieceUom, boxUom, item);
    }

    private static async Task<AdjustmentReferences> LoadReferencesAsync(AppDbContext dbContext)
    {
        var warehouse = await dbContext.Warehouses.SingleAsync(entity => entity.Code == "MAIN");
        var pieceUom = await dbContext.Uoms.SingleAsync(entity => entity.Code == "PCS");
        var boxUom = await dbContext.Uoms.SingleAsync(entity => entity.Code == "BOX");
        var item = await dbContext.Items.SingleAsync(entity => entity.Code == "ITM-ADJ");

        return new AdjustmentReferences(warehouse, pieceUom, boxUom, item);
    }

    private static StockLedgerEntry CreateLedgerEntry(AdjustmentReferences references, string operationKey)
    {
        return new StockLedgerEntry
        {
            ItemId = references.Item.Id,
            WarehouseId = references.Warehouse.Id,
            TransactionType = StockTransactionType.InventoryAdjustmentIncrease,
            SourceDocType = SourceDocumentType.InventoryAdjustment,
            SourceDocId = Guid.NewGuid(),
            SourceLineId = Guid.NewGuid(),
            LedgerOperationKey = operationKey,
            QtyIn = 1m,
            QtyOut = 0m,
            UomId = references.PieceUom.Id,
            BaseQty = 1m,
            RunningBalanceQty = 1m,
            TransactionDate = DateTime.UtcNow.Date,
            CreatedBy = "seed"
        };
    }

    private static async Task SeedBalanceAsync(AppDbContext dbContext, AdjustmentReferences references, decimal baseQty)
    {
        dbContext.StockLedgerEntries.Add(new StockLedgerEntry
        {
            ItemId = references.Item.Id,
            WarehouseId = references.Warehouse.Id,
            TransactionType = StockTransactionType.PurchaseReceipt,
            SourceDocType = SourceDocumentType.PurchaseReceipt,
            SourceDocId = Guid.NewGuid(),
            SourceLineId = Guid.NewGuid(),
            QtyIn = baseQty,
            QtyOut = 0m,
            UomId = references.PieceUom.Id,
            BaseQty = baseQty,
            RunningBalanceQty = baseQty,
            TransactionDate = DateTime.UtcNow.Date.AddDays(-1),
            CreatedBy = "seed"
        });

        await dbContext.SaveChangesAsync();
    }

    private static async Task SeedStockOutAsync(AppDbContext dbContext, AdjustmentReferences references, decimal baseQty)
    {
        dbContext.StockLedgerEntries.Add(new StockLedgerEntry
        {
            ItemId = references.Item.Id,
            WarehouseId = references.Warehouse.Id,
            TransactionType = StockTransactionType.PurchaseReceiptReversal,
            SourceDocType = SourceDocumentType.PurchaseReceiptReversal,
            SourceDocId = Guid.NewGuid(),
            SourceLineId = Guid.NewGuid(),
            QtyIn = 0m,
            QtyOut = baseQty,
            UomId = references.PieceUom.Id,
            BaseQty = baseQty,
            RunningBalanceQty = 0m,
            TransactionDate = DateTime.UtcNow.Date,
            CreatedBy = "seed"
        });

        await dbContext.SaveChangesAsync();
    }

    private static async Task SetAllowNegativeStockAsync(AppDbContext dbContext, bool allowNegativeStock)
    {
        var organization = await dbContext.Organizations.IgnoreQueryFilters().SingleAsync();
        organization.AllowNegativeStock = allowNegativeStock;
        await dbContext.SaveChangesAsync();
    }

    private static UpsertInventoryAdjustmentRequest Request(
        string? adjustmentNo,
        AdjustmentReferences references,
        IReadOnlyList<UpsertInventoryAdjustmentLineRequest> lines)
    {
        return new UpsertInventoryAdjustmentRequest(
            adjustmentNo,
            DateTime.UtcNow.Date,
            references.Warehouse.Id,
            "Cycle count correction",
            null,
            lines);
    }

    private static UpsertInventoryAdjustmentLineRequest Line(
        int lineNo,
        Guid itemId,
        Guid uomId,
        decimal quantity,
        InventoryAdjustmentType adjustmentType)
    {
        return new UpsertInventoryAdjustmentLineRequest(lineNo, itemId, uomId, quantity, adjustmentType, null);
    }

    private sealed record AdjustmentReferences(
        Warehouse Warehouse,
        Uom PieceUom,
        Uom BoxUom,
        Item Item);
}
