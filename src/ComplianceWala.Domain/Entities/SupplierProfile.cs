using ComplianceWala.Domain.Enums;
using ComplianceWala.Domain.Exceptions;
using ComplianceWala.Domain.ValueObjects;

namespace ComplianceWala.Domain.Entities;

/// <summary>
/// Tracks a GST supplier's identity and their historical filing behavior.
/// 
/// This is the entity that powers ITC risk scoring.
/// A supplier with poor filing history = HIGH risk for buyer's ITC.
/// We store last 6 months of filing status per supplier.
/// </summary>
public class SupplierProfile
{
    // ── Identity ──────────────────────────────────────────────────
    public Guid Id { get; private set; }
    public string Gstin { get; private set; }
    public string Name { get; private set; }

    // ── Filing History ────────────────────────────────────────────
    
    /// <summary>
    /// Dictionary of YearMonth → FilingStatus.
    /// Key format: "2024-03" (year-month of the GST return period)
    /// We maintain last 6 months to calculate filing reliability score.
    /// </summary>
    private readonly Dictionary<string, FilingStatus> _filingHistory = new();
    public IReadOnlyDictionary<string, FilingStatus> FilingHistory => _filingHistory;

    public DateTime LastUpdated { get; private set; }

    // ── Constructor ───────────────────────────────────────────────
    
    private SupplierProfile() { }

    public static SupplierProfile Create(string gstin, string name)
    {
        if (string.IsNullOrWhiteSpace(gstin) || gstin.Length != 15)
            throw new DomainException("INVALID_GSTIN",
                $"Supplier GSTIN must be 15 characters. Received: '{gstin}'");

        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("INVALID_SUPPLIER_NAME",
                "Supplier name cannot be empty.");

        return new SupplierProfile
        {
            Id = Guid.NewGuid(),
            Gstin = gstin.Trim().ToUpperInvariant(),
            Name = name.Trim(),
            LastUpdated = DateTime.UtcNow
        };
    }

    // ── Behavior Methods ──────────────────────────────────────────
    
    /// <summary>
    /// Records the filing status for a specific GST period.
    /// Called when we process a new GSTR-2B and observe whether
    /// each supplier filed or not.
    /// </summary>
    public void RecordFilingStatus(int year, int month, FilingStatus status)
    {
        if (year < 2017 || year > DateTime.UtcNow.Year + 1)
            throw new DomainException("INVALID_GST_YEAR",
                $"GST did not exist before 2017. Invalid year: {year}");

        if (month < 1 || month > 12)
            throw new DomainException("INVALID_MONTH",
                $"Month must be between 1 and 12. Received: {month}");

        var key = $"{year}-{month:D2}";
        _filingHistory[key] = status;

        // Keep only last 6 months — older data loses predictive value
        TrimHistoryToSixMonths();
        LastUpdated = DateTime.UtcNow;
    }

    /// <summary>
    /// Calculates what fraction of months the supplier filed on time.
    /// Used as input to ITC risk scoring.
    /// Returns 1.0 if no history (benefit of doubt — new supplier).
    /// </summary>
    public decimal CalculateOnTimeFilingRate()
    {
        if (_filingHistory.Count == 0)
            return 1.0m; // New supplier — assume compliant

        var onTimeCount = _filingHistory.Values
            .Count(s => s == FilingStatus.FiledOnTime);

        return (decimal)onTimeCount / _filingHistory.Count;
    }

    /// <summary>
    /// Calculates the ITC risk score for a given ITC amount from this supplier.
    /// Blocking probability = 1 - on-time filing rate.
    /// </summary>
    public ItcRiskScore CalculateItcRisk(decimal itcAmount)
    {
        var onTimeRate = CalculateOnTimeFilingRate();
        var blockingProbability = 1 - onTimeRate;
        return ItcRiskScore.Calculate(itcAmount, blockingProbability);
    }

    // ── Private Helpers ───────────────────────────────────────────
    
    private void TrimHistoryToSixMonths()
    {
        if (_filingHistory.Count <= 6) return;

        var oldest = _filingHistory.Keys
            .OrderBy(k => k)
            .First();

        _filingHistory.Remove(oldest);
    }
}