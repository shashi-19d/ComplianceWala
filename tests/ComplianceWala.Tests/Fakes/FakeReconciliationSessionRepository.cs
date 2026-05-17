using ComplianceWala.Application.Interfaces;
using ComplianceWala.Domain.Entities;

namespace ComplianceWala.Tests.Fakes;

public sealed class FakeReconciliationSessionRepository
    : IReconciliationSessionRepository
{
    private readonly List<ReconciliationSession> _store = new();

    public Task<ReconciliationSession?> GetByIdAsync(
        Guid id, CancellationToken ct = default)
    {
        var session = _store.FirstOrDefault(s => s.Id == id);
        return Task.FromResult(session);
    }

    public Task<IReadOnlyList<ReconciliationSession>> GetByBusinessGstinAsync(
        string businessGstin, CancellationToken ct = default)
    {
        IReadOnlyList<ReconciliationSession> result = _store
            .Where(s => s.BusinessGstin == businessGstin)
            .OrderByDescending(s => s.CreatedAt)
            .ToList();
        return Task.FromResult(result);
    }

    public Task AddAsync(
        ReconciliationSession session, CancellationToken ct = default)
    {
        _store.Add(session);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(
        ReconciliationSession session, CancellationToken ct = default)
    {
        var index = _store.FindIndex(s => s.Id == session.Id);
        if (index >= 0) _store[index] = session;
        return Task.CompletedTask;
    }

    public void Seed(ReconciliationSession session) => _store.Add(session);
    public int Count => _store.Count;
}