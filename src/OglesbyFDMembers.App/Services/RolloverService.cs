using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OglesbyFDMembers.Data;
using OglesbyFDMembers.Domain.Entities;
using OglesbyFDMembers.Domain.Enums;

namespace OglesbyFDMembers.App.Services;

/// <summary>
/// Idempotently ensures Assessments exist for active properties for a given year.
/// Respects unique index on (PropertyId, Year). Does not modify existing rows.
/// </summary>
public class RolloverService
{
    private readonly AppDbContext _db;
    private readonly ILogger<RolloverService> _logger;

    public RolloverService(AppDbContext db, ILogger<RolloverService> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Ensure FeeSchedule exists and create missing Assessments for active properties.
    /// Returns number of created rows. Safe to run multiple times.
    /// </summary>
    public async Task<int> CreateMissingAssessmentsAsync(int year, CancellationToken ct = default)
    {
        if (year < 2000 || year > 3000) throw new ValidationException("Invalid year");

        var fee = await _db.FeeSchedules.AsNoTracking()
            .Where(f => f.Year == year)
            .Select(f => f.AmountPerProperty)
            .FirstOrDefaultAsync(ct);

        if (fee == default)
        {
            _logger.LogWarning("No FeeSchedule configured for {Year}. Skipping assessment creation.", year);
            return 0;
        }

        // Compute set of active properties missing an assessment for the year
        var existingPropIds = await _db.Assessments.AsNoTracking()
            .Where(a => a.Year == year)
            .Select(a => a.PropertyId)
            .ToListAsync(ct);

        var targetPropIds = await _db.Properties.AsNoTracking()
            .Where(p => p.Active && !existingPropIds.Contains(p.Id))
            .Select(p => p.Id)
            .ToListAsync(ct);

        if (targetPropIds.Count == 0)
        {
            return 0; // idempotent, nothing to do
        }

        var now = DateTime.UtcNow;
        var toInsert = new List<Assessment>(capacity: targetPropIds.Count);
        foreach (var pid in targetPropIds)
        {
            toInsert.Add(new Assessment
            {
                PropertyId = pid,
                Year = year,
                AmountDue = fee,
                AmountPaid = 0,
                Status = AssessmentStatus.Unpaid,
                CreatedUtc = now
            });
        }

        // Wrap in a transaction; tolerate duplicates by unique index (ignore conflicts on save)
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            _db.Assessments.AddRange(toInsert);
            var created = await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return created;
        }
        catch (DbUpdateException ex)
        {
            _logger.LogWarning(ex, "Conflict while creating assessments; reconciling with current state.");
            await tx.RollbackAsync(ct);

            // Re-check individually to insert any that truly remain missing
            var created = 0;
            foreach (var s in toInsert)
            {
                var exists = await _db.Assessments
                    .AnyAsync(a => a.PropertyId == s.PropertyId && a.Year == s.Year, ct);
                if (!exists)
                {
                    _db.Assessments.Add(s);
                    created += await _db.SaveChangesAsync(ct);
                }
            }
            return created;
        }
    }
}

