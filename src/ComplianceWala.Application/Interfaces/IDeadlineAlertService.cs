using ComplianceWala.Application.DTOs;
using ComplianceWala.Domain.Enums;

namespace ComplianceWala.Application.Interfaces;

/// <summary>
/// Scans all active reconciliation sessions for approaching
/// GSTR-3B deadlines and escalates critical mismatches.
///
/// Called by DeadlineAlertJob (Quartz) every morning at 8 AM.
/// Can also be triggered manually via API endpoint for testing.
/// </summary>
public interface IDeadlineAlertService
{
    /// <summary>
    /// Finds all sessions with mismatches where:
    /// 1. Deadline is within AlertThresholdDays (default: 10)
    /// 2. Mismatch is not yet resolved
    /// 3. ITC at risk > 0
    ///
    /// Returns a summary of what was found and alerted.
    /// </summary>
    Task<DeadlineAlertSummary> ScanAndAlertAsync(
        CancellationToken ct = default);
}

/// <summary>Result of one deadline scan run.</summary>
public record DeadlineAlertSummary(
    DateTime ScanTime,
    int SessionsScanned,
    int CriticalMismatchesFound,
    int HighMismatchesFound,
    decimal TotalItcAtRisk,
    IReadOnlyList<SessionDeadlineAlert> Alerts
);

/// <summary>Alert for one specific session.</summary>
public record SessionDeadlineAlert(
    Guid SessionId,
    string BusinessGstin,
    string FilingPeriod,
    int DaysToDeadline,
    int UnresolvedMismatches,
    decimal ItcAtRisk,
    MismatchPriority HighestPriority
);