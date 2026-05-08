namespace ComplianceWala.Domain.Enums;

/// <summary>
/// Represents the urgency level of ITC recovery risk for a supplier.
/// Used to prioritize which supplier the SMB should contact first.
/// </summary>
public enum RiskLevel
{
    /// <summary>Less than 20% probability of ITC being blocked.</summary>
    Low = 1,

    /// <summary>20–50% probability. Supplier has occasional filing delays.</summary>
    Medium = 2,

    /// <summary>50–80% probability. Supplier frequently misses deadlines.</summary>
    High = 3,

    /// <summary>
    /// Above 80% probability OR supplier has never filed in last 3 months.
    /// ITC should be treated as unrecoverable for cash flow planning.
    /// </summary>
    Critical = 4
}