using ComplianceWala.Application.Interfaces;
using ComplianceWala.Domain.Entities;

namespace ComplianceWala.Tests.Fakes;

/// <summary>
/// In-memory implementation of ISupplierProfileRepository for testing.
/// 
/// FAKE vs MOCK:
/// Mock: a framework-generated object that records calls (Moq, NSubstitute).
/// Fake: a real, working implementation with simplified backing store.
/// 
/// We use a Fake here because:
/// 1. It tests the real interaction logic, not just "was method called?"
/// 2. No framework dependency — pure C#
/// 3. The test reads like production code
/// </summary>
public sealed class FakeSupplierProfileRepository : ISupplierProfileRepository
{
    // The "database" — a simple in-memory dictionary
    private readonly Dictionary<string, SupplierProfile> _store = new(
        StringComparer.OrdinalIgnoreCase);

    public Task<SupplierProfile?> GetByGstinAsync(string gstin, CancellationToken ct = default)
    {
        _store.TryGetValue(gstin, out var profile);
        return Task.FromResult(profile);
    }

    public Task<Dictionary<string, SupplierProfile?>> GetByGstinsAsync(
        IEnumerable<string> gstins,
        CancellationToken ct = default)
    {
        var result = gstins.ToDictionary(
            gstin => gstin,
            gstin => _store.TryGetValue(gstin, out var p)
                     ? p
                     : (SupplierProfile?)null,
            StringComparer.OrdinalIgnoreCase);

        return Task.FromResult(result);
    }

    public Task UpsertAsync(SupplierProfile profile, CancellationToken ct = default)
    {
        _store[profile.Gstin] = profile;
        return Task.CompletedTask;
    }

    public Task UpsertBatchAsync(
        IEnumerable<SupplierProfile> profiles,
        CancellationToken ct = default)
    {
        foreach (var profile in profiles)
            _store[profile.Gstin] = profile;

        return Task.CompletedTask;
    }

    // ── Test helper methods ───────────────────────────────────────

    /// <summary>Seeds the fake with an existing profile (simulates prior history).</summary>
    public void Seed(SupplierProfile profile) => _store[profile.Gstin] = profile;

    /// <summary>Returns how many profiles are currently stored.</summary>
    public int Count => _store.Count;
}