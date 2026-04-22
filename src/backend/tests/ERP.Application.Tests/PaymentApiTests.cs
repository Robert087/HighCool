using System.Net;
using System.Net.Http.Json;
using ERP.Domain.Common;
using ERP.Domain.MasterData;
using ERP.Domain.Purchasing;
using ERP.Domain.Statements;
using ERP.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace ERP.Application.Tests;

public sealed class PaymentApiTests : IClassFixture<PaymentApiTests.ApiFactory>
{
    private readonly ApiFactory _factory;

    public PaymentApiTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task PaymentApis_ShouldCreatePostAndReturnOpenBalances()
    {
        await _factory.ResetDatabaseAsync();
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var supplier = new Supplier
        {
            Code = "SUP-PAY-API",
            Name = "Payment API Supplier",
            StatementName = "Payment API Supplier",
            IsActive = true,
            CreatedBy = "seed"
        };

        var warehouse = new Warehouse
        {
            Code = "WH-PAY-API",
            Name = "Payment API Warehouse",
            IsActive = true,
            CreatedBy = "seed"
        };

        dbContext.Suppliers.Add(supplier);
        dbContext.Warehouses.Add(warehouse);
        await dbContext.SaveChangesAsync();

        var receipt = new PurchaseReceipt
        {
            ReceiptNo = "PR-PAY-API-0001",
            SupplierId = supplier.Id,
            WarehouseId = warehouse.Id,
            ReceiptDate = new DateTime(2026, 4, 22),
            SupplierPayableAmount = 100m,
            Status = DocumentStatus.Posted,
            CreatedBy = "seed"
        };

        dbContext.PurchaseReceipts.Add(receipt);
        await dbContext.SaveChangesAsync();

        dbContext.SupplierStatementEntries.Add(new SupplierStatementEntry
        {
            SupplierId = supplier.Id,
            EntryDate = receipt.ReceiptDate,
            SourceDocType = SupplierStatementSourceDocumentType.PurchaseReceipt,
            SourceDocId = receipt.Id,
            SourceLineId = receipt.Id,
            EffectType = SupplierStatementEffectType.PurchaseReceipt,
            Debit = 0m,
            Credit = 100m,
            RunningBalance = 100m,
            Currency = "EGP",
            Notes = "Receipt payable",
            CreatedBy = "seed"
        });
        await dbContext.SaveChangesAsync();

        var client = _factory.CreateClient();

        var createResponse = await client.PostAsJsonAsync("/api/payments", new
        {
            paymentNo = "PAY-API-0001",
            partyType = "Supplier",
            partyId = supplier.Id,
            direction = "OutboundToParty",
            amount = 100m,
            paymentDate = new DateTime(2026, 4, 22),
            currency = "EGP",
            exchangeRate = (decimal?)null,
            paymentMethod = "BankTransfer",
            referenceNote = "BANK-REF-001",
            notes = "Supplier payment",
            allocations = new[]
            {
                new
                {
                    targetDocType = "PurchaseReceipt",
                    targetDocId = receipt.Id,
                    targetLineId = (Guid?)null,
                    allocatedAmount = 100m,
                    allocationOrder = 1
                }
            }
        });

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<PaymentResponse>();
        Assert.NotNull(created);

        var postResponse = await client.PostAsync($"/api/payments/{created!.Id}/post", null);
        Assert.Equal(HttpStatusCode.OK, postResponse.StatusCode);
        var posted = await postResponse.Content.ReadFromJsonAsync<PaymentResponse>();
        Assert.NotNull(posted);
        Assert.Equal("Posted", posted!.Status);

        var allocationsResponse = await client.GetAsync($"/api/payments/{created.Id}/allocations");
        Assert.Equal(HttpStatusCode.OK, allocationsResponse.StatusCode);
        var allocations = await allocationsResponse.Content.ReadFromJsonAsync<PaymentAllocationResponse[]>();
        var allocation = Assert.Single(allocations!);
        Assert.Equal("PR-PAY-API-0001", allocation.TargetDocumentNo);
        Assert.Equal(100m, allocation.AllocatedAmount);

        var openBalancesResponse = await client.GetAsync($"/api/suppliers/{supplier.Id}/open-balances?direction=OutboundToParty");
        Assert.Equal(HttpStatusCode.OK, openBalancesResponse.StatusCode);
        var balances = await openBalancesResponse.Content.ReadFromJsonAsync<SupplierOpenBalanceResponse[]>();
        Assert.NotNull(balances);
        Assert.Empty(balances!);
    }

    public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
    {
        private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"highcool-payment-api-tests-{Guid.NewGuid():N}.db");

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

    private sealed record PaymentResponse(Guid Id, string Status);
    private sealed record PaymentAllocationResponse(string TargetDocumentNo, decimal AllocatedAmount);
    private sealed record SupplierOpenBalanceResponse(string TargetDocumentNo);
}
