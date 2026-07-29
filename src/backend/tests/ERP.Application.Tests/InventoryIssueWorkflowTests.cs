using ERP.Application.Common.Pagination;
using ERP.Application.Inventory;
using ERP.Application.Inventory.Issues;
using ERP.Application.Purchasing.PurchaseReceipts;
using ERP.Domain.Common;
using ERP.Domain.Inventory;
using ERP.Domain.MasterData;
using ERP.Infrastructure.Common.Numbering;
using ERP.Infrastructure.Inventory;
using ERP.Infrastructure.Inventory.Issues;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Purchasing.PurchaseReceipts;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ERP.Application.Tests;

public sealed class InventoryIssueWorkflowTests
{
    [Fact]
    public async Task CreateUpdateAndDeleteDraft_ShouldNumberSequentiallyAndRejectDuplicateItems()
    {
        await using var dbContext = CreateDbContext();
        var references = await SeedReferencesAsync(dbContext);
        var service = CreateDocumentService(dbContext);

        var first = await service.CreateDraftAsync(Request(references, [Line(1, references.Item.Id, references.PieceUom.Id, 2m)]), "tester", CancellationToken.None);
        var second = await service.CreateDraftAsync(Request(references, [Line(1, references.SecondItem.Id, references.PieceUom.Id, 1m)]), "tester", CancellationToken.None);
        var updated = await service.UpdateDraftAsync(
            first.Id,
            Request(references, [Line(1, references.Item.Id, references.PieceUom.Id, 3m)]) with { IssueNo = "CLIENT-CONTROLLED" },
            "tester",
            CancellationToken.None);

        Assert.Equal("ISS-000001", first.IssueNo);
        Assert.Equal("ISS-000002", second.IssueNo);
        Assert.NotNull(updated);
        Assert.Equal("ISS-000001", updated!.IssueNo);
        Assert.Equal(3m, updated.Lines.Single().Quantity);

        var duplicateException = await Assert.ThrowsAsync<ValidationException>(() =>
            new UpsertInventoryIssueRequestValidator().ValidateAndThrowAsync(
                Request(references, [
                    Line(1, references.Item.Id, references.PieceUom.Id, 1m),
                    Line(2, references.Item.Id, references.BoxUom.Id, 1m)
                ])));
        Assert.Contains("same item", duplicateException.Message, StringComparison.OrdinalIgnoreCase);

        Assert.True(await service.DeleteDraftAsync(first.Id, CancellationToken.None));
        Assert.Equal(1, await dbContext.InventoryIssues.CountAsync());
    }

    [Fact]
    public async Task CreateDraft_ShouldUseDocumentSequenceWithoutScanningExistingIssueNumbers()
    {
        await using var dbContext = CreateDbContext();
        var references = await SeedReferencesAsync(dbContext);
        dbContext.InventoryIssues.Add(new InventoryIssue
        {
            IssueNo = "ISS-999999",
            IssueDate = DateTime.UtcNow.Date,
            WarehouseId = references.Warehouse.Id,
            Reason = InventoryIssueReason.Damage,
            Status = DocumentStatus.Draft,
            CreatedBy = "seed",
            Lines =
            {
                new InventoryIssueLine
                {
                    LineNo = 1,
                    ItemId = references.Item.Id,
                    UomId = references.PieceUom.Id,
                    Quantity = 1m,
                    BaseQty = 1m,
                    CreatedBy = "seed"
                }
            }
        });
        await dbContext.SaveChangesAsync();

        var created = await CreateDocumentService(dbContext).CreateDraftAsync(
            Request(references, [Line(1, references.SecondItem.Id, references.PieceUom.Id, 1m)]),
            "tester",
            CancellationToken.None);

        Assert.Equal("ISS-000001", created.IssueNo);
    }

