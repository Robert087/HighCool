using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ERP.Application.LocalData;
using ERP.Domain.Identity;
using ERP.Domain.MasterData;
using ERP.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace ERP.Application.Tests;

public sealed class LocalDatabaseApiTests : IClassFixture<LocalDatabaseApiTests.ApiFactory>
{
    private readonly ApiFactory _factory;

    public LocalDatabaseApiTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task LocalDatabaseEndpoints_RejectUnauthenticatedAndMissingPermissionAndNonDesktop()
    {
        await _factory.ResetDatabaseAsync();
        var client = _factory.CreateClient();

        _factory.ClearAuthenticatedContext();
        var unauthenticated = await client.GetAsync("/api/local-database/backups/summary");
        Assert.Equal(HttpStatusCode.Unauthorized, unauthenticated.StatusCode);

        await _factory.ResetDatabaseAsync();
        await _factory.MakeCurrentUserNonOwnerWithoutRolesAsync();
        var forbidden = await client.GetAsync("/api/local-database/backups/summary");
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);

        await using var nonDesktopFactory = new ApiFactory("Testing", enableEndpointCapability: false);
        await nonDesktopFactory.InitializeAsync();
        var nonDesktop = await nonDesktopFactory.CreateClient().GetAsync("/api/local-database/backups/summary");
        Assert.Equal(HttpStatusCode.Conflict, nonDesktop.StatusCode);
        Assert.Contains("LocalDatabaseFeatureUnavailable", await nonDesktop.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task BackupCatalogEndpoints_ReturnSafeSummaryListDetailsAndRejectInvalidIds()
    {
        await _factory.ResetDatabaseAsync();
        var client = _factory.CreateClient();

        var backup = await CreateBackupAsync(client);
        var summary = await client.GetAsync("/api/local-database/backups/summary");
        summary.EnsureSuccessStatusCode();
        var summaryPayload = await summary.Content.ReadAsStringAsync();
        Assert.Contains("\"availableBackupCount\":1", summaryPayload);
        Assert.DoesNotContain(_factory.RootDirectory, summaryPayload, StringComparison.OrdinalIgnoreCase);

        var list = await client.GetFromJsonAsync<JsonElement[]>("/api/local-database/backups");
        var item = Assert.Single(list!);
        Assert.Equal(backup.BackupId, item.GetProperty("backupId").GetString());

        var details = await client.GetAsync($"/api/local-database/backups/{backup.BackupId}");
        details.EnsureSuccessStatusCode();
        var detailsPayload = await details.Content.ReadAsStringAsync();
        Assert.Contains("\"databaseFileName\"", detailsPayload);
        Assert.DoesNotContain(_factory.RootDirectory, detailsPayload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("keyBytes", detailsPayload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("JwtSecret", detailsPayload, StringComparison.OrdinalIgnoreCase);

        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/local-database/backups/unknown")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/local-database/backups/..%2Fsecret")).StatusCode);
    }

    [Fact]
    public async Task CloudConfigurationEndpoints_RejectUnsafeAccessAndNeverReturnSecrets()
    {
        await _factory.ResetDatabaseAsync();
        var client = _factory.CreateClient();

        _factory.ClearAuthenticatedContext();
        var unauthenticated = await client.GetAsync("/api/local-database/cloud/status");
        Assert.Equal(HttpStatusCode.Unauthorized, unauthenticated.StatusCode);

        await _factory.ResetDatabaseAsync();
        await _factory.MakeCurrentUserNonOwnerWithoutRolesAsync();
        var forbidden = await client.GetAsync("/api/local-database/cloud/status");
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);

        await _factory.ResetDatabaseAsync();
        var unsafeEndpoint = await client.PutAsJsonAsync("/api/local-database/cloud/configuration", new CloudBackupConfigurationRequest(
            true,
            true,
            "highcool-backups",
            "https://localhost",
            "r2-access-key",
            "r2-secret-key",
            "desktop",
            30,
            30,
            3,
            CloudBackupCredentialUpdateMode.Replace));
        Assert.Equal(HttpStatusCode.BadRequest, unsafeEndpoint.StatusCode);
        Assert.DoesNotContain("r2-secret-key", await unsafeEndpoint.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);

