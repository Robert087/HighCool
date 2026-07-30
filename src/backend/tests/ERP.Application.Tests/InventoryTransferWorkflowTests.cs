using ERP.Application.Inventory.Transfers;
using ERP.Application.Purchasing.PurchaseReceipts;
using ERP.Domain.Common;
using ERP.Domain.Inventory;
using ERP.Domain.MasterData;
using ERP.Infrastructure.Common.Numbering;
using ERP.Infrastructure.Inventory;
using ERP.Infrastructure.Inventory.Transfers;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Purchasing.PurchaseReceipts;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ERP.Application.Tests;

public sealed class InventoryTransferWorkflowTests
{
    [Fact]
    public async Task CreateDraft_ShouldGenerateSequentialNumbersAndKeepNumberImmutableOnUpdate()
    {
        await using var dbContext = CreateDbContext();
        var references = await SeedReferencesAsync(dbContext);
        var service = CreateDocumentService(dbContext);

        var first = await service.CreateDraftAsync(
            Request(
                references,
                [
                    Line(1, references.Item.Id, references.PieceUom.Id, 1m)
                ]),
            "tester",
            CancellationToken.None);
        var second = await service.CreateDraftAsync(
            Request(
                references,
                [
                    Line(1, references.Item.Id, references.PieceUom.Id, 1m)
                ]),
            "tester",
            CancellationToken.None);

        var updated = await service.UpdateDraftAsync(
            first.Id,
            Request(
                references,
                [
                    Line(1, references.Item.Id, references.BoxUom.Id, 1m)
                ]),
            "tester",
            CancellationToken.None);

        Assert.Equal("TRF-000001", first.TransferNo);
        Assert.Equal("TRF-000002", second.TransferNo);
        Assert.NotNull(updated);
        Assert.Equal("TRF-000001", updated!.TransferNo);
        Assert.Equal(10m, updated.Lines.Single().BaseQty);
        Assert.Equal(3, await dbContext.DocumentNumberSequences.Select(entity => entity.NextValue).SingleAsync());
    }

    [Fact]
    public async Task CreateDraft_ShouldRejectSameSourceAndDestinationWarehouse()
    {
        await using var dbContext = CreateDbContext();
        var references = await SeedReferencesAsync(dbContext);
        var service = CreateDocumentService(dbContext);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateDraftAsync(
                new UpsertInventoryTransferRequest(
                    null,
                    DateTime.UtcNow.Date,
                    references.SourceWarehouse.Id,
                    references.SourceWarehouse.Id,
                    null,
                    [
                        Line(1, references.Item.Id, references.PieceUom.Id, 1m)
                    ]),
                "tester",
                CancellationToken.None));

