using ComplianceWala.Application.DTOs;
using ComplianceWala.Domain.Entities;

namespace ComplianceWala.Application.Interfaces;

/// <summary>
/// Orchestrates supplier risk scoring for a reconciliation session.
/// 
/// Responsibilities:
/// 1. Parse filing dates from GSTR-2B result
/// 2. Update each supplier's filing history
/// 3. Identify suppliers NOT in GSTR-2B (NotFiled status)
/// 4. Calculate risk scores for all suppliers
/// 5. Persist updated profiles
/// 6. Return risk summaries for dashboard display
/// </summary>
public interface ISupplierRiskService
{
    /// <summary>
    /// Processes GSTR-2B filing data and updates supplier risk profiles.
    /// 
    /// Call this AFTER parsing GSTR-2B, BEFORE running reconciliation.
    /// The updated supplier profiles are then passed into ReconciliationEngine
    /// so each MismatchRecord gets an accurate ITC risk score.
    /// </summary>
    Task<IReadOnlyList<SupplierRiskSummary>> ProcessGstr2bFilingDataAsync(
        /// <summary>All supplier GSTINs found in buyer's purchase register.</summary>
        IEnumerable<string> purchaseRegisterSupplierGstins,

        /// <summary>
        /// Supplier GSTINs and their filing dates from GSTR-2B.
        /// Suppliers NOT in this dictionary = NotFiled this period.
        /// </summary>
        IReadOnlyDictionary<string, DateTime> gstr2bSupplierFilingDates,

        /// <summary>
        /// Total ITC per supplier from the purchase register.
        /// Key = GSTIN, Value = total ITC amount.
        /// </summary>
        IReadOnlyDictionary<string, decimal> supplierItcAmounts,

        /// <summary>The GST filing period being processed. E.g., "2024-03"</summary>
        string filingPeriod,

        CancellationToken ct = default);

    /// <summary>
    /// Returns current risk profiles for all suppliers in a session.
    /// Used to enrich the dashboard with historical context.
    /// </summary>
    Task<IReadOnlyList<SupplierRiskSummary>> GetSupplierRiskSummariesAsync(
        IEnumerable<string> supplierGstins,
        IReadOnlyDictionary<string, decimal> supplierItcAmounts,
        CancellationToken ct = default);
}