using ComplianceWala.Application.Interfaces;
using Microsoft.Extensions.Logging;
using Quartz;

namespace ComplianceWala.Infrastructure.BackgroundJobs;

/// <summary>
/// Quartz.NET job that runs the deadline alert scan on a schedule.
///
/// SEPARATION OF CONCERNS:
/// - DeadlineAlertJob (Infrastructure): knows about Quartz, handles scheduling
/// - DeadlineAlertService (Application): knows about business rules, knows nothing about Quartz
///
/// This means we can replace Quartz with Hangfire or Azure Functions
/// by only changing this class. The service logic is untouched.
///
/// SCHEDULE: Runs daily at 8:00 AM UTC (configurable in Program.cs)
/// </summary>
[DisallowConcurrentExecution]  // Prevents two instances running simultaneously
public sealed class DeadlineAlertJob : IJob
{
    private readonly IDeadlineAlertService _alertService;
    private readonly ILogger<DeadlineAlertJob> _logger;

    public DeadlineAlertJob(
        IDeadlineAlertService alertService,
        ILogger<DeadlineAlertJob> logger)
    {
        _alertService = alertService;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        _logger.LogInformation(
            "DeadlineAlertJob triggered at {FireTime}",
            context.FireTimeUtc);

        try
        {
            var summary = await _alertService.ScanAndAlertAsync(
                context.CancellationToken);

            _logger.LogInformation(
                "DeadlineAlertJob completed. Scanned {Sessions} sessions. " +
                "₹{ItcAtRisk:N0} total ITC at risk.",
                summary.SessionsScanned,
                summary.TotalItcAtRisk);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Log but don't rethrow — let Quartz handle retry logic
            // Rethrowing causes Quartz to mark the job as failed
            // and may disable future executions depending on config
            _logger.LogError(ex,
                "DeadlineAlertJob failed at {FireTime}",
                context.FireTimeUtc);
        }
    }
}