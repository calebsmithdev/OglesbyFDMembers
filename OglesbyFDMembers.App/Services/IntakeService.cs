using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using OglesbyFDMembers.Data;
using OglesbyFDMembers.Domain.Entities;
using OglesbyFDMembers.Domain.Enums;

namespace OglesbyFDMembers.App.Services;

public class IntakeService
{
    private readonly AppDbContext _db;

    public IntakeService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<int> CreateAsync(IntakeRequest request, CancellationToken ct = default)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        // Person
        var person = new Person
        {
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim(),
            Phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim(),
            Active = true,
            CreatedUtc = DateTime.UtcNow
        };
        _db.People.Add(person);
        await _db.SaveChangesAsync(ct);

        // Address (primary)
        var address = new PersonAddress
        {
            PersonId = person.Id,
            Line1 = request.AddressLine1.Trim(),
            Line2 = string.IsNullOrWhiteSpace(request.AddressLine2) ? null : request.AddressLine2.Trim(),
            City = string.IsNullOrWhiteSpace(request.City) ? null : request.City!.Trim(),
            State = string.IsNullOrWhiteSpace(request.State) ? null : request.State!.Trim(),
            PostalCode = string.IsNullOrWhiteSpace(request.PostalCode) ? null : request.PostalCode!.Trim(),
            IsPrimary = true,
            IsValidForMail = true,
            CreatedUtc = DateTime.UtcNow
        };
        _db.PersonAddresses.Add(address);
        await _db.SaveChangesAsync(ct);

        // Property (find by parcel if exists)
        Property property;
        var parcel = request.ParcelNumber.Trim();
        property = await _db.Properties.FirstOrDefaultAsync(p => p.ParcelNumber == parcel, ct)
                   ?? new Property
                   {
                       ParcelNumber = parcel,
                       SitusAddress = (request.PropertySameAsAddress ? request.AddressLine1 : request.PropertySitusAddress)!.Trim(),
                       Active = true,
                       CreatedUtc = DateTime.UtcNow
                   };
        if (property.Id == 0)
        {
            _db.Properties.Add(property);
            await _db.SaveChangesAsync(ct);
        }

        // Ownership
        var ownership = new Ownership
        {
            PersonId = person.Id,
            PropertyId = property.Id,
            StartDate = DateTime.UtcNow.Date
        };
        _db.Ownerships.Add(ownership);
        await _db.SaveChangesAsync(ct);

        // Optional payment
        if (request.HasPaid)
        {
            // Resolve amount using current-year FeeSchedule, default 0 if not configured
            var year = request.AssessmentYear ?? DateTime.UtcNow.Year;
            var fee = await _db.Set<FeeSchedule>().Where(f => f.Year == year).Select(f => f.AmountPerProperty).FirstOrDefaultAsync(ct);

            var payment = new Payment
            {
                PersonId = person.Id,
                PaymentType = request.PaymentType ?? PaymentType.Cash,
                Amount = fee,
                PaidUtc = request.PaidDateUtc ?? DateTime.UtcNow,
                CheckNumber = string.IsNullOrWhiteSpace(request.CheckNumber) ? null : request.CheckNumber.Trim(),
                IsDonation = false,
                Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes!.Trim()
            };
            _db.Payments.Add(payment);
            await _db.SaveChangesAsync(ct);

            // Allocation will be handled by Allocation Wizard later.
        }

        await tx.CommitAsync(ct);
        return person.Id;
    }
}

public class IntakeRequest
{
    // Person
    [Required, MaxLength(100)] public string FirstName { get; set; } = string.Empty;
    [Required, MaxLength(100)] public string LastName { get; set; } = string.Empty;
    [EmailAddress, MaxLength(200)] public string? Email { get; set; }
    [MaxLength(32)] public string? Phone { get; set; }

    // Mailing Address
    [Required, MaxLength(200)] public string AddressLine1 { get; set; } = string.Empty;
    [MaxLength(200)] public string? AddressLine2 { get; set; }
    [MaxLength(100)] public string? City { get; set; }
    [MaxLength(64)] public string? State { get; set; }
    [MaxLength(32)] public string? PostalCode { get; set; }

    // Property
    public bool PropertySameAsAddress { get; set; } = true;
    [Required, MaxLength(64)] public string ParcelNumber { get; set; } = string.Empty;
    [MaxLength(200)] public string? PropertySitusAddress { get; set; }

    // Payment
    public bool HasPaid { get; set; }
    public PaymentType? PaymentType { get; set; }
    public string? CheckNumber { get; set; }
    public DateTime? PaidDateUtc { get; set; }
    public string? Notes { get; set; }

    // Optional year override for fee lookup
    public int? AssessmentYear { get; set; }
}

