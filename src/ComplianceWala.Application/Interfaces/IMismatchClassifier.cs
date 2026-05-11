using ComplianceWala.Application.DTOs;
using ComplianceWala.Domain.Entities;

namespace ComplianceWala.Application.Interfaces;

/// <summary>
/// Classifies a detected mismatch with priority, recommended action,
/// and deadline awareness.
/// 
/// This is a PURE RULE ENGINE — no randomness, no AI, no external calls.
/// Same MismatchRecord on same date always produces same classification.
/// This determinism is intentional: financial decisions must be auditable.
/// </summary>
public interface IMismatchClassifier
{
    /// <summary>
    /// Classifies a single mismatch.
    /// </summary>
    MismatchClassification Classify(
        MismatchRecord mismatch,
        DateOnly reconciliationDate);

    /// <summary>
    /// Classifies all mismatches in a session.
    /// Returns ordered by Priority ascending (Critical first).
    /// </summary>
    IReadOnlyList<MismatchClassification> ClassifyAll(
        IEnumerable<MismatchRecord> mismatches,
        DateOnly reconciliationDate);
}