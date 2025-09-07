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

public class PaymentsService
{
    private readonly AppDbContext _db;

    public PaymentsService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<List<PaymentListItem>> ListByPersonAsync(int personId, CancellationToken ct = default)
    {
        var items = await _db.Payments.AsNoTracking()
            .Where(p => p.PersonId == personId)
            .OrderByDescending(p => p.PaidUtc)
            .Select(p => new PaymentListItem
            {
                Id = p.Id,
                PersonId = p.PersonId,
                Amount = p.Amount,
                PaidUtc = p.PaidUtc,
                PaymentType = p.PaymentType,
                CheckNumber = p.CheckNumber,
                IsDonation = p.IsDonation,
                Notes = p.Notes,
                TargetPropertyId = p.TargetPropertyId,
                TargetYear = p.TargetYear
            })
            .ToListAsync(ct);

        // Compute YearDisplay from allocations' assessment years
        var paymentIds = items.Select(i => i.Id).ToList();
        if (paymentIds.Count > 0)
        {
            var pairs = await (from pa in _db.PaymentAllocations.AsNoTracking()
                               join a in _db.Assessments.AsNoTracking() on pa.AssessmentId equals a.Id
                               where paymentIds.Contains(pa.PaymentId)
                               select new { pa.PaymentId, a.Year })
                .ToListAsync(ct);

            var dict = pairs
                .GroupBy(x => x.PaymentId)
                .ToDictionary(g => g.Key, g => g.Select(x => x.Year).Distinct().OrderBy(y => y).ToList());

            foreach (var i in items)
            {
                if (dict.TryGetValue(i.Id, out var years))
                {
                    i.YearDisplay = years.Count switch
                    {
                        0 => "—",
                        1 => years[0].ToString(),
                        _ => "Multiple"
                    };
                }
                else
                {
                    // No allocations. If a target year is present, show it as pending.
                    i.YearDisplay = i.TargetYear.HasValue ? $"Pending {i.TargetYear}" : "—";
                }
            }
        }

        // Fill AppliedTo display: Donation | Split | AddressLine1
        var targetedIds = items.Where(i => i.TargetPropertyId.HasValue).Select(i => i.TargetPropertyId!.Value).Distinct().ToList();
        var addr = new Dictionary<int, string>();
        if (targetedIds.Count > 0)
        {
            addr = await _db.Properties.AsNoTracking()
                .Where(p => targetedIds.Contains(p.Id))
                .Select(p => new { p.Id, p.AddressLine1 })
                .ToDictionaryAsync(x => x.Id, x => x.AddressLine1, ct);
        }

        foreach (var i in items)
        {
            if (i.IsDonation)
            {
                i.AppliedTo = "Donation";
            }
            else if (i.TargetPropertyId is int tid && addr.TryGetValue(tid, out var line1))
            {
                i.AppliedTo = line1;
            }
            else
            {
                i.AppliedTo = "Split";
            }
        }

        return items;
    }

    public async Task<int> CreateAsync(AddPaymentRequest request, CancellationToken ct = default)
    {
        if (request.Amount <= 0) throw new ValidationException("Amount must be greater than 0.");
        if (await _db.People.AnyAsync(p => p.Id == request.PersonId, ct) == false)
            throw new ValidationException("Person not found.");

        var paidUtc = request.PaidDateUtc ?? DateTime.UtcNow;

        var payment = new Payment
        {
            PersonId = request.PersonId,
            Amount = Math.Round(request.Amount, 2, MidpointRounding.AwayFromZero),
            PaymentType = request.PaymentType,
            PaidUtc = paidUtc,
            CheckNumber = string.IsNullOrWhiteSpace(request.CheckNumber) ? null : request.CheckNumber.Trim(),
            IsDonation = request.IsDonation,
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes!.Trim()
        };

        _db.Payments.Add(payment);
        await _db.SaveChangesAsync(ct);

        // Allocation will be handled separately by Allocation Wizard.
        return payment.Id;
    }

