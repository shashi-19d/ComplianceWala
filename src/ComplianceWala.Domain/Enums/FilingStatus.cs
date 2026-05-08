namespace ComplianceWala.Domain.Enums;

/// <summary>
/// Tracks a supplier's GSTR-1 filing status for a specific month.
/// We store 6 months of history per supplier to calculate risk scores.
/// </summary>
public enum FilingStatus
{
    /// <summary>Filed on or before the 11th of following month.</summary>
    FiledOnTime = 1,

    /// <summary>Filed after the 11th but before month-end.</summary>
    FiledLate = 2,

    /// <summary>Not filed as of reconciliation date. ITC fully blocked.</summary>
    NotFiled = 3,

    /// <summary>
    /// Filed but with amendments — ITC amount may differ from original invoice.
    /// </summary>
    FiledWithAmendment = 4
}