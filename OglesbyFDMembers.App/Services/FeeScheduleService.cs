using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using OglesbyFDMembers.Data;
using OglesbyFDMembers.Domain.Entities;

namespace OglesbyFDMembers.App.Services;

public class FeeScheduleService
{
    private readonly AppDbContext _db;

    public FeeScheduleService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<FeeSchedule?> GetAsync(int year, CancellationToken ct = default)
        => await _db.Set<FeeSchedule>().FirstOrDefaultAsync(f => f.Year == year, ct);

    public async Task<List<FeeSchedule>> ListAsync(CancellationToken ct = default)
        => await _db.Set<FeeSchedule>().OrderByDescending(f => f.Year).ToListAsync(ct);

    public async Task SetAsync(int year, decimal amount, CancellationToken ct = default)
    {
        if (year < 2000 || year > 3000) throw new ArgumentOutOfRangeException(nameof(year));
        if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));

        var existing = await _db.Set<FeeSchedule>().FirstOrDefaultAsync(f => f.Year == year, ct);
        if (existing == null)
        {
            _db.Add(new FeeSchedule { Year = year, AmountPerProperty = amount });
        }
        else
        {
            existing.AmountPerProperty = amount;
            _db.Update(existing);
        }
        await _db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var entity = await _db.Set<FeeSchedule>().FirstOrDefaultAsync(f => f.Id == id, ct);
        if (entity == null) return; // idempotent
        _db.Remove(entity);
        await _db.SaveChangesAsync(ct);
    }
}