    public async Task<int> CreateAndAllocateAsync(AddPaymentRequest request, CancellationToken ct = default)
    {
        if (request.Amount <= 0) throw new ValidationException("Amount must be greater than 0.");
        if (await _db.People.AnyAsync(p => p.Id == request.PersonId, ct) == false)
            throw new ValidationException("Person not found.");

        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        var paymentId = await CreateAsync(request, ct);

        if (!request.IsDonation)
        {
            var mode = request.AllocationMode ?? AllocationMode.SplitAcrossAll;
            var year = request.AllocationYear ?? DateTime.UtcNow.Year;
            var currentYear = DateTime.UtcNow.Year;

            // If the selected year is in the future, record intent and skip allocation now.
            if (year > currentYear)
            {
                var payment = await _db.Payments.FirstAsync(p => p.Id == paymentId, ct);
                payment.TargetYear = year;
                // For future-year single targeting we would need a property selection.
                // Current UI only supports splitting when no assessments exist; keep TargetPropertyId null.
                await _db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
                return paymentId;
            }

            if (mode == AllocationMode.SingleAssessment)
            {
                if (request.TargetAssessmentId is null)
                    throw new ValidationException("Target assessment must be selected.");

                var assess = await _db.Assessments.FirstOrDefaultAsync(a => a.Id == request.TargetAssessmentId, ct);
                if (assess == null) throw new ValidationException("Assessment not found.");

                // Mark payment as targeted to this property
                var payment = await _db.Payments.FirstAsync(p => p.Id == paymentId, ct);
                payment.TargetPropertyId = assess.PropertyId;
                await _db.SaveChangesAsync(ct);

                var bal = Math.Max(0, assess.AmountDue - assess.AmountPaid);
                var allocAmt = Math.Min(bal, request.Amount);
                if (allocAmt > 0)
                {
                    _db.PaymentAllocations.Add(new PaymentAllocation
                    {
                        PaymentId = paymentId,
                        AssessmentId = assess.Id,
                        Amount = allocAmt
                    });
                    assess.AmountPaid += allocAmt;
                    await _db.SaveChangesAsync(ct);
                }
            }
            else
            {
                // Split across all open assessments for the selected year for properties owned by the person today
                var today = DateTime.UtcNow.Date;
                var openAss = await (from o in _db.Ownerships
                                     where o.PersonId == request.PersonId && o.StartDate <= today && (o.EndDate == null || o.EndDate >= today)
                                     join s in _db.Assessments.Where(a => a.Year == year) on o.PropertyId equals s.PropertyId
                                     select s).Distinct().ToListAsync(ct);

                var withBalance = openAss
                    .Select(a => new { A = a, Balance = Math.Max(0, a.AmountDue - a.AmountPaid) })
                    .Where(x => x.Balance > 0)
                    .ToList();

                var amount = request.Amount;
                var totalBalance = withBalance.Sum(x => x.Balance);

                if (withBalance.Count > 0 && totalBalance > 0)
                {
                    if (amount >= totalBalance)
                    {
                        // Exact/over: allocate up to each balance
                        foreach (var x in withBalance)
                        {
                            if (x.Balance <= 0) continue;
                            _db.PaymentAllocations.Add(new PaymentAllocation
                            {
                                PaymentId = paymentId,
                                AssessmentId = x.A.Id,
                                Amount = x.Balance
                            });
                            x.A.AmountPaid += x.Balance;
                        }
                    }
                    else
                    {
                        // Proportional split by balance; fix rounding on last
                        decimal allocated = 0;
                        for (int i = 0; i < withBalance.Count; i++)
                        {
                            var x = withBalance[i];
                            decimal alloc;
                            if (i == withBalance.Count - 1)
                            {
                                alloc = amount - allocated; // remainder
                            }
                            else
                            {
                                alloc = Math.Round(amount * (x.Balance / totalBalance), 2, MidpointRounding.AwayFromZero);
                            }

                            alloc = Math.Min(alloc, x.Balance);
                            if (alloc <= 0) continue;

                            _db.PaymentAllocations.Add(new PaymentAllocation
                            {
                                PaymentId = paymentId,
                                AssessmentId = x.A.Id,
                                Amount = alloc
                            });
                            x.A.AmountPaid += alloc;
                            allocated += alloc;
                        }
                    }

                    await _db.SaveChangesAsync(ct);
                }
            }
        }

        await tx.CommitAsync(ct);
        return paymentId;
    }

    /// <summary>
    /// Idempotently allocate pending payments for the specified year.
    /// Pending payments are those with TargetYear == year, IsDonation == false,
    /// and no existing allocations. Allocates using the same split logic
    /// across open assessments for the person for that year.
    /// </summary>
    public async Task<int> AllocatePendingForYearAsync(int year, CancellationToken ct = default)
    {
        var today = DateTime.UtcNow.Date;

        // Find payments that target this year and are not yet allocated
        var pending = await _db.Payments
            .Where(p => p.TargetYear == year && !p.IsDonation)
            .Select(p => new { p.Id, p.PersonId })
            .ToListAsync(ct);

        if (pending.Count == 0) return 0;

        // Exclude those with any existing allocations
        var pendingIds = pending.Select(p => p.Id).ToList();
        var allocatedIds = await _db.PaymentAllocations
            .Where(a => pendingIds.Contains(a.PaymentId))
            .Select(a => a.PaymentId)
            .Distinct()
            .ToListAsync(ct);

        var toApply = pending.Where(p => !allocatedIds.Contains(p.Id)).ToList();
        if (toApply.Count == 0) return 0;

        int appliedCount = 0;

        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        foreach (var p in toApply)
        {
            var payment = await _db.Payments.FirstAsync(x => x.Id == p.Id, ct);

            // Split across all open assessments for the person for the given year (based on ownership as-of today)
            var openAss = await (from o in _db.Ownerships
                                 where o.PersonId == p.PersonId && o.StartDate <= today && (o.EndDate == null || o.EndDate >= today)
                                 join s in _db.Assessments.Where(a => a.Year == year) on o.PropertyId equals s.PropertyId
                                 select s).Distinct().ToListAsync(ct);

            var withBalance = openAss
                .Select(a => new { A = a, Balance = Math.Max(0, a.AmountDue - a.AmountPaid) })
                .Where(x => x.Balance > 0)
                .ToList();

            if (withBalance.Count == 0)
            {
                // No assessments or all paid: leave as pending
                continue;
            }

            var amount = payment.Amount;
            var totalBalance = withBalance.Sum(x => x.Balance);

            if (amount >= totalBalance)
            {
                foreach (var x in withBalance)
                {
                    if (x.Balance <= 0) continue;
                    _db.PaymentAllocations.Add(new PaymentAllocation
                    {
                        PaymentId = payment.Id,
                        AssessmentId = x.A.Id,
                        Amount = x.Balance
                    });
                    x.A.AmountPaid += x.Balance;
                }
            }
            else
            {
                decimal allocated = 0;
                for (int i = 0; i < withBalance.Count; i++)
                {
                    var x = withBalance[i];
                    decimal alloc;
                    if (i == withBalance.Count - 1)
                    {
                        alloc = amount - allocated; // remainder
                    }
                    else
                    {
                        alloc = Math.Round(amount * (x.Balance / totalBalance), 2, MidpointRounding.AwayFromZero);
                    }

                    alloc = Math.Min(alloc, x.Balance);
                    if (alloc <= 0) continue;

                    _db.PaymentAllocations.Add(new PaymentAllocation
                    {
                        PaymentId = payment.Id,
                        AssessmentId = x.A.Id,
                        Amount = alloc
                    });
                    x.A.AmountPaid += alloc;
                    allocated += alloc;
                }
            }

            // Clear the target year now that it has been applied
            payment.TargetYear = null;
            appliedCount++;

            await _db.SaveChangesAsync(ct);
        }

        await tx.CommitAsync(ct);
        return appliedCount;
    }

    public async Task<List<AssessmentOption>> ListAssessmentOptionsAsync(int personId, int? year = null, CancellationToken ct = default)
    {
        var y = year ?? DateTime.UtcNow.Year;
        var today = DateTime.UtcNow.Date;

        var query = from o in _db.Ownerships.AsNoTracking()
                    where o.PersonId == personId && o.StartDate <= today && (o.EndDate == null || o.EndDate >= today)
                    join s in _db.Assessments.AsNoTracking().Where(a => a.Year == y) on o.PropertyId equals s.PropertyId
                    join pr in _db.Properties.AsNoTracking() on s.PropertyId equals pr.Id
                    select new { s, pr };

        var raw = await query
            .OrderBy(x => x.pr.AddressLine1)
            .Select(x => new
            {
                AssessmentId = x.s.Id,
                PropertyId = x.s.PropertyId,
                Line1 = x.pr.AddressLine1,
                Line2 = x.pr.AddressLine2,
                City = x.pr.City,
                State = x.pr.State,
                Zip = x.pr.Zip,
                AmountDue = x.s.AmountDue,
                AmountPaid = x.s.AmountPaid
            })
            .ToListAsync(ct);

        var list = raw.Select(r => new AssessmentOption
        {
            AssessmentId = r.AssessmentId,
            PropertyId = r.PropertyId,
            Address = string.Join(", ", new[] { r.Line1, r.Line2 }.Where(t => !string.IsNullOrWhiteSpace(t))),
            City = r.City,
            State = r.State,
            Zip = r.Zip,
            AmountDue = r.AmountDue,
            AmountPaid = r.AmountPaid,
            Balance = Math.Max(0, r.AmountDue - r.AmountPaid)
        }).ToList();

        return list;
    }
}

public class PaymentListItem
{
    public int Id { get; set; }
    public int PersonId { get; set; }
    public decimal Amount { get; set; }
    public DateTime PaidUtc { get; set; }
    public PaymentType PaymentType { get; set; }
    public string? CheckNumber { get; set; }
    public bool IsDonation { get; set; }
    public string? Notes { get; set; }
    public string? YearDisplay { get; set; }
    public int? TargetPropertyId { get; set; }
    public string? AppliedTo { get; set; }
    public int? TargetYear { get; set; }
}

public class AddPaymentRequest
{
    [Required]
    public int PersonId { get; set; }

    [Required]
    public decimal Amount { get; set; }

    [Required]
    public PaymentType PaymentType { get; set; } = PaymentType.Cash;

    public DateTime? PaidDateUtc { get; set; }

    public string? CheckNumber { get; set; }
    public bool IsDonation { get; set; }
    public string? Notes { get; set; }

    public AllocationMode? AllocationMode { get; set; }
    public int? TargetAssessmentId { get; set; }
    public int? AllocationYear { get; set; }
}

public enum AllocationMode
{
    SplitAcrossAll = 0,
    SingleAssessment = 1,
}

public class AssessmentOption
{
    public int AssessmentId { get; set; }
    public int PropertyId { get; set; }
    public string Address { get; set; } = string.Empty;
    public string? City { get; set; }
    public string? State { get; set; }
    public string? Zip { get; set; }
    public decimal AmountDue { get; set; }
    public decimal AmountPaid { get; set; }
    public decimal Balance { get; set; }
}
