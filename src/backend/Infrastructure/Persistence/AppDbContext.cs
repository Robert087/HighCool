using ERP.Domain.Common;
using ERP.Domain.MasterData;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    private const string SystemActor = "system";

    public DbSet<Supplier> Suppliers => Set<Supplier>();

    public DbSet<Item> Items => Set<Item>();

    public DbSet<ItemComponent> ItemComponents => Set<ItemComponent>();

    public DbSet<ItemUomConversion> ItemUomConversions => Set<ItemUomConversion>();

    public DbSet<Warehouse> Warehouses => Set<Warehouse>();

    public DbSet<Uom> Uoms => Set<Uom>();

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
}
