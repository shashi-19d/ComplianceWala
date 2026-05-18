using ComplianceWala.Application.DTOs;

namespace ComplianceWala.Application.Interfaces;

/// <summary>
/// Generates plain-language explanations for detected GST mismatches.
///
/// DESIGN CONTRACT:
/// 1. This service NEVER makes classification decisions
/// 2. It only translates rule-engine output into human language
/// 3. If AI is unavailable, it returns a fallback — never throws
/// 4. Confidence score reflects how certain the model is about its output
///
/// This is intentionally separated from IMismatchClassifier.
/// Classification = deterministic rules.
/// Narration = probabilistic AI.
/// They must never be mixed.
/// </summary>
public interface IAiNarrativeService
{
    /// <summary>
    /// Generates English and Hindi explanation for a single mismatch.
    /// </summary>
    /// <param name="context">All data the AI needs to generate explanation</param>
    /// <returns>Generated narrative — never null, falls back to rule-generated text</returns>
    Task<AiNarrativeResult> GenerateNarrativeAsync(
        MismatchNarrativeContext context,
        CancellationToken ct = default);

    /// <summary>
    /// Batch generates narratives for multiple mismatches.
    /// More efficient than calling GenerateNarrativeAsync in a loop.
    /// </summary>
    Task<IReadOnlyList<AiNarrativeResult>> GenerateBatchNarrativesAsync(
        IReadOnlyList<MismatchNarrativeContext> contexts,
        CancellationToken ct = default);
}

/// <summary>
/// All context the AI needs to generate a mismatch explanation.
/// We pass structured data — not raw domain entities — to keep
/// the AI layer decoupled from domain models.
/// </summary>
public record MismatchNarrativeContext(
    Guid MismatchId,
    string MismatchTypeName,
    string SupplierName,
    string SupplierGstin,
    string InvoiceNumber,
    decimal ItcAmountAtRisk,
    int DaysToDeadline,
    string RecommendedActionName,
    /// <summary>The deterministic English summary from rule engine.</summary>
    string RuleGeneratedSummary
);

/// <summary>
/// Result of AI narrative generation.
/// Always has content — either AI-generated or fallback.
/// </summary>
public record AiNarrativeResult(
    Guid MismatchId,
    string ExplanationEnglish,
    string ExplanationHindi,
    /// <summary>0.0 = fallback used (AI failed). 1.0 = high confidence.</summary>
    decimal ConfidenceScore,
    bool UsedFallback,
    long GenerationTimeMs
);