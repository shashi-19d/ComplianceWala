using ComplianceWala.Domain.Enums;
using ComplianceWala.Domain.Exceptions;

namespace ComplianceWala.Domain.ValueObjects;

/// <summary>
/// Immutable value object representing the calculated ITC risk for a supplier.
/// 
/// VALUE OBJECT RULES ENFORCED HERE:
/// 1. All properties are init-only (cannot change after creation)
/// 2. Equality is based on values, not reference
/// 3. Created only through a factory method that validates inputs
/// </summary>
public sealed class ItcRiskScore : IEquatable<ItcRiskScore>
{
    // ── Properties ───────────────────────────────────────────────
    
    /// <summary>Total ITC amount at risk in rupees.</summary>
    public decimal AmountAtRisk { get; }

    /// <summary>
    /// Probability (0.0 to 1.0) that this ITC will be blocked.
    /// Calculated from supplier's 6-month filing history.
    /// </summary>
    public decimal BlockingProbability { get; }

    /// <summary>Expected recoverable ITC = AmountAtRisk × (1 - BlockingProbability)</summary>
    public decimal ExpectedRecoverableAmount => 
        AmountAtRisk * (1 - BlockingProbability);

    /// <summary>Derived risk level bucket for UI display and sorting.</summary>
    public RiskLevel Level { get; }

    // ── Constructor (private — use factory method) ────────────────
    
    private ItcRiskScore(decimal amountAtRisk, decimal blockingProbability)
    {
        AmountAtRisk = amountAtRisk;
        BlockingProbability = blockingProbability;
        Level = CalculateRiskLevel(blockingProbability);
    }

    // ── Factory Method ────────────────────────────────────────────
    
    /// <summary>
    /// Creates a validated ITC risk score.
    /// Factory method pattern ensures no invalid object can exist.
    /// </summary>
    public static ItcRiskScore Calculate(decimal itcAmount, decimal blockingProbability)
    {
        if (itcAmount < 0)
            throw new DomainException("NEGATIVE_ITC", 
                $"ITC amount cannot be negative. Received: {itcAmount}");

        if (blockingProbability < 0 || blockingProbability > 1)
            throw new DomainException("INVALID_PROBABILITY",
                $"Blocking probability must be between 0 and 1. Received: {blockingProbability}");

        return new ItcRiskScore(itcAmount, blockingProbability);
    }

    /// <summary>Zero risk score — used when supplier has perfect filing history.</summary>
    public static ItcRiskScore Zero() => new(0m, 0m);

    // ── Private Helpers ───────────────────────────────────────────
    
    private static RiskLevel CalculateRiskLevel(decimal probability) => probability switch
    {
        < 0.20m => RiskLevel.Low,
        < 0.50m => RiskLevel.Medium,
        < 0.80m => RiskLevel.High,
        _       => RiskLevel.Critical
    };

    // ── Value Object Equality ─────────────────────────────────────
    // Two risk scores are equal if their values are equal.
    
    public bool Equals(ItcRiskScore? other)
    {
        if (other is null) return false;
        return AmountAtRisk == other.AmountAtRisk &&
               BlockingProbability == other.BlockingProbability;
    }

    public override bool Equals(object? obj) => Equals(obj as ItcRiskScore);

    public override int GetHashCode() => 
        HashCode.Combine(AmountAtRisk, BlockingProbability);

    public override string ToString() =>
        $"₹{AmountAtRisk:N0} at risk | {BlockingProbability:P0} blocking probability | {Level}";
}