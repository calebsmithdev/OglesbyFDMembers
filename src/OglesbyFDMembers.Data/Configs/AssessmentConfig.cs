using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OglesbyFDMembers.Domain.Entities;

namespace OglesbyFDMembers.Data.Configs;

public class AssessmentConfig : IEntityTypeConfiguration<Assessment>
{
    public void Configure(EntityTypeBuilder<Assessment> builder)
    {
        builder.ToTable("Assessments");

        builder.HasKey(a => a.Id);

        builder.HasIndex(a => new { a.PropertyId, a.Year })
            .IsUnique();

        builder.Property(a => a.AmountDue)
            .HasColumnType("decimal(10,2)")
            .IsRequired();

        builder.Property(a => a.AmountPaid)
            .HasColumnType("decimal(10,2)")
            .IsRequired();

        builder.Property(a => a.Status)
            .IsRequired();

        builder.HasOne(a => a.Property)
            .WithMany(p => p.Assessments)
            .HasForeignKey(a => a.PropertyId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

