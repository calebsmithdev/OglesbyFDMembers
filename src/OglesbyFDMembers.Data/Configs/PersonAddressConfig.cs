using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OglesbyFDMembers.Domain.Entities;

namespace OglesbyFDMembers.Data.Configs;

public class PersonAddressConfig : IEntityTypeConfiguration<PersonAddress>
{
    public void Configure(EntityTypeBuilder<PersonAddress> builder)
    {
        builder.ToTable("PersonAddresses");

        builder.HasKey(pa => pa.Id);

        builder.Property(pa => pa.Line1).HasMaxLength(200).IsRequired();
        builder.Property(pa => pa.AddresseeName).HasMaxLength(200);
        builder.Property(pa => pa.Line2).HasMaxLength(200);
        builder.Property(pa => pa.City).HasMaxLength(100);
        builder.Property(pa => pa.State).HasMaxLength(64);
        builder.Property(pa => pa.PostalCode).HasMaxLength(32);

        builder.HasIndex(pa => new { pa.PersonId, pa.IsPrimary, pa.IsValidForMail });

        builder.HasOne(pa => pa.Person)
            .WithMany(p => p.Addresses)
            .HasForeignKey(pa => pa.PersonId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
