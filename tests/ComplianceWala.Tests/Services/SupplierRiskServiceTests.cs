using ComplianceWala.Application.Services;
using ComplianceWala.Domain.Entities;
using ComplianceWala.Domain.Enums;
using ComplianceWala.Tests.Fakes;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace ComplianceWala.Tests.Services;

public class SupplierRiskServiceTests
{
    private const string SupplierA = "27AABCU9603R1ZX";
    private const string SupplierB = "29GGGGG1314R9Z6";
    private const string FilingPeriod = "2024-03"; // March 2024

    // GSTR-1 deadline for March 2024 = 11th April 2024
    private static readonly DateTime OnTimeDate  = new(2024, 4, 9);   // Before 11th ✅
    private static readonly DateTime LateDate    = new(2024, 4, 18);  // After 11th ⚠️

    private FakeSupplierProfileRepository _repo;
    private SupplierRiskService _service;

    public SupplierRiskServiceTests()
    {
        _repo = new FakeSupplierProfileRepository();
        _service = new SupplierRiskService(
            _repo,
            NullLogger<SupplierRiskService>.Instance);
    }

    // ── Test 1: Supplier filed on time → FiledOnTime status ───────
    [Fact]
    public async Task ProcessGstr2bFilingData_SupplierFiledOnTime_RecordsOnTimeStatus()
    {
        var gstins = new[] { SupplierA };
        var filingDates = new Dictionary<string, DateTime>
            { [SupplierA] = OnTimeDate };
        var itcAmounts = new Dictionary<string, decimal>
            { [SupplierA] = 18000m };

        var result = await _service.ProcessGstr2bFilingDataAsync(
            gstins, filingDates, itcAmounts, FilingPeriod);

        result.Should().HaveCount(1);
        result[0].CurrentPeriodStatus.Should().Be(FilingStatus.FiledOnTime);
        result[0].FiledLateThisPeriod.Should().BeFalse();
    }

    // ── Test 2: Supplier filed late → FiledLate status ────────────
    [Fact]
    public async Task ProcessGstr2bFilingData_SupplierFiledLate_RecordsLateStatus()
    {
        var gstins = new[] { SupplierA };
        var filingDates = new Dictionary<string, DateTime>
            { [SupplierA] = LateDate };  // After 11th April
        var itcAmounts = new Dictionary<string, decimal>
            { [SupplierA] = 18000m };

        var result = await _service.ProcessGstr2bFilingDataAsync(
            gstins, filingDates, itcAmounts, FilingPeriod);

        result[0].CurrentPeriodStatus.Should().Be(FilingStatus.FiledLate);
        result[0].FiledLateThisPeriod.Should().BeTrue();
    }

    // ── Test 3: Supplier absent from GSTR-2B → NotFiled ──────────
    [Fact]
    public async Task ProcessGstr2bFilingData_SupplierNotInGstr2b_RecordsNotFiled()
    {
        var gstins = new[] { SupplierA };
        var filingDates = new Dictionary<string, DateTime>(); // Empty — supplier absent
        var itcAmounts = new Dictionary<string, decimal>
            { [SupplierA] = 45000m };

        var result = await _service.ProcessGstr2bFilingDataAsync(
            gstins, filingDates, itcAmounts, FilingPeriod);

        result[0].CurrentPeriodStatus.Should().Be(FilingStatus.NotFiled);
        result[0].ItcAtRisk.Should().Be(45000m); // Full ITC at risk
    }

    // ── Test 4: Profiles are persisted after processing ───────────
    [Fact]
    public async Task ProcessGstr2bFilingData_AfterProcessing_ProfilesArePersisted()
    {
        var gstins = new[] { SupplierA, SupplierB };
        var filingDates = new Dictionary<string, DateTime>
        {
            [SupplierA] = OnTimeDate,
            [SupplierB] = LateDate
        };
        var itcAmounts = new Dictionary<string, decimal>
        {
            [SupplierA] = 18000m,
            [SupplierB] = 9000m
        };

        await _service.ProcessGstr2bFilingDataAsync(
            gstins, filingDates, itcAmounts, FilingPeriod);

        // Check that fake repository now has both profiles
        _repo.Count.Should().Be(2);
    }

