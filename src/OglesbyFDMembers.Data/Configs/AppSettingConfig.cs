using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OglesbyFDMembers.Domain.Entities;

namespace OglesbyFDMembers.Data.Configs;

public class AppSettingConfig : IEntityTypeConfiguration<AppSetting>
{
    public void Configure(EntityTypeBuilder<AppSetting> b)
    {
        b.ToTable("AppSettings");
        b.HasKey(x => x.Key);
        b.Property(x => x.Key).HasMaxLength(200).IsRequired();
        b.Property(x => x.Value).HasMaxLength(4000);
        b.Property(x => x.UpdatedAtUtc).IsRequired();
    }
}

