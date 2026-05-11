using ComplianceWala.Domain.Enums;

namespace ComplianceWala.Application.DTOs;

/// <summary>
/// The enriched output of running a MismatchRecord through the classifier.
/// This is what the API returns to the Angular dashboard.
///
/// WHY A SEPARATE DTO AND NOT MODIFY MismatchRecord?
/// MismatchRecord is a domain entity — it holds state and enforces rules.
/// Classification is a READ-ONLY VIEW of that state, enriched with
/// computed fields (deadline, priority, action). Mixing them violates SRP.
/// 
/// This also means classification can be recalculated any time 
/// (e.g., priority changes as deadline approaches) without 
/// mutating the domain entity.
/// </summary>
public record MismatchClassification(
    Guid MismatchId,
    Guid SessionId,
    MismatchType MismatchType,
    MismatchPriority Priority,
    ResolutionAction RecommendedAction,
    decimal ItcAmountAtRisk,

    /// <summary>
    /// Days remaining until GSTR-3B filing deadline.
    /// Negative value = deadline has already passed.
    /// </summary>
    int DaysToDeadline,

    /// <summary>
    /// Human-readable deadline date.
    /// Example: "20 April 2024"
    /// </summary>
    string DeadlineDate,

    /// <summary>
    /// Whether a CA (Chartered Accountant) must be involved.
    /// True when: ITC > ₹1 lakh OR RateDifference OR GstinMismatch.
    /// </summary>
    bool RequiresCaReview,

    /// <summary>
    /// Short action summary for dashboard display.
    /// Example: "Contact ABC Traders to file their March GSTR-1 immediately."
    /// This is rule-generated (deterministic), not AI-generated.
    /// AI explanation comes later (Day 10) and is richer/multilingual.
    /// </summary>
    string ActionSummary,

    string SupplierGstin,
    string SupplierName,
    string InvoiceNumber
);