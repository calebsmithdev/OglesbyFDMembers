using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using OglesbyFDMembers.Data;
using OglesbyFDMembers.Domain.Entities;
using OglesbyFDMembers.Domain.Enums;

namespace OglesbyFDMembers.App.Services;

public interface IVvecImportService
{
    Task<List<UtilityRow>> ExtractAsync(Stream pdf, CancellationToken ct = default);
    Task<List<UtilityRowWithMatch>> MatchAsync(List<UtilityRow> rows, CancellationToken ct = default);
    Task<ProcessResult> ProcessAsync(ProcessRequest request, CancellationToken ct = default);
}

public class VvecImportService : IVvecImportService
{
    private readonly AppDbContext _db;
    private readonly IPdfVvecExtractor _extractor;
    private readonly PaymentsService _payments;
    private readonly PeopleService _people;

    public VvecImportService(AppDbContext db, IPdfVvecExtractor extractor, PaymentsService payments, PeopleService people)
    {
        _db = db;
        _extractor = extractor;
        _payments = payments;
        _people = people;
    }

    public Task<List<UtilityRow>> ExtractAsync(Stream pdf, CancellationToken ct = default)
        => _extractor.ExtractAsync(pdf, ct);

    public async Task<List<UtilityRowWithMatch>> MatchAsync(List<UtilityRow> rows, CancellationToken ct = default)
    {
        var result = new List<UtilityRowWithMatch>(rows.Count);

        // Preload alias map (VVEC only)
        var aliasMap = await _db.PersonAliases.AsNoTracking()
            .Where(a => a.Type == PersonAliasType.VVEC)
            .Select(a => new { a.PersonId, a.Alias })
            .ToListAsync(ct);

        var aliasLookup = aliasMap
            .GroupBy(x => Normalize(x.Alias))
            .ToDictionary(g => g.Key, g => g.Select(v => v.PersonId).Distinct().ToList());

        // Preload simple full-name -> personId map (First Last)
        var people = await _db.People.AsNoTracking()
            .Select(p => new { p.Id, p.FirstName, p.LastName })
            .ToListAsync(ct);

        var nameLookup = people
            .GroupBy(p => Normalize(p.FirstName + " " + p.LastName))
            .ToDictionary(g => g.Key, g => g.Select(v => v.Id).Distinct().ToList());

        foreach (var r in rows)
        {
            var raw = r.Name ?? string.Empty;
            var norm = Normalize(raw);

            int? personId = null;
            string? personName = null;
            string matchType = "";

            // Try First Last exact
            if (nameLookup.TryGetValue(norm, out var pidList) && pidList.Count == 1)
            {
                personId = pidList[0];
                var p = people.First(p => p.Id == personId.Value);
                personName = p.FirstName + " " + p.LastName;
                matchType = "Name";
            }
            else if (aliasLookup.TryGetValue(norm, out var apids) && apids.Count == 1)
            {
                personId = apids[0];
                var p = people.First(p => p.Id == personId.Value);
                personName = p.FirstName + " " + p.LastName;
                matchType = "Alias(VVEC)";
            }

            result.Add(new UtilityRowWithMatch
            {
                Name = r.Name,
                Amount = r.Amount,
                MatchedPersonId = personId,
                MatchedPersonName = personName,
                MatchType = matchType
            });
        }

        return result;
    }

    public async Task<ProcessResult> ProcessAsync(ProcessRequest request, CancellationToken ct = default)
    {
        if (request.Rows is null || request.Rows.Count == 0)
            return new ProcessResult();

        var now = DateTime.UtcNow;
        int createdPayments = 0;
        int createdAliases = 0;
        int createdNotices = 0;

        foreach (var row in request.Rows)
        {
            var assignedId = row.AssignedPersonId ?? row.MatchedPersonId;

            // Always create a UtilityNotice row for traceability
            var notice = new UtilityNotice
            {
                PayerNameRaw = row.Name.Trim(),
                Amount = row.Amount,
                ImportedUtc = now,
                MatchedPersonId = assignedId,
                OriginalFullName = null,
                IsAllocated = false,
                NeedsReview = assignedId == null
            };
            _db.UtilityNotices.Add(notice);
            createdNotices++;

            if (assignedId is int personId)
            {
                // Snapshot the full name used during import assignment
                var p = await _db.People.AsNoTracking().Where(x => x.Id == personId)
                    .Select(x => new { x.FirstName, x.LastName }).FirstOrDefaultAsync(ct);
                if (p != null)
                {
                    notice.OriginalFullName = (p.FirstName + " " + p.LastName).Trim();
                }

                // If user manually assigned and opted to auto-alias, create VVEC alias if not exists
                if (row.AssignedPersonId.HasValue && request.CreateAliasOnManualAssign)
                {
                    var alias = row.Name.Trim();
                    var exists = await _db.PersonAliases
                        .AnyAsync(a => a.PersonId == personId && a.Type == PersonAliasType.VVEC && a.Alias.ToLower() == alias.ToLower(), ct);
                    if (!exists)
                    {
                        await _people.AddAliasAsync(personId, alias, PersonAliasType.VVEC, ct);
                        createdAliases++;
                    }
                }

                if (request.CreatePayments)
                {
                    var newPaymentId = await _payments.CreateAndAllocateAsync(new AddPaymentRequest
                    {
                        PersonId = personId,
                        Amount = row.Amount,
                        PaymentType = PaymentType.VVEC,
                        Notes = $"VVEC import: {row.Name} {now:yyyy-MM-dd}",
                        AllocationMode = AllocationMode.SplitAcrossAll,
                        AllocationYear = request.AllocationYear ?? DateTime.UtcNow.Year
                    }, ct);
                    notice.IsAllocated = true;
                    notice.PaymentId = newPaymentId;
                    createdPayments++;
                }
            }
        }

        await _db.SaveChangesAsync(ct);

        return new ProcessResult
        {
            CreatedNotices = createdNotices,
            CreatedAliases = createdAliases,
            CreatedPayments = createdPayments
        };
    }

    private static string Normalize(string? s)
    {
        s ??= string.Empty;
        s = s.Trim();
        s = s.Replace(",", " ");
        s = System.Text.RegularExpressions.Regex.Replace(s, "\\s+", " ");
        return s.ToLowerInvariant();
    }
}

public class UtilityRow
{
    public string Name { get; set; } = string.Empty;
    public decimal Amount { get; set; }
}

public class UtilityRowWithMatch : UtilityRow
{
    public int? MatchedPersonId { get; set; }
    public string? MatchedPersonName { get; set; }
    public string? MatchType { get; set; }

    // UI uses this to override match when needed
    public int? AssignedPersonId { get; set; }
}

public class ProcessRequest
{
    public List<UtilityRowWithMatch> Rows { get; set; } = new();
    public bool CreateAliasOnManualAssign { get; set; } = true;
    public bool CreatePayments { get; set; } = true;
    public int? AllocationYear { get; set; }
}

public class ProcessResult
{
    public int CreatedNotices { get; set; }
    public int CreatedAliases { get; set; }
    public int CreatedPayments { get; set; }
}
