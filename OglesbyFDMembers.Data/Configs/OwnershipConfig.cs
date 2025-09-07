using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OglesbyFDMembers.Domain.Entities;

namespace OglesbyFDMembers.Data.Configs;

public class OwnershipConfig : IEntityTypeConfiguration<Ownership>
{
    public void Configure(EntityTypeBuilder<Ownership> builder)
    {
        builder.ToTable("Ownerships");

        builder.HasKey(o => o.Id);

        builder.HasIndex(o => new { o.PropertyId, o.StartDate, o.EndDate });

        builder.HasOne(o => o.Person)
            .WithMany(p => p.Ownerships)
            .HasForeignKey(o => o.PersonId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(o => o.Property)
            .WithMany(p => p.Ownerships)
            .HasForeignKey(o => o.PropertyId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