    // ── Test 5: Prior history affects risk score ───────────────────
    [Fact]
    public async Task ProcessGstr2bFilingData_SupplierWithBadHistory_HasHigherRisk()
    {
        // Seed: supplier has 3 months of not-filing history
        var badSupplier = SupplierProfile.Create(SupplierA, "Bad Supplier Ltd");
        badSupplier.RecordFilingStatus(2024, 1, FilingStatus.NotFiled);
        badSupplier.RecordFilingStatus(2024, 2, FilingStatus.NotFiled);
        _repo.Seed(badSupplier);

        // Now process March — also not filed
        var gstins = new[] { SupplierA };
        var filingDates = new Dictionary<string, DateTime>(); // NotFiled
        var itcAmounts = new Dictionary<string, decimal> { [SupplierA] = 50000m };

        var result = await _service.ProcessGstr2bFilingDataAsync(
            gstins, filingDates, itcAmounts, FilingPeriod);

        // 3 NotFiled out of 3 months = 100% blocking probability
        result[0].OnTimeFilingRate.Should().Be(0m);
        result[0].RiskLevel.Should().Be(RiskLevel.Critical);
        result[0].ItcAtRisk.Should().Be(50000m);
    }

    // ── Test 6: Good history → lower risk ─────────────────────────
    [Fact]
    public async Task ProcessGstr2bFilingData_SupplierWithGoodHistory_HasLowRisk()
    {
        // Seed: supplier has 5 months of on-time filing
        var goodSupplier = SupplierProfile.Create(SupplierA, "Reliable Supplier");
        goodSupplier.RecordFilingStatus(2023, 10, FilingStatus.FiledOnTime);
        goodSupplier.RecordFilingStatus(2023, 11, FilingStatus.FiledOnTime);
        goodSupplier.RecordFilingStatus(2023, 12, FilingStatus.FiledOnTime);
        goodSupplier.RecordFilingStatus(2024, 1,  FilingStatus.FiledOnTime);
        goodSupplier.RecordFilingStatus(2024, 2,  FilingStatus.FiledOnTime);
        _repo.Seed(goodSupplier);

        // March: also on time
        var gstins = new[] { SupplierA };
        var filingDates = new Dictionary<string, DateTime>
            { [SupplierA] = OnTimeDate };
        var itcAmounts = new Dictionary<string, decimal>
            { [SupplierA] = 90000m };

        var result = await _service.ProcessGstr2bFilingDataAsync(
            gstins, filingDates, itcAmounts, FilingPeriod);

        result[0].OnTimeFilingRate.Should().Be(1.0m); // 6/6 on time
        result[0].RiskLevel.Should().Be(RiskLevel.Low);
    }

    // ── Test 7: Results ordered by ITC at risk descending ─────────
    [Fact]
    public async Task ProcessGstr2bFilingData_ReturnsOrderedByItcAtRiskDescending()
    {
        var gstins = new[] { SupplierA, SupplierB };
        var filingDates = new Dictionary<string, DateTime>(); // Both NotFiled
        var itcAmounts = new Dictionary<string, decimal>
        {
            [SupplierA] = 10000m,   // Less ITC
            [SupplierB] = 90000m    // More ITC
        };

        var result = await _service.ProcessGstr2bFilingDataAsync(
            gstins, filingDates, itcAmounts, FilingPeriod);

        // Supplier B (higher ITC at risk) should come first
        result[0].SupplierGstin.Should().Be(SupplierB);
        result[1].SupplierGstin.Should().Be(SupplierA);
    }

    // ── Test 8: December filing period crosses year boundary ───────
    [Fact]
    public async Task ProcessGstr2bFilingData_DecemberPeriod_DeadlineIsJanuary11()
    {
        var decemberPeriod = "2024-12";

        // Supplier filed January 10, 2025 (before 11th Jan = on time)
        var jan10Filing = new DateTime(2025, 1, 10);

        var gstins = new[] { SupplierA };
        var filingDates = new Dictionary<string, DateTime>
            { [SupplierA] = jan10Filing };
        var itcAmounts = new Dictionary<string, decimal>
            { [SupplierA] = 18000m };

        var result = await _service.ProcessGstr2bFilingDataAsync(
            gstins, filingDates, itcAmounts, decemberPeriod);

        // January 10 is before January 11 deadline → on time
        result[0].CurrentPeriodStatus.Should().Be(FilingStatus.FiledOnTime);
    }
}