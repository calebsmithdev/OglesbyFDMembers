using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OglesbyFDMembers.Domain.Entities;

namespace OglesbyFDMembers.Data.Configs;

public class UtilityNoticeConfig : IEntityTypeConfiguration<UtilityNotice>
{
    public void Configure(EntityTypeBuilder<UtilityNotice> builder)
    {
        builder.ToTable("UtilityNotices");

        builder.HasKey(u => u.Id);

        builder.Property(u => u.PayerNameRaw).HasMaxLength(200).IsRequired();
        builder.Property(u => u.Amount).HasColumnType("decimal(10,2)").IsRequired();

        builder.HasIndex(u => new { u.MatchedPersonId, u.IsAllocated });

        builder.HasOne(u => u.MatchedPerson)
            .WithMany(p => p.MatchedUtilityNotices)
            .HasForeignKey(u => u.MatchedPersonId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

