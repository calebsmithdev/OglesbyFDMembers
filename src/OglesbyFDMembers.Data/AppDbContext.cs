using System.Linq;
using Microsoft.EntityFrameworkCore;
using OglesbyFDMembers.Domain.Entities;

namespace OglesbyFDMembers.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Person> People => Set<Person>();
    public DbSet<PersonAlias> PersonAliases => Set<PersonAlias>();
    public DbSet<PersonAddress> PersonAddresses => Set<PersonAddress>();
    public DbSet<Property> Properties => Set<Property>();
    public DbSet<Ownership> Ownerships => Set<Ownership>();
    public DbSet<Assessment> Assessments => Set<Assessment>();
    public DbSet<FeeSchedule> FeeSchedules => Set<FeeSchedule>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<PaymentAllocation> PaymentAllocations => Set<PaymentAllocation>();
    public DbSet<UtilityNotice> UtilityNotices => Set<UtilityNotice>();
    public DbSet<OglesbyFDMembers.Domain.Entities.AppSetting> AppSettings => Set<OglesbyFDMembers.Domain.Entities.AppSetting>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Apply all IEntityTypeConfiguration<> in this assembly
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        // Enforce Restrict delete behavior globally
        foreach (var fk in modelBuilder.Model.GetEntityTypes().SelectMany(e => e.GetForeignKeys()))
        {
            fk.DeleteBehavior = DeleteBehavior.Restrict;
        }

        base.OnModelCreating(modelBuilder);
    }
}
