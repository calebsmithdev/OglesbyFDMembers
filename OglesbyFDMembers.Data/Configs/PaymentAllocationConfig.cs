using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OglesbyFDMembers.Domain.Entities;

namespace OglesbyFDMembers.Data.Configs;

public class PaymentAllocationConfig : IEntityTypeConfiguration<PaymentAllocation>
{
    public void Configure(EntityTypeBuilder<PaymentAllocation> builder)
    {
        builder.ToTable("PaymentAllocations");

        builder.HasKey(pa => pa.Id);

        builder.Property(pa => pa.Amount)
            .HasColumnType("decimal(10,2)")
            .IsRequired();

        builder.HasIndex(pa => pa.AssessmentId);

        builder.HasOne(pa => pa.Payment)
            .WithMany(p => p.Allocations)
            .HasForeignKey(pa => pa.PaymentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(pa => pa.Assessment)
            .WithMany(a => a.PaymentAllocations)
            .HasForeignKey(pa => pa.AssessmentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