        var saved = await client.PutAsJsonAsync("/api/local-database/cloud/configuration", new CloudBackupConfigurationRequest(
            true,
            true,
            "highcool-backups",
            "https://aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa.r2.cloudflarestorage.com",
            "r2-access-key",
            "r2-secret-key",
            "desktop",
            30,
            30,
            3,
            CloudBackupCredentialUpdateMode.Replace));
        saved.EnsureSuccessStatusCode();
        var payload = await saved.Content.ReadAsStringAsync();
        Assert.Contains("\"hasAccessKey\":true", payload);
        Assert.Contains("\"hasSecretKey\":true", payload);
        Assert.DoesNotContain("r2-secret-key", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("r2-access-key\"", payload, StringComparison.OrdinalIgnoreCase);

        await using var nonDesktopFactory = new ApiFactory("Testing", enableEndpointCapability: false);
        await nonDesktopFactory.InitializeAsync();
        var nonDesktop = await nonDesktopFactory.CreateClient().GetAsync("/api/local-database/cloud/status");
        Assert.Equal(HttpStatusCode.Conflict, nonDesktop.StatusCode);
        Assert.Contains("LocalDatabaseFeatureUnavailable", await nonDesktop.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task BackupVerifyAndRetentionEndpoints_ReturnSafeResultsAndClampSettings()
    {
        await _factory.ResetDatabaseAsync();
        var client = _factory.CreateClient();
        var backup = await CreateBackupAsync(client);

        var verify = await client.PostAsync($"/api/local-database/backups/{backup.BackupId}/verify", null);
        verify.EnsureSuccessStatusCode();
        Assert.Contains("\"status\":\"Verified\"", await verify.Content.ReadAsStringAsync());

        await TamperEncryptedBackupAsync(_factory.BackupDirectory, backup.BackupFileName!);
        var failedVerify = await client.PostAsync($"/api/local-database/backups/{backup.BackupId}/verify", null);
        failedVerify.EnsureSuccessStatusCode();
        var failedVerifyPayload = await failedVerify.Content.ReadAsStringAsync();
        Assert.Contains("\"status\":\"Failed\"", failedVerifyPayload);
        Assert.DoesNotContain(_factory.RootDirectory, failedVerifyPayload, StringComparison.OrdinalIgnoreCase);

        var retention = await client.GetFromJsonAsync<BackupRetentionSettingsDto>("/api/local-database/backup-retention");
        Assert.NotNull(retention);

        var saved = await client.PutAsJsonAsync("/api/local-database/backup-retention", new BackupRetentionSettingsDto(
            true,
            ManualCount: 0,
            ScheduledCount: 800,
            BeforeMigrationCount: 2,
            BeforeRestoreCount: 3,
            BeforeApplicationUpdateCount: 4,
            MinimumAgeHoursBeforeDeletion: 9000));
        saved.EnsureSuccessStatusCode();
        var savedSettings = await saved.Content.ReadFromJsonAsync<BackupRetentionSettingsDto>();
        Assert.Equal(1, savedSettings!.ManualCount);
        Assert.Equal(365, savedSettings.ScheduledCount);
        Assert.Equal(8760, savedSettings.MinimumAgeHoursBeforeDeletion);
    }

    [Fact]
    public async Task ManualBackup_RejectsClientPathsAndConcurrentOperations()
    {
        await _factory.ResetDatabaseAsync();
        var client = _factory.CreateClient();

        var pathAttempt = await client.PostAsJsonAsync("/api/local-database/backups", new { backupPath = "/tmp/outside.db" });
        Assert.Equal(HttpStatusCode.BadRequest, pathAttempt.StatusCode);
        Assert.Contains("filesystem paths", await pathAttempt.Content.ReadAsStringAsync());

        var coordinator = _factory.Services.GetRequiredService<ILocalDatabaseOperationCoordinator>();
        await using var lease = await coordinator.TryAcquireExclusiveAsync(LocalDatabaseOperationKind.Restore, "activebackup", CancellationToken.None);
        var conflict = await client.PostAsync("/api/local-database/backups", null);
        Assert.Equal(HttpStatusCode.BadRequest, conflict.StatusCode);
        Assert.Contains("already in progress", await conflict.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task RestorePreflightAndConfirmation_AreBoundExpiringAndSingleUse()
    {
        await _factory.ResetDatabaseAsync();
        var client = _factory.CreateClient();
        var backup = await CreateBackupAsync(client);
        var secondBackup = await CreateBackupAsync(client);

        var preflight = await RunPreflightAsync(client, backup.BackupId);
        Assert.Equal("Valid", preflight.GetProperty("status").GetString());
        var operationId = preflight.GetProperty("operationId").GetString();
        Assert.False(string.IsNullOrWhiteSpace(operationId));
        Assert.True(preflight.TryGetProperty("operationExpiresAtUtc", out var expiresAt));
        Assert.False(string.IsNullOrWhiteSpace(expiresAt.GetString()));

        var missingToken = await client.PostAsJsonAsync(
            "/api/local-database/restore",
            new { backupId = backup.BackupId, confirmation = "RESTORE_LOCAL_DATABASE" });
        Assert.Equal(HttpStatusCode.OK, missingToken.StatusCode);
        Assert.Contains("\"status\":\"Rejected\"", await missingToken.Content.ReadAsStringAsync());

        var wrongBackup = await client.PostAsJsonAsync(
            "/api/local-database/restore",
            new { backupId = secondBackup.BackupId, operationId, confirmation = "RESTORE_LOCAL_DATABASE" });
        Assert.Contains("does not match", await wrongBackup.Content.ReadAsStringAsync());

        await _factory.SwitchToSecondOwnerAsync();
        var wrongUser = await client.PostAsJsonAsync(
            "/api/local-database/restore",
            new { backupId = backup.BackupId, operationId, confirmation = "RESTORE_LOCAL_DATABASE" });
        Assert.Contains("current user", await wrongUser.Content.ReadAsStringAsync());

        await _factory.SwitchToPrimaryOwnerAsync();
        var success = await client.PostAsJsonAsync(
            "/api/local-database/restore",
            new { backupId = backup.BackupId, operationId, confirmation = "RESTORE_LOCAL_DATABASE" });
        success.EnsureSuccessStatusCode();
        Assert.Contains("\"status\":\"Completed\"", await success.Content.ReadAsStringAsync());

        var replay = await client.PostAsJsonAsync(
            "/api/local-database/restore",
            new { backupId = backup.BackupId, operationId, confirmation = "RESTORE_LOCAL_DATABASE" });
        Assert.Contains("not found", await replay.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task RestoreConfirmation_RejectsExpiredOperationToken()
    {
        await using var expiringFactory = new ApiFactory("Testing", restorePreflightLifetimeSeconds: 1);
        await expiringFactory.InitializeAsync();
        var client = expiringFactory.CreateClient();
        var backup = await CreateBackupAsync(client);
        var preflight = await RunPreflightAsync(client, backup.BackupId);
        var operationId = preflight.GetProperty("operationId").GetString();

        await Task.Delay(TimeSpan.FromMilliseconds(1200));

        var expired = await client.PostAsJsonAsync(
            "/api/local-database/restore",
            new { backupId = backup.BackupId, operationId, confirmation = "RESTORE_LOCAL_DATABASE" });
        Assert.Contains("expired", await expired.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task RestorePreflight_RejectsCorruptBackupSafely()
    {
        await _factory.ResetDatabaseAsync();
        var client = _factory.CreateClient();
        var backup = await CreateBackupAsync(client);

        await TamperEncryptedBackupAsync(_factory.BackupDirectory, backup.BackupFileName!);

        var preflight = await RunPreflightAsync(client, backup.BackupId);
        Assert.Equal("ChecksumMismatch", preflight.GetProperty("status").GetString());
        Assert.False(preflight.TryGetProperty("operationId", out var operationId) && !string.IsNullOrWhiteSpace(operationId.GetString()));
        Assert.DoesNotContain(_factory.RootDirectory, preflight.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    private static async Task TamperEncryptedBackupAsync(string backupDirectory, string backupFileName)
    {
        var encryptedPath = Directory
            .EnumerateFiles(backupDirectory, "*.db.enc")
            .Single(path => path.Contains(backupFileName, StringComparison.Ordinal));
        await using (var stream = File.Open(encryptedPath, FileMode.Open, FileAccess.ReadWrite))
        {
            stream.Position = Math.Max(0, stream.Length - 1);
            var current = stream.ReadByte();
            stream.Position = Math.Max(0, stream.Length - 1);
            stream.WriteByte((byte)(current ^ 0xFF));
        }
    }

    private static async Task<JsonElement> RunPreflightAsync(HttpClient client, string backupId)
    {
        var response = await client.PostAsJsonAsync("/api/local-database/restore/validate", new { backupId });
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.Clone();
    }

    private static async Task<BackupApiResult> CreateBackupAsync(HttpClient client)
    {
        var response = await client.PostAsync("/api/local-database/backups", null);
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;
        Assert.Equal("Succeeded", root.GetProperty("status").GetString());
        return new BackupApiResult(
            root.GetProperty("backupId").GetString()!,
            root.TryGetProperty("backupFileName", out var backupFileName) ? backupFileName.GetString() : null);
    }

    public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
    {
        private readonly string _environment;
        private readonly int? _restorePreflightLifetimeSeconds;
        private readonly bool _enableEndpointCapability;
        private readonly string _databasePath;
        private TestIdentity? _primaryOwner;
        private TestIdentity? _secondOwner;

        public ApiFactory()
            : this("Testing")
        {
        }

        internal ApiFactory(
            string environment,
            int? restorePreflightLifetimeSeconds = null,
            bool enableEndpointCapability = true)
        {
            _environment = environment;
            _restorePreflightLifetimeSeconds = restorePreflightLifetimeSeconds;
            _enableEndpointCapability = enableEndpointCapability;
            RootDirectory = Path.Combine(Path.GetTempPath(), $"highcool-localdb-api-tests-{Guid.NewGuid():N}");
            DataDirectory = Path.Combine(RootDirectory, "Data");
            BackupDirectory = Path.Combine(RootDirectory, "Backups");
            PendingDirectory = Path.Combine(RootDirectory, "Pending");
            LogDirectory = Path.Combine(RootDirectory, "Logs");
            _databasePath = Path.Combine(DataDirectory, "highcool.db");
        }

        public string RootDirectory { get; }

        public string DataDirectory { get; }

        public string BackupDirectory { get; }

        public string PendingDirectory { get; }

        public string LogDirectory { get; }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment(_environment);
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Database:Provider"] = "Sqlite",
                    ["Database:SqliteFileName"] = "highcool.db",
                    ["LocalStorage:DataDirectory"] = DataDirectory,
                    ["LocalStorage:BackupDirectory"] = BackupDirectory,
                    ["LocalStorage:PendingBackupDirectory"] = PendingDirectory,
                    ["LocalStorage:LogDirectory"] = LogDirectory,
                    ["Authentication:JwtSecret"] = "local-database-api-tests-signing-key-with-enough-length",
                    ["Authentication:Issuer"] = "HighCool.Tests",
                    ["Authentication:Audience"] = "HighCool.Tests",
                    ["RestorePreflightOperation:LifetimeSeconds"] = _restorePreflightLifetimeSeconds?.ToString(),
                    ["LocalDatabase:EnableEndpointCapability"] = _enableEndpointCapability.ToString()
                });
            });
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<AppDbContext>>();
                services.RemoveAll<AppDbContext>();
                services.AddDbContext<AppDbContext>(options => options.UseSqlite($"Data Source={_databasePath}"));
                AuthenticatedApiTestSupport.ConfigureServices(services);
            });
        }

        public async Task InitializeAsync()
        {
            await ResetDatabaseAsync();
        }

        public new async Task DisposeAsync()
        {
            DeleteDirectoryIfExists(RootDirectory);
            await base.DisposeAsync();
        }

        public async Task ResetDatabaseAsync()
        {
            Directory.CreateDirectory(DataDirectory);
            Directory.CreateDirectory(BackupDirectory);
            Directory.CreateDirectory(PendingDirectory);
            Directory.CreateDirectory(LogDirectory);

            await using var scope = Services.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await dbContext.Database.EnsureDeletedAsync();
            await dbContext.Database.EnsureCreatedAsync();
            await AuthenticatedApiTestSupport.SeedAuthenticatedContextAsync(scope.ServiceProvider, dbContext);
            _primaryOwner = CaptureCurrentIdentity(scope.ServiceProvider);
            _secondOwner = await CreateSecondOwnerAsync(scope.ServiceProvider, dbContext);
            await AddSentinelAsync(dbContext);
            SwitchToPrimaryOwner();
        }

        public void ClearAuthenticatedContext()
        {
            var context = Services.GetRequiredService<TestRequestExecutionContext>();
            context.UserId = null;
            context.OrganizationId = null;
            context.MembershipId = null;
            context.SessionId = null;
        }

        public async Task MakeCurrentUserNonOwnerWithoutRolesAsync()
        {
            await using var scope = Services.CreateAsyncScope();
            var context = scope.ServiceProvider.GetRequiredService<TestRequestExecutionContext>();
            var membership = await scope.ServiceProvider.GetRequiredService<AppDbContext>()
                .OrganizationMemberships
                .IgnoreQueryFilters()
                .SingleAsync(entity => entity.Id == context.MembershipId);
            membership.IsOwner = false;
            await scope.ServiceProvider.GetRequiredService<AppDbContext>().SaveChangesAsync();
        }

        public Task SwitchToSecondOwnerAsync()
        {
            ApplyIdentity(_secondOwner!);
            return Task.CompletedTask;
        }

        public Task SwitchToPrimaryOwnerAsync()
        {
            SwitchToPrimaryOwner();
            return Task.CompletedTask;
        }

        private void SwitchToPrimaryOwner()
            => ApplyIdentity(_primaryOwner!);

        private void ApplyIdentity(TestIdentity identity)
        {
            var context = Services.GetRequiredService<TestRequestExecutionContext>();
            context.UserId = identity.UserId;
            context.OrganizationId = identity.OrganizationId;
            context.MembershipId = identity.MembershipId;
            context.SessionId = identity.SessionId;
        }

        private static TestIdentity CaptureCurrentIdentity(IServiceProvider services)
        {
            var context = services.GetRequiredService<TestRequestExecutionContext>();
            return new TestIdentity(context.UserId!.Value, context.OrganizationId!.Value, context.MembershipId!.Value, context.SessionId!.Value);
        }

        private static async Task<TestIdentity> CreateSecondOwnerAsync(IServiceProvider services, AppDbContext dbContext)
        {
            var primary = CaptureCurrentIdentity(services);
            var user = new UserAccount
            {
                FullName = "Second Owner",
                Email = "second-owner@highcool.test",
                PasswordHash = "test",
                EmailVerified = true,
                Status = UserAccountStatus.Active,
                CreatedBy = "seed"
            };
            dbContext.UserAccounts.Add(user);
            await dbContext.SaveChangesAsync();

            var profile = new UserProfile
            {
                OrganizationId = primary.OrganizationId,
                LanguagePreference = "en",
                CreatedBy = "seed"
            };
            dbContext.UserProfiles.Add(profile);
            await dbContext.SaveChangesAsync();

            var membership = new OrganizationMembership
            {
                OrganizationId = primary.OrganizationId,
                UserId = user.Id,
                ProfileId = profile.Id,
                Status = MembershipStatus.Active,
                IsOwner = true,
                BranchAccessMode = AccessScopeMode.All,
                WarehouseAccessMode = AccessScopeMode.All,
                CreatedBy = "seed"
            };
            dbContext.OrganizationMemberships.Add(membership);
            await dbContext.SaveChangesAsync();

            var session = new UserSession
            {
                UserId = user.Id,
                OrganizationId = primary.OrganizationId,
                MembershipId = membership.Id,
                SessionTokenHash = "second-session",
                DeviceName = "API Tests",
                ExpiresAt = DateTime.UtcNow.AddHours(8),
                IsActive = true,
                CreatedBy = "seed"
            };
            dbContext.UserSessions.Add(session);
            await dbContext.SaveChangesAsync();

            return new TestIdentity(user.Id, primary.OrganizationId, membership.Id, session.Id);
        }

        private static async Task AddSentinelAsync(AppDbContext dbContext)
        {
            dbContext.Uoms.Add(new Uom
            {
                Code = $"LDB{Guid.NewGuid():N}"[..10],
                Name = "Local DB API Sentinel",
                Precision = 0,
                AllowsFraction = false,
                IsActive = true,
                CreatedBy = "seed"
            });
            await dbContext.SaveChangesAsync();
        }

        private static void DeleteDirectoryIfExists(string path)
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
    }

    private sealed record TestIdentity(Guid UserId, Guid OrganizationId, Guid MembershipId, Guid SessionId);

    private sealed record BackupApiResult(string BackupId, string? BackupFileName);
}
