using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.IdentityModel.Tokens.Jwt;
using System.Text;
using ERP.Application.Security;
using ERP.Domain.Identity;
using ERP.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace ERP.Application.Tests;

public sealed class IdentityApiTests : IClassFixture<IdentityApiTests.ApiFactory>
{
    private readonly ApiFactory _factory;

    public IdentityApiTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task SignupEndpoint_ShouldReturnWorkspaceAndToken()
    {
        await _factory.ResetDatabaseAsync();
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/signup", new
        {
            fullName = "Owner Two",
            email = "owner2@highcool.test",
            password = "StrongPass!123",
            organizationName = "West Org"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<AuthApiResponse>();
        Assert.NotNull(payload);
        Assert.False(string.IsNullOrWhiteSpace(payload!.AccessToken));
        Assert.Equal("West Org", payload.Workspace.OrganizationName);
        Assert.Equal("owner2@highcool.test", payload.Workspace.Email);
    }

    [Fact]
    public async Task Signup_Login_And_Logout_ShouldWork()
    {
        await _factory.ResetDatabaseAsync();
        var client = _factory.CreateClient();

        var signupResponse = await client.PostAsJsonAsync("/api/auth/signup", new
        {
            fullName = "Owner One",
            email = "owner1@highcool.test",
            password = "StrongPass!123",
            organizationName = "North Org"
        });

        Assert.Equal(HttpStatusCode.OK, signupResponse.StatusCode);
        var signup = await signupResponse.Content.ReadFromJsonAsync<AuthApiResponse>();
        Assert.NotNull(signup);
        Assert.False(string.IsNullOrWhiteSpace(signup!.AccessToken));
        var token = new JwtSecurityTokenHandler().ReadJwtToken(signup.AccessToken);
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var sessionId = Guid.Parse(token.Claims.Single(claim => claim.Type == "session_id").Value);
            Assert.True(await dbContext.UserSessions.AnyAsync(entity => entity.Id == sessionId));
        }
        _ = new JwtSecurityTokenHandler().ValidateToken(
            signup.AccessToken,
            new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateIssuerSigningKey = true,
                ValidateLifetime = true,
                ValidIssuer = "HighCool.Tests",
                ValidAudience = "HighCool.Tests.Client",
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("test-secret-that-is-long-enough-for-jwt-signing"))
            },
            out _);
        Assert.True(signup.Workspace.EmailVerified);
        Assert.Equal("North Org", signup.Workspace.OrganizationName);
        Assert.Contains(signup.Workspace.Roles, role => role.Name == "Owner");

        var meBeforeLogoutResponse = await WithAuth(client, signup.AccessToken).GetAsync("/api/auth/me");
        if (meBeforeLogoutResponse.StatusCode != HttpStatusCode.OK)
        {
            throw new InvalidOperationException(
                $"Unexpected auth status: {(int)meBeforeLogoutResponse.StatusCode} {meBeforeLogoutResponse.StatusCode}; " +
                $"body='{await meBeforeLogoutResponse.Content.ReadAsStringAsync()}'; " +
                $"auth='{string.Join(" | ", meBeforeLogoutResponse.Headers.WwwAuthenticate)}'");
        }

        var logoutResponse = await WithAuth(client, signup.AccessToken).PostAsJsonAsync("/api/auth/logout", new
        {
            allDevices = false
        });
        Assert.True(logoutResponse.StatusCode == HttpStatusCode.NoContent, await logoutResponse.Content.ReadAsStringAsync());

        var meAfterLogoutResponse = await WithAuth(client, signup.AccessToken).GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, meAfterLogoutResponse.StatusCode);

        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email = "owner1@highcool.test",
            password = "StrongPass!123",
            rememberMe = false,
            deviceName = "Test Browser"
        });

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        var login = await loginResponse.Content.ReadFromJsonAsync<AuthApiResponse>();
        Assert.NotNull(login);
        Assert.True(login!.Workspace.EmailVerified);
        Assert.Equal("North Org", login.Workspace.OrganizationName);
    }

    [Fact]
    public async Task OrganizationIsolation_ShouldHideOtherOrganizationSupplierData()
    {
        await _factory.ResetDatabaseAsync();
        var client = _factory.CreateClient();

        var north = await SignupAsync(client, "north-owner@highcool.test", "North Org");
        var south = await SignupAsync(client, "south-owner@highcool.test", "South Org");

        await CompleteSetupAsync(client, north.AccessToken);
        await CompleteSetupAsync(client, south.AccessToken);

        var northCreateResponse = await WithAuth(client, north.AccessToken).PostAsJsonAsync("/api/suppliers", new
        {
            code = "SUP-NORTH",
            name = "North Supplier",
            statementName = "North Supplier",
            creditLimit = 0,
            isActive = true
        });
        Assert.True(northCreateResponse.StatusCode == HttpStatusCode.Created, await northCreateResponse.Content.ReadAsStringAsync());

        var southCreateResponse = await WithAuth(client, south.AccessToken).PostAsJsonAsync("/api/suppliers", new
        {
            code = "SUP-SOUTH",
            name = "South Supplier",
            statementName = "South Supplier",
            creditLimit = 0,
            isActive = true
        });
        Assert.True(southCreateResponse.StatusCode == HttpStatusCode.Created, await southCreateResponse.Content.ReadAsStringAsync());

        var southSupplier = await southCreateResponse.Content.ReadFromJsonAsync<SupplierResponse>();
        Assert.NotNull(southSupplier);

        var forbiddenReadResponse = await WithAuth(client, north.AccessToken).GetAsync($"/api/suppliers/{southSupplier!.Id}");
        Assert.Equal(HttpStatusCode.NotFound, forbiddenReadResponse.StatusCode);
    }

    [Fact]
    public async Task Login_ShouldBlockUnverifiedUser()
    {
        await _factory.ResetDatabaseAsync();
        var client = _factory.CreateClient();
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<UserAccount>>();

            var organization = new Organization
            {
                Name = "Pending Org",
                DefaultCurrency = "EGP",
                Timezone = "Africa/Cairo",
                DefaultLanguage = "en",
                PurchaseOrderPrefix = "PO",
                PurchaseReceiptPrefix = "PR",
                PurchaseReturnPrefix = "RTN",
                PaymentPrefix = "PAY",
                CreatedBy = "pending-owner@highcool.test"
            };

            var user = new UserAccount
            {
                FullName = "pending-owner",
                Email = "pending-owner@highcool.test",
                EmailVerified = false,
                Status = UserAccountStatus.Active,
                CreatedBy = "pending-owner@highcool.test"
            };
            user.PasswordHash = passwordHasher.HashPassword(user, "StrongPass!123");

            dbContext.Organizations.Add(organization);
            dbContext.UserAccounts.Add(user);
            await dbContext.SaveChangesAsync();

            var profile = new UserProfile
            {
                OrganizationId = organization.Id,
                LanguagePreference = "en",
                CreatedBy = user.Email
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
                CreatedBy = user.Email
            };

            dbContext.OrganizationSecuritySettings.Add(new OrganizationSecuritySettings
            {
                OrganizationId = organization.Id,
                CreatedBy = user.Email
            });
            dbContext.OrganizationMemberships.Add(membership);
            await dbContext.SaveChangesAsync();
        }

        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email = "pending-owner@highcool.test",
            password = "StrongPass!123",
            rememberMe = false
        });

        Assert.Equal(HttpStatusCode.Unauthorized, loginResponse.StatusCode);
    }

    [Fact]
    public async Task Owner_ShouldAccessWorkspaceSettingsAndAuditEndpoints()
    {
        await _factory.ResetDatabaseAsync();
        var client = _factory.CreateClient();

        var signup = await SignupAsync(client, "settings-owner@highcool.test", "Settings Org");

        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email = "settings-owner@highcool.test",
            password = "StrongPass!123",
            rememberMe = false,
            deviceName = "Settings Browser"
        });

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        var login = await loginResponse.Content.ReadFromJsonAsync<AuthApiResponse>();
        Assert.NotNull(login);

        var organizationResponse = await WithAuth(client, login!.AccessToken).GetAsync("/api/settings/organization");
        Assert.Equal(HttpStatusCode.OK, organizationResponse.StatusCode);

        var securityResponse = await WithAuth(client, login.AccessToken).GetAsync("/api/settings/security");
        Assert.Equal(HttpStatusCode.OK, securityResponse.StatusCode);

        var usersResponse = await WithAuth(client, login.AccessToken).GetAsync("/api/settings/users");
        Assert.Equal(HttpStatusCode.OK, usersResponse.StatusCode);
        var usersPayload = await usersResponse.Content.ReadAsStringAsync();
        Assert.Contains("settings-owner@highcool.test", usersPayload);
        Assert.Contains("\"isOwner\":true", usersPayload);

        var rolesResponse = await WithAuth(client, login.AccessToken).GetAsync("/api/settings/roles");
        Assert.Equal(HttpStatusCode.OK, rolesResponse.StatusCode);
        var rolesPayload = await rolesResponse.Content.ReadAsStringAsync();
        Assert.Contains("\"templateKey\":\"owner\"", rolesPayload);

        var featuresResponse = await WithAuth(client, login.AccessToken).GetAsync("/api/settings/features");
        Assert.Equal(HttpStatusCode.OK, featuresResponse.StatusCode);

        var auditResponse = await WithAuth(client, login.AccessToken).GetAsync("/api/settings/audit-log?page=1&pageSize=20");
        Assert.Equal(HttpStatusCode.OK, auditResponse.StatusCode);
        var auditPayload = await auditResponse.Content.ReadAsStringAsync();
        Assert.Contains("\"action\":\"signup\"", auditPayload);
        Assert.Contains("\"action\":\"login\"", auditPayload);
    }

    [Fact]
    public async Task UsersAccessManagement_ShouldAuditAndProtectLastActiveOwner()
    {
        await _factory.ResetDatabaseAsync();
        var client = _factory.CreateClient();
        var owner = await SignupAsync(client, "access-owner@highcool.test", "Access Org");

        var member = await CreateMemberAsync(_factory, owner.Workspace.OrganizationId, "buyer@highcool.test", "viewer");
        var purchaserRole = await GetRoleAsync(_factory, owner.Workspace.OrganizationId, "purchaser");
        var viewerRole = await GetRoleAsync(_factory, owner.Workspace.OrganizationId, "viewer");
        var ownerMembershipId = await GetMembershipIdAsync(_factory, owner.Workspace.OrganizationId, owner.Workspace.Email);

        var usersResponse = await WithAuth(client, owner.AccessToken).GetAsync("/api/settings/users?page=1&pageSize=20");
        Assert.Equal(HttpStatusCode.OK, usersResponse.StatusCode);
        var usersPayload = await usersResponse.Content.ReadAsStringAsync();
        Assert.Contains("access-owner@highcool.test", usersPayload);
        Assert.Contains("\"isOwner\":true", usersPayload);

        var invitationResponse = await WithAuth(client, owner.AccessToken).PostAsJsonAsync("/api/settings/invitations", new
        {
            email = "invited@highcool.test",
            fullName = "Invited User",
            roleIds = new[] { viewerRole.Id },
            profileId = (Guid?)null,
            branchAccessMode = "All",
            warehouseAccessMode = "All",
            branchCodes = Array.Empty<string>(),
            warehouseIds = Array.Empty<Guid>()
        });
        Assert.Equal(HttpStatusCode.OK, invitationResponse.StatusCode);

        var changeRoleResponse = await WithAuth(client, owner.AccessToken).PutAsJsonAsync($"/api/settings/users/{member.MembershipId}/roles", new
        {
            roleIds = new[] { purchaserRole.Id }
        });
        Assert.Equal(HttpStatusCode.OK, changeRoleResponse.StatusCode);

        var suspendResponse = await WithAuth(client, owner.AccessToken).PostAsJsonAsync($"/api/settings/users/{member.MembershipId}/suspend", new { });
        Assert.Equal(HttpStatusCode.OK, suspendResponse.StatusCode);

        var blockedLoginResponse = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email = "buyer@highcool.test",
            password = "StrongPass!123",
            rememberMe = false,
            deviceName = "Suspended Browser"
        });
        Assert.Equal(HttpStatusCode.Unauthorized, blockedLoginResponse.StatusCode);

        var activateResponse = await WithAuth(client, owner.AccessToken).PostAsJsonAsync($"/api/settings/users/{member.MembershipId}/activate", new { });
        Assert.Equal(HttpStatusCode.OK, activateResponse.StatusCode);

        var allowedLoginResponse = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email = "buyer@highcool.test",
            password = "StrongPass!123",
            rememberMe = false,
            deviceName = "Active Browser"
        });
        Assert.Equal(HttpStatusCode.OK, allowedLoginResponse.StatusCode);

        var suspendOwnerResponse = await WithAuth(client, owner.AccessToken).PostAsJsonAsync($"/api/settings/users/{ownerMembershipId}/suspend", new { });
        Assert.Equal(HttpStatusCode.BadRequest, suspendOwnerResponse.StatusCode);
        Assert.Contains("At least one Owner must remain active.", await suspendOwnerResponse.Content.ReadAsStringAsync());

        var downgradeOwnerResponse = await WithAuth(client, owner.AccessToken).PutAsJsonAsync($"/api/settings/users/{ownerMembershipId}/roles", new
        {
            roleIds = new[] { viewerRole.Id }
        });
        Assert.Equal(HttpStatusCode.BadRequest, downgradeOwnerResponse.StatusCode);
        Assert.Contains("At least one Owner must remain active.", await downgradeOwnerResponse.Content.ReadAsStringAsync());

        var auditResponse = await WithAuth(client, owner.AccessToken).GetAsync("/api/settings/audit-log?page=1&pageSize=100");
        Assert.Equal(HttpStatusCode.OK, auditResponse.StatusCode);
        var auditPayload = await auditResponse.Content.ReadAsStringAsync();
        Assert.Contains("user_invited", auditPayload);
        Assert.Contains("user_role_changed", auditPayload);
        Assert.Contains("user_suspended", auditPayload);
        Assert.Contains("user_activated", auditPayload);
    }

    [Fact]
    public async Task DefaultRoles_ShouldMapRequiredPermissions_AndBlockUnauthorizedSettingsApis()
    {
        await _factory.ResetDatabaseAsync();
        var client = _factory.CreateClient();
        var owner = await SignupAsync(client, "roles-owner@highcool.test", "Roles Org");

        await CreateMemberAsync(_factory, owner.Workspace.OrganizationId, "accountant@highcool.test", "accountant");
        await CreateMemberAsync(_factory, owner.Workspace.OrganizationId, "viewer@highcool.test", "viewer");
        await CreateMemberAsync(_factory, owner.Workspace.OrganizationId, "purchaser@highcool.test", "purchaser");

        var rolesResponse = await WithAuth(client, owner.AccessToken).GetAsync("/api/settings/roles");
        Assert.Equal(HttpStatusCode.OK, rolesResponse.StatusCode);
        var roles = await rolesResponse.Content.ReadFromJsonAsync<List<SettingsRoleApiResponse>>();
        Assert.NotNull(roles);

        var ownerRole = roles!.Single(role => role.TemplateKey == "owner");
        Assert.Contains(Permissions.SettingsUsersManage, ownerRole.Permissions);
        Assert.Contains(Permissions.SupplierFinancialsPaymentsPost, ownerRole.Permissions);

        var viewerRole = roles.Single(role => role.TemplateKey == "viewer");
        Assert.Contains(Permissions.ProcurementPurchaseOrderView, viewerRole.Permissions);
        Assert.DoesNotContain(Permissions.ProcurementPurchaseOrderCreate, viewerRole.Permissions);
        Assert.DoesNotContain(Permissions.SupplierFinancialsPaymentsPost, viewerRole.Permissions);

        var purchaserRole = roles.Single(role => role.TemplateKey == "purchaser");
        Assert.Contains(Permissions.ProcurementPurchaseOrderCreate, purchaserRole.Permissions);
        Assert.Contains(Permissions.ProcurementPurchaseReceiptCreate, purchaserRole.Permissions);
        Assert.DoesNotContain(Permissions.SupplierFinancialsPaymentsCreate, purchaserRole.Permissions);

        var accountantRole = roles.Single(role => role.TemplateKey == "accountant");
        Assert.Contains(Permissions.SuppliersView, accountantRole.Permissions);
        Assert.Contains(Permissions.SupplierFinancialsPayablesView, accountantRole.Permissions);
        Assert.Contains(Permissions.SupplierFinancialsPaymentsCreate, accountantRole.Permissions);
        Assert.Contains(Permissions.SupplierFinancialsPaymentsPost, accountantRole.Permissions);
        Assert.DoesNotContain(Permissions.SettingsUsersManage, accountantRole.Permissions);

        var matrixResponse = await WithAuth(client, owner.AccessToken).GetAsync("/api/settings/permissions/matrix");
        Assert.Equal(HttpStatusCode.OK, matrixResponse.StatusCode);
        var matrixPayload = await matrixResponse.Content.ReadAsStringAsync();
        Assert.Contains(Permissions.ProcurementPurchaseOrderView, matrixPayload);
        Assert.Contains(Permissions.SettingsUsersManage, matrixPayload);

        var accountantLogin = await LoginAsync(client, "accountant@highcool.test");
        var accountantSettingsResponse = await WithAuth(client, accountantLogin.AccessToken).GetAsync("/api/settings/users");
        Assert.Equal(HttpStatusCode.Forbidden, accountantSettingsResponse.StatusCode);

        var viewerLogin = await LoginAsync(client, "viewer@highcool.test");
        var viewerCreatePoResponse = await WithAuth(client, viewerLogin.AccessToken).PostAsJsonAsync("/api/purchase-orders", new { });
        Assert.Equal(HttpStatusCode.Forbidden, viewerCreatePoResponse.StatusCode);

        var purchaserLogin = await LoginAsync(client, "purchaser@highcool.test");
        var purchaserPaymentsResponse = await WithAuth(client, purchaserLogin.AccessToken).GetAsync("/api/payments");
        Assert.Equal(HttpStatusCode.Forbidden, purchaserPaymentsResponse.StatusCode);
    }

    [Fact]
    public async Task Signup_ShouldRequireOrganizationSetup_BeforeWorkspaceAccess_ThenAllowCompletion()
    {
        await _factory.ResetDatabaseAsync();
        var client = _factory.CreateClient();

        var signup = await SignupAsync(client, "setup-owner@highcool.test", "Setup Org");

        var meResponse = await WithAuth(client, signup.AccessToken).GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.OK, meResponse.StatusCode);
        var mePayload = await meResponse.Content.ReadAsStringAsync();
        Assert.Contains("\"setupCompleted\":false", mePayload);

        var statusResponse = await WithAuth(client, signup.AccessToken).GetAsync("/api/settings/organization/setup-status");
        Assert.Equal(HttpStatusCode.OK, statusResponse.StatusCode);
        var statusPayload = await statusResponse.Content.ReadAsStringAsync();
        Assert.Contains("\"setupCompleted\":false", statusPayload);

        var saveResponse = await WithAuth(client, signup.AccessToken).PutAsJsonAsync("/api/settings/organization/setup", new
        {
            name = "Setup Org",
            logo = (string?)null,
            address = "Cairo",
            phone = "01000000000",
            taxId = "123456",
            commercialRegistry = "987654",
            defaultCurrency = "EGP",
            timezone = "Africa/Cairo",
            defaultLanguage = "en",
            rtlEnabled = false,
            fiscalYearStartMonth = 1,
            purchaseOrderPrefix = "PO",
            purchaseReceiptPrefix = "PR",
            purchaseReturnPrefix = "RTN",
            paymentPrefix = "PAY",
            defaultWarehouseId = (Guid?)null,
            autoPostDrafts = false,
            enableProcurement = true,
            enablePurchaseOrders = true,
            enablePurchaseReceipts = true,
            enableInventory = false,
            enableWarehouses = false,
            enableMultipleWarehouses = false,
            enableSupplierManagement = true,
            enableSupplierFinancials = true,
            enableShortageManagement = false,
            enableComponentsBom = false,
            enableUom = false,
            enableUomConversion = false,
            requirePoBeforeReceipt = true,
            allowDirectPurchaseReceipt = false,
            allowPartialReceipt = true,
            allowOverReceipt = false,
            overReceiptTolerancePercent = 0,
            enablePostingWorkflow = true,
            lockPostedDocuments = true,
            requireApprovalBeforePosting = false,
            enableReversals = true,
            requireReasonForCancelOrReversal = true,
            allowNegativeStock = false,
            enableBatchTracking = false,
            enableSerialTracking = false,
            enableExpiryTracking = false,
            enableStockTransfers = false,
            enableStockAdjustments = false,
            defaultWarehouseName = (string?)null,
            setupStep = "review",
            setupVersion = "v1"
        });
        Assert.Equal(HttpStatusCode.OK, saveResponse.StatusCode);

        var blockedInventoryResponse = await WithAuth(client, signup.AccessToken).GetAsync("/api/items");
        Assert.Equal(HttpStatusCode.OK, blockedInventoryResponse.StatusCode);

        var completeResponse = await WithAuth(client, signup.AccessToken).PostAsJsonAsync("/api/settings/organization/setup/complete", new { });
        Assert.Equal(HttpStatusCode.OK, completeResponse.StatusCode);

        var meAfterCompleteResponse = await WithAuth(client, signup.AccessToken).GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.OK, meAfterCompleteResponse.StatusCode);
        var meAfterCompletePayload = await meAfterCompleteResponse.Content.ReadAsStringAsync();
        Assert.Contains("\"setupCompleted\":true", meAfterCompletePayload);

        var inventoryDisabledResponse = await WithAuth(client, signup.AccessToken).GetAsync("/api/items");
        Assert.Equal(HttpStatusCode.OK, inventoryDisabledResponse.StatusCode);

        var settingsResponse = await WithAuth(client, signup.AccessToken).GetAsync("/api/settings/organization/setup");
        Assert.Equal(HttpStatusCode.OK, settingsResponse.StatusCode);
        var settingsPayload = await settingsResponse.Content.ReadAsStringAsync();
        Assert.Contains("\"enableInventory\":false", settingsPayload);

        var auditResponse = await WithAuth(client, signup.AccessToken).GetAsync("/api/settings/audit-log?page=1&pageSize=50");
        Assert.Equal(HttpStatusCode.OK, auditResponse.StatusCode);
        var auditPayload = await auditResponse.Content.ReadAsStringAsync();
        Assert.Contains("setup_started", auditPayload);
        Assert.Contains("setup_saved", auditPayload);
        Assert.Contains("setup_completed", auditPayload);
    }

    private static HttpClient WithAuth(HttpClient client, string token)
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static async Task<AuthApiResponse> LoginAsync(HttpClient client, string email)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email,
            password = "StrongPass!123",
            rememberMe = false,
            deviceName = "Test Browser"
        });
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<AuthApiResponse>();
        Assert.NotNull(payload);
        return payload!;
    }

    private static async Task<TestMember> CreateMemberAsync(ApiFactory factory, Guid organizationId, string email, string roleTemplateKey)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<UserAccount>>();
        var role = await dbContext.Roles.IgnoreQueryFilters().SingleAsync(entity => entity.OrganizationId == organizationId && entity.TemplateKey == roleTemplateKey);

        var user = new UserAccount
        {
            FullName = email.Split('@')[0],
            Email = email,
            EmailVerified = true,
            Status = UserAccountStatus.Active,
            CreatedBy = "test"
        };
        user.PasswordHash = passwordHasher.HashPassword(user, "StrongPass!123");
        dbContext.UserAccounts.Add(user);
        await dbContext.SaveChangesAsync();

        var profile = new UserProfile
        {
            OrganizationId = organizationId,
            LanguagePreference = "en",
            CreatedBy = "test"
        };
        dbContext.UserProfiles.Add(profile);
        await dbContext.SaveChangesAsync();

        var membership = new OrganizationMembership
        {
            OrganizationId = organizationId,
            UserId = user.Id,
            ProfileId = profile.Id,
            Status = MembershipStatus.Active,
            IsOwner = false,
            BranchAccessMode = AccessScopeMode.All,
            WarehouseAccessMode = AccessScopeMode.All,
            CreatedBy = "test"
        };
        dbContext.OrganizationMemberships.Add(membership);
        await dbContext.SaveChangesAsync();

        dbContext.MembershipRoles.Add(new MembershipRole
        {
            OrganizationId = organizationId,
            MembershipId = membership.Id,
            RoleId = role.Id,
            CreatedBy = "test"
        });
        await dbContext.SaveChangesAsync();

        return new TestMember(user.Id, membership.Id);
    }

    private static async Task<TestRole> GetRoleAsync(ApiFactory factory, Guid organizationId, string roleTemplateKey)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var role = await dbContext.Roles.IgnoreQueryFilters().SingleAsync(entity => entity.OrganizationId == organizationId && entity.TemplateKey == roleTemplateKey);
        return new TestRole(role.Id);
    }

    private static async Task<Guid> GetMembershipIdAsync(ApiFactory factory, Guid organizationId, string email)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await dbContext.OrganizationMemberships
            .IgnoreQueryFilters()
            .Where(entity => entity.OrganizationId == organizationId && entity.User!.Email == email)
            .Select(entity => entity.Id)
            .SingleAsync();
    }

    private static async Task<AuthApiResponse> SignupAsync(HttpClient client, string email, string organizationName)
    {
        var response = await client.PostAsJsonAsync("/api/auth/signup", new
        {
            fullName = email.Split('@')[0],
            email,
            password = "StrongPass!123",
            organizationName
        });
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<AuthApiResponse>();
        Assert.NotNull(payload);
        return payload!;
    }

    private static async Task CompleteSetupAsync(HttpClient client, string token)
    {
        var saveResponse = await WithAuth(client, token).PutAsJsonAsync("/api/settings/organization/setup", new
        {
            name = "Configured Org",
            logo = (string?)null,
            address = "Cairo",
            phone = "01000000000",
            taxId = "123456",
            commercialRegistry = "987654",
            defaultCurrency = "EGP",
            timezone = "Africa/Cairo",
            defaultLanguage = "en",
            rtlEnabled = false,
            fiscalYearStartMonth = 1,
            purchaseOrderPrefix = "PO",
            purchaseReceiptPrefix = "PR",
            purchaseReturnPrefix = "RTN",
            paymentPrefix = "PAY",
            defaultWarehouseId = (Guid?)null,
            autoPostDrafts = false,
            enableProcurement = true,
            enablePurchaseOrders = true,
            enablePurchaseReceipts = true,
            enableInventory = true,
            enableWarehouses = true,
            enableMultipleWarehouses = true,
            enableSupplierManagement = true,
            enableSupplierFinancials = true,
            enableShortageManagement = true,
            enableComponentsBom = false,
            enableUom = true,
            enableUomConversion = true,
            requirePoBeforeReceipt = false,
            allowDirectPurchaseReceipt = true,
            allowPartialReceipt = true,
            allowOverReceipt = false,
            overReceiptTolerancePercent = 0,
            enablePostingWorkflow = true,
            lockPostedDocuments = true,
            requireApprovalBeforePosting = false,
            enableReversals = true,
            requireReasonForCancelOrReversal = true,
            allowNegativeStock = false,
            enableBatchTracking = false,
            enableSerialTracking = false,
            enableExpiryTracking = false,
            enableStockTransfers = true,
            enableStockAdjustments = true,
            defaultWarehouseName = "Main Warehouse",
            setupStep = "review-finish",
            setupVersion = "v1"
        });
        saveResponse.EnsureSuccessStatusCode();

        var completeResponse = await WithAuth(client, token).PostAsJsonAsync("/api/settings/organization/setup/complete", new { });
        completeResponse.EnsureSuccessStatusCode();
    }

    public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
    {
        private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"highcool-identity-api-tests-{Guid.NewGuid():N}.db");

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["DatabaseProvider"] = "Sqlite",
                    ["ConnectionStrings:DefaultConnection"] = $"Data Source={_databasePath}",
                    ["Authentication:JwtSecret"] = "test-secret-that-is-long-enough-for-jwt-signing",
                    ["Authentication:Issuer"] = "HighCool.Tests",
                    ["Authentication:Audience"] = "HighCool.Tests.Client"
                });
            });
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<AppDbContext>>();
                services.RemoveAll<AppDbContext>();
                services.AddDbContext<AppDbContext>(options => options.UseSqlite($"Data Source={_databasePath}"));
                services.PostConfigureAll<JwtBearerOptions>(options =>
                {
                    options.TokenValidationParameters.ValidIssuer = "HighCool.Tests";
                    options.TokenValidationParameters.ValidAudience = "HighCool.Tests.Client";
                    options.TokenValidationParameters.IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("test-secret-that-is-long-enough-for-jwt-signing"));
                });
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

    public sealed record SupplierResponse(Guid Id);

    public sealed record TestMember(Guid UserId, Guid MembershipId);

    public sealed record TestRole(Guid Id);

    public sealed record RoleApiResponse(Guid Id, string Name);

    public sealed record SettingsRoleApiResponse(Guid Id, string Name, string? TemplateKey, IReadOnlyList<string> Permissions);

    public sealed record WorkspaceApiResponse(
        Guid UserId,
        string Email,
        bool EmailVerified,
        Guid OrganizationId,
        string OrganizationName,
        IReadOnlyList<RoleApiResponse> Roles);

    public sealed record AuthApiResponse(
        string AccessToken,
        DateTime ExpiresAt,
        WorkspaceApiResponse Workspace,
        string? EmailVerificationToken);
}
