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

public sealed class MasterDataApiTests : IClassFixture<MasterDataApiTests.ApiFactory>
{
    private readonly ApiFactory _factory;

    public MasterDataApiTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task CustomersApi_ShouldCreateListAndToggleCustomerStatus()
    {
        await _factory.ResetDatabaseAsync();

        var client = _factory.CreateClient();
        var createResponse = await client.PostAsJsonAsync("/api/customers", new
        {
            code = "CUS-ERP-01",
            name = "North Coast Projects",
            phone = "+20-100-123-4567",
            email = "finance@northcoast.example",
            taxNumber = "TN-7788",
            address = "Industrial Road 22",
            city = "Cairo",
            area = "Heliopolis",
            creditLimit = 25000m,
            paymentTerms = "21 days",
            notes = "Key account",
            isActive = true
        });

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var created = await createResponse.Content.ReadFromJsonAsync<CustomerResponse>();
        Assert.NotNull(created);
        Assert.Equal("CUS-ERP-01", created!.Code);
        Assert.Equal(25000m, created.CreditLimit);

        var listResponse = await client.GetAsync("/api/customers?search=123&isActive=true");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);

        var list = await listResponse.Content.ReadFromJsonAsync<List<CustomerListItemResponse>>();
        Assert.NotNull(list);
        Assert.Single(list!);
        Assert.Equal(created.Id, list[0].Id);

        var deactivateResponse = await client.PostAsync($"/api/customers/{created.Id}/deactivate", null);
        Assert.Equal(HttpStatusCode.NoContent, deactivateResponse.StatusCode);

        var inactiveResponse = await client.GetAsync("/api/customers?isActive=false");
        Assert.Equal(HttpStatusCode.OK, inactiveResponse.StatusCode);
        var inactiveList = await inactiveResponse.Content.ReadFromJsonAsync<List<CustomerListItemResponse>>();
        Assert.NotNull(inactiveList);
        Assert.Contains(inactiveList!, row => row.Id == created.Id && !row.IsActive);

        var activateResponse = await client.PostAsync($"/api/customers/{created.Id}/activate", null);
        Assert.Equal(HttpStatusCode.NoContent, activateResponse.StatusCode);

        var getResponse = await client.GetAsync($"/api/customers/{created.Id}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var fetched = await getResponse.Content.ReadFromJsonAsync<CustomerResponse>();
        Assert.NotNull(fetched);
        Assert.True(fetched!.IsActive);
    }

    [Fact]
    public async Task ItemsApi_ShouldCreateAndReturnItemWithComponents()
    {
        await _factory.ResetDatabaseAsync();
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var pieceUom = new Domain.MasterData.Uom
        {
            Code = "PCS",
            Name = "Pieces",
            Precision = 0,
            AllowsFraction = false,
            IsActive = true,
            CreatedBy = "seed"
        };

        var boxUom = new Domain.MasterData.Uom
        {
            Code = "BOX",
            Name = "Box",
            Precision = 0,
            AllowsFraction = false,
            IsActive = true,
            CreatedBy = "seed"
        };

        dbContext.Uoms.AddRange(pieceUom, boxUom);
        await dbContext.SaveChangesAsync();

        var componentItem = new Domain.MasterData.Item
        {
            Code = "ITM-COMP",
            Name = "Component",
            BaseUomId = pieceUom.Id,
            IsActive = true,
            IsSellable = false,
            HasComponents = false,
            CreatedBy = "seed"
        };

        dbContext.Items.Add(componentItem);
        dbContext.UomConversions.Add(new Domain.MasterData.UomConversion
        {
            FromUomId = boxUom.Id,
            ToUomId = pieceUom.Id,
            Factor = 10m,
            RoundingMode = Domain.MasterData.RoundingMode.Round,
            IsActive = true,
            CreatedBy = "seed"
        });
        await dbContext.SaveChangesAsync();

        var client = _factory.CreateClient();
        var createResponse = await client.PostAsJsonAsync("/api/items", new
        {
            code = "ITM-ASM",
            name = "Assembly",
            baseUomId = pieceUom.Id,
            isActive = true,
            isSellable = true,
            hasComponents = true,
            components = new[]
            {
                new
                {
                    componentItemId = componentItem.Id,
                    uomId = boxUom.Id,
                    quantity = 2m
                }
            }
        });

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var created = await createResponse.Content.ReadFromJsonAsync<ItemResponse>();
        Assert.NotNull(created);
        Assert.True(created!.HasComponents);
        Assert.Single(created.Components);

        var getResponse = await client.GetAsync($"/api/items/{created.Id}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        var fetched = await getResponse.Content.ReadFromJsonAsync<ItemResponse>();
        Assert.NotNull(fetched);
        Assert.Single(fetched!.Components);
        Assert.Equal("BOX", fetched.Components[0].UomCode);
    }

    [Fact]
    public async Task UomConversionsApi_ShouldCreateAndListGlobalConversions()
    {
        await _factory.ResetDatabaseAsync();
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var fromUom = new Domain.MasterData.Uom
        {
            Code = "BOX",
            Name = "Box",
            Precision = 0,
            AllowsFraction = false,
            IsActive = true,
            CreatedBy = "seed"
        };

        var toUom = new Domain.MasterData.Uom
        {
            Code = "PCS",
            Name = "Pieces",
            Precision = 0,
            AllowsFraction = false,
            IsActive = true,
            CreatedBy = "seed"
        };

        dbContext.Uoms.AddRange(fromUom, toUom);
        await dbContext.SaveChangesAsync();

        var client = _factory.CreateClient();
        var createResponse = await client.PostAsJsonAsync("/api/uom-conversions", new
        {
            fromUomId = fromUom.Id,
            toUomId = toUom.Id,
            factor = 12m,
            roundingMode = "Round",
            isActive = true
        });

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var listResponse = await client.GetAsync("/api/uom-conversions");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);

        var rows = await listResponse.Content.ReadFromJsonAsync<List<UomConversionResponse>>();
        Assert.NotNull(rows);
        Assert.Single(rows!);
        Assert.Equal("BOX", rows[0].FromUomCode);
        Assert.Equal("PCS", rows[0].ToUomCode);
    }

    public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
    {
        private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"highcool-api-tests-{Guid.NewGuid():N}.db");

        protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
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

    public sealed record ItemResponse(Guid Id, bool HasComponents, List<ItemComponentResponse> Components);

    public sealed record ItemComponentResponse(Guid ComponentItemId, string UomCode, decimal Quantity);

    public sealed record UomConversionResponse(Guid Id, string FromUomCode, string ToUomCode, decimal Factor);

    public sealed record CustomerResponse(Guid Id, string Code, decimal CreditLimit, bool IsActive);

    public sealed record CustomerListItemResponse(Guid Id, string Code, string Name, string? Phone, bool IsActive);
}
