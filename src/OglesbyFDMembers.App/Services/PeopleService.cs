using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using OglesbyFDMembers.Data;

namespace OglesbyFDMembers.App.Services;

public class PeopleService
{
    private readonly AppDbContext _db;

    public PeopleService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<List<int>> GetAvailableYearsAsync(CancellationToken ct = default)
    {
        var years = await _db.Assessments
            .Select(a => a.Year)
            .Distinct()
            .OrderByDescending(y => y)
            .ToListAsync(ct);
        if (years.Count == 0)
        {
            years.Add(DateTime.UtcNow.Year);
        }
        return years;
    }

    public async Task<List<PersonListItem>> ListAsync(int year, string? search = null, CancellationToken ct = default)
    {
        var asOf = new DateTime(year, 1, 1);

        // Base query joins: People -> primary mailing address (latest CreatedUtc), left join ownerships as-of date -> assessments for given year.
        var query = from p in _db.People.AsNoTracking()
                    join a in _db.PersonAddresses.AsNoTracking().Where(a => a.IsPrimary) on p.Id equals a.PersonId into pa
                    from primary in pa.OrderByDescending(x => x.CreatedUtc).Take(1).DefaultIfEmpty()
                    join o in _db.Ownerships.AsNoTracking()
                        .Where(o => o.StartDate <= asOf && (o.EndDate == null || o.EndDate >= asOf))
                        on p.Id equals o.PersonId into owns
                    from o in owns.DefaultIfEmpty()
                    join s in _db.Assessments.AsNoTracking().Where(s => s.Year == year) on o.PropertyId equals s.PropertyId into ass
                    from s in ass.DefaultIfEmpty()
                    group new { p, primary, s } by new
                    {
                        p.Id,
                        p.FirstName,
                        p.LastName,
                        p.Email,
                        p.Phone,
                        Line1 = primary != null ? primary.Line1 : null,
                        Line2 = primary != null ? primary.Line2 : null,
                        City = primary != null ? primary.City : null,
                        State = primary != null ? primary.State : null,
                        PostalCode = primary != null ? primary.PostalCode : null
                    }
            into g
                    select new PersonRow
                    {
                        PersonId = g.Key.Id,
                        FirstName = g.Key.FirstName,
                        LastName = g.Key.LastName,
                        Email = g.Key.Email,
                        Phone = g.Key.Phone,
                        Line1 = g.Key.Line1,
                        Line2 = g.Key.Line2,
                        City = g.Key.City,
                        State = g.Key.State,
                        PostalCode = g.Key.PostalCode,
                        AmountPaid = g.Sum(x => x.s != null ? x.s.AmountPaid : 0m),
                        AmountDue = g.Sum(x => x.s != null ? x.s.AmountDue : 0m)
                    };

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            query = query.Where(x =>
                ((x.FirstName + " " + x.LastName).ToLower().Contains(s)) ||
                (x.Email != null && x.Email.ToLower().Contains(s)) ||
                (x.Phone != null && x.Phone.ToLower().Contains(s)) ||
                (x.Line1 != null && x.Line1.ToLower().Contains(s)) ||
                (x.Line2 != null && x.Line2.ToLower().Contains(s)) ||
                (x.City != null && x.City.ToLower().Contains(s)) ||
                (x.State != null && x.State.ToLower().Contains(s)) ||
                (x.PostalCode != null && x.PostalCode.ToLower().Contains(s))
            );
        }

        var data = await query
            .OrderBy(x => x.LastName)
            .ThenBy(x => x.FirstName)
            .ToListAsync(ct);

        var list = data.Select(x => new PersonListItem
        {
            PersonId = x.PersonId,
            FullName = (x.FirstName + " " + x.LastName).Trim(),
            Email = x.Email,
            Phone = x.Phone,
            Address = FormatAddress(x.Line1, x.Line2, x.City, x.State, x.PostalCode),
            AmountPaid = x.AmountPaid,
            AmountDue = x.AmountDue
        }).ToList();

        // Compute status and balances client-side for clarity
        foreach (var item in list)
        {
            var balance = item.AmountDue - item.AmountPaid;
            item.Balance = balance;
            if (item.AmountDue <= 0 && item.AmountPaid <= 0)
            {
                item.Status = PaymentStatus.None;
            }
            else if (balance <= 0)
            {
                item.Status = PaymentStatus.Paid;
            }
            else if (item.AmountPaid == 0)
            {
                item.Status = PaymentStatus.Unpaid;
            }
            else
            {
                item.Status = PaymentStatus.Partial;
            }
        }

        return list;
    }

    private static string? FormatAddress(string? line1, string? line2, string? city, string? state, string? postal)
    {
        var parts = new List<string?>();
        if (!string.IsNullOrWhiteSpace(line1)) parts.Add(line1);
        if (!string.IsNullOrWhiteSpace(line2)) parts.Add(line2);
        var cityState = string.Join(" ", new[] { city, state }.Where(s => !string.IsNullOrWhiteSpace(s)));
        if (!string.IsNullOrWhiteSpace(cityState)) parts.Add(cityState);
        if (!string.IsNullOrWhiteSpace(postal)) parts.Add(postal);
        var result = string.Join(", ", parts.Where(s => !string.IsNullOrWhiteSpace(s)));
        return string.IsNullOrWhiteSpace(result) ? null : result;
    }
}

public enum PaymentStatus
{
    None,
    Unpaid,
    Partial,
    Paid
}

public class PersonListItem
{
    public int PersonId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public decimal AmountPaid { get; set; }
    public decimal AmountDue { get; set; }
    public decimal Balance { get; set; }
    public PaymentStatus Status { get; set; }
}

internal class PersonRow
{
    public int PersonId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Line1 { get; set; }
    public string? Line2 { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? PostalCode { get; set; }
    public decimal AmountPaid { get; set; }
    public decimal AmountDue { get; set; }
}
