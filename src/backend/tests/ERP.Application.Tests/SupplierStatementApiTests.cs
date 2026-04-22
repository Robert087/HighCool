using System.Net;
using System.Net.Http.Json;
using ERP.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace ERP.Application.Tests;

public sealed class SupplierStatementApiTests : IClassFixture<SupplierStatementApiTests.ApiFactory>
{
    private readonly ApiFactory _factory;

    public SupplierStatementApiTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task SupplierStatementApis_ShouldReturnStatementRowsAndSummary()
    {
        await _factory.ResetDatabaseAsync();
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var supplier = new Domain.MasterData.Supplier
        {
            Code = "SUP-STMT-API",
            Name = "Supplier Statement API",
            StatementName = "Supplier Statement API",
            IsActive = true,
            CreatedBy = "seed"
        };

        var warehouse = new Domain.MasterData.Warehouse
        {
            Code = "WH-STMT-API",
            Name = "Statement Warehouse",
            IsActive = true,
            CreatedBy = "seed"
        };

        dbContext.Suppliers.Add(supplier);
        dbContext.Warehouses.Add(warehouse);
        await dbContext.SaveChangesAsync();

        var receipt = new Domain.Purchasing.PurchaseReceipt
        {
            ReceiptNo = "PR-STMT-API-0001",
            SupplierId = supplier.Id,
            WarehouseId = warehouse.Id,
            ReceiptDate = new DateTime(2026, 4, 20),
            Status = Domain.Common.DocumentStatus.Posted,
            CreatedBy = "seed"
        };

        var resolution = new Domain.Shortages.ShortageResolution
        {
            ResolutionNo = "SR-STMT-API-0001",
            SupplierId = supplier.Id,
            ResolutionType = Domain.Shortages.ShortageResolutionType.Financial,
            ResolutionDate = new DateTime(2026, 4, 21),
            Status = Domain.Common.DocumentStatus.Posted,
            CreatedBy = "seed"
        };

        dbContext.PurchaseReceipts.Add(receipt);
        dbContext.ShortageResolutions.Add(resolution);
        await dbContext.SaveChangesAsync();

        dbContext.SupplierStatementEntries.AddRange(
            new Domain.Statements.SupplierStatementEntry
            {
                SupplierId = supplier.Id,
                EntryDate = receipt.ReceiptDate,
                SourceDocType = Domain.Statements.SupplierStatementSourceDocumentType.PurchaseReceipt,
                SourceDocId = receipt.Id,
                SourceLineId = receipt.Id,
                EffectType = Domain.Statements.SupplierStatementEffectType.PurchaseReceipt,
                Debit = 0m,
                Credit = 0m,
                RunningBalance = 0m,
                Notes = "Receipt amount pending valuation",
                CreatedBy = "seed"
            },
            new Domain.Statements.SupplierStatementEntry
            {
                SupplierId = supplier.Id,
                EntryDate = resolution.ResolutionDate,
                SourceDocType = Domain.Statements.SupplierStatementSourceDocumentType.ShortageResolution,
                SourceDocId = resolution.Id,
                SourceLineId = Guid.NewGuid(),
                EffectType = Domain.Statements.SupplierStatementEffectType.ShortageFinancialResolution,
                Debit = 30m,
                Credit = 0m,
                RunningBalance = -30m,
                Currency = "EGP",
                Notes = "Financial settlement",
                CreatedBy = "seed"
            });

        await dbContext.SaveChangesAsync();

        var client = _factory.CreateClient();

        var statementResponse = await client.GetAsync($"/api/suppliers/{supplier.Id}/statement?sourceDocType=ShortageResolution");
        Assert.Equal(HttpStatusCode.OK, statementResponse.StatusCode);
        var statementRows = await statementResponse.Content.ReadFromJsonAsync<SupplierStatementRowResponse[]>();
        var row = Assert.Single(statementRows!);
        Assert.Equal("SR-STMT-API-0001", row.SourceDocumentNo);
        Assert.Equal(30m, row.Debit);

        var summaryResponse = await client.GetAsync($"/api/suppliers/{supplier.Id}/statement/summary?fromDate=2026-04-20&toDate=2026-04-21");
        Assert.Equal(HttpStatusCode.OK, summaryResponse.StatusCode);
        var summary = await summaryResponse.Content.ReadFromJsonAsync<SupplierStatementSummaryResponse>();
        Assert.NotNull(summary);
        Assert.Equal(-30m, summary!.ClosingBalance);
        Assert.Equal(30m, summary.TotalDebit);
    }

    public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
    {
        private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"highcool-supplier-statement-api-tests-{Guid.NewGuid():N}.db");

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["DatabaseProvider"] = "Sqlite",
                    ["ConnectionStrings:DefaultConnection"] = $"Data Source={_databasePath}"
                });
            });
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<AppDbContext>>();
                services.RemoveAll<AppDbContext>();
                services.AddDbContext<AppDbContext>(options => options.UseSqlite($"Data Source={_databasePath}"));
            });
        }

        public async Task InitializeAsync()
        {
            await ResetDatabaseAsync();
        }

        public new async Task DisposeAsync()
        {
            if (File.Exists(_databasePath))
            {
                File.Delete(_databasePath);
            }

            await base.DisposeAsync();
        }

        public async Task ResetDatabaseAsync()
        {
            await using var scope = Services.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await dbContext.Database.EnsureDeletedAsync();
            await dbContext.Database.EnsureCreatedAsync();
        }
    }

    private sealed record SupplierStatementRowResponse(Guid Id, string SourceDocumentNo, decimal Debit);

    private sealed record SupplierStatementSummaryResponse(decimal ClosingBalance, decimal TotalDebit);
}
