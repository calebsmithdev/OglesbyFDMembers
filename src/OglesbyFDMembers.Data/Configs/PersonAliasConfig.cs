using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OglesbyFDMembers.Domain.Entities;

namespace OglesbyFDMembers.Data.Configs;

public class PersonAliasConfig : IEntityTypeConfiguration<PersonAlias>
{
    public void Configure(EntityTypeBuilder<PersonAlias> builder)
    {
        builder.ToTable("PersonAliases");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Alias).HasMaxLength(200).IsRequired();
        builder.Property(a => a.Type).IsRequired();

        builder.HasOne(a => a.Person)
            .WithMany(p => p.Aliases)
            .HasForeignKey(a => a.PersonId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