    [Fact]
    public async Task CreateDraft_ShouldNumberIssuesSeparatelyByOrganization()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"highcool-issue-org-numbering-{Guid.NewGuid():N}.db");
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
                    EnableInventoryIssues = true,
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
                firstNumber = (await CreateDocumentService(dbContext, firstOrganizationId).CreateDraftAsync(
                    Request(references, [Line(1, references.Item.Id, references.PieceUom.Id, 1m)]),
                    "tester",
                    CancellationToken.None)).IssueNo;
            }

            string secondNumber;
            await using (var dbContext = CreateDbContext(databasePath, TestOrganizationContext.CreateExecutionContext(secondOrganizationId)))
            {
                var references = await SeedReferencesAsync(dbContext);
                secondNumber = (await CreateDocumentService(dbContext, secondOrganizationId).CreateDraftAsync(
                    Request(references, [Line(1, references.Item.Id, references.PieceUom.Id, 1m)]),
                    "tester",
                    CancellationToken.None)).IssueNo;
            }

            Assert.Equal("ISS-000001", firstNumber);
            Assert.Equal("ISS-000001", secondNumber);
        }
        finally
        {
            SqliteTestDatabase.DeleteSqliteFileSet(databasePath);
        }
    }

    [Fact]
    public async Task CreateDraftConcurrently_ShouldAllocateUniqueSequentialIssueNumbers()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"highcool-issue-concurrent-numbering-{Guid.NewGuid():N}.db");
        try
        {
            Guid organizationId;
            await using (var dbContext = CreateInitializedDbContext(databasePath, out var initContext))
            {
                organizationId = initContext.OrganizationId!.Value;
                await SeedReferencesAsync(dbContext);
            }

            var tasks = Enumerable.Range(0, 4)
                .Select(_ => Task.Run(async () =>
                {
                    await using var dbContext = CreateDbContext(databasePath, TestOrganizationContext.CreateExecutionContext(organizationId));
                    var references = await LoadReferencesAsync(dbContext);
                    return (await CreateDocumentService(dbContext, organizationId).CreateDraftAsync(
                        Request(references, [Line(1, references.Item.Id, references.PieceUom.Id, 1m)]),
                        "tester",
                        CancellationToken.None)).IssueNo;
                }))
                .ToArray();

            var numbers = (await Task.WhenAll(tasks)).OrderBy(value => value, StringComparer.Ordinal).ToArray();

            Assert.Equal(new[] { "ISS-000001", "ISS-000002", "ISS-000003", "ISS-000004" }, numbers);
        }
        finally
        {
            SqliteTestDatabase.DeleteSqliteFileSet(databasePath);
        }
    }

    [Fact]
    public async Task InvalidTransitions_ShouldPreserveDocumentLifecycle()
    {
        await using var dbContext = CreateDbContext();
        var references = await SeedReferencesAsync(dbContext);
        await SeedStockInAsync(dbContext, references, references.Item.Id, 10m);
        var documentService = CreateDocumentService(dbContext);
        var postingService = CreatePostingService(dbContext);
        var created = await documentService.CreateDraftAsync(Request(references, [Line(1, references.Item.Id, references.PieceUom.Id, 4m)]), "tester", CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(() => postingService.CancelAsync(created.Id, "canceler", CancellationToken.None));

        var posted = await postingService.PostAsync(created.Id, "poster", CancellationToken.None);
        Assert.NotNull(posted);

        await Assert.ThrowsAsync<InvalidOperationException>(() => documentService.UpdateDraftAsync(created.Id, Request(references, [Line(1, references.Item.Id, references.PieceUom.Id, 1m)]), "tester", CancellationToken.None));
        await Assert.ThrowsAsync<InvalidOperationException>(() => documentService.DeleteDraftAsync(created.Id, CancellationToken.None));

        var canceled = await postingService.CancelAsync(created.Id, "canceler", CancellationToken.None);
        Assert.NotNull(canceled);
        await Assert.ThrowsAsync<InvalidOperationException>(() => postingService.PostAsync(created.Id, "poster", CancellationToken.None));
        await Assert.ThrowsAsync<InvalidOperationException>(() => documentService.UpdateDraftAsync(created.Id, Request(references, [Line(1, references.Item.Id, references.PieceUom.Id, 1m)]), "tester", CancellationToken.None));
        await Assert.ThrowsAsync<InvalidOperationException>(() => documentService.DeleteDraftAsync(created.Id, CancellationToken.None));
    }

    [Fact]
    public async Task Post_ShouldCreateOutLedgerRowsUsingServerCalculatedBaseQuantities()
    {
        await using var dbContext = CreateDbContext();
        var references = await SeedReferencesAsync(dbContext);
        await SeedStockInAsync(dbContext, references, references.Item.Id, 30m);
        await SeedStockInAsync(dbContext, references, references.SecondItem.Id, 5m);
        var documentService = CreateDocumentService(dbContext);
        var postingService = CreatePostingService(dbContext);
        var created = await documentService.CreateDraftAsync(
            Request(references, [
                Line(1, references.Item.Id, references.BoxUom.Id, 2m),
                Line(2, references.SecondItem.Id, references.PieceUom.Id, 5m)
            ]),
            "tester",
            CancellationToken.None);

        var posted = await postingService.PostAsync(created.Id, "poster", CancellationToken.None);
        var repeated = await postingService.PostAsync(created.Id, "poster", CancellationToken.None);

        Assert.NotNull(posted);
        Assert.NotNull(repeated);
        Assert.Equal(DocumentStatus.Posted, posted!.Status);
        Assert.Equal(20m, posted.Lines.Single(line => line.ItemId == references.Item.Id).BaseQty);
        Assert.Equal(5m, posted.Lines.Single(line => line.ItemId == references.SecondItem.Id).BaseQty);

        var entries = await dbContext.StockLedgerEntries
            .Where(entry => entry.SourceDocId == created.Id)
            .OrderBy(entry => entry.SourceLineId)
            .ToListAsync();
        Assert.Equal(2, entries.Count);
        Assert.All(entries, entry =>
        {
            Assert.Equal(StockTransactionType.InventoryIssue, entry.TransactionType);
            Assert.Equal(SourceDocumentType.InventoryIssue, entry.SourceDocType);
            Assert.Equal(0m, entry.QtyIn);
            Assert.NotNull(entry.LedgerOperationKey);
            Assert.StartsWith($"inventory-issue:{created.Id}:post:", entry.LedgerOperationKey);
        });
        Assert.Equal(10m, await LatestBalanceAsync(dbContext, references.Item.Id, references.Warehouse.Id));
        Assert.Equal(0m, await LatestBalanceAsync(dbContext, references.SecondItem.Id, references.Warehouse.Id));
    }

    [Fact]
    public async Task Post_ShouldBlockInsufficientStockUnlessNegativeStockAllowed()
    {
        await using var dbContext = CreateDbContext();
        var references = await SeedReferencesAsync(dbContext);
        await SeedStockInAsync(dbContext, references, references.Item.Id, 3m);
        var documentService = CreateDocumentService(dbContext);
        var postingService = CreatePostingService(dbContext);
        var created = await documentService.CreateDraftAsync(Request(references, [Line(1, references.Item.Id, references.PieceUom.Id, 5m)]), "tester", CancellationToken.None);

        var blocked = await Assert.ThrowsAsync<InvalidOperationException>(() => postingService.PostAsync(created.Id, "poster", CancellationToken.None));
        Assert.Contains("Insufficient stock", blocked.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(DocumentStatus.Draft, await dbContext.InventoryIssues.Where(entity => entity.Id == created.Id).Select(entity => entity.Status).SingleAsync());
        Assert.Equal(0, await dbContext.StockLedgerEntries.CountAsync(entry => entry.SourceDocId == created.Id));

        await SetAllowNegativeStockAsync(dbContext, true);
        var posted = await postingService.PostAsync(created.Id, "poster", CancellationToken.None);

        Assert.NotNull(posted);
        Assert.Equal(DocumentStatus.Posted, posted!.Status);
        Assert.Equal(-2m, await LatestBalanceAsync(dbContext, references.Item.Id, references.Warehouse.Id));
    }

    [Fact]
    public async Task Cancel_ShouldCreateInReversalAndBeIdempotent()
    {
        await using var dbContext = CreateDbContext();
        var references = await SeedReferencesAsync(dbContext);
        await SeedStockInAsync(dbContext, references, references.Item.Id, 10m);
        var documentService = CreateDocumentService(dbContext);
        var postingService = CreatePostingService(dbContext);
        var created = await documentService.CreateDraftAsync(Request(references, [Line(1, references.Item.Id, references.PieceUom.Id, 4m)]), "tester", CancellationToken.None);
        await postingService.PostAsync(created.Id, "poster", CancellationToken.None);

        var canceled = await postingService.CancelAsync(created.Id, "canceler", CancellationToken.None);
        var repeated = await postingService.CancelAsync(created.Id, "canceler", CancellationToken.None);

        Assert.NotNull(canceled);
        Assert.NotNull(repeated);
        Assert.Equal(DocumentStatus.Canceled, canceled!.Status);
        Assert.Equal(1, await dbContext.StockLedgerEntries.CountAsync(entry => entry.SourceDocId == created.Id && entry.TransactionType == StockTransactionType.InventoryIssue));
        var cancellation = await dbContext.StockLedgerEntries.SingleAsync(entry => entry.SourceDocId == created.Id && entry.TransactionType == StockTransactionType.InventoryIssueCancellation);
        Assert.Equal(SourceDocumentType.InventoryIssueCancellation, cancellation.SourceDocType);
        Assert.Equal(4m, cancellation.QtyIn);
        Assert.Equal(0m, cancellation.QtyOut);
        Assert.Equal(10m, await LatestBalanceAsync(dbContext, references.Item.Id, references.Warehouse.Id));
    }

    [Fact]
    public async Task Cancel_ShouldReversePostedBaseQuantitiesAfterSetupChanges()
    {
        await using var dbContext = CreateDbContext();
        var references = await SeedReferencesAsync(dbContext);
        await SeedStockInAsync(dbContext, references, references.Item.Id, 30m);
        var documentService = CreateDocumentService(dbContext);
        var postingService = CreatePostingService(dbContext);
        var created = await documentService.CreateDraftAsync(Request(references, [Line(1, references.Item.Id, references.BoxUom.Id, 2m)]), "tester", CancellationToken.None);
        var posted = await postingService.PostAsync(created.Id, "poster", CancellationToken.None);
        Assert.NotNull(posted);
        Assert.Equal(20m, posted!.Lines.Single().BaseQty);

        var conversion = await dbContext.UomConversions.SingleAsync(entity => entity.FromUomId == references.BoxUom.Id && entity.ToUomId == references.PieceUom.Id);
        conversion.Factor = 99m;
        references.Warehouse.IsActive = false;
        references.Item.IsActive = false;
        references.BoxUom.IsActive = false;
        await dbContext.SaveChangesAsync();

        var canceled = await postingService.CancelAsync(created.Id, "canceler", CancellationToken.None);

        Assert.NotNull(canceled);
        Assert.Equal(DocumentStatus.Canceled, canceled!.Status);
        var cancellation = await dbContext.StockLedgerEntries.SingleAsync(entry => entry.SourceDocId == created.Id && entry.TransactionType == StockTransactionType.InventoryIssueCancellation);
        Assert.Equal(2m, cancellation.QtyIn);
        Assert.Equal(20m, cancellation.BaseQty);
        Assert.Equal(30m, await LatestBalanceAsync(dbContext, references.Item.Id, references.Warehouse.Id));
    }

    [Fact]
    public async Task List_ShouldFilterSortAndPageOnServer()
    {
        await using var dbContext = CreateDbContext();
        var references = await SeedReferencesAsync(dbContext);
        var service = CreateDocumentService(dbContext);
        var first = await service.CreateDraftAsync(Request(references, [Line(1, references.Item.Id, references.PieceUom.Id, 1m)]), "tester", CancellationToken.None);
        var second = await service.CreateDraftAsync(Request(references, [Line(1, references.SecondItem.Id, references.PieceUom.Id, 1m)]), "tester", CancellationToken.None);
        var third = await service.CreateDraftAsync(Request(references, [Line(1, references.ThirdItem.Id, references.PieceUom.Id, 1m)]), "tester", CancellationToken.None);

        var firstEntity = await dbContext.InventoryIssues.SingleAsync(entity => entity.Id == first.Id);
        var secondEntity = await dbContext.InventoryIssues.SingleAsync(entity => entity.Id == second.Id);
        var thirdEntity = await dbContext.InventoryIssues.SingleAsync(entity => entity.Id == third.Id);
        firstEntity.IssueDate = DateTime.UtcNow.Date.AddDays(-2);
        firstEntity.Reason = InventoryIssueReason.Damage;
        secondEntity.IssueDate = DateTime.UtcNow.Date.AddDays(-1);
        secondEntity.Reason = InventoryIssueReason.Damage;
        thirdEntity.IssueDate = DateTime.UtcNow.Date;
        thirdEntity.Reason = InventoryIssueReason.Scrap;
        await dbContext.SaveChangesAsync();

        var page = await service.ListAsync(
            new InventoryIssueListQuery(
                Search: "Issue Warehouse",
                IssueNo: "ISS-",
                WarehouseId: references.Warehouse.Id,
                Reason: InventoryIssueReason.Damage,
                Status: DocumentStatus.Draft,
                FromDate: DateTime.UtcNow.Date.AddDays(-2),
                ToDate: DateTime.UtcNow.Date,
                Page: 2,
                PageSize: 1,
                SortBy: "issueDate",
                SortDirection: SortDirection.Asc),
            CancellationToken.None);

        Assert.Equal(2, page.TotalCount);
        Assert.Equal(2, page.TotalPages);
        Assert.Equal(second.Id, page.Items.Single().Id);
    }

    [Fact]
    public async Task PostConcurrently_ShouldCreateOneSetOfLedgerRows()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"highcool-issue-concurrent-post-{Guid.NewGuid():N}.db");
        try
        {
            Guid organizationId;
            Guid issueId;
            await using (var dbContext = CreateInitializedDbContext(databasePath, out var initContext))
            {
                organizationId = initContext.OrganizationId!.Value;
                var references = await SeedReferencesAsync(dbContext);
                await SeedStockInAsync(dbContext, references, references.Item.Id, 10m);
                var documentService = CreateDocumentService(dbContext);
                var created = await documentService.CreateDraftAsync(Request(references, [Line(1, references.Item.Id, references.PieceUom.Id, 4m)]), "tester", CancellationToken.None);
                issueId = created.Id;
            }

            var tasks = Enumerable.Range(0, 2)
                .Select(_ => Task.Run(async () =>
                {
                    await using var dbContext = CreateDbContext(databasePath, TestOrganizationContext.CreateExecutionContext(organizationId));
                    return await CreatePostingService(dbContext).PostAsync(issueId, "poster", CancellationToken.None);
                }))
                .ToArray();

            var results = await Task.WhenAll(tasks);
            await using var verifyContext = CreateDbContext(databasePath, TestOrganizationContext.CreateExecutionContext(organizationId));

            Assert.All(results, result => Assert.NotNull(result));
            Assert.Equal(DocumentStatus.Posted, await verifyContext.InventoryIssues.Where(entity => entity.Id == issueId).Select(entity => entity.Status).SingleAsync());
            Assert.Equal(1, await verifyContext.StockLedgerEntries.CountAsync(entry => entry.SourceDocId == issueId && entry.TransactionType == StockTransactionType.InventoryIssue));
        }
        finally
        {
            SqliteTestDatabase.DeleteSqliteFileSet(databasePath);
        }
    }

    [Fact]
    public async Task CancelConcurrently_ShouldCreateOneSetOfReversalRows()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"highcool-issue-concurrent-cancel-{Guid.NewGuid():N}.db");
        try
        {
            Guid organizationId;
            Guid issueId;
            await using (var dbContext = CreateInitializedDbContext(databasePath, out var initContext))
            {
                organizationId = initContext.OrganizationId!.Value;
                var references = await SeedReferencesAsync(dbContext);
                await SeedStockInAsync(dbContext, references, references.Item.Id, 10m);
                var documentService = CreateDocumentService(dbContext);
                var postingService = CreatePostingService(dbContext);
                var created = await documentService.CreateDraftAsync(Request(references, [Line(1, references.Item.Id, references.PieceUom.Id, 4m)]), "tester", CancellationToken.None);
                await postingService.PostAsync(created.Id, "poster", CancellationToken.None);
                issueId = created.Id;
            }

            var tasks = Enumerable.Range(0, 2)
                .Select(_ => Task.Run(async () =>
                {
                    await using var dbContext = CreateDbContext(databasePath, TestOrganizationContext.CreateExecutionContext(organizationId));
                    return await CreatePostingService(dbContext).CancelAsync(issueId, "canceler", CancellationToken.None);
                }))
                .ToArray();

            var results = await Task.WhenAll(tasks);
            await using var verifyContext = CreateDbContext(databasePath, TestOrganizationContext.CreateExecutionContext(organizationId));

            Assert.All(results, result => Assert.NotNull(result));
            Assert.Equal(DocumentStatus.Canceled, await verifyContext.InventoryIssues.Where(entity => entity.Id == issueId).Select(entity => entity.Status).SingleAsync());
            Assert.Equal(1, await verifyContext.StockLedgerEntries.CountAsync(entry => entry.SourceDocId == issueId && entry.TransactionType == StockTransactionType.InventoryIssue));
            Assert.Equal(1, await verifyContext.StockLedgerEntries.CountAsync(entry => entry.SourceDocId == issueId && entry.TransactionType == StockTransactionType.InventoryIssueCancellation));
        }
        finally
        {
            SqliteTestDatabase.DeleteSqliteFileSet(databasePath);
        }
    }

    private static AppDbContext CreateDbContext()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"highcool-issue-tests-{Guid.NewGuid():N}.db");
        return CreateInitializedDbContext(databasePath, out _);
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

    private static IInventoryIssueService CreateDocumentService(AppDbContext dbContext)
    {
        var organizationId = dbContext.Organizations
            .Select(entity => entity.Id)
            .Single();
        return CreateDocumentService(dbContext, organizationId);
    }

    private static IInventoryIssueService CreateDocumentService(AppDbContext dbContext, Guid organizationId)
    {
        var executionContext = TestOrganizationContext.CreateExecutionContext(organizationId);

        return new InventoryIssueService(
            dbContext,
            new QuantityConversionService(dbContext),
            new DocumentNumberService(dbContext, executionContext));
    }

    private static IInventoryIssuePostingService CreatePostingService(AppDbContext dbContext)
    {
        return new InventoryIssuePostingService(
            dbContext,
            CreateDocumentService(dbContext),
            new QuantityConversionService(dbContext),
            new StockAvailabilityService(dbContext));
    }

    private static async Task<IssueReferences> SeedReferencesAsync(AppDbContext dbContext)
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
            Name = "Boxes",
            Precision = 6,
            AllowsFraction = true,
            IsActive = true,
            CreatedBy = "seed"
        };
        var warehouse = new Warehouse
        {
            Code = $"ISS-{Guid.NewGuid():N}"[..12],
            Name = "Issue Warehouse",
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
        await dbContext.SaveChangesAsync();

        var items = Enumerable.Range(1, 3)
            .Select(index => new Item
            {
                Code = $"ISS-IT{index}-{Guid.NewGuid():N}"[..12],
                Name = $"Issue Item {index}",
                BaseUomId = pieceUom.Id,
                IsActive = true,
                IsSellable = true,
                HasComponents = false,
                CreatedBy = "seed"
            })
            .ToArray();
        dbContext.Items.AddRange(items);
        await dbContext.SaveChangesAsync();

        return new IssueReferences(warehouse, pieceUom, boxUom, items[0], items[1], items[2]);
    }

    private static async Task<IssueReferences> LoadReferencesAsync(AppDbContext dbContext)
    {
        var warehouse = await dbContext.Warehouses.SingleAsync(entity => entity.Name == "Issue Warehouse");
        var pieceUom = await dbContext.Uoms.SingleAsync(entity => entity.Name == "Pieces");
        var boxUom = await dbContext.Uoms.SingleAsync(entity => entity.Name == "Boxes");
        var items = await dbContext.Items
            .OrderBy(entity => entity.Name)
            .Where(entity => entity.Name.StartsWith("Issue Item "))
            .ToArrayAsync();

        return new IssueReferences(warehouse, pieceUom, boxUom, items[0], items[1], items[2]);
    }

    private static async Task SeedStockInAsync(AppDbContext dbContext, IssueReferences references, Guid itemId, decimal baseQty)
    {
        var currentBalance = await CurrentBalanceAsync(dbContext, itemId, references.Warehouse.Id);
        dbContext.StockLedgerEntries.Add(new StockLedgerEntry
        {
            ItemId = itemId,
            WarehouseId = references.Warehouse.Id,
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

    private static async Task<decimal> CurrentBalanceAsync(AppDbContext dbContext, Guid itemId, Guid warehouseId)
    {
        return await dbContext.StockLedgerEntries
            .Where(entry => entry.ItemId == itemId && entry.WarehouseId == warehouseId)
            .OrderByDescending(entry => entry.TransactionDate)
            .ThenByDescending(entry => entry.CreatedAt)
            .ThenByDescending(entry => entry.Id)
            .Select(entry => (decimal?)entry.RunningBalanceQty)
            .FirstOrDefaultAsync() ?? 0m;
    }

    private static async Task<decimal> LatestBalanceAsync(AppDbContext dbContext, Guid itemId, Guid warehouseId)
    {
        return await dbContext.StockLedgerEntries
            .Where(entry => entry.ItemId == itemId && entry.WarehouseId == warehouseId)
            .OrderByDescending(entry => entry.TransactionDate)
            .ThenByDescending(entry => entry.CreatedAt)
            .ThenByDescending(entry => entry.Id)
            .Select(entry => entry.RunningBalanceQty)
            .FirstAsync();
    }

    private static UpsertInventoryIssueRequest Request(
        IssueReferences references,
        IReadOnlyList<UpsertInventoryIssueLineRequest> lines)
    {
        return new UpsertInventoryIssueRequest(
            null,
            DateTime.UtcNow.Date,
            references.Warehouse.Id,
            InventoryIssueReason.InternalConsumption,
            "REQ-1",
            "Maintenance Team",
            "Operational issue",
            lines);
    }

    private static UpsertInventoryIssueLineRequest Line(int lineNo, Guid itemId, Guid uomId, decimal quantity)
    {
        return new UpsertInventoryIssueLineRequest(lineNo, itemId, uomId, quantity, null);
    }

    private sealed record IssueReferences(
        Warehouse Warehouse,
        Uom PieceUom,
        Uom BoxUom,
        Item Item,
        Item SecondItem,
        Item ThirdItem);
}
