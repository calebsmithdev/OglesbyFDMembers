using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OglesbyFDMembers.Domain.Entities;

namespace OglesbyFDMembers.Data.Configs;

public class FeeScheduleConfig : IEntityTypeConfiguration<FeeSchedule>
{
    public void Configure(EntityTypeBuilder<FeeSchedule> builder)
    {
        builder.ToTable("FeeSchedules");

        builder.HasKey(f => f.Id);

        builder.HasIndex(f => f.Year).IsUnique();

        builder.Property(f => f.AmountPerProperty)
            .HasColumnType("decimal(10,2)")
            .IsRequired();
    }
}

