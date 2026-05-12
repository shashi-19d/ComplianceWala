using ComplianceWala.Domain.Entities;

namespace ComplianceWala.Application.Interfaces;

/// <summary>
/// Data access contract for SupplierProfile persistence.
/// 
/// WHY DEFINED IN APPLICATION, NOT INFRASTRUCTURE?
/// The Application layer owns the USE CASE. The use case says
/// "I need to get and save supplier profiles." It doesn't care
/// if storage is PostgreSQL, Redis, or an in-memory dictionary.
/// Infrastructure implements this contract on Day 7.
/// 
/// This is Dependency Inversion in practice:
/// High-level (Application) defines the contract.
/// Low-level (Infrastructure) fulfills it.
/// </summary>
public interface ISupplierProfileRepository
{
    /// <summary>
    /// Retrieves a supplier profile by GSTIN.
    /// Returns null if no history exists yet for this supplier.
    /// </summary>
    Task<SupplierProfile?> GetByGstinAsync(
        string gstin,
        CancellationToken ct = default);

    /// <summary>
    /// Retrieves profiles for multiple suppliers in a single call.
    /// More efficient than calling GetByGstinAsync in a loop.
    /// Key = GSTIN, Value = profile (null if not found).
    /// </summary>
    Task<Dictionary<string, SupplierProfile?>> GetByGstinsAsync(
        IEnumerable<string> gstins,
        CancellationToken ct = default);

    /// <summary>
    /// Persists a supplier profile — creates if new, updates if exists.
    /// </summary>
    Task UpsertAsync(
        SupplierProfile profile,
        CancellationToken ct = default);

    /// <summary>
    /// Persists multiple profiles in a single transaction.
    /// Used after processing a full GSTR-2B file.
    /// </summary>
    Task UpsertBatchAsync(
        IEnumerable<SupplierProfile> profiles,
        CancellationToken ct = default);
}