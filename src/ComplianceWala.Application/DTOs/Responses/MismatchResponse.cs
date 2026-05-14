using ComplianceWala.Domain.Enums;

namespace ComplianceWala.Application.DTOs.Responses;

public record MismatchResponse(
    Guid MismatchId,
    MismatchType MismatchType,
    MismatchPriority Priority,
    ResolutionAction RecommendedAction,
    decimal ItcAmountAtRisk,
    string SupplierName,
    string SupplierGstin,
    string InvoiceNumber,
    int DaysToDeadline,
    string DeadlineDate,
    bool RequiresCaReview,
    string ActionSummary,

    /// <summary>
    /// Null until Day 10 (AI layer). Populated after LLM processing.
    /// </summary>
    string? AiExplanation,
    string? AiExplanationHindi
);

public record ReconciliationCompleteResponse(
    SessionResponse Session,
    IReadOnlyList<MismatchResponse> Mismatches,
    IReadOnlyList<SupplierRiskSummary> SupplierRisks,
    long ProcessingTimeMs
);