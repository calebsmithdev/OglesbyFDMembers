using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using OglesbyFDMembers.Data;
using OglesbyFDMembers.Domain.Entities;

namespace OglesbyFDMembers.App.Services;

public class UtilityNoticeService
{
    private readonly AppDbContext _db;
    private readonly PeopleService _people;

    public UtilityNoticeService(AppDbContext db, PeopleService people)
    {
        _db = db;
        _people = people;
    }

    public async Task<List<UtilityNoticeItem>> ListAsync(NoticeFilter filter, string? search = null, CancellationToken ct = default)
    {
        var q = _db.UtilityNotices.AsNoTracking();

        if (filter == NoticeFilter.Matched)
            q = q.Where(n => n.MatchedPersonId != null);
        else if (filter == NoticeFilter.Unmatched)
            q = q.Where(n => n.MatchedPersonId == null);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            q = q.Where(n => n.PayerNameRaw.ToLower().Contains(s));
        }

        var list = await q
            .OrderByDescending(n => n.ImportedUtc)
            .Select(n => new UtilityNoticeItem
            {
                Id = n.Id,
                PayerNameRaw = n.PayerNameRaw,
                Amount = n.Amount,
                ImportedUtc = n.ImportedUtc,
                MatchedPersonId = n.MatchedPersonId,
                OriginalFullName = n.OriginalFullName,
                IsAllocated = n.IsAllocated,
                NeedsReview = n.NeedsReview
            })
            .ToListAsync(ct);

        // Fill matched person names
        var matchedIds = list.Where(i => i.MatchedPersonId != null).Select(i => i.MatchedPersonId!.Value).Distinct().ToList();
        if (matchedIds.Count > 0)
        {
            var names = await _db.People.AsNoTracking()
                .Where(p => matchedIds.Contains(p.Id))
                .Select(p => new { p.Id, p.FirstName, p.LastName })
                .ToDictionaryAsync(x => x.Id, x => (x.FirstName + " " + x.LastName).Trim(), ct);

            foreach (var i in list)
            {
                if (i.MatchedPersonId is int id && names.TryGetValue(id, out var nm))
                    i.MatchedPersonName = nm;
            }
        }

        return list;
    }

    public async Task UpdateMatchAsync(int noticeId, int? personId, bool createAlias = false, CancellationToken ct = default)
    {
        var notice = await _db.UtilityNotices.FirstOrDefaultAsync(n => n.Id == noticeId, ct);
        if (notice == null) throw new ValidationException("Notice not found.");

        notice.MatchedPersonId = personId;
        notice.NeedsReview = personId == null; // unmatched -> needs review

        if (createAlias && personId is int pid && !string.IsNullOrWhiteSpace(notice.PayerNameRaw))
        {
            var alias = notice.PayerNameRaw.Trim();
            var exists = await _db.PersonAliases.AsNoTracking()
                .AnyAsync(a => a.PersonId == pid && a.Type == PersonAliasType.VVEC && a.Alias.ToLower() == alias.ToLower(), ct);
            if (!exists)
            {
                await _people.AddAliasAsync(pid, alias, PersonAliasType.VVEC, ct);
            }
        }

        await _db.SaveChangesAsync(ct);
    }
}

public enum NoticeFilter
{
    All,
    Matched,
    Unmatched
}

public class UtilityNoticeItem
{
    public int Id { get; set; }
    public string PayerNameRaw { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTime ImportedUtc { get; set; }
    public int? MatchedPersonId { get; set; }
    public string? MatchedPersonName { get; set; }
    public string? OriginalFullName { get; set; }
    public bool IsAllocated { get; set; }
    public bool NeedsReview { get; set; }
}