        Assert.Contains("must be different", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Post_ShouldCreateSourceOutAndDestinationInLedgerEntriesAndBeIdempotent()
    {
        await using var dbContext = CreateDbContext();
        var references = await SeedReferencesAsync(dbContext);
        await SeedBalanceAsync(dbContext, references, references.SourceWarehouse.Id, 50m);
        var documentService = CreateDocumentService(dbContext);
        var postingService = CreatePostingService(dbContext);
        var created = await documentService.CreateDraftAsync(
            Request(
                references,
                [
                    Line(1, references.Item.Id, references.BoxUom.Id, 2m)
                ]),
            "tester",
            CancellationToken.None);

        var posted = await postingService.PostAsync(created.Id, "poster", CancellationToken.None);
        var secondPost = await postingService.PostAsync(created.Id, "poster", CancellationToken.None);

        Assert.NotNull(posted);
        Assert.NotNull(secondPost);
        Assert.Equal(DocumentStatus.Posted, posted!.Status);
        Assert.Equal(DocumentStatus.Posted, secondPost!.Status);

        var transferEntries = await dbContext.StockLedgerEntries
            .Where(entry => entry.SourceDocId == created.Id)
            .ToListAsync();
        var sourceOut = transferEntries.Single(entry => entry.TransactionType == StockTransactionType.InventoryTransferOut);
        var destinationIn = transferEntries.Single(entry => entry.TransactionType == StockTransactionType.InventoryTransferIn);

        Assert.Equal(2, transferEntries.Count);
        Assert.Equal(SourceDocumentType.InventoryTransfer, sourceOut.SourceDocType);
        Assert.Equal(references.SourceWarehouse.Id, sourceOut.WarehouseId);
        Assert.Equal(0m, sourceOut.QtyIn);
        Assert.Equal(2m, sourceOut.QtyOut);
        Assert.Equal(20m, sourceOut.BaseQty);
        Assert.Equal(30m, sourceOut.RunningBalanceQty);

        Assert.Equal(SourceDocumentType.InventoryTransfer, destinationIn.SourceDocType);
        Assert.Equal(references.DestinationWarehouse.Id, destinationIn.WarehouseId);
        Assert.Equal(2m, destinationIn.QtyIn);
        Assert.Equal(0m, destinationIn.QtyOut);
        Assert.Equal(20m, destinationIn.BaseQty);
        Assert.Equal(20m, destinationIn.RunningBalanceQty);
        Assert.Equal(2, transferEntries.Select(entry => entry.LedgerOperationKey).Distinct().Count());
    }

    [Fact]
    public async Task Post_ShouldValidateAggregatedSourceWarehouseStockBeforePosting()
    {
        await using var dbContext = CreateDbContext();
        var references = await SeedReferencesAsync(dbContext);
        await SeedBalanceAsync(dbContext, references, references.SourceWarehouse.Id, 24m);
        var documentService = CreateDocumentService(dbContext);
        var postingService = CreatePostingService(dbContext);
        var created = await documentService.CreateDraftAsync(
            Request(
                references,
                [
                    Line(1, references.Item.Id, references.BoxUom.Id, 1m),
                    Line(2, references.Item.Id, references.PieceUom.Id, 15m)
                ]),
            "tester",
            CancellationToken.None);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            postingService.PostAsync(created.Id, "poster", CancellationToken.None));

        Assert.Contains("Insufficient stock", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("line 1", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("line 2", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(DocumentStatus.Draft, await dbContext.InventoryTransfers.Where(entity => entity.Id == created.Id).Select(entity => entity.Status).SingleAsync());
        Assert.Equal(1, await dbContext.StockLedgerEntries.CountAsync());
    }

    [Fact]
    public async Task Post_ShouldNotValidateDestinationWarehouseAvailability()
    {
        await using var dbContext = CreateDbContext();
        var references = await SeedReferencesAsync(dbContext);
        await SeedBalanceAsync(dbContext, references, references.SourceWarehouse.Id, 5m);
        var documentService = CreateDocumentService(dbContext);
        var postingService = CreatePostingService(dbContext);
        var created = await documentService.CreateDraftAsync(
            Request(
                references,
                [
                    Line(1, references.Item.Id, references.PieceUom.Id, 5m)
                ]),
            "tester",
            CancellationToken.None);

        var posted = await postingService.PostAsync(created.Id, "poster", CancellationToken.None);

        Assert.NotNull(posted);
        Assert.Equal(DocumentStatus.Posted, posted!.Status);
        Assert.Equal(0m, await LatestBalanceAsync(dbContext, references.Item.Id, references.SourceWarehouse.Id));
        Assert.Equal(5m, await LatestBalanceAsync(dbContext, references.Item.Id, references.DestinationWarehouse.Id));
    }

    [Fact]
    public async Task Post_ShouldAllowExactZeroSourceStockAndRemainAtomic()
    {
        await using var dbContext = CreateDbContext();
        var references = await SeedReferencesAsync(dbContext);
        await SeedBalanceAsync(dbContext, references, references.SourceWarehouse.Id, 5m);
        var documentService = CreateDocumentService(dbContext);
        var postingService = CreatePostingService(dbContext);
        var created = await documentService.CreateDraftAsync(
            Request(
                references,
                [
                    Line(1, references.Item.Id, references.PieceUom.Id, 5m)
                ]),
            "tester",
            CancellationToken.None);

        var posted = await postingService.PostAsync(created.Id, "poster", CancellationToken.None);

        Assert.NotNull(posted);
        Assert.Equal(DocumentStatus.Posted, posted!.Status);
        Assert.Equal(0m, await LatestBalanceAsync(dbContext, references.Item.Id, references.SourceWarehouse.Id));
        Assert.Equal(5m, await LatestBalanceAsync(dbContext, references.Item.Id, references.DestinationWarehouse.Id));
    }

    [Fact]
    public async Task Post_ShouldAllowNegativeSourceStockWhenOrganizationAllowsIt()
    {
        await using var dbContext = CreateDbContext();
        await SetAllowNegativeStockAsync(dbContext, true);
        var references = await SeedReferencesAsync(dbContext);
        var documentService = CreateDocumentService(dbContext);
        var postingService = CreatePostingService(dbContext);
        var created = await documentService.CreateDraftAsync(
            Request(
                references,
                [
                    Line(1, references.Item.Id, references.PieceUom.Id, 3m)
                ]),
            "tester",
            CancellationToken.None);

        var posted = await postingService.PostAsync(created.Id, "poster", CancellationToken.None);

        Assert.NotNull(posted);
        Assert.Equal(DocumentStatus.Posted, posted!.Status);
        Assert.Equal(-3m, await LatestBalanceAsync(dbContext, references.Item.Id, references.SourceWarehouse.Id));
        Assert.Equal(3m, await LatestBalanceAsync(dbContext, references.Item.Id, references.DestinationWarehouse.Id));
    }

    [Fact]
    public async Task Post_ShouldRollbackAllLinesWhenOneItemFailsAvailability()
    {
        await using var dbContext = CreateDbContext();
        var references = await SeedReferencesAsync(dbContext);
        var secondItem = await SeedSecondItemAsync(dbContext, references.PieceUom.Id);
        await SeedBalanceAsync(dbContext, references, references.SourceWarehouse.Id, 10m);
        var documentService = CreateDocumentService(dbContext);
        var postingService = CreatePostingService(dbContext);
        var created = await documentService.CreateDraftAsync(
            Request(
                references,
                [
                    Line(1, references.Item.Id, references.PieceUom.Id, 5m),
                    Line(2, secondItem.Id, references.PieceUom.Id, 1m)
                ]),
            "tester",
            CancellationToken.None);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            postingService.PostAsync(created.Id, "poster", CancellationToken.None));

        Assert.Contains("Insufficient stock", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(DocumentStatus.Draft, await dbContext.InventoryTransfers.Where(entity => entity.Id == created.Id).Select(entity => entity.Status).SingleAsync());
        Assert.Equal(0, await dbContext.StockLedgerEntries.CountAsync(entry => entry.SourceDocId == created.Id));
        Assert.Equal(1, await dbContext.StockLedgerEntries.CountAsync());
    }

    [Fact]
    public async Task Cancel_ShouldCreateReversingEntriesWithoutEditingOriginalTransferEntries()
    {
        await using var dbContext = CreateDbContext();
        var references = await SeedReferencesAsync(dbContext);
        await SeedBalanceAsync(dbContext, references, references.SourceWarehouse.Id, 12m);
        var documentService = CreateDocumentService(dbContext);
        var postingService = CreatePostingService(dbContext);
        var created = await documentService.CreateDraftAsync(
            Request(
                references,
                [
                    Line(1, references.Item.Id, references.PieceUom.Id, 4m)
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

        var transferEntries = await dbContext.StockLedgerEntries
            .Where(entry => entry.SourceDocId == created.Id)
            .ToListAsync();
        var sourceOut = transferEntries.Single(entry => entry.TransactionType == StockTransactionType.InventoryTransferOut);
        var destinationIn = transferEntries.Single(entry => entry.TransactionType == StockTransactionType.InventoryTransferIn);
        var sourceCancellationIn = transferEntries.Single(entry => entry.TransactionType == StockTransactionType.InventoryTransferCancellationIn);
        var destinationCancellationOut = transferEntries.Single(entry => entry.TransactionType == StockTransactionType.InventoryTransferCancellationOut);

        Assert.Equal(4, transferEntries.Count);
        Assert.Equal(references.SourceWarehouse.Id, sourceOut.WarehouseId);
        Assert.Equal(references.DestinationWarehouse.Id, destinationIn.WarehouseId);
        Assert.Equal(SourceDocumentType.InventoryTransferCancellation, sourceCancellationIn.SourceDocType);
        Assert.Equal(references.SourceWarehouse.Id, sourceCancellationIn.WarehouseId);
        Assert.Equal(4m, sourceCancellationIn.QtyIn);
        Assert.Equal(0m, sourceCancellationIn.QtyOut);
        Assert.Equal(12m, sourceCancellationIn.RunningBalanceQty);
        Assert.Equal(SourceDocumentType.InventoryTransferCancellation, destinationCancellationOut.SourceDocType);
        Assert.Equal(references.DestinationWarehouse.Id, destinationCancellationOut.WarehouseId);
        Assert.Equal(0m, destinationCancellationOut.QtyIn);
        Assert.Equal(4m, destinationCancellationOut.QtyOut);
        Assert.Equal(0m, destinationCancellationOut.RunningBalanceQty);
    }

    [Fact]
    public async Task Cancel_ShouldBlockWhenDestinationStockWasConsumedAndRollback()
    {
        await using var dbContext = CreateDbContext();
        var references = await SeedReferencesAsync(dbContext);
        await SeedBalanceAsync(dbContext, references, references.SourceWarehouse.Id, 6m);
        var documentService = CreateDocumentService(dbContext);
        var postingService = CreatePostingService(dbContext);
        var created = await documentService.CreateDraftAsync(
            Request(
                references,
                [
                    Line(1, references.Item.Id, references.PieceUom.Id, 6m)
                ]),
            "tester",
            CancellationToken.None);

        await postingService.PostAsync(created.Id, "poster", CancellationToken.None);
        await SeedStockOutAsync(dbContext, references, references.DestinationWarehouse.Id, 4m);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            postingService.CancelAsync(created.Id, "cancel-user", CancellationToken.None));

        Assert.Contains("Insufficient stock", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(DocumentStatus.Posted, await dbContext.InventoryTransfers.Where(entity => entity.Id == created.Id).Select(entity => entity.Status).SingleAsync());
        Assert.Equal(2, await dbContext.StockLedgerEntries.CountAsync(entry => entry.SourceDocId == created.Id));
        Assert.Equal(2m, await LatestBalanceAsync(dbContext, references.Item.Id, references.DestinationWarehouse.Id));
    }

    [Fact]
    public async Task Cancel_ShouldAllowExactZeroDestinationStock()
    {
        await using var dbContext = CreateDbContext();
        var references = await SeedReferencesAsync(dbContext);
        await SeedBalanceAsync(dbContext, references, references.SourceWarehouse.Id, 5m);
        var documentService = CreateDocumentService(dbContext);
        var postingService = CreatePostingService(dbContext);
        var created = await documentService.CreateDraftAsync(
            Request(
                references,
                [
                    Line(1, references.Item.Id, references.PieceUom.Id, 5m)
                ]),
            "tester",
            CancellationToken.None);

        await postingService.PostAsync(created.Id, "poster", CancellationToken.None);
        var canceled = await postingService.CancelAsync(created.Id, "cancel-user", CancellationToken.None);

        Assert.NotNull(canceled);
        Assert.Equal(DocumentStatus.Canceled, canceled!.Status);
        Assert.Equal(0m, await LatestBalanceAsync(dbContext, references.Item.Id, references.DestinationWarehouse.Id));
        Assert.Equal(5m, await LatestBalanceAsync(dbContext, references.Item.Id, references.SourceWarehouse.Id));
    }

    [Fact]
    public async Task Cancel_ShouldAllowNegativeDestinationStockWhenOrganizationAllowsIt()
    {
        await using var dbContext = CreateDbContext();
        await SetAllowNegativeStockAsync(dbContext, true);
        var references = await SeedReferencesAsync(dbContext);
        await SeedBalanceAsync(dbContext, references, references.SourceWarehouse.Id, 5m);
        var documentService = CreateDocumentService(dbContext);
        var postingService = CreatePostingService(dbContext);
        var created = await documentService.CreateDraftAsync(
            Request(
                references,
                [
                    Line(1, references.Item.Id, references.PieceUom.Id, 5m)
                ]),
            "tester",
            CancellationToken.None);

        await postingService.PostAsync(created.Id, "poster", CancellationToken.None);
        await SeedStockOutAsync(dbContext, references, references.DestinationWarehouse.Id, 3m);
        var canceled = await postingService.CancelAsync(created.Id, "cancel-user", CancellationToken.None);

        Assert.NotNull(canceled);
        Assert.Equal(DocumentStatus.Canceled, canceled!.Status);
        Assert.Equal(-3m, await LatestBalanceAsync(dbContext, references.Item.Id, references.DestinationWarehouse.Id));
    }

    [Fact]
    public async Task Cancel_ShouldRetrySuccessfullyAfterDestinationStockBecomesAvailable()
    {
        await using var dbContext = CreateDbContext();
        var references = await SeedReferencesAsync(dbContext);
        await SeedBalanceAsync(dbContext, references, references.SourceWarehouse.Id, 6m);
        var documentService = CreateDocumentService(dbContext);
        var postingService = CreatePostingService(dbContext);
        var created = await documentService.CreateDraftAsync(
            Request(
                references,
                [
                    Line(1, references.Item.Id, references.PieceUom.Id, 6m)
                ]),
            "tester",
            CancellationToken.None);

        await postingService.PostAsync(created.Id, "poster", CancellationToken.None);
        await SeedStockOutAsync(dbContext, references, references.DestinationWarehouse.Id, 4m);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            postingService.CancelAsync(created.Id, "cancel-user", CancellationToken.None));

        await SeedStockInAsync(dbContext, references, references.DestinationWarehouse.Id, 4m);
        var canceled = await postingService.CancelAsync(created.Id, "cancel-user", CancellationToken.None);

        Assert.NotNull(canceled);
        Assert.Equal(DocumentStatus.Canceled, canceled!.Status);
        Assert.Equal(0m, await LatestBalanceAsync(dbContext, references.Item.Id, references.DestinationWarehouse.Id));
    }

    [Fact]
    public async Task UpdateDeleteAndCancelDraft_ShouldRejectInvalidLifecycleTransitions()
    {
        await using var dbContext = CreateDbContext();
        var references = await SeedReferencesAsync(dbContext);
        await SeedBalanceAsync(dbContext, references, references.SourceWarehouse.Id, 2m);
        var documentService = CreateDocumentService(dbContext);
        var postingService = CreatePostingService(dbContext);
        var created = await documentService.CreateDraftAsync(
            Request(
                references,
                [
                    Line(1, references.Item.Id, references.PieceUom.Id, 2m)
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
                    references,
                    [
                        Line(1, references.Item.Id, references.PieceUom.Id, 1m)
                    ]),
                "tester",
                CancellationToken.None));
        var deletePostedException = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            documentService.DeleteDraftAsync(created.Id, CancellationToken.None));

        Assert.Contains("Only Posted", cancelDraftException.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Only Draft", updatePostedException.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Only Draft", deletePostedException.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(DocumentStatus.Posted, await dbContext.InventoryTransfers.Where(entity => entity.Id == created.Id).Select(entity => entity.Status).SingleAsync());
    }

    [Fact]
    public async Task PostCanceledTransfer_ShouldRejectWithoutWritingRows()
    {
        await using var dbContext = CreateDbContext();
        var references = await SeedReferencesAsync(dbContext);
        await SeedBalanceAsync(dbContext, references, references.SourceWarehouse.Id, 2m);
        var documentService = CreateDocumentService(dbContext);
        var postingService = CreatePostingService(dbContext);
        var created = await documentService.CreateDraftAsync(
            Request(
                references,
                [
                    Line(1, references.Item.Id, references.PieceUom.Id, 2m)
                ]),
            "tester",
            CancellationToken.None);

        await postingService.PostAsync(created.Id, "poster", CancellationToken.None);
        await postingService.CancelAsync(created.Id, "cancel-user", CancellationToken.None);
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            postingService.PostAsync(created.Id, "poster", CancellationToken.None));

        Assert.Contains("Only Draft", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(4, await dbContext.StockLedgerEntries.CountAsync(entry => entry.SourceDocId == created.Id));
    }

    [Fact]
    public async Task List_ShouldFilterSortAndPageOnServerSide()
    {
        await using var dbContext = CreateDbContext();
        var references = await SeedReferencesAsync(dbContext);
        var service = CreateDocumentService(dbContext);

        var first = await service.CreateDraftAsync(Request(references, [Line(1, references.Item.Id, references.PieceUom.Id, 1m)]), "tester", CancellationToken.None);
        var second = await service.CreateDraftAsync(Request(references, [Line(1, references.Item.Id, references.PieceUom.Id, 1m)]), "tester", CancellationToken.None);
        var third = await service.CreateDraftAsync(Request(references, [Line(1, references.Item.Id, references.PieceUom.Id, 1m)]), "tester", CancellationToken.None);
        var firstEntity = await dbContext.InventoryTransfers.SingleAsync(entity => entity.Id == first.Id);
        var secondEntity = await dbContext.InventoryTransfers.SingleAsync(entity => entity.Id == second.Id);
        var thirdEntity = await dbContext.InventoryTransfers.SingleAsync(entity => entity.Id == third.Id);
        firstEntity.TransferDate = new DateTime(2026, 7, 25);
        secondEntity.TransferDate = new DateTime(2026, 7, 26);
        thirdEntity.TransferDate = new DateTime(2026, 7, 27);
        thirdEntity.Status = DocumentStatus.Posted;
        await dbContext.SaveChangesAsync();

        var result = await service.ListAsync(
            new InventoryTransferListQuery(
                Search: references.SourceWarehouse.Code,
                TransferNo: "TRF",
                SourceWarehouseId: references.SourceWarehouse.Id,
                DestinationWarehouseId: references.DestinationWarehouse.Id,
                Status: DocumentStatus.Draft,
                FromDate: new DateTime(2026, 7, 25),
                ToDate: new DateTime(2026, 7, 26),
                Page: 2,
                PageSize: 1,
                SortBy: "transferDate",
                SortDirection: ERP.Application.Common.Pagination.SortDirection.Asc),
            CancellationToken.None);

        Assert.Equal(2, result.TotalCount);
        Assert.Equal(2, result.TotalPages);
        Assert.Equal(2, result.Page);
        Assert.Single(result.Items);
        Assert.Equal(second.Id, result.Items.Single().Id);
    }

    [Fact]
    public async Task CreateDraft_ShouldNumberTransfersSeparatelyByOrganization()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"highcool-transfer-org-numbering-{Guid.NewGuid():N}.db");
        try
        {
            Guid firstOrganizationId;
            Guid secondOrganizationId;
            await using (var dbContext = CreateInitializedDbContext(databasePath, out var firstExecutionContext))
            {
                firstOrganizationId = firstExecutionContext.OrganizationId!.Value;
                await SeedReferencesAsync(dbContext);

                var secondOrganization = new ERP.Domain.Identity.Organization
                {
                    Name = "Second Test Organization",
                    DefaultCurrency = "EGP",
                    Timezone = "Africa/Cairo",
                    DefaultLanguage = "en",
                    SetupCompleted = true,
                    EnableInventory = true,
                    EnableStockTransfers = true,
                    CreatedBy = "seed"
                };
                dbContext.Organizations.Add(secondOrganization);
                await dbContext.SaveChangesAsync();
                secondOrganizationId = secondOrganization.Id;
            }

            string firstNumber;
            await using (var dbContext = CreateDbContext(databasePath, TestOrganizationContext.CreateExecutionContext(firstOrganizationId)))
            {
                var references = await LoadReferencesAsync(dbContext);
                firstNumber = (await CreateDocumentService(dbContext, firstOrganizationId).CreateDraftAsync(Request(references, [Line(1, references.Item.Id, references.PieceUom.Id, 1m)]), "tester", CancellationToken.None)).TransferNo;
            }

            string secondNumber;
            await using (var dbContext = CreateDbContext(databasePath, TestOrganizationContext.CreateExecutionContext(secondOrganizationId)))
            {
                var references = await SeedReferencesAsync(dbContext);
                secondNumber = (await CreateDocumentService(dbContext, secondOrganizationId).CreateDraftAsync(Request(references, [Line(1, references.Item.Id, references.PieceUom.Id, 1m)]), "tester", CancellationToken.None)).TransferNo;
            }

            Assert.Equal("TRF-000001", firstNumber);
            Assert.Equal("TRF-000001", secondNumber);
        }
        finally
        {
            SqliteTestDatabase.DeleteSqliteFileSet(databasePath);
        }
    }

    [Fact]
    public async Task PostConcurrently_ShouldCreateOneOriginalLedgerPair()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"highcool-transfer-concurrent-post-{Guid.NewGuid():N}.db");
        try
        {
            Guid organizationId;
            Guid transferId;
            await using (var dbContext = CreateInitializedDbContext(databasePath, out var executionContext))
            {
                organizationId = executionContext.OrganizationId!.Value;
                var references = await SeedReferencesAsync(dbContext);
                await SeedBalanceAsync(dbContext, references, references.SourceWarehouse.Id, 20m);
                var created = await CreateDocumentService(dbContext).CreateDraftAsync(
                    Request(
                        references,
                        [
                            Line(1, references.Item.Id, references.PieceUom.Id, 2m)
                        ]),
                    "tester",
                    CancellationToken.None);
                transferId = created.Id;
            }

            var tasks = Enumerable.Range(0, 6)
                .Select(_ => Task.Run(async () =>
                {
                    await using var dbContext = CreateDbContext(databasePath, TestOrganizationContext.CreateExecutionContext(organizationId));
                    return await CreatePostingService(dbContext).PostAsync(transferId, "poster", CancellationToken.None);
                }))
                .ToArray();

            var results = await Task.WhenAll(tasks);

            await using var verifyContext = CreateDbContext(databasePath, TestOrganizationContext.CreateExecutionContext(organizationId));
            Assert.All(results, result => Assert.Equal(DocumentStatus.Posted, result!.Status));
            Assert.Equal(DocumentStatus.Posted, await verifyContext.InventoryTransfers.Where(entity => entity.Id == transferId).Select(entity => entity.Status).SingleAsync());
            Assert.Equal(1, await verifyContext.StockLedgerEntries.CountAsync(entry => entry.SourceDocId == transferId && entry.TransactionType == StockTransactionType.InventoryTransferOut));
            Assert.Equal(1, await verifyContext.StockLedgerEntries.CountAsync(entry => entry.SourceDocId == transferId && entry.TransactionType == StockTransactionType.InventoryTransferIn));
            Assert.Equal(2, await verifyContext.StockLedgerEntries.Where(entry => entry.SourceDocId == transferId).Select(entry => entry.LedgerOperationKey).Distinct().CountAsync());
        }
        finally
        {
            SqliteTestDatabase.DeleteSqliteFileSet(databasePath);
        }
    }

    [Fact]
    public async Task CancelConcurrently_ShouldCreateOneCancellationLedgerPair()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"highcool-transfer-concurrent-cancel-{Guid.NewGuid():N}.db");
        try
        {
            Guid organizationId;
            Guid transferId;
            await using (var dbContext = CreateInitializedDbContext(databasePath, out var executionContext))
            {
                organizationId = executionContext.OrganizationId!.Value;
                var references = await SeedReferencesAsync(dbContext);
                await SeedBalanceAsync(dbContext, references, references.SourceWarehouse.Id, 20m);
                var created = await CreateDocumentService(dbContext).CreateDraftAsync(
                    Request(
                        references,
                        [
                            Line(1, references.Item.Id, references.PieceUom.Id, 2m)
                        ]),
                    "tester",
                    CancellationToken.None);
                transferId = created.Id;
                await CreatePostingService(dbContext).PostAsync(transferId, "poster", CancellationToken.None);
            }

            var tasks = Enumerable.Range(0, 6)
                .Select(_ => Task.Run(async () =>
                {
                    await using var dbContext = CreateDbContext(databasePath, TestOrganizationContext.CreateExecutionContext(organizationId));
                    return await CreatePostingService(dbContext).CancelAsync(transferId, "cancel-user", CancellationToken.None);
                }))
                .ToArray();

            var results = await Task.WhenAll(tasks);

            await using var verifyContext = CreateDbContext(databasePath, TestOrganizationContext.CreateExecutionContext(organizationId));
            Assert.All(results, result => Assert.Equal(DocumentStatus.Canceled, result!.Status));
            Assert.Equal(DocumentStatus.Canceled, await verifyContext.InventoryTransfers.Where(entity => entity.Id == transferId).Select(entity => entity.Status).SingleAsync());
            Assert.Equal(1, await verifyContext.StockLedgerEntries.CountAsync(entry => entry.SourceDocId == transferId && entry.TransactionType == StockTransactionType.InventoryTransferOut));
            Assert.Equal(1, await verifyContext.StockLedgerEntries.CountAsync(entry => entry.SourceDocId == transferId && entry.TransactionType == StockTransactionType.InventoryTransferIn));
            Assert.Equal(1, await verifyContext.StockLedgerEntries.CountAsync(entry => entry.SourceDocId == transferId && entry.TransactionType == StockTransactionType.InventoryTransferCancellationIn));
            Assert.Equal(1, await verifyContext.StockLedgerEntries.CountAsync(entry => entry.SourceDocId == transferId && entry.TransactionType == StockTransactionType.InventoryTransferCancellationOut));
            Assert.Equal(4, await verifyContext.StockLedgerEntries.Where(entry => entry.SourceDocId == transferId).Select(entry => entry.LedgerOperationKey).Distinct().CountAsync());
        }
        finally
        {
            SqliteTestDatabase.DeleteSqliteFileSet(databasePath);
        }
    }

    private static IInventoryTransferService CreateDocumentService(AppDbContext dbContext)
    {
        var organizationId = dbContext.Organizations
            .Select(entity => entity.Id)
            .Single();
        return CreateDocumentService(dbContext, organizationId);
    }

    private static IInventoryTransferService CreateDocumentService(AppDbContext dbContext, Guid organizationId)
    {
        var executionContext = TestOrganizationContext.CreateExecutionContext(organizationId);

        return new InventoryTransferService(
            dbContext,
            new QuantityConversionService(dbContext),
            new DocumentNumberService(dbContext, executionContext));
    }

    private static IInventoryTransferPostingService CreatePostingService(AppDbContext dbContext)
    {
        var documentService = CreateDocumentService(dbContext);
        return new InventoryTransferPostingService(dbContext, documentService, new StockAvailabilityService(dbContext));
    }

    private static AppDbContext CreateDbContext()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"highcool-transfer-tests-{Guid.NewGuid():N}.db");
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

    private static async Task<TransferReferences> SeedReferencesAsync(AppDbContext dbContext)
    {
        var pieceUom = new Uom
        {
            Code = $"PCS-{Guid.NewGuid():N}"[..12],
            Name = "Pieces",
            Precision = 0,
            AllowsFraction = false,
            IsActive = true,
            CreatedBy = "seed"
        };

        var boxUom = new Uom
        {
            Code = $"BOX-{Guid.NewGuid():N}"[..12],
            Name = "Box",
            Precision = 0,
            AllowsFraction = false,
            IsActive = true,
            CreatedBy = "seed"
        };

        var sourceWarehouse = new Warehouse
        {
            Code = $"SRC-{Guid.NewGuid():N}"[..12],
            Name = "Source Warehouse",
            IsActive = true,
            CreatedBy = "seed"
        };

        var destinationWarehouse = new Warehouse
        {
            Code = $"DST-{Guid.NewGuid():N}"[..12],
            Name = "Destination Warehouse",
            IsActive = true,
            CreatedBy = "seed"
        };

        dbContext.Uoms.AddRange(pieceUom, boxUom);
        dbContext.Warehouses.AddRange(sourceWarehouse, destinationWarehouse);
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
            Code = $"ITM-{Guid.NewGuid():N}"[..12],
            Name = "Transfer Item",
            BaseUomId = pieceUom.Id,
            IsActive = true,
            IsSellable = true,
            HasComponents = false,
            CreatedBy = "seed"
        };

        dbContext.Items.Add(item);
        await dbContext.SaveChangesAsync();

        return new TransferReferences(sourceWarehouse, destinationWarehouse, pieceUom, boxUom, item);
    }

    private static async Task<TransferReferences> LoadReferencesAsync(AppDbContext dbContext)
    {
        var sourceWarehouse = await dbContext.Warehouses.SingleAsync(entity => entity.Name == "Source Warehouse");
        var destinationWarehouse = await dbContext.Warehouses.SingleAsync(entity => entity.Name == "Destination Warehouse");
        var pieceUom = await dbContext.Uoms.SingleAsync(entity => entity.Name == "Pieces");
        var boxUom = await dbContext.Uoms.SingleAsync(entity => entity.Name == "Box");
        var item = await dbContext.Items.SingleAsync(entity => entity.Name == "Transfer Item");

        return new TransferReferences(sourceWarehouse, destinationWarehouse, pieceUom, boxUom, item);
    }

    private static async Task<Item> SeedSecondItemAsync(AppDbContext dbContext, Guid baseUomId)
    {
        var item = new Item
        {
            Code = $"IT2-{Guid.NewGuid():N}"[..12],
            Name = "Second Transfer Item",
            BaseUomId = baseUomId,
            IsActive = true,
            IsSellable = true,
            HasComponents = false,
            CreatedBy = "seed"
        };

        dbContext.Items.Add(item);
        await dbContext.SaveChangesAsync();
        return item;
    }

    private static async Task SeedBalanceAsync(
        AppDbContext dbContext,
        TransferReferences references,
        Guid warehouseId,
        decimal baseQty)
    {
        dbContext.StockLedgerEntries.Add(new StockLedgerEntry
        {
            ItemId = references.Item.Id,
            WarehouseId = warehouseId,
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

    private static async Task SeedStockOutAsync(
        AppDbContext dbContext,
        TransferReferences references,
        Guid warehouseId,
        decimal baseQty)
    {
        var currentBalance = (decimal)await dbContext.StockLedgerEntries
            .Where(entry => entry.ItemId == references.Item.Id && entry.WarehouseId == warehouseId)
            .SumAsync(entry => entry.QtyIn > 0m ? (double)entry.BaseQty : -(double)entry.BaseQty);

        dbContext.StockLedgerEntries.Add(new StockLedgerEntry
        {
            ItemId = references.Item.Id,
            WarehouseId = warehouseId,
            TransactionType = StockTransactionType.PurchaseReceiptReversal,
            SourceDocType = SourceDocumentType.PurchaseReceiptReversal,
            SourceDocId = Guid.NewGuid(),
            SourceLineId = Guid.NewGuid(),
            QtyIn = 0m,
            QtyOut = baseQty,
            UomId = references.PieceUom.Id,
            BaseQty = baseQty,
            RunningBalanceQty = currentBalance - baseQty,
            TransactionDate = DateTime.UtcNow.Date,
            CreatedBy = "seed"
        });

        await dbContext.SaveChangesAsync();
    }

    private static async Task SeedStockInAsync(
        AppDbContext dbContext,
        TransferReferences references,
        Guid warehouseId,
        decimal baseQty)
    {
        var currentBalance = (decimal)await dbContext.StockLedgerEntries
            .Where(entry => entry.ItemId == references.Item.Id && entry.WarehouseId == warehouseId)
            .SumAsync(entry => entry.QtyIn > 0m ? (double)entry.BaseQty : -(double)entry.BaseQty);

        dbContext.StockLedgerEntries.Add(new StockLedgerEntry
        {
            ItemId = references.Item.Id,
            WarehouseId = warehouseId,
            TransactionType = StockTransactionType.PurchaseReceipt,
            SourceDocType = SourceDocumentType.PurchaseReceipt,
            SourceDocId = Guid.NewGuid(),
            SourceLineId = Guid.NewGuid(),
            QtyIn = baseQty,
            QtyOut = 0m,
            UomId = references.PieceUom.Id,
            BaseQty = baseQty,
            RunningBalanceQty = currentBalance + baseQty,
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

    private static async Task<decimal> LatestBalanceAsync(AppDbContext dbContext, Guid itemId, Guid warehouseId)
    {
        var balance = await dbContext.StockLedgerEntries
            .Where(entry => entry.ItemId == itemId && entry.WarehouseId == warehouseId)
            .SumAsync(entry => entry.QtyIn > 0m ? (double)entry.BaseQty : -(double)entry.BaseQty);
        return decimal.Round((decimal)balance, 6, MidpointRounding.AwayFromZero);
    }

    private static UpsertInventoryTransferRequest Request(
        TransferReferences references,
        IReadOnlyList<UpsertInventoryTransferLineRequest> lines)
    {
        return new UpsertInventoryTransferRequest(
            null,
            DateTime.UtcNow.Date,
            references.SourceWarehouse.Id,
            references.DestinationWarehouse.Id,
            "Warehouse transfer",
            lines);
    }

    private static UpsertInventoryTransferLineRequest Line(
        int lineNo,
        Guid itemId,
        Guid uomId,
        decimal quantity)
    {
        return new UpsertInventoryTransferLineRequest(lineNo, itemId, uomId, quantity, null);
    }

    private sealed record TransferReferences(
        Warehouse SourceWarehouse,
        Warehouse DestinationWarehouse,
        Uom PieceUom,
        Uom BoxUom,
        Item Item);
}
