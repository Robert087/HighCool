using ERP.Domain.Payments;
using ERP.Infrastructure.Persistence.Configurations.Base;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ERP.Infrastructure.Persistence.Configurations;

public sealed class PaymentAllocationConfiguration : AuditableEntityConfigurationBase<PaymentAllocation>
{
    protected override void ConfigureAuditableEntity(EntityTypeBuilder<PaymentAllocation> builder)
    {
        builder.ToTable("payment_allocations");

        builder.Property(entity => entity.PaymentId)
            .HasColumnName("payment_id")
            .IsRequired();

        builder.Property(entity => entity.TargetDocType)
            .HasColumnName("target_doc_type")
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(entity => entity.TargetDocId)
            .HasColumnName("target_doc_id")
            .IsRequired();

        builder.Property(entity => entity.TargetLineId)
            .HasColumnName("target_line_id");

        builder.Property(entity => entity.AllocatedAmount)
            .HasColumnName("allocated_amount")
            .HasColumnType("decimal(18,6)")
            .IsRequired();

        builder.Property(entity => entity.AllocationOrder)
            .HasColumnName("allocation_order")
            .IsRequired();

        builder.HasIndex(entity => new { entity.PaymentId, entity.AllocationOrder })
            .IsUnique();

        builder.HasIndex(entity => new { entity.TargetDocType, entity.TargetDocId, entity.TargetLineId });
    }
}
