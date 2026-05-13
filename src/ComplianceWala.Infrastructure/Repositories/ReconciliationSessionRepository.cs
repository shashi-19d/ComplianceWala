using ComplianceWala.Application.Interfaces;
using ComplianceWala.Domain.Entities;
using ComplianceWala.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ComplianceWala.Infrastructure.Repositories;

public sealed class ReconciliationSessionRepository : IReconciliationSessionRepository
{
    private readonly AppDbContext _db;

    public ReconciliationSessionRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task<ReconciliationSession?> GetByIdAsync(
        Guid id,
        CancellationToken ct = default)
    {
        return await _db.ReconciliationSessions
            .Include(s => s.PurchaseRegisterInvoices)
            .Include(s => s.Gstr2bInvoices)
            .Include(s => s.Mismatches)
            .FirstOrDefaultAsync(s => s.Id == id, ct);
    }

    public async Task<IReadOnlyList<ReconciliationSession>> GetByBusinessGstinAsync(
        string businessGstin,
        CancellationToken ct = default)
    {
        return await _db.ReconciliationSessions
            .Where(s => s.BusinessGstin == businessGstin.ToUpperInvariant())
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task AddAsync(
        ReconciliationSession session,
        CancellationToken ct = default)
    {
        _db.ReconciliationSessions.Add(session);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(
        ReconciliationSession session,
        CancellationToken ct = default)
    {
        _db.ReconciliationSessions.Update(session);
        await _db.SaveChangesAsync(ct);
    }
}