using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OglesbyFDMembers.App.Services;

namespace OglesbyFDMembers.App.Background;

/// <summary>
/// Creates a weekly SQLite backup and applies retention (16 weeks).
/// </summary>
public class WeeklyBackupJob : BackgroundService
{
    private readonly ILogger<WeeklyBackupJob> _logger;
    private readonly IServiceProvider _sp;

    public WeeklyBackupJob(ILogger<WeeklyBackupJob> logger, IServiceProvider sp)
    {
        _logger = logger;
        _sp = sp;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("WeeklyBackupJob starting");

        await RunOnceAsync(stoppingToken);

        using var timer = new PeriodicTimer(TimeSpan.FromDays(7));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await RunOnceAsync(stoppingToken);
        }
    }

    private async Task RunOnceAsync(CancellationToken ct)
    {
        try
        {
            using var scope = _sp.CreateScope();
            var svc = scope.ServiceProvider.GetRequiredService<BackupService>();
            var settings = scope.ServiceProvider.GetRequiredService<BackupSettingsStore>();
            var folder = await settings.GetFolderAsync();
            if (string.IsNullOrWhiteSpace(folder))
            {
                _logger.LogDebug("Weekly backup skipped: backup folder not configured.");
                return;
            }

            var utcNow = DateTime.UtcNow;
            var lastWeekly = await settings.GetLastWeeklyUtcAsync();
            if (IsSameIsoWeek(lastWeekly, utcNow))
            {
                _logger.LogDebug("Weekly backup skipped: already ran this week (last: {Date}).", lastWeekly?.ToString("u"));
            }
            else
            {
                var result = await svc.CreateBackupAsync(BackupKind.Weekly, ct);
                if (result.Success)
                {
                    await settings.SetLastWeeklyUtcAsync(utcNow);
                    _logger.LogInformation("Weekly backup created: {Path}", result.FilePath);
                }
                else
                {
                    _logger.LogWarning("Weekly backup failed: {Message}", result.Message);
                }
            }

            await svc.ApplyRetentionAsync(dailyDays: 14, weeklyWeeks: 16, ct);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "WeeklyBackupJob failed");
        }
    }

    private static bool IsSameIsoWeek(DateTime? aUtc, DateTime bUtc)
    {
        if (aUtc == null) return false;
        static DateTime StartOfIsoWeekUtc(DateTime dt)
        {
            var d = dt.Date;
            int diff = (7 + (int)d.DayOfWeek - (int)DayOfWeek.Monday) % 7;
            return d.AddDays(-diff);
        }
        return StartOfIsoWeekUtc(aUtc.Value) == StartOfIsoWeekUtc(bUtc);
    }
}
