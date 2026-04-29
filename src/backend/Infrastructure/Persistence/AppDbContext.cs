using ERP.Domain.Common;
using ERP.Domain.Identity;
using ERP.Domain.Inventory;
using ERP.Domain.MasterData;
using ERP.Domain.Payments;
using ERP.Domain.Purchasing;
using ERP.Domain.Reversals;
using ERP.Domain.Shortages;
using ERP.Domain.Statements;
using ERP.Application.Security;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;
using ERP.Infrastructure.Security;

namespace ERP.Infrastructure.Persistence;

public sealed class AppDbContext(
    DbContextOptions<AppDbContext> options,
    IRequestExecutionContext? executionContext = null) : DbContext(options)
{
    private const string SystemActor = "system";
    private readonly IRequestExecutionContext _executionContext = executionContext ?? SystemRequestExecutionContext.Instance;

    public DbSet<Customer> Customers => Set<Customer>();

    public DbSet<Organization> Organizations => Set<Organization>();

    public DbSet<OrganizationSecuritySettings> OrganizationSecuritySettings => Set<OrganizationSecuritySettings>();

    public DbSet<UserAccount> UserAccounts => Set<UserAccount>();

    public DbSet<OrganizationMembership> OrganizationMemberships => Set<OrganizationMembership>();

    public DbSet<UserProfile> UserProfiles => Set<UserProfile>();

    public DbSet<Role> Roles => Set<Role>();

    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();

    public DbSet<MembershipRole> MembershipRoles => Set<MembershipRole>();

    public DbSet<MembershipWarehouseAccess> MembershipWarehouseAccesses => Set<MembershipWarehouseAccess>();

    public DbSet<MembershipBranchAccess> MembershipBranchAccesses => Set<MembershipBranchAccess>();

    public DbSet<UserInvitation> UserInvitations => Set<UserInvitation>();

    public DbSet<UserInvitationRole> UserInvitationRoles => Set<UserInvitationRole>();

    public DbSet<UserInvitationWarehouseAccess> UserInvitationWarehouseAccesses => Set<UserInvitationWarehouseAccess>();

    public DbSet<UserInvitationBranchAccess> UserInvitationBranchAccesses => Set<UserInvitationBranchAccess>();

    public DbSet<UserSession> UserSessions => Set<UserSession>();

    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();

    public DbSet<EmailVerificationToken> EmailVerificationTokens => Set<EmailVerificationToken>();

    public DbSet<AuditLogEntry> AuditLogEntries => Set<AuditLogEntry>();

    public DbSet<Supplier> Suppliers => Set<Supplier>();

    public DbSet<Item> Items => Set<Item>();

    public DbSet<ItemComponent> ItemComponents => Set<ItemComponent>();

    public DbSet<UomConversion> UomConversions => Set<UomConversion>();

    public DbSet<Warehouse> Warehouses => Set<Warehouse>();

    public DbSet<Uom> Uoms => Set<Uom>();

    public DbSet<PurchaseOrder> PurchaseOrders => Set<PurchaseOrder>();

    public DbSet<PurchaseOrderLine> PurchaseOrderLines => Set<PurchaseOrderLine>();

    public DbSet<PurchaseReceipt> PurchaseReceipts => Set<PurchaseReceipt>();

    public DbSet<PurchaseReceiptLine> PurchaseReceiptLines => Set<PurchaseReceiptLine>();

    public DbSet<PurchaseReceiptLineComponent> PurchaseReceiptLineComponents => Set<PurchaseReceiptLineComponent>();

    public DbSet<PurchaseReturn> PurchaseReturns => Set<PurchaseReturn>();

    public DbSet<PurchaseReturnLine> PurchaseReturnLines => Set<PurchaseReturnLine>();

    public DbSet<StockLedgerEntry> StockLedgerEntries => Set<StockLedgerEntry>();

    public DbSet<ShortageReasonCode> ShortageReasonCodes => Set<ShortageReasonCode>();

    public DbSet<ShortageLedgerEntry> ShortageLedgerEntries => Set<ShortageLedgerEntry>();

    public DbSet<ShortageResolution> ShortageResolutions => Set<ShortageResolution>();

    public DbSet<ShortageResolutionAllocation> ShortageResolutionAllocations => Set<ShortageResolutionAllocation>();

    public DbSet<SupplierStatementEntry> SupplierStatementEntries => Set<SupplierStatementEntry>();

    public DbSet<Payment> Payments => Set<Payment>();

    public DbSet<PaymentAllocation> PaymentAllocations => Set<PaymentAllocation>();

    public DbSet<DocumentReversal> DocumentReversals => Set<DocumentReversal>();

    public override int SaveChanges()
    {
        ApplyAuditMetadata();
        return base.SaveChanges();
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        ApplyAuditMetadata();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ApplyAuditMetadata();
        return base.SaveChangesAsync(cancellationToken);
    }

    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        ApplyAuditMetadata();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        ApplyOrganizationQueryFilters(modelBuilder);
        ConfigureIdentityModel(modelBuilder);
        base.OnModelCreating(modelBuilder);
    }

    private void ApplyAuditMetadata()
    {
        GuardAppendOnlyLedgers();

        var utcNow = DateTime.UtcNow;

        foreach (var entry in ChangeTracker.Entries<IAuditableEntity>())
        {
            if (entry.State == EntityState.Added)
            {
                if (entry.Entity is IOrganizationScopedEntity organizationScoped &&
                    organizationScoped.OrganizationId == Guid.Empty &&
                    _executionContext.OrganizationId.HasValue)
                {
                    organizationScoped.OrganizationId = _executionContext.OrganizationId.Value;
                }

                entry.Entity.CreatedAt = utcNow;
                entry.Entity.CreatedBy = string.IsNullOrWhiteSpace(entry.Entity.CreatedBy)
                    ? ResolveActor()
                    : entry.Entity.CreatedBy;
            }

            if (entry.State == EntityState.Modified)
            {
                entry.Property(entity => entity.CreatedAt).IsModified = false;
                entry.Property(entity => entity.CreatedBy).IsModified = false;

                if (entry.Entity is IOrganizationScopedEntity)
                {
                    entry.Property(nameof(IOrganizationScopedEntity.OrganizationId)).IsModified = false;
                }

                entry.Entity.UpdatedAt = utcNow;
                entry.Entity.UpdatedBy = string.IsNullOrWhiteSpace(entry.Entity.UpdatedBy)
                    ? ResolveActor()
                    : entry.Entity.UpdatedBy;
            }
        }
    }

    private void ApplyOrganizationQueryFilters(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (!typeof(IOrganizationScopedEntity).IsAssignableFrom(entityType.ClrType))
            {
                continue;
            }

            var method = typeof(AppDbContext)
                .GetMethod(nameof(SetOrganizationFilter), System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?
                .MakeGenericMethod(entityType.ClrType);

            method?.Invoke(this, [modelBuilder]);
        }
    }

    private void SetOrganizationFilter<TEntity>(ModelBuilder modelBuilder)
        where TEntity : class, IOrganizationScopedEntity
    {
        modelBuilder.Entity<TEntity>()
            .HasQueryFilter(BuildOrganizationFilter<TEntity>());
    }

    private bool IsSystemScope => _executionContext.IsSystem;

    private bool HasOrganizationScope => _executionContext.OrganizationId.HasValue;

    private Guid CurrentOrganizationId => _executionContext.OrganizationId ?? Guid.Empty;

    private Expression<Func<TEntity, bool>> BuildOrganizationFilter<TEntity>()
        where TEntity : class, IOrganizationScopedEntity
    {
        return entity => IsSystemScope || (HasOrganizationScope && entity.OrganizationId == CurrentOrganizationId);
    }

    private void ConfigureIdentityModel(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UserAccount>()
            .HasIndex(entity => entity.Email)
            .IsUnique();

        modelBuilder.Entity<Organization>()
            .HasIndex(entity => entity.Name);

        modelBuilder.Entity<OrganizationMembership>()
            .HasIndex(entity => new { entity.OrganizationId, entity.UserId })
            .IsUnique();

        modelBuilder.Entity<Role>()
            .HasIndex(entity => new { entity.OrganizationId, entity.Name })
            .IsUnique();

        modelBuilder.Entity<RolePermission>()
            .HasIndex(entity => new { entity.RoleId, entity.PermissionKey })
            .IsUnique();

        modelBuilder.Entity<MembershipRole>()
            .HasIndex(entity => new { entity.MembershipId, entity.RoleId })
            .IsUnique();

        modelBuilder.Entity<MembershipWarehouseAccess>()
            .HasIndex(entity => new { entity.MembershipId, entity.WarehouseId })
            .IsUnique();

        modelBuilder.Entity<MembershipBranchAccess>()
            .HasIndex(entity => new { entity.MembershipId, entity.BranchCode })
            .IsUnique();

        modelBuilder.Entity<UserInvitation>()
            .HasIndex(entity => new { entity.OrganizationId, entity.Email, entity.Status });

        modelBuilder.Entity<UserInvitationRole>()
            .HasIndex(entity => new { entity.InvitationId, entity.RoleId })
            .IsUnique();

        modelBuilder.Entity<UserInvitationWarehouseAccess>()
            .HasIndex(entity => new { entity.InvitationId, entity.WarehouseId })
            .IsUnique();

        modelBuilder.Entity<UserInvitationBranchAccess>()
            .HasIndex(entity => new { entity.InvitationId, entity.BranchCode })
            .IsUnique();

        modelBuilder.Entity<UserSession>()
            .HasIndex(entity => entity.SessionTokenHash)
            .IsUnique();

        modelBuilder.Entity<PasswordResetToken>()
            .HasIndex(entity => entity.TokenHash)
            .IsUnique();

        modelBuilder.Entity<EmailVerificationToken>()
            .HasIndex(entity => entity.TokenHash)
            .IsUnique();

        modelBuilder.Entity<AuditLogEntry>()
            .HasIndex(entity => new { entity.OrganizationId, entity.CreatedAt });
    }

    private void GuardAppendOnlyLedgers()
    {
        var invalidStockLedgerEntry = ChangeTracker.Entries<StockLedgerEntry>()
            .FirstOrDefault(entry => entry.State is EntityState.Modified or EntityState.Deleted);

        if (invalidStockLedgerEntry is not null)
        {
            throw new InvalidOperationException("Stock ledger entries are append-only and cannot be edited or deleted.");
        }

        var invalidSupplierStatementEntry = ChangeTracker.Entries<SupplierStatementEntry>()
            .FirstOrDefault(entry => entry.State is EntityState.Modified or EntityState.Deleted);

        if (invalidSupplierStatementEntry is not null)
        {
            throw new InvalidOperationException("Supplier statement entries are append-only and cannot be edited or deleted.");
        }

        var invalidAddedSupplierStatementEntry = ChangeTracker.Entries<SupplierStatementEntry>()
            .FirstOrDefault(entry =>
                entry.State == EntityState.Added &&
                Round(entry.Entity.Debit) == 0m &&
                Round(entry.Entity.Credit) == 0m);

        if (invalidAddedSupplierStatementEntry is not null)
        {
            throw new InvalidOperationException("Supplier statement financial entries must have a non-zero debit or credit amount.");
        }
    }

    private static decimal Round(decimal value)
    {
        return decimal.Round(value, 6, MidpointRounding.AwayFromZero);
    }

    private string ResolveActor()
    {
        return string.IsNullOrWhiteSpace(_executionContext.Actor)
            ? SystemActor
            : _executionContext.Actor;
    }
}
