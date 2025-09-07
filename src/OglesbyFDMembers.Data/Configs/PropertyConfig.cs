using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OglesbyFDMembers.Domain.Entities;

namespace OglesbyFDMembers.Data.Configs;

public class PropertyConfig : IEntityTypeConfiguration<Property>
{
    public void Configure(EntityTypeBuilder<Property> builder)
    {
        builder.ToTable("Properties");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.AddressLine1).HasMaxLength(200).IsRequired();
        builder.Property(p => p.AddressLine2).HasMaxLength(200);
        builder.Property(p => p.City).HasMaxLength(100);
        builder.Property(p => p.State).HasMaxLength(64);
        builder.Property(p => p.Zip).HasMaxLength(32);
    }
}
