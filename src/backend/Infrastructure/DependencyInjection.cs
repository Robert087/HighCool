using ERP.Application.MasterData.Items;
using ERP.Application.MasterData.Customers;
using ERP.Application.MasterData.Suppliers;
using ERP.Application.MasterData.UomConversions;
using ERP.Application.MasterData.Uoms;
using ERP.Application.MasterData.Warehouses;
using ERP.Application.Inventory;
using ERP.Application.Payments;
using ERP.Application.Purchasing.PurchaseReceipts;
using ERP.Application.Purchasing.PurchaseReturns;
using ERP.Application.Purchasing.PurchaseOrders;
using ERP.Application.Purchasing.ShortageReasonCodes;
using ERP.Application.Reversals;
using ERP.Application.Security;
using ERP.Application.Shortages;
using ERP.Application.Statements;
using ERP.Domain.Identity;
using ERP.Infrastructure.Inventory;
using ERP.Infrastructure.MasterData.Items;
using ERP.Infrastructure.MasterData.Customers;
using ERP.Infrastructure.MasterData.Suppliers;
using ERP.Infrastructure.MasterData.UomConversions;
using ERP.Infrastructure.MasterData.Uoms;
using ERP.Infrastructure.MasterData.Warehouses;
using ERP.Infrastructure.Payments;
using ERP.Infrastructure.Purchasing.PurchaseReceipts;
using ERP.Infrastructure.Purchasing.PurchaseReturns;
using ERP.Infrastructure.Purchasing.PurchaseOrders;
using ERP.Infrastructure.Purchasing.ShortageReasonCodes;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Reversals;
using ERP.Infrastructure.Security;
using ERP.Infrastructure.Shortages;
using ERP.Infrastructure.Statements;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.Text;

