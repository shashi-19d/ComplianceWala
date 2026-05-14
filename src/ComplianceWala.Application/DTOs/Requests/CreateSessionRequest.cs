namespace ComplianceWala.Application.DTOs.Requests;

/// <summary>
/// Request to create a new reconciliation session.
/// Validated at the API layer before reaching the orchestrator.
/// </summary>
public record CreateSessionRequest(
    /// <summary>GSTIN of the business running reconciliation.</summary>
    string BusinessGstin,

    /// <summary>Year of the GST filing period. Example: 2024</summary>
    int Year,

    /// <summary>Month of the GST filing period. Example: 3 for March</summary>
    int Month
);