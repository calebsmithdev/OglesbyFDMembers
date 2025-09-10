using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OglesbyFDMembers.App.Services;

namespace OglesbyFDMembers.App.Background;

/// <summary>
/// Creates a full SQLite backup once per day using BackupService.
/// </summary>
public class DailyBackupJob : BackgroundService
{
    private readonly ILogger<DailyBackupJob> _logger;
    private readonly IServiceProvider _sp;

    public DailyBackupJob(ILogger<DailyBackupJob> logger, IServiceProvider sp)
    {
        _logger = logger;
        _sp = sp;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("DailyBackupJob starting");

        await RunOnceAsync(stoppingToken);

        using var timer = new PeriodicTimer(TimeSpan.FromDays(1));
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
                _logger.LogDebug("Daily backup skipped: backup folder not configured.");
                return;
            }

            // Skip if already ran today (UTC date)
            var utcNow = DateTime.UtcNow;
            var lastDaily = await settings.GetLastDailyUtcAsync();
            if (lastDaily?.Date == utcNow.Date)
            {
                _logger.LogDebug("Daily backup skipped: already ran today ({Date}).", lastDaily?.ToString("u"));
            }
            else
            {
                var result = await svc.CreateBackupAsync(BackupKind.Daily, ct);
                if (result.Success)
                {
                    await settings.SetLastDailyUtcAsync(utcNow);
                    _logger.LogInformation("Daily backup created: {Path}", result.FilePath);
                }
                else
                {
                    _logger.LogWarning("Daily backup failed: {Message}", result.Message);
                }
            }

            await svc.ApplyRetentionAsync(dailyDays: 14, weeklyWeeks: 16, ct);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DailyBackupJob failed");
        }
    }
}
