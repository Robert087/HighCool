using ERP.Domain.Shortages;
using ERP.Infrastructure.Persistence.Configurations.Base;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations;

public sealed class ShortageResolutionAllocationConfiguration : AuditableEntityConfigurationBase<ShortageResolutionAllocation>
{
    protected override void ConfigureAuditableEntity(EntityTypeBuilder<ShortageResolutionAllocation> builder)
    {
        builder.ToTable("shortage_resolution_allocations");

        builder.Property(entity => entity.ResolutionId)
            .HasColumnName("resolution_id")
            .IsRequired();

        builder.Property(entity => entity.ShortageLedgerId)
            .HasColumnName("shortage_ledger_id")
            .IsRequired();

        builder.Property(entity => entity.AllocationType)
            .HasColumnName("allocation_type")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(entity => entity.AllocatedQty)
            .HasColumnName("allocated_qty")
            .HasColumnType("decimal(18,6)");

        builder.Property(entity => entity.AllocatedAmount)
            .HasColumnName("allocated_amount")
            .HasColumnType("decimal(18,6)");

        builder.Property(entity => entity.ValuationRate)
            .HasColumnName("valuation_rate")
            .HasColumnType("decimal(18,6)");

        builder.Property(entity => entity.FinancialQtyEquivalent)
            .HasColumnName("financial_qty_equivalent")
            .HasColumnType("decimal(18,6)");

        builder.Property(entity => entity.AllocationMethod)
            .HasColumnName("allocation_method")
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(entity => entity.SequenceNo)
            .HasColumnName("sequence_no")
            .IsRequired();

        builder.HasOne(entity => entity.Resolution)
            .WithMany(entity => entity.Allocations)
            .HasForeignKey(entity => entity.ResolutionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(entity => entity.ShortageLedgerEntry)
            .WithMany(entity => entity.Allocations)
            .HasForeignKey(entity => entity.ShortageLedgerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(entity => entity.ResolutionId);
        builder.HasIndex(entity => entity.ShortageLedgerId);
        builder.HasIndex(entity => new { entity.ResolutionId, entity.SequenceNo })
            .IsUnique();
        builder.HasIndex(entity => new { entity.ResolutionId, entity.ShortageLedgerId })
            .IsUnique();
        builder.HasIndex(entity => new { entity.ShortageLedgerId, entity.AllocationType });
    }
}
