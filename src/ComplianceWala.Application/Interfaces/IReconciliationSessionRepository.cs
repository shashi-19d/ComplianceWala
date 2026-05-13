using ComplianceWala.Domain.Entities;

namespace ComplianceWala.Application.Interfaces;

public interface IReconciliationSessionRepository
{
    Task<ReconciliationSession?> GetByIdAsync(
        Guid id,
        CancellationToken ct = default);

    /// <summary>
    /// Returns all sessions for a specific business GSTIN,
    /// ordered by creation date descending (newest first).
    /// </summary>
    Task<IReadOnlyList<ReconciliationSession>> GetByBusinessGstinAsync(
        string businessGstin,
        CancellationToken ct = default);

    Task AddAsync(
        ReconciliationSession session,
        CancellationToken ct = default);

    Task UpdateAsync(
        ReconciliationSession session,
        CancellationToken ct = default);
}