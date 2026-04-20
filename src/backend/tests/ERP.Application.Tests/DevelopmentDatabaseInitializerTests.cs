using ERP.Infrastructure;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace ERP.Application.Tests;

public sealed class DevelopmentDatabaseInitializerTests
{
    [Fact]
    public async Task StartAsync_SeedsSqliteDevelopmentDatabase()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.db");

        try
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["DatabaseProvider"] = "Sqlite",
                    ["ConnectionStrings:DefaultConnection"] = $"Data Source={databasePath}"
                })
                .Build();

            var services = new ServiceCollection();
            services.AddDbContext<AppDbContext>(options =>
                options.UseSqlite(configuration.GetConnectionString("DefaultConnection")));

            await using var provider = services.BuildServiceProvider();
            var initializer = new DevelopmentDatabaseInitializer(
                provider,
                configuration,
                new TestHostEnvironment { EnvironmentName = Environments.Development });

            await initializer.StartAsync(CancellationToken.None);

            await using var scope = provider.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            Assert.True(await dbContext.Suppliers.AnyAsync());
            Assert.True(await dbContext.Warehouses.AnyAsync());
            Assert.True(await dbContext.Uoms.AnyAsync());
            Assert.True(await dbContext.Items.AnyAsync());
            Assert.True(await dbContext.ItemComponents.AnyAsync());
            Assert.True(await dbContext.ItemUomConversions.AnyAsync());
        }
        finally
        {
            if (File.Exists(databasePath))
            {
                File.Delete(databasePath);
            }
        }
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;

        public string ApplicationName { get; set; } = "ERP.Tests";

        public string ContentRootPath { get; set; } = Directory.GetCurrentDirectory();

        public IFileProvider ContentRootFileProvider { get; set; } =
            new PhysicalFileProvider(Directory.GetCurrentDirectory());
    }
}
