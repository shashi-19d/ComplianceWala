using ComplianceWala.Domain.Entities;

namespace ComplianceWala.Application.Interfaces;

/// <summary>
/// Core reconciliation contract.
/// Compares invoices in a ReconciliationSession and 
/// populates it with detected MismatchRecords.
///
/// WHY IN APPLICATION LAYER?
/// This is a use case — it orchestrates domain objects.
/// It has no knowledge of HTTP, databases, or JSON.
/// It only knows about domain entities and domain rules.
/// </summary>
public interface IReconciliationEngine
{
    /// <summary>
    /// Runs the full reconciliation algorithm on a session.
    /// Pre-condition:  session.Status == ReadyForReconciliation
    /// Post-condition: session.Status == Completed, 
    ///                 session.Mismatches populated
    /// </summary>
    Task<ReconciliationResult> ReconcileAsync(
        ReconciliationSession session,
        IEnumerable<SupplierProfile> supplierProfiles,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Summary result of a completed reconciliation run.
/// Returned to the API layer for the HTTP response.
/// </summary>
public record ReconciliationResult(
    Guid SessionId,
    string FilingPeriod,
    int TotalPurchaseRegisterInvoices,
    int TotalGstr2bInvoices,
    int TotalMismatches,
    int MatchedInvoices,
    decimal TotalItcInBooks,
    decimal TotalItcInGstr2b,
    decimal TotalItcAtRisk,
    TimeSpan ProcessingTime
);