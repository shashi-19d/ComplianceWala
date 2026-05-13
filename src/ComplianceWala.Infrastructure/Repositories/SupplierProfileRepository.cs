using ComplianceWala.Application.Interfaces;
using ComplianceWala.Domain.Entities;
using ComplianceWala.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ComplianceWala.Infrastructure.Repositories;

public sealed class SupplierProfileRepository : ISupplierProfileRepository
{
    private readonly AppDbContext _db;

    public SupplierProfileRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task<SupplierProfile?> GetByGstinAsync(
        string gstin,
        CancellationToken ct = default)
    {
        return await _db.SupplierProfiles
            .FirstOrDefaultAsync(s => s.Gstin == gstin.ToUpperInvariant(), ct);
    }

    public async Task<Dictionary<string, SupplierProfile?>> GetByGstinsAsync(
        IEnumerable<string> gstins,
        CancellationToken ct = default)
    {
        var normalizedGstins = gstins
            .Select(g => g.ToUpperInvariant())
            .ToList();

        // Single DB query for all GSTINs — not N separate queries
        var found = await _db.SupplierProfiles
            .Where(s => normalizedGstins.Contains(s.Gstin))
            .ToDictionaryAsync(s => s.Gstin, ct);

        // Return all requested GSTINs — null for those not in DB
        return normalizedGstins.ToDictionary(
            gstin => gstin,
            gstin => found.TryGetValue(gstin, out var profile)
                     ? profile
                     : (SupplierProfile?)null);
    }

    public async Task UpsertAsync(
        SupplierProfile profile,
        CancellationToken ct = default)
    {
        var existing = await _db.SupplierProfiles
            .FirstOrDefaultAsync(s => s.Gstin == profile.Gstin, ct);

        if (existing is null)
            _db.SupplierProfiles.Add(profile);
        else
            _db.Entry(existing).CurrentValues.SetValues(profile);

        await _db.SaveChangesAsync(ct);
    }

    public async Task UpsertBatchAsync(
        IEnumerable<SupplierProfile> profiles,
        CancellationToken ct = default)
    {
        var profileList = profiles.ToList();
        var gstins = profileList.Select(p => p.Gstin).ToList();

        var existing = await _db.SupplierProfiles
            .Where(s => gstins.Contains(s.Gstin))
            .ToDictionaryAsync(s => s.Gstin, ct);

        foreach (var profile in profileList)
        {
            if (existing.TryGetValue(profile.Gstin, out var tracked))
                _db.Entry(tracked).CurrentValues.SetValues(profile);
            else
                _db.SupplierProfiles.Add(profile);
        }

        // Single SaveChangesAsync = single transaction for all profiles
        await _db.SaveChangesAsync(ct);
    }
}