using ERP.Domain.Common;
using ERP.Domain.Inventory;
using ERP.Domain.MasterData;
using ERP.Domain.Payments;
using ERP.Domain.Purchasing;
using ERP.Domain.Shortages;
using ERP.Domain.Statements;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    private const string SystemActor = "system";

    public DbSet<Customer> Customers => Set<Customer>();

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

    public DbSet<StockLedgerEntry> StockLedgerEntries => Set<StockLedgerEntry>();

    public DbSet<ShortageReasonCode> ShortageReasonCodes => Set<ShortageReasonCode>();

    public DbSet<ShortageLedgerEntry> ShortageLedgerEntries => Set<ShortageLedgerEntry>();

    public DbSet<ShortageResolution> ShortageResolutions => Set<ShortageResolution>();

    public DbSet<ShortageResolutionAllocation> ShortageResolutionAllocations => Set<ShortageResolutionAllocation>();

    public DbSet<SupplierStatementEntry> SupplierStatementEntries => Set<SupplierStatementEntry>();

    public DbSet<Payment> Payments => Set<Payment>();

    public DbSet<PaymentAllocation> PaymentAllocations => Set<PaymentAllocation>();

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
                entry.Entity.CreatedAt = utcNow;
                entry.Entity.CreatedBy = string.IsNullOrWhiteSpace(entry.Entity.CreatedBy)
                    ? SystemActor
                    : entry.Entity.CreatedBy;
            }

            if (entry.State == EntityState.Modified)
            {
                entry.Property(entity => entity.CreatedAt).IsModified = false;
                entry.Property(entity => entity.CreatedBy).IsModified = false;

                entry.Entity.UpdatedAt = utcNow;
                entry.Entity.UpdatedBy = string.IsNullOrWhiteSpace(entry.Entity.UpdatedBy)
                    ? SystemActor
                    : entry.Entity.UpdatedBy;
            }
        }
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
    }
}
