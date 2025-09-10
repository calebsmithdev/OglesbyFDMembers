using Microsoft.EntityFrameworkCore;
using OglesbyFDMembers.Data;
using OglesbyFDMembers.Domain.Entities;

namespace OglesbyFDMembers.App.Services;

public class BackupSettingsStore
{
    private readonly AppDbContext _db;

    public BackupSettingsStore(AppDbContext db)
    {
        _db = db;
    }

    public async Task<string> GetFolderAsync()
    {
        var row = await _db.AppSettings.AsNoTracking().FirstOrDefaultAsync(x => x.Key == "Backup.Folder");
        return row?.Value ?? string.Empty;
    }

    public async Task SetFolderAsync(string folder)
    {
        var value = folder ?? string.Empty;
        var row = await _db.AppSettings.FirstOrDefaultAsync(x => x.Key == "Backup.Folder");
        if (row == null)
        {
            row = new AppSetting { Key = "Backup.Folder", Value = value, UpdatedAtUtc = DateTime.UtcNow };
            _db.AppSettings.Add(row);
        }
        else
        {
            row.Value = value;
            row.UpdatedAtUtc = DateTime.UtcNow;
        }
        await _db.SaveChangesAsync();
    }

    public async Task<DateTime?> GetLastDailyUtcAsync()
    {
        var row = await _db.AppSettings.AsNoTracking().FirstOrDefaultAsync(x => x.Key == "Backup.LastDailyUtc");
        if (row?.Value is not { Length: > 0 }) return null;
        return DateTime.TryParse(row.Value, null, System.Globalization.DateTimeStyles.AdjustToUniversal, out var dt)
            ? DateTime.SpecifyKind(dt, DateTimeKind.Utc)
            : null;
    }

    public async Task SetLastDailyUtcAsync(DateTime utc)
    {
        var value = utc.ToUniversalTime().ToString("o");
        var row = await _db.AppSettings.FirstOrDefaultAsync(x => x.Key == "Backup.LastDailyUtc");
        if (row == null)
        {
            row = new AppSetting { Key = "Backup.LastDailyUtc", Value = value, UpdatedAtUtc = DateTime.UtcNow };
            _db.AppSettings.Add(row);
        }
        else
        {
            row.Value = value;
            row.UpdatedAtUtc = DateTime.UtcNow;
        }
        await _db.SaveChangesAsync();
    }

    public async Task<DateTime?> GetLastWeeklyUtcAsync()
    {
        var row = await _db.AppSettings.AsNoTracking().FirstOrDefaultAsync(x => x.Key == "Backup.LastWeeklyUtc");
        if (row?.Value is not { Length: > 0 }) return null;
        return DateTime.TryParse(row.Value, null, System.Globalization.DateTimeStyles.AdjustToUniversal, out var dt)
            ? DateTime.SpecifyKind(dt, DateTimeKind.Utc)
            : null;
    }

    public async Task SetLastWeeklyUtcAsync(DateTime utc)
    {
        var value = utc.ToUniversalTime().ToString("o");
        var row = await _db.AppSettings.FirstOrDefaultAsync(x => x.Key == "Backup.LastWeeklyUtc");
        if (row == null)
        {
            row = new AppSetting { Key = "Backup.LastWeeklyUtc", Value = value, UpdatedAtUtc = DateTime.UtcNow };
            _db.AppSettings.Add(row);
        }
        else
        {
            row.Value = value;
            row.UpdatedAtUtc = DateTime.UtcNow;
        }
        await _db.SaveChangesAsync();
    }
}
