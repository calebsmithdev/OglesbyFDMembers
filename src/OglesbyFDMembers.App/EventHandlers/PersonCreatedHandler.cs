using Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OglesbyFDMembers.App.Events;
using OglesbyFDMembers.Data;
using OglesbyFDMembers.Domain.Entities;
using OglesbyFDMembers.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;

namespace OglesbyFDMembers.App.EventHandlers;

public sealed class PersonCreatedHandler : INotificationHandler<PersonCreated>
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<PersonCreatedHandler> _log;

    public PersonCreatedHandler(
        IServiceScopeFactory scopeFactory,
        ILogger<PersonCreatedHandler> log)
    {
        _scopeFactory = scopeFactory;
        _log = log;
    }

    public async ValueTask Handle(PersonCreated notification, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var _db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var personId = notification.PersonId;
        var year = DateTime.UtcNow.Year;

        // Resolve fee amount; default to 0 if not configured yet (idempotent behavior)
        var feeAmount = await _db.FeeSchedules
            .Where(f => f.Year == year)
            .Select(f => f.AmountPerProperty)
            .FirstOrDefaultAsync(ct);
        if (feeAmount == 0)
        {
            _log.LogWarning("No FeeSchedule configured for {Year}; creating assessments with AmountDue=0 for PersonId={PersonId}", year, personId);
        }

        var today = DateTime.UtcNow.Date;

        // Properties owned as of today and active
        var propertyIds = await _db.Ownerships.AsNoTracking()
            .Where(o => o.PersonId == personId && o.StartDate <= today && (o.EndDate == null || o.EndDate >= today))
            .Join(_db.Properties.AsNoTracking().Where(p => p.Active), o => o.PropertyId, p => p.Id, (o, p) => p.Id)
            .Distinct()
            .ToListAsync(ct);

        if (propertyIds.Count == 0)
        {
            _log.LogInformation("PersonCreated: no active properties for PersonId={PersonId}; nothing to assess", personId);
            return;
        }

        // Find which of those properties are missing an assessment for this year
        var existingAssessedPropertyIds = await _db.Assessments.AsNoTracking()
            .Where(a => a.Year == year && propertyIds.Contains(a.PropertyId))
            .Select(a => a.PropertyId)
            .Distinct()
            .ToListAsync(ct);

        var toCreate = propertyIds.Except(existingAssessedPropertyIds).ToList();
        if (toCreate.Count == 0)
        {
            _log.LogInformation("PersonCreated: assessments already exist for PersonId={PersonId} in {Year}", personId, year);
            return; // idempotent
        }

        foreach (var pid in toCreate)
        {
            _db.Assessments.Add(new Assessment
            {
                PropertyId = pid,
                Year = year,
                AmountDue = feeAmount,
                AmountPaid = 0m,
                Status = AssessmentStatus.Unpaid,
                CreatedUtc = DateTime.UtcNow
            });
        }

        try
        {
            await _db.SaveChangesAsync(ct);
            _log.LogInformation("PersonCreated: created {Count} assessment(s) for PersonId={PersonId} for {Year}", toCreate.Count, personId, year);
        }
        catch (DbUpdateException ex)
        {
            // Unique constraint on (PropertyId, Year) will make this safe across concurrent runs
            _log.LogWarning(ex, "PersonCreated: race creating assessments; safe to ignore if unique constraint hit for PersonId={PersonId} {Year}", personId, year);
        }
    }
}
