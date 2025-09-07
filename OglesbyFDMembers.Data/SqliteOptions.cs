using System.IO;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace OglesbyFDMembers.Data;

public static class SqliteOptions
{
    public static IServiceCollection AddSqliteDb(this IServiceCollection services, IConfiguration config, IHostEnvironment env)
    {
        var conn = config.GetConnectionString("DefaultConnection")
                   ?? config.GetConnectionString("Default");

        if (string.IsNullOrWhiteSpace(conn))
        {
            var dbFile = Path.Combine(env.ContentRootPath, "oglesbyfdmembers.db");
            conn = $"Data Source={dbFile}";
        }

        services.AddDbContext<AppDbContext>(options =>
        {
            options.UseSqlite(conn, sqlite =>
            {
                sqlite.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName);
            });

            if (env.IsDevelopment())
            {
                options.EnableDetailedErrors();
                options.EnableSensitiveDataLogging();
            }
        });

        return services;
    }
}

