using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OglesbyFDMembers.Data;

namespace OglesbyFDMembers.App.Services;

public enum BackupKind
{
    Manual,
    Daily,
    Weekly
}

public class BackupResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? FilePath { get; set; }
}

public class BackupService
{
    private readonly AppDbContext _db;
    private readonly ILogger<BackupService> _logger;
    private readonly BackupSettingsStore _settings;

    public BackupService(AppDbContext db, ILogger<BackupService> logger, BackupSettingsStore settings)
    {
        _db = db;
        _logger = logger;
        _settings = settings;
    }

    public async Task<string> GetBackupFolderAsync() => await _settings.GetFolderAsync();
    public async Task SetBackupFolderAsync(string folder) => await _settings.SetFolderAsync(folder);

    public async Task<List<FileInfo>> ListBackupsAsync()
    {
        var folder = await _settings.GetFolderAsync();
        if (string.IsNullOrWhiteSpace(folder)) return new List<FileInfo>();
        var di = new DirectoryInfo(folder);
        if (!di.Exists) di.Create();
        return di.EnumerateFiles("*.db").Concat(di.EnumerateFiles("*.sqlite")).OrderByDescending(f => f.CreationTimeUtc).ToList();
    }

    public async Task<BackupResult> CreateBackupAsync(BackupKind kind = BackupKind.Manual, CancellationToken ct = default)
    {
        try
        {
            var folder = await _settings.GetFolderAsync();
            if (string.IsNullOrWhiteSpace(folder))
            {
                return new BackupResult { Success = false, Message = "Backup folder not configured." };
            }
            Directory.CreateDirectory(folder);

            // Ensure EF connection is closed to minimize locking
            await _db.Database.CloseConnectionAsync();

            if (_db.Database.GetDbConnection() is not SqliteConnection efConn)
            {
                return new BackupResult { Success = false, Message = "Not using SQLite provider." };
            }

            var dbPath = efConn.DataSource;
            var timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            var tag = kind.ToString().ToLowerInvariant();
            var backupPath = Path.Combine(folder, $"oglesbyfd-{tag}-{timestamp}.db");

            await using (var conn = new SqliteConnection(efConn.ConnectionString))
            {
                await conn.OpenAsync(ct);
                await using var cmd = conn.CreateCommand();
                // checkpoint WAL then vacuum into a new file atomically
                cmd.CommandText = $"PRAGMA wal_checkpoint(FULL); VACUUM INTO '{backupPath.Replace("'", "''")}';";
                await cmd.ExecuteNonQueryAsync(ct);
            }

            _logger.LogInformation("Database backup created at {Path}", backupPath);
            return new BackupResult { Success = true, Message = "Backup created successfully.", FilePath = backupPath };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Backup failed");
            return new BackupResult { Success = false, Message = ex.Message };
        }
    }

    public async Task<BackupResult> RestoreBackupAsync(string backupFilePath, CancellationToken ct = default)
    {
        try
        {
            var folder = await _settings.GetFolderAsync();
            if (string.IsNullOrWhiteSpace(folder))
            {
                return new BackupResult { Success = false, Message = "Backup folder not configured." };
            }
            var full = Path.GetFullPath(backupFilePath);
            if (!full.StartsWith(Path.GetFullPath(folder), StringComparison.OrdinalIgnoreCase))
            {
                return new BackupResult { Success = false, Message = "Invalid backup file location." };
            }
            if (!File.Exists(full))
            {
                return new BackupResult { Success = false, Message = "Backup file not found." };
            }

            await _db.Database.CloseConnectionAsync();
            if (_db.Database.GetDbConnection() is not SqliteConnection efConn)
            {
                return new BackupResult { Success = false, Message = "Not using SQLite provider." };
            }

            var dbPath = efConn.DataSource;
            var now = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            var safetyCopy = dbPath + $".pre-restore-{now}";

            // Safety copy then overwrite
            File.Copy(dbPath, safetyCopy, overwrite: false);
            File.Copy(full, dbPath, overwrite: true);

            _logger.LogWarning("Database restored from {Backup}. Previous file saved at {Safety}", full, safetyCopy);
            return new BackupResult
            {
                Success = true,
                Message = "Restore completed. Please restart the application to ensure all connections reload.",
                FilePath = full
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Restore failed");
            return new BackupResult { Success = false, Message = ex.Message };
        }
    }

    /// <summary>
    /// Apply retention: keep daily backups for last 14 days; weekly backups for last 16 weeks.
    /// Manual backups are kept indefinitely.
    /// </summary>
    public async Task ApplyRetentionAsync(int dailyDays = 14, int weeklyWeeks = 16, CancellationToken ct = default)
    {
        try
        {
            var folder = await _settings.GetFolderAsync();
            if (string.IsNullOrWhiteSpace(folder)) return;
            var di = new DirectoryInfo(folder);
            if (!di.Exists) return;

            var dailyCutoff = DateTime.UtcNow.Date.AddDays(-dailyDays);
            var weeklyCutoff = DateTime.UtcNow.Date.AddDays(-(7 * weeklyWeeks));

            foreach (var file in di.EnumerateFiles("oglesbyfd-*-*.db"))
            {
                if (ct.IsCancellationRequested) break;

                var name = Path.GetFileNameWithoutExtension(file.Name);
                // Expected: oglesbyfd-<tag>-yyyyMMdd-HHmmss
                var parts = name.Split('-');
                if (parts.Length < 4 || !parts[0].Equals("oglesbyfd", StringComparison.OrdinalIgnoreCase))
                    continue;

                var tag = parts[1];
                var datePart = parts[2];
                var timePart = parts[3];
                if (DateTime.TryParseExact(datePart + "-" + timePart, "yyyyMMdd-HHmmss", null, System.Globalization.DateTimeStyles.AssumeLocal, out var localTs))
                {
                    var tsUtc = localTs.ToUniversalTime();
                    if (string.Equals(tag, "daily", StringComparison.OrdinalIgnoreCase) && tsUtc < dailyCutoff)
                    {
                        TryDelete(file);
                    }
                    else if (string.Equals(tag, "weekly", StringComparison.OrdinalIgnoreCase) && tsUtc < weeklyCutoff)
                    {
                        TryDelete(file);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Backup retention cleanup failed");
        }

        static void TryDelete(FileInfo file)
        {
            try { file.Delete(); }
            catch { /* ignore */ }
        }
    }
}