namespace ERP.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var provider = configuration["DatabaseProvider"] ?? "SqlServer";
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("DefaultConnection is not configured.");

        services.AddDbContext<AppDbContext>(options =>
        {
            if (string.Equals(provider, "Sqlite", StringComparison.OrdinalIgnoreCase))
            {
                options.UseSqlite(connectionString);
                return;
            }

            options.UseSqlServer(connectionString, sqlOptions =>
                sqlOptions.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName));
        });

        var jwtSecret = configuration["Authentication:JwtSecret"] ?? "highcool-dev-secret-change-me-immediately";
        var issuer = configuration["Authentication:Issuer"] ?? "HighCool";
        var audience = configuration["Authentication:Audience"] ?? "HighCool.Client";

        services.AddHttpContextAccessor();
        services.AddScoped<IRequestExecutionContext, HttpRequestExecutionContext>();
        services.AddScoped<JwtTokenService>();
        services.AddScoped<IPasswordHasher<UserAccount>, PasswordHasher<UserAccount>>();
        services.AddScoped<IAuditLogService, AuditLogService>();
        services.AddScoped<IAuthorizationService, AuthorizationService>();
        services.AddScoped<IIdentityService, IdentityService>();
        services.AddScoped<IOrganizationAdministrationService, OrganizationAdministrationService>();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateIssuerSigningKey = true,
                    ValidateLifetime = true,
                    ValidIssuer = issuer,
                    ValidAudience = audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret))
                };

                options.Events = new JwtBearerEvents
                {
                    OnTokenValidated = async context =>
                    {
                        var dbContext = context.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
                        var userId = ParseGuid(context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier));
                        var organizationId = ParseGuid(context.Principal?.FindFirstValue(CustomClaimTypes.OrganizationId));
                        var membershipId = ParseGuid(context.Principal?.FindFirstValue(CustomClaimTypes.MembershipId));
                        var sessionId = ParseGuid(context.Principal?.FindFirstValue(CustomClaimTypes.SessionId));

                        if (userId is null || organizationId is null || membershipId is null || sessionId is null)
                        {
                            context.Fail("Missing security context.");
                            return;
                        }

                        var session = await dbContext.UserSessions
                            .IgnoreQueryFilters()
                            .AsNoTracking()
                            .SingleOrDefaultAsync(entity =>
                                entity.Id == sessionId.Value &&
                                entity.UserId == userId.Value &&
                                entity.OrganizationId == organizationId.Value &&
                                entity.MembershipId == membershipId.Value,
                                context.HttpContext.RequestAborted);

                        if (session is null || !session.IsActive || session.RevokedAt.HasValue || session.ExpiresAt <= DateTime.UtcNow)
                        {
                            context.Fail("Session is no longer active.");
                            return;
                        }

                        var membership = await dbContext.OrganizationMemberships
                            .IgnoreQueryFilters()
                            .AsNoTracking()
                            .SingleOrDefaultAsync(entity =>
                                entity.Id == membershipId.Value &&
                                entity.UserId == userId.Value &&
                                entity.OrganizationId == organizationId.Value,
                                context.HttpContext.RequestAborted);

                        var user = await dbContext.UserAccounts
                            .IgnoreQueryFilters()
                            .AsNoTracking()
                            .SingleOrDefaultAsync(entity => entity.Id == userId.Value, context.HttpContext.RequestAborted);

                        if (membership is null || user is null ||
                            membership.Status is MembershipStatus.Suspended or MembershipStatus.Disabled or MembershipStatus.Deleted ||
                            user.Status is UserAccountStatus.Suspended or UserAccountStatus.Disabled or UserAccountStatus.Deleted ||
                            user.IsDeleted)
                        {
                            context.Fail("User access is not active.");
                        }
                    }
                };
            });

        services.AddAuthorization();

        services.AddScoped<IItemService, ItemService>();
        services.AddScoped<ICustomerService, CustomerService>();
        services.AddScoped<IUomConversionService, UomConversionService>();
        services.AddScoped<ISupplierService, SupplierService>();
        services.AddScoped<IWarehouseService, WarehouseService>();
        services.AddScoped<IUomService, UomService>();
        services.AddScoped<IStockLedgerQueryService, StockLedgerQueryService>();
        services.AddScoped<IStockBalanceService, StockBalanceService>();
        services.AddScoped<IPurchaseOrderService, PurchaseOrderService>();
        services.AddScoped<IPurchaseOrderPostingService, PurchaseOrderPostingService>();
        services.AddScoped<IPurchaseOrderCancellationService, PurchaseOrderCancellationService>();
        services.AddScoped<IPurchaseReceiptService, PurchaseReceiptService>();
        services.AddScoped<IPurchaseReceiptPostingService, PurchaseReceiptPostingService>();
        services.AddScoped<IPurchaseReturnService, PurchaseReturnService>();
        services.AddScoped<IPurchaseReturnPostingService, PurchaseReturnPostingService>();
        services.AddScoped<IQuantityConversionService, QuantityConversionService>();
        services.AddScoped<IStockLedgerService, StockLedgerService>();
        services.AddScoped<IShortageDetectionService, ShortageDetectionService>();
        services.AddScoped<IShortageReasonCodeService, ShortageReasonCodeService>();
        services.AddScoped<IShortageResolutionService, ShortageResolutionService>();
        services.AddScoped<IShortageResolutionPostingService, ShortageResolutionPostingService>();
        services.AddScoped<IShortageResolutionValidationService, ShortageResolutionValidationService>();
        services.AddScoped<IShortageResolutionAllocationService, ShortageResolutionAllocationService>();
        services.AddScoped<ISupplierStatementPostingService, SupplierStatementPostingService>();
        services.AddScoped<ISupplierStatementQueryService, SupplierStatementQueryService>();
        services.AddScoped<ISupplierBalanceService, SupplierBalanceService>();
        services.AddScoped<IPaymentService, PaymentService>();
        services.AddScoped<IPaymentQueryService, PaymentQueryService>();
        services.AddScoped<ISupplierPaymentPostingService, SupplierPaymentPostingService>();
        services.AddScoped<IPaymentAllocationService, PaymentAllocationService>();
        services.AddScoped<SupplierFinancialTargetStateService>();
        services.AddScoped<ISupplierOpenBalanceService, SupplierOpenBalanceService>();
        services.AddScoped<IReceiptReversalService, ReceiptReversalService>();
        services.AddScoped<IPaymentReversalService, PaymentReversalService>();
        services.AddScoped<IShortageResolutionReversalService, ShortageResolutionReversalService>();
        services.AddScoped<IReversalService, ReversalService>();
        services.AddHostedService<DevelopmentDatabaseInitializer>();

        return services;
    }

    private static Guid? ParseGuid(string? value)
    {
        return Guid.TryParse(value, out var parsed) ? parsed : null;
    }
}
