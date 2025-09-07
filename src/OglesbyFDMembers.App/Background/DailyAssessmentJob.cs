using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OglesbyFDMembers.App.Services;

namespace OglesbyFDMembers.App.Background;

/// <summary>
/// Background job that ensures all active properties have an Assessment for the current year.
/// Runs on startup and then once per day.
/// </summary>
public class DailyAssessmentJob : BackgroundService
{
    private readonly ILogger<DailyAssessmentJob> _logger;
    private readonly IServiceProvider _sp;

    public DailyAssessmentJob(ILogger<DailyAssessmentJob> logger, IServiceProvider sp)
    {
        _logger = logger;
        _sp = sp;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("DailyAssessmentJob starting");

        // Run immediately on startup
        await RunOnceAsync(stoppingToken);

        // Then run every 24 hours while the app is running
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
            var year = DateTime.UtcNow.Year;
            using var scope = _sp.CreateScope();
            var svc = scope.ServiceProvider.GetRequiredService<RolloverService>();
            var created = await svc.CreateMissingAssessmentsAsync(year, ct);
            _logger.LogInformation("DailyAssessmentJob completed for {Year}. Created {Count} missing assessments.", year, created);
        }
        catch (OperationCanceledException)
        {
            // Swallow cancellation and let the host manage shutdown
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DailyAssessmentJob failed");
        }
    }
}

