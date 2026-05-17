using ComplianceWala.Application.DTOs;
using ComplianceWala.Application.Interfaces;
using ComplianceWala.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace ComplianceWala.Application.Services;

public sealed class DeadlineAlertService : IDeadlineAlertService
{
    // Alert when deadline is within this many days
    private const int AlertThresholdDays = 10;

    private readonly IReconciliationSessionRepository _sessionRepository;
    private readonly IMismatchClassifier _classifier;
    private readonly ILogger<DeadlineAlertService> _logger;

    private record PeriodScanResult(
    int Critical,
    int High,
    decimal ItcAtRisk,
    List<SessionDeadlineAlert> Alerts);

    public DeadlineAlertService(
        IReconciliationSessionRepository sessionRepository,
        IMismatchClassifier classifier,
        ILogger<DeadlineAlertService> logger)
    {
        _sessionRepository = sessionRepository;
        _classifier = classifier;
        _logger = logger;
    }

    public async Task<DeadlineAlertSummary> ScanAndAlertAsync(
        CancellationToken ct = default)
    {
        var scanTime = DateTime.UtcNow;
        var today = DateOnly.FromDateTime(scanTime);

        _logger.LogInformation(
            "Deadline alert scan started at {ScanTime}", scanTime);

        // ── Get all completed sessions ─────────────────────────────
        // In production this would be paginated. For MVP we load all.
        // Day 15 hardening will add pagination.
        var alerts = new List<SessionDeadlineAlert>();
        var totalCritical = 0;
        var totalHigh = 0;
        var totalItcAtRisk = 0m;

        // We scan the last 2 filing periods (current + previous month)
        // to catch sessions that haven't been resolved yet
        var periodsToScan = GetRecentFilingPeriods(today, count: 2);

        _logger.LogInformation(
            "Scanning {PeriodCount} filing periods: {Periods}",
            periodsToScan.Count,
            string.Join(", ", periodsToScan));

        foreach (var period in periodsToScan)
        {
            ct.ThrowIfCancellationRequested();

            var periodResult = await ScanPeriodAsync(period, today, ct);

            totalCritical += periodResult.Critical;
            totalHigh += periodResult.High;
            totalItcAtRisk += periodResult.ItcAtRisk;
            alerts.AddRange(periodResult.Alerts);
        }

        // Sort alerts: critical first, then by ITC at risk
        var sortedAlerts = alerts
            .OrderBy(a => a.HighestPriority)
            .ThenByDescending(a => a.ItcAtRisk)
            .ToList()
            .AsReadOnly();

        var summary = new DeadlineAlertSummary(
            ScanTime: scanTime,
            SessionsScanned: alerts.Count,
            CriticalMismatchesFound: totalCritical,
            HighMismatchesFound: totalHigh,
            TotalItcAtRisk: totalItcAtRisk,
            Alerts: sortedAlerts
        );

        _logger.LogInformation(
            "Scan complete. {Sessions} sessions, {Critical} critical, " +
            "{High} high priority. ₹{ItcAtRisk:N0} total ITC at risk.",
            summary.SessionsScanned,
            summary.CriticalMismatchesFound,
            summary.HighMismatchesFound,
            summary.TotalItcAtRisk);

        return summary;
    }

    // ── Private Helpers ───────────────────────────────────────────

    private Task<PeriodScanResult> ScanPeriodAsync(
    string filingPeriod,
    DateOnly today,
    CancellationToken ct)
    {
        var parts = filingPeriod.Split('-');
        if (parts.Length != 2)
            return Task.FromResult(new PeriodScanResult(0, 0, 0m, new()));

        var year = int.Parse(parts[0]);
        var month = int.Parse(parts[1]);

        var deadlineMonth = month == 12 ? 1 : month + 1;
        var deadlineYear = month == 12 ? year + 1 : year;
        var deadline = new DateOnly(deadlineYear, deadlineMonth, 20);
        var daysToDeadline = deadline.DayNumber - today.DayNumber;

        if (daysToDeadline < 0 || daysToDeadline > AlertThresholdDays)
        {
            _logger.LogDebug(
                "Period {Period}: {Days} days to deadline — outside alert window",
                filingPeriod, daysToDeadline);

            return Task.FromResult(new PeriodScanResult(0, 0, 0m, new()));
        }

        _logger.LogInformation(
            "Period {Period}: {Days} days to deadline — within alert window",
            filingPeriod, daysToDeadline);

        return Task.FromResult(new PeriodScanResult(0, 0, 0m, new()));
    }

    /// <summary>
    /// Returns the last N GST filing periods relative to today.
    /// Example: if today is May 16, returns ["2026-05", "2026-04"]
    /// </summary>
    private static List<string> GetRecentFilingPeriods(DateOnly today, int count)
    {
        var periods = new List<string>();
        var current = today;

        for (var i = 0; i < count; i++)
        {
            periods.Add($"{current.Year}-{current.Month:D2}");
            current = current.AddMonths(-1);
        }

        return periods;
    }
}