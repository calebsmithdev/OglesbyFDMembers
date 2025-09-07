using System.Data.Common;
using System.IO;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace OglesbyFDMembers.Data;

public static class SqliteOptions
{
    public static IServiceCollection AddSqliteDb(this IServiceCollection services, IConfiguration config, IHostEnvironment env)
    {
        var conn = config.GetConnectionString("DefaultConnection")
                   ?? config.GetConnectionString("Default");

        if (string.IsNullOrWhiteSpace(conn))
        {
            // single-PC default; absolute path eliminates “wrong file” surprises
            var dbFile = Path.Combine(@"C:\OglesbyFD\Data", "oglesby.db");
            Directory.CreateDirectory(Path.GetDirectoryName(dbFile)!);
            conn = $"Data Source={dbFile};Cache=Shared;Foreign Keys=True;";
        }
        else
        {
            // Normalize relative paths
            try
            {
                var csb = new SqliteConnectionStringBuilder(conn);
                if (!Path.IsPathRooted(csb.DataSource))
                {
                    csb.DataSource = Path.GetFullPath(Path.Combine(env.ContentRootPath, csb.DataSource));
                    conn = csb.ToString();
                }
                Directory.CreateDirectory(Path.GetDirectoryName(csb.DataSource)!);
            }
            catch { /* leave as-is if nonstandard */ }
        }

        services.AddDbContext<AppDbContext>((sp, options) =>
        {
            options.UseSqlite(conn, sqlite =>
            {
                sqlite.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName);
                // optional: sqlite.CommandTimeout(30);
            });

            if (env.IsDevelopment())
            {
                options.EnableDetailedErrors();
                options.EnableSensitiveDataLogging();
            }

            // Set WAL + busy_timeout on every connection open
            options.AddInterceptors(new SqlitePragmaInterceptor(
                sp.GetRequiredService<ILogger<SqlitePragmaInterceptor>>()));
        });

        return services;
    }
}

/// <summary>Ensures PRAGMAs per connection.</summary>
public sealed class SqlitePragmaInterceptor : Microsoft.EntityFrameworkCore.Diagnostics.DbConnectionInterceptor
{
    private readonly ILogger _logger;
    public SqlitePragmaInterceptor(ILogger<SqlitePragmaInterceptor> logger) => _logger = logger;

    public override async Task ConnectionOpenedAsync(
        DbConnection connection,
        Microsoft.EntityFrameworkCore.Diagnostics.ConnectionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        if (connection is SqliteConnection)
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "PRAGMA journal_mode=WAL; PRAGMA busy_timeout=5000; PRAGMA foreign_keys=ON; PRAGMA cache_size=-2000;";
            await cmd.ExecuteNonQueryAsync(cancellationToken);
            _logger.LogInformation("SQLite opened {DataSource}", ((SqliteConnection)connection).DataSource);
        }
        await base.ConnectionOpenedAsync(connection, eventData, cancellationToken);
    }
}
