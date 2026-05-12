using ComplianceWala.Domain.Enums;

namespace ComplianceWala.Application.DTOs;

/// <summary>
/// Read-only risk snapshot for a single supplier.
/// Returned by SupplierRiskService after processing GSTR-2B.
/// Displayed in the Angular dashboard's supplier risk table.
/// 
/// This is a DTO — it carries data out of the service layer.
/// It has no behavior, no validation, no domain rules.
/// </summary>
public record SupplierRiskSummary(
    string SupplierGstin,
    string SupplierName,

    /// <summary>
    /// Fraction of months supplier filed on time (0.0 to 1.0).
    /// 1.0 = perfect compliance. 0.0 = never filed on time.
    /// </summary>
    decimal OnTimeFilingRate,

    /// <summary>Total ITC from this supplier in the current period.</summary>
    decimal TotalItcThisPeriod,

    /// <summary>ITC amount at risk based on filing history.</summary>
    decimal ItcAtRisk,

    RiskLevel RiskLevel,

    /// <summary>How many months of history we have for this supplier.</summary>
    int MonthsOfHistoryAvailable,

    FilingStatus CurrentPeriodStatus,

    /// <summary>
    /// When the supplier filed this period (null if not filed).
    /// </summary>
    DateTime? CurrentPeriodFilingDate,

    /// <summary>True if supplier filed after the 11th (late but filed).</summary>
    bool FiledLateThisPeriod
);