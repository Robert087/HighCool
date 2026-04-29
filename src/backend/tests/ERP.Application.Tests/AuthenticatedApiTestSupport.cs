using System.Security.Claims;
using System.Text.Encodings.Web;
using ERP.Application.Security;
using ERP.Domain.Identity;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ERP.Application.Tests;

internal static class AuthenticatedApiTestSupport
{
    public const string AuthenticationScheme = "Test";

    public static void ConfigureServices(IServiceCollection services)
    {
        services.RemoveAll<IRequestExecutionContext>();
        services.AddSingleton<TestRequestExecutionContext>();
        services.AddScoped<IRequestExecutionContext>(provider => provider.GetRequiredService<TestRequestExecutionContext>());

        services
            .AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = AuthenticationScheme;
                options.DefaultChallengeScheme = AuthenticationScheme;
                options.DefaultScheme = AuthenticationScheme;
            })
            .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(AuthenticationScheme, _ => { });
    }

    public static async Task SeedAuthenticatedContextAsync(IServiceProvider services, AppDbContext dbContext)
    {
        var organization = new Organization
        {
            Name = "Test Organization",
            DefaultCurrency = "EGP",
            Timezone = "Africa/Cairo",
            DefaultLanguage = "en",
            PurchaseOrderPrefix = "PO",
            PurchaseReceiptPrefix = "PR",
            PurchaseReturnPrefix = "RTN",
            PaymentPrefix = "PAY",
            SetupCompleted = true,
            SetupVersion = "v1",
            EnableProcurement = true,
            EnablePurchaseOrders = true,
            EnablePurchaseReceipts = true,
            EnableInventory = true,
            EnableWarehouses = true,
            EnableMultipleWarehouses = true,
            EnableSupplierManagement = true,
            EnableSupplierFinancials = true,
            EnableShortageManagement = true,
            EnableComponentsBom = true,
            EnableUom = true,
            EnableUomConversion = true,
            RequirePoBeforeReceipt = false,
            AllowDirectPurchaseReceipt = true,
            AllowPartialReceipt = true,
            AllowOverReceipt = false,
            OverReceiptTolerancePercent = 0,
            EnablePostingWorkflow = true,
            LockPostedDocuments = true,
            RequireApprovalBeforePosting = false,
            EnableReversals = true,
            RequireReasonForCancelOrReversal = true,
            AllowNegativeStock = false,
            EnableBatchTracking = false,
            EnableSerialTracking = false,
            EnableExpiryTracking = false,
            EnableStockTransfers = true,
            EnableStockAdjustments = true,
            CreatedBy = "seed"
        };

        var user = new UserAccount
        {
            FullName = "API Test Owner",
            Email = "api-owner@highcool.test",
            PasswordHash = "test-password-hash",
            EmailVerified = true,
            Status = UserAccountStatus.Active,
            CreatedBy = "seed"
        };

        dbContext.Organizations.Add(organization);
        dbContext.UserAccounts.Add(user);
        await dbContext.SaveChangesAsync();

        var profile = new UserProfile
        {
            OrganizationId = organization.Id,
            LanguagePreference = "en",
            CreatedBy = "seed"
        };

        dbContext.UserProfiles.Add(profile);
        await dbContext.SaveChangesAsync();

        var membership = new OrganizationMembership
        {
            OrganizationId = organization.Id,
            UserId = user.Id,
            ProfileId = profile.Id,
            Status = MembershipStatus.Active,
            IsOwner = true,
            BranchAccessMode = AccessScopeMode.All,
            WarehouseAccessMode = AccessScopeMode.All,
            CreatedBy = "seed"
        };

        dbContext.OrganizationSecuritySettings.Add(new OrganizationSecuritySettings
        {
            OrganizationId = organization.Id,
            CreatedBy = "seed"
        });
        dbContext.OrganizationMemberships.Add(membership);
        await dbContext.SaveChangesAsync();

        var session = new UserSession
        {
            UserId = user.Id,
            OrganizationId = organization.Id,
            MembershipId = membership.Id,
            SessionTokenHash = "test-session-token",
            DeviceName = "API Tests",
            ExpiresAt = DateTime.UtcNow.AddHours(8),
            IsActive = true,
            CreatedBy = "seed"
        };

        dbContext.UserSessions.Add(session);
        await dbContext.SaveChangesAsync();

        var executionContext = services.GetRequiredService<TestRequestExecutionContext>();
        executionContext.UserId = user.Id;
        executionContext.OrganizationId = organization.Id;
        executionContext.MembershipId = membership.Id;
        executionContext.SessionId = session.Id;
    }
}

internal sealed class TestAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var executionContext = Context.RequestServices.GetRequiredService<TestRequestExecutionContext>();

        if (!executionContext.UserId.HasValue ||
            !executionContext.OrganizationId.HasValue ||
            !executionContext.MembershipId.HasValue ||
            !executionContext.SessionId.HasValue)
        {
            return Task.FromResult(AuthenticateResult.Fail("Authenticated API test context was not seeded."));
        }

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, executionContext.UserId.Value.ToString()),
            new Claim(ClaimTypes.Email, executionContext.Email ?? "api-owner@highcool.test"),
            new Claim(CustomClaimTypes.OrganizationId, executionContext.OrganizationId.Value.ToString()),
            new Claim(CustomClaimTypes.MembershipId, executionContext.MembershipId.Value.ToString()),
            new Claim(CustomClaimTypes.SessionId, executionContext.SessionId.Value.ToString())
        };

        var identity = new ClaimsIdentity(claims, AuthenticatedApiTestSupport.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, AuthenticatedApiTestSupport.AuthenticationScheme);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
