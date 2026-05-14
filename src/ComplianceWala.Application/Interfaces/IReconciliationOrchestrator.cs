using ComplianceWala.Application.DTOs.Requests;
using ComplianceWala.Application.DTOs.Responses;

namespace ComplianceWala.Application.Interfaces;

public interface IReconciliationOrchestrator
{
    /// <summary>
    /// Creates a new reconciliation session for a business GSTIN.
    /// </summary>
    Task<SessionResponse> CreateSessionAsync(
        CreateSessionRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// Uploads purchase register (GSTR-1 format) to an existing session.
    /// </summary>
    Task<SessionResponse> UploadPurchaseRegisterAsync(
        Guid sessionId,
        UploadGstrRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// Uploads GSTR-2B JSON and triggers full reconciliation pipeline:
    /// Parse → Risk Score → Reconcile → Classify → Save.
    /// This is the main workflow trigger.
    /// </summary>
    Task<ReconciliationCompleteResponse> UploadGstr2bAndReconcileAsync(
        Guid sessionId,
        UploadGstrRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// Returns a completed session with all classified mismatches.
    /// </summary>
    Task<ReconciliationCompleteResponse?> GetSessionResultAsync(
        Guid sessionId,
        CancellationToken ct = default);
}