using ComplianceWala.Application.Services;
using ComplianceWala.Tests.Fakes;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace ComplianceWala.Tests.Services;

public class DeadlineAlertServiceTests
{
    private readonly FakeSupplierProfileRepository _supplierRepo;
    private readonly MismatchClassifier _classifier;

    public DeadlineAlertServiceTests()
    {
        _supplierRepo = new FakeSupplierProfileRepository();
        _classifier = new MismatchClassifier();
    }

    [Fact]
    public async Task ScanAndAlertAsync_ReturnsValidSummary()
    {
        var repo = new FakeReconciliationSessionRepository();
        var service = CreateService(repo);

        var result = await service.ScanAndAlertAsync();

        result.Should().NotBeNull();
        result.ScanTime.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        result.Alerts.Should().NotBeNull();
    }

    [Fact]
    public async Task ScanAndAlertAsync_WithNoSessions_ReturnsEmptySummary()
    {
        var repo = new FakeReconciliationSessionRepository();
        var service = CreateService(repo);

        var result = await service.ScanAndAlertAsync();

        result.CriticalMismatchesFound.Should().Be(0);
        result.TotalItcAtRisk.Should().Be(0m);
    }

    private DeadlineAlertService CreateService(
        FakeReconciliationSessionRepository repo)
    {
        return new DeadlineAlertService(
            repo,
            _classifier,
            NullLogger<DeadlineAlertService>.Instance);
    }
}