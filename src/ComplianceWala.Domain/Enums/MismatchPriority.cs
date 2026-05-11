namespace ComplianceWala.Domain.Enums;

/// <summary>
/// How urgently the SMB owner must act on this mismatch.
/// Calculated from: ITC amount at risk + days to GSTR-3B deadline.
/// 
/// Displayed in the Angular dashboard as colored badges:
/// Critical = Red | High = Orange | Medium = Yellow | Low = Green
/// </summary>
public enum MismatchPriority
{
    /// <summary>
    /// Act within 24-48 hours.
    /// Either deadline is ≤3 days away OR ITC at risk > ₹1 lakh
    /// OR mismatch is a DuplicateInvoice (claiming it = legal fraud).
    /// </summary>
    Critical = 1,

    /// <summary>
    /// Act within the week.
    /// Deadline is 4-10 days away OR ITC at risk > ₹25,000.
    /// </summary>
    High = 2,

    /// <summary>
    /// Act this month.
    /// Deadline is 11-20 days away AND ITC at risk ≤ ₹25,000.
    /// </summary>
    Medium = 3,

    /// <summary>
    /// Monitor and track.
    /// Deadline is >20 days away OR mismatch has no ITC impact
    /// (e.g., DateMismatch that will self-resolve next period).
    /// </summary>
    Low = 4
}