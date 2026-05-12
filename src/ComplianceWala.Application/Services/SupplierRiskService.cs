using ComplianceWala.Application.DTOs;
using ComplianceWala.Application.Interfaces;
using ComplianceWala.Domain.Entities;
using ComplianceWala.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace ComplianceWala.Application.Services;

public sealed class SupplierRiskService : ISupplierRiskService
{
    // GSTR-1 legal deadline: 11th of the month following the filing period
    private const int Gstr1DeadlineDayOfMonth = 11;

    private readonly ISupplierProfileRepository _repository;
    private readonly ILogger<SupplierRiskService> _logger;

    public SupplierRiskService(
        ISupplierProfileRepository repository,
        ILogger<SupplierRiskService> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<IReadOnlyList<SupplierRiskSummary>> ProcessGstr2bFilingDataAsync(
        IEnumerable<string> purchaseRegisterSupplierGstins,
        IReadOnlyDictionary<string, DateTime> gstr2bSupplierFilingDates,
        IReadOnlyDictionary<string, decimal> supplierItcAmounts,
        string filingPeriod,
        CancellationToken ct = default)
    {
        // ── Parse filing period ───────────────────────────────────
        var (year, month) = ParseFilingPeriod(filingPeriod);

        // GSTR-1 deadline for this period = 11th of next month
        var gstr1Deadline = CalculateGstr1Deadline(year, month);

        _logger.LogInformation(
            "Processing supplier risk for period {Period}. " +
            "GSTR-1 deadline was {Deadline:dd-MM-yyyy}",
            filingPeriod, gstr1Deadline);

        // ── Fetch existing profiles for all suppliers ─────────────
        var allGstins = purchaseRegisterSupplierGstins.ToHashSet();
        var existingProfiles = await _repository.GetByGstinsAsync(allGstins, ct);

        var updatedProfiles = new List<SupplierProfile>();
        var summaries = new List<SupplierRiskSummary>();

        foreach (var gstin in allGstins)
        {
            ct.ThrowIfCancellationRequested();

            // ── Get or create supplier profile ────────────────────
            var profile = existingProfiles.GetValueOrDefault(gstin)
                       ?? SupplierProfile.Create(gstin, gstin); // Name = GSTIN until enriched

            // ── Determine filing status for this period ───────────
            FilingStatus status;
            DateTime? filingDate = null;
            bool filedLate = false;

            if (gstr2bSupplierFilingDates.TryGetValue(gstin, out var supplierFilingDate))
            {
                filingDate = supplierFilingDate;

                // Compare filing date against legal deadline (11th of next month)
                if (supplierFilingDate.Date <= gstr1Deadline.ToDateTime(TimeOnly.MinValue).Date)
                {
                    status = FilingStatus.FiledOnTime;
                }
                else
                {
                    status = FilingStatus.FiledLate;
                    filedLate = true;
                    _logger.LogWarning(
                        "Supplier {Gstin} filed late: {FilingDate:dd-MM-yyyy} " +
                        "(deadline was {Deadline:dd-MM-yyyy})",
                        gstin, supplierFilingDate, gstr1Deadline);
                }
            }
            else
            {
                // Supplier completely absent from GSTR-2B = NotFiled
                status = FilingStatus.NotFiled;
                _logger.LogWarning(
                    "Supplier {Gstin} has NOT filed GSTR-1 for period {Period}",
                    gstin, filingPeriod);
            }

            // ── Update profile filing history ─────────────────────
            profile.RecordFilingStatus(year, month, status);
            updatedProfiles.Add(profile);

            // ── Calculate ITC risk ────────────────────────────────
            var itcAmount = supplierItcAmounts.GetValueOrDefault(gstin, 0m);
            var riskScore = profile.CalculateItcRisk(itcAmount);

            // ── Build summary ─────────────────────────────────────
            summaries.Add(new SupplierRiskSummary(
                SupplierGstin: gstin,
                SupplierName: profile.Name,
                OnTimeFilingRate: profile.CalculateOnTimeFilingRate(),
                TotalItcThisPeriod: itcAmount,
                ItcAtRisk: riskScore.AmountAtRisk,
                RiskLevel: riskScore.Level,
                MonthsOfHistoryAvailable: profile.FilingHistory.Count,
                CurrentPeriodStatus: status,
                CurrentPeriodFilingDate: filingDate,
                FiledLateThisPeriod: filedLate
            ));
        }

        // ── Persist all updated profiles in one transaction ────────
        await _repository.UpsertBatchAsync(updatedProfiles, ct);

        _logger.LogInformation(
            "Supplier risk processing complete. " +
            "{Total} suppliers: {OnTime} on time, {Late} late, {NotFiled} not filed",
            summaries.Count,
            summaries.Count(s => s.CurrentPeriodStatus == FilingStatus.FiledOnTime),
            summaries.Count(s => s.CurrentPeriodStatus == FilingStatus.FiledLate),
            summaries.Count(s => s.CurrentPeriodStatus == FilingStatus.NotFiled));

        return summaries
            .OrderByDescending(s => s.ItcAtRisk)  // Highest risk first
            .ToList()
            .AsReadOnly();
    }

    public async Task<IReadOnlyList<SupplierRiskSummary>> GetSupplierRiskSummariesAsync(
        IEnumerable<string> supplierGstins,
        IReadOnlyDictionary<string, decimal> supplierItcAmounts,
        CancellationToken ct = default)
    {
        var profiles = await _repository.GetByGstinsAsync(supplierGstins, ct);
        var summaries = new List<SupplierRiskSummary>();

        foreach (var (gstin, profile) in profiles)
        {
            if (profile is null) continue;

            var itcAmount = supplierItcAmounts.GetValueOrDefault(gstin, 0m);
            var riskScore = profile.CalculateItcRisk(itcAmount);

            summaries.Add(new SupplierRiskSummary(
                SupplierGstin: gstin,
                SupplierName: profile.Name,
                OnTimeFilingRate: profile.CalculateOnTimeFilingRate(),
                TotalItcThisPeriod: itcAmount,
                ItcAtRisk: riskScore.AmountAtRisk,
                RiskLevel: riskScore.Level,
                MonthsOfHistoryAvailable: profile.FilingHistory.Count,
                CurrentPeriodStatus: FilingStatus.FiledOnTime, // Unknown without period
                CurrentPeriodFilingDate: null,
                FiledLateThisPeriod: false
            ));
        }

        return summaries
            .OrderByDescending(s => s.ItcAtRisk)
            .ToList()
            .AsReadOnly();
    }

    // ── Private Helpers ───────────────────────────────────────────

    private static (int year, int month) ParseFilingPeriod(string filingPeriod)
    {
        // Expected format: "2024-03"
        var parts = filingPeriod.Split('-');
        if (parts.Length != 2
            || !int.TryParse(parts[0], out var year)
            || !int.TryParse(parts[1], out var month))
        {
            throw new Domain.Exceptions.DomainException("INVALID_FILING_PERIOD",
                $"Filing period must be in YYYY-MM format. Received: '{filingPeriod}'");
        }

        return (year, month);
    }

    private static DateOnly CalculateGstr1Deadline(int filingYear, int filingMonth)
    {
        // GSTR-1 for period M/YYYY is due on 11th of M+1/YYYY
        var deadlineMonth = filingMonth == 12 ? 1 : filingMonth + 1;
        var deadlineYear  = filingMonth == 12 ? filingYear + 1 : filingYear;
        return new DateOnly(deadlineYear, deadlineMonth, Gstr1DeadlineDayOfMonth);
    }
}