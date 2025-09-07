using System;
using System.Collections.Generic;
using System.Linq;
using System.ComponentModel.DataAnnotations;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using OglesbyFDMembers.Data;
using OglesbyFDMembers.Domain.Entities;

namespace OglesbyFDMembers.App.Services;

public class PeopleService
{
    private readonly AppDbContext _db;

    public PeopleService(AppDbContext db)
    {
        _db = db;
    }

    public async Task MakeAddressPrimaryAsync(int personId, int addressId, CancellationToken ct = default)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        var address = await _db.PersonAddresses
            .FirstOrDefaultAsync(a => a.Id == addressId && a.PersonId == personId, ct);
        if (address == null)
        {
            throw new InvalidOperationException("Address not found for person.");
        }

        if (!address.IsValidForMail)
        {
            throw new InvalidOperationException("Cannot set an address that is not in service as primary.");
        }

        // Idempotent: if already primary, still ensure all others are not
        var all = await _db.PersonAddresses
            .Where(a => a.PersonId == personId)
            .ToListAsync(ct);

        foreach (var a in all)
        {
            a.IsPrimary = a.Id == addressId;
        }

        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    public async Task MarkAddressNotInServiceAsync(int personId, int addressId, CancellationToken ct = default)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        var address = await _db.PersonAddresses
            .FirstOrDefaultAsync(a => a.Id == addressId && a.PersonId == personId, ct);
        if (address == null)
        {
            throw new InvalidOperationException("Address not found for person.");
        }

        if (!address.IsValidForMail && !address.IsPrimary)
        {
            // Already marked and not primary; nothing to do
            await tx.CommitAsync(ct);
            return;
        }

        var wasPrimary = address.IsPrimary;
        address.IsValidForMail = false;
        address.IsPrimary = false;

        await _db.SaveChangesAsync(ct);

        if (wasPrimary)
        {
            // Promote the most recent valid address as primary, if any
            var next = await _db.PersonAddresses
                .Where(a => a.PersonId == personId && a.IsValidForMail && a.Id != addressId)
                .OrderByDescending(a => a.CreatedUtc)
                .FirstOrDefaultAsync(ct);

            if (next != null)
            {
                next.IsPrimary = true;
                await _db.SaveChangesAsync(ct);
            }
        }

        await tx.CommitAsync(ct);
    }

    public async Task DeleteAddressAsync(int personId, int addressId, CancellationToken ct = default)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        var address = await _db.PersonAddresses
            .FirstOrDefaultAsync(a => a.Id == addressId && a.PersonId == personId, ct);
        if (address == null)
        {
            // Idempotent: consider already deleted
            await tx.CommitAsync(ct);
            return;
        }

        var wasPrimary = address.IsPrimary;
        _db.PersonAddresses.Remove(address);
        await _db.SaveChangesAsync(ct);

        if (wasPrimary)
        {
            // Promote the most recent valid address as primary, if any
            var next = await _db.PersonAddresses
                .Where(a => a.PersonId == personId && a.IsValidForMail)
                .OrderByDescending(a => a.CreatedUtc)
                .FirstOrDefaultAsync(ct);

            if (next != null)
            {
                next.IsPrimary = true;
                await _db.SaveChangesAsync(ct);
            }
        }

        await tx.CommitAsync(ct);
    }

    public async Task<int> AddAddressAsync(int personId, string line1, string? line2, string? city, string? state, string? postalCode, bool isPrimary, bool isValidForMail = true, CancellationToken ct = default)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        var personExists = await _db.People.AnyAsync(p => p.Id == personId, ct);
        if (!personExists)
        {
            throw new InvalidOperationException("Person not found.");
        }

        if (isPrimary && !isValidForMail)
        {
            throw new InvalidOperationException("Primary address must be valid for mail.");
        }

        if (isPrimary)
        {
            var existing = await _db.PersonAddresses.Where(a => a.PersonId == personId && a.IsPrimary).ToListAsync(ct);
            foreach (var a in existing)
            {
                a.IsPrimary = false;
            }
        }

        var entity = new PersonAddress
        {
            PersonId = personId,
            Line1 = line1.Trim(),
            Line2 = string.IsNullOrWhiteSpace(line2) ? null : line2.Trim(),
            City = string.IsNullOrWhiteSpace(city) ? null : city.Trim(),
            State = string.IsNullOrWhiteSpace(state) ? null : state.Trim(),
            PostalCode = string.IsNullOrWhiteSpace(postalCode) ? null : postalCode.Trim(),
            IsPrimary = isPrimary,
            IsValidForMail = isValidForMail,
            CreatedUtc = DateTime.UtcNow
        };

        _db.PersonAddresses.Add(entity);
        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return entity.Id;
    }

    public async Task<List<int>> GetAvailableYearsAsync(CancellationToken ct = default)
    {
        var existing = await _db.Assessments
            .Select(a => a.Year)
            .Distinct()
            .ToListAsync(ct);

        var nowYear = DateTime.UtcNow.Year;
        var baseYears = new List<int> { nowYear, nowYear + 1, nowYear + 2 };

        var combined = existing
            .Concat(baseYears)
            .Distinct()
            .OrderByDescending(y => y)
            .ToList();

        return combined;
    }

    public class CreatePropertyRequest
    {
        public string AddressLine1 { get; set; } = string.Empty;
        public string? AddressLine2 { get; set; }
        public string? City { get; set; }
        public string? State { get; set; }
        public string? Zip { get; set; }
        public bool Active { get; set; } = true;
        public DateTime? OwnershipStartDate { get; set; }
    }

    /// <summary>
    /// Adds or links a property to a person, creating the Property row if it doesn't exist
    /// and an Ownership starting today (or provided date). Idempotent for an existing open ownership.
    /// Returns the PropertyId.
    /// </summary>
    public async Task<int> AddPropertyToPersonAsync(int personId, CreatePropertyRequest req, CancellationToken ct = default)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        var addr1 = req.AddressLine1.Trim();
        var addr2 = string.IsNullOrWhiteSpace(req.AddressLine2) ? null : req.AddressLine2.Trim();
        var city = string.IsNullOrWhiteSpace(req.City) ? null : req.City!.Trim();
        var state = string.IsNullOrWhiteSpace(req.State) ? null : req.State!.Trim();
        var zip = string.IsNullOrWhiteSpace(req.Zip) ? null : req.Zip!.Trim();

        // Try to reuse existing property by exact address match
        var property = await _db.Properties
            .FirstOrDefaultAsync(p => p.AddressLine1 == addr1 && p.AddressLine2 == addr2 && p.City == city && p.State == state && p.Zip == zip, ct);

        if (property == null)
        {
            property = new Property
            {
                AddressLine1 = addr1,
                AddressLine2 = addr2,
                City = city,
                State = state,
                Zip = zip,
                Active = req.Active,
                CreatedUtc = DateTime.UtcNow
            };
            _db.Properties.Add(property);
            await _db.SaveChangesAsync(ct);
        }
        else
        {
            // Optionally update Active flag if changed
            if (property.Active != req.Active)
            {
                property.Active = req.Active;
                _db.Properties.Update(property);
                await _db.SaveChangesAsync(ct);
            }
        }

        // Ensure an open ownership does not already exist
        var today = DateTime.UtcNow.Date;
        var start = req.OwnershipStartDate?.Date ?? today;
        var hasOpen = await _db.Ownerships.AnyAsync(o => o.PersonId == personId && o.PropertyId == property.Id && (o.EndDate == null || o.EndDate >= today), ct);
        if (!hasOpen)
        {
            var ownership = new Ownership
            {
                PersonId = personId,
                PropertyId = property.Id,
                StartDate = start,
                EndDate = null
            };
            _db.Ownerships.Add(ownership);
            await _db.SaveChangesAsync(ct);
        }

        await tx.CommitAsync(ct);
        return property.Id;
    }

    public async Task<List<PersonListItem>> ListAsync(int year, string? search = null, CancellationToken ct = default)
    {
        var today = DateTime.UtcNow.Date;

        // Base query joins: People -> primary mailing address (latest CreatedUtc), left join ownerships as-of date -> assessments for given year.
        var query = from p in _db.People.AsNoTracking()
                    join a in _db.PersonAddresses.AsNoTracking().Where(a => a.IsPrimary) on p.Id equals a.PersonId into pa
                    from primary in pa.OrderByDescending(x => x.CreatedUtc).Take(1).DefaultIfEmpty()
                    join o in _db.Ownerships.AsNoTracking()
                        .Where(o => o.StartDate <= today && (o.EndDate == null || o.EndDate >= today))
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

            // Match by name/email/phone/primary mailing address
            var nameOrMailFilter = query.Where(x =>
                ((x.FirstName + " " + x.LastName).ToLower().Contains(s)) ||
                (x.Email != null && x.Email.ToLower().Contains(s)) ||
                (x.Phone != null && x.Phone.ToLower().Contains(s)) ||
                (x.Line1 != null && x.Line1.ToLower().Contains(s)) ||
                (x.Line2 != null && x.Line2.ToLower().Contains(s)) ||
                (x.City != null && x.City.ToLower().Contains(s)) ||
                (x.State != null && x.State.ToLower().Contains(s)) ||
                (x.PostalCode != null && x.PostalCode.ToLower().Contains(s))
            );

            // Additionally, match people who own a property with address matching the search (as-of date)
            var propertyMatches = await (from o in _db.Ownerships.AsNoTracking()
                                         join pr in _db.Properties.AsNoTracking() on o.PropertyId equals pr.Id
                                         where o.StartDate <= today && (o.EndDate == null || o.EndDate >= today)
                                         where (pr.AddressLine1 != null && EF.Functions.Like(pr.AddressLine1.ToLower(), $"%{s}%"))
                                               || (pr.AddressLine2 != null && EF.Functions.Like(pr.AddressLine2.ToLower(), $"%{s}%"))
                                               || (pr.City != null && EF.Functions.Like(pr.City.ToLower(), $"%{s}%"))
                                               || (pr.State != null && EF.Functions.Like(pr.State.ToLower(), $"%{s}%"))
                                               || (pr.Zip != null && EF.Functions.Like(pr.Zip.ToLower(), $"%{s}%"))
                                         select o.PersonId)
                .Distinct()
                .ToListAsync(ct);

            // Combine filters
            query = nameOrMailFilter
                .Union(query.Where(x => propertyMatches.Contains(x.PersonId)));
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

    public async Task<PersonDetails?> GetDetailsAsync(int personId, CancellationToken ct = default)
    {
        var person = await _db.People.AsNoTracking()
            .Where(p => p.Id == personId)
            .Select(p => new
            {
                p.Id,
                p.FirstName,
                p.LastName,
                p.Email,
                p.Phone,
                p.Notes,
                p.Active,
                p.CreatedUtc
            })
            .SingleOrDefaultAsync(ct);

        if (person == null) return null;

        var addresses = await _db.PersonAddresses.AsNoTracking()
            .Where(a => a.PersonId == personId)
            .OrderByDescending(a => a.IsPrimary)
            .ThenByDescending(a => a.CreatedUtc)
            .Select(a => new MailingAddress
            {
                Id = a.Id,
                Line1 = a.Line1,
                Line2 = a.Line2,
                City = a.City,
                State = a.State,
                PostalCode = a.PostalCode,
                IsPrimary = a.IsPrimary,
                IsValidForMail = a.IsValidForMail,
                CreatedUtc = a.CreatedUtc
            })
            .ToListAsync(ct);

        var today = DateTime.UtcNow.Date;
        var properties = await (from o in _db.Ownerships.AsNoTracking()
                                join pr in _db.Properties.AsNoTracking() on o.PropertyId equals pr.Id
                                where o.PersonId == personId && o.StartDate <= today && (o.EndDate == null || o.EndDate >= today)
                                orderby pr.AddressLine1, pr.City
                                select new OwnedProperty
                                {
                                    PropertyId = pr.Id,
                                    AddressLine1 = pr.AddressLine1,
                                    AddressLine2 = pr.AddressLine2,
                                    City = pr.City,
                                    State = pr.State,
                                    Zip = pr.Zip,
                                    Active = pr.Active,
                                    OwnershipStart = o.StartDate,
                                    OwnershipEnd = o.EndDate
                                })
            .ToListAsync(ct);

        // Populate current-year amounts for each owned property
        var year = DateTime.UtcNow.Year;
        var propertyIds = properties.Select(p => p.PropertyId).Distinct().ToList();
        if (propertyIds.Count > 0)
        {
            var sums = await _db.Assessments.AsNoTracking()
                .Where(a => a.Year == year && propertyIds.Contains(a.PropertyId))
                .GroupBy(a => a.PropertyId)
                .Select(g => new { PropertyId = g.Key, AmountPaid = g.Sum(x => x.AmountPaid), AmountDue = g.Sum(x => x.AmountDue) })
                .ToListAsync(ct);
            var dict = sums.ToDictionary(x => x.PropertyId, x => x);
            foreach (var p in properties)
            {
                if (dict.TryGetValue(p.PropertyId, out var s))
                {
                    p.CurrentYearAmountPaid = s.AmountPaid;
                    p.CurrentYearAmountDue = s.AmountDue;
                    p.CurrentYearBalance = s.AmountDue - s.AmountPaid;
                }
                else
                {
                    p.CurrentYearAmountPaid = 0m;
                    p.CurrentYearAmountDue = 0m;
                    p.CurrentYearBalance = 0m;
                }
            }
        }

        // Compute current-year paid/owed across currently owned properties
        var currentPaid = properties.Sum(p => p.CurrentYearAmountPaid);
        var currentOwed = properties.Sum(p => p.CurrentYearBalance);

        return new PersonDetails
        {
            PersonId = person.Id,
            FirstName = person.FirstName,
            LastName = person.LastName,
            Email = person.Email,
            Phone = person.Phone,
            Notes = person.Notes,
            Active = person.Active,
            CreatedUtc = person.CreatedUtc,
            Addresses = addresses,
            Properties = properties,
            CurrentYearPaid = currentPaid,
            CurrentYearOwed = currentOwed
        };
    }

    public async Task UpdatePersonAsync(int personId, UpdatePersonRequest req, CancellationToken ct = default)
    {
        var person = await _db.People.FirstOrDefaultAsync(p => p.Id == personId, ct);
        if (person == null) throw new InvalidOperationException("Person not found.");

        var first = (req.FirstName ?? string.Empty).Trim();
        var last = (req.LastName ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(first) || string.IsNullOrWhiteSpace(last))
            throw new ValidationException("First and Last name are required.");

        person.FirstName = first;
        person.LastName = last;
        person.Email = string.IsNullOrWhiteSpace(req.Email) ? null : req.Email.Trim();
        person.Phone = string.IsNullOrWhiteSpace(req.Phone) ? null : req.Phone.Trim();
        person.Notes = string.IsNullOrWhiteSpace(req.Notes) ? null : req.Notes.Trim();
        person.Active = req.Active;

        await _db.SaveChangesAsync(ct);
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

public class PersonDetails
{
    public int PersonId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Notes { get; set; }
    public bool Active { get; set; }
    public DateTime CreatedUtc { get; set; }
    public List<MailingAddress> Addresses { get; set; } = new();
    public List<OwnedProperty> Properties { get; set; } = new();
    public string FullName => (FirstName + " " + LastName).Trim();

    // Current-year summary
    public decimal CurrentYearPaid { get; set; }
    public decimal CurrentYearOwed { get; set; }
}

public class UpdatePersonRequest
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Notes { get; set; }
    public bool Active { get; set; }
}


public class MailingAddress
{
    public int Id { get; set; }
    public string Line1 { get; set; } = string.Empty;
    public string? Line2 { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? PostalCode { get; set; }
    public bool IsPrimary { get; set; }
    public bool IsValidForMail { get; set; }
    public DateTime CreatedUtc { get; set; }
}

public class OwnedProperty
{
    public int PropertyId { get; set; }
    public string AddressLine1 { get; set; } = string.Empty;
    public string? AddressLine2 { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? Zip { get; set; }
    public bool Active { get; set; }
    public DateTime OwnershipStart { get; set; }
    public DateTime? OwnershipEnd { get; set; }

    // Current-year amounts for this property
    public decimal CurrentYearAmountPaid { get; set; }
    public decimal CurrentYearAmountDue { get; set; }
    public decimal CurrentYearBalance { get; set; }
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
