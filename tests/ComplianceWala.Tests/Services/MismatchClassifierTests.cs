using ComplianceWala.Application.Services;
using ComplianceWala.Domain.Entities;
using ComplianceWala.Domain.Enums;
using ComplianceWala.Domain.ValueObjects;
using FluentAssertions;

namespace ComplianceWala.Tests.Services;

public class MismatchClassifierTests
{
    private readonly MismatchClassifier _classifier = new();
    private static readonly DateOnly TestDate = new(2024, 4, 5);
    // deadline = 20th April 2024 → 15 days away from TestDate

    // ── Test 1: SupplierNotFiled → High base priority ─────────────
    [Fact]
    public void Classify_SupplierNotFiled_ReturnsHighPriority()
    {
        var mismatch = CreateMismatch(MismatchType.SupplierNotFiled, itcAtRisk: 5000m);

        var result = _classifier.Classify(mismatch, TestDate);

        result.Priority.Should().Be(MismatchPriority.High);
        result.RecommendedAction.Should().Be(ResolutionAction.ContactSupplierToFile);
    }

    // ── Test 2: DuplicateInvoice → always Critical ────────────────
    [Fact]
    public void Classify_DuplicateInvoice_AlwaysCritical()
    {
        // Even with ₹1 ITC, duplicate = Critical (it's a fraud risk)
        var mismatch = CreateMismatch(MismatchType.DuplicateInvoice, itcAtRisk: 1m);

        var result = _classifier.Classify(mismatch, TestDate);

        result.Priority.Should().Be(MismatchPriority.Critical);
        result.RecommendedAction.Should().Be(ResolutionAction.RejectDuplicateEntry);
        result.RequiresCaReview.Should().BeTrue();
    }

    // ── Test 3: Large ITC escalates to Critical regardless of type ─
    [Fact]
    public void Classify_ItcAbove1Lakh_EscalatesToCritical()
    {
        // DateMismatch is normally Low priority
        // But ₹1.5 lakh ITC at risk → Critical
        var mismatch = CreateMismatch(
            MismatchType.InvoiceDateMismatch,
            itcAtRisk: 150_000m);

        var result = _classifier.Classify(mismatch, TestDate);

        result.Priority.Should().Be(MismatchPriority.Critical);
        result.RequiresCaReview.Should().BeTrue();
    }

    // ── Test 4: Deadline ≤3 days → always Critical ────────────────
    [Fact]
    public void Classify_DeadlineIn2Days_EscalatesToCritical()
    {
        // 20th April is deadline, we're on 18th April (2 days left)
        var nearDeadlineDate = new DateOnly(2024, 4, 18);
        var mismatch = CreateMismatch(MismatchType.AmountDifference, itcAtRisk: 1000m);

        var result = _classifier.Classify(mismatch, nearDeadlineDate);

        result.Priority.Should().Be(MismatchPriority.Critical);
        result.DaysToDeadline.Should().Be(2);
    }

    // ── Test 5: DateMismatch → Low priority, defer action ─────────
    [Fact]
    public void Classify_InvoiceDateMismatch_ReturnsLowPriorityAndDeferAction()
    {
        var mismatch = CreateMismatch(MismatchType.InvoiceDateMismatch, itcAtRisk: 0m);

        var result = _classifier.Classify(mismatch, TestDate);

        result.Priority.Should().Be(MismatchPriority.Low);
        result.RecommendedAction.Should().Be(ResolutionAction.AcceptAndDeferToNextPeriod);
        result.RequiresCaReview.Should().BeFalse();
    }

    // ── Test 6: ClassifyAll returns ordered by priority ────────────
    [Fact]
    public void ClassifyAll_ReturnsMismatchesOrderedByCriticalityFirst()
    {
        var mismatches = new[]
        {
            CreateMismatch(MismatchType.InvoiceDateMismatch, itcAtRisk: 0m),
            CreateMismatch(MismatchType.DuplicateInvoice,    itcAtRisk: 100m),
            CreateMismatch(MismatchType.SupplierNotFiled,    itcAtRisk: 50_000m)
        };

        var results = _classifier.ClassifyAll(mismatches, TestDate);

        // First = highest urgency (Critical)
        results[0].MismatchType.Should().Be(MismatchType.DuplicateInvoice);
        // Last = lowest urgency
        results[^1].MismatchType.Should().Be(MismatchType.InvoiceDateMismatch);
    }

    // ── Test 7: ActionSummary contains URGENT for near deadline ───
    [Fact]
    public void Classify_NearDeadline_ActionSummaryContainsUrgent()
    {
        var urgentDate = new DateOnly(2024, 4, 18); // 2 days to deadline
        var mismatch = CreateMismatch(MismatchType.SupplierNotFiled, itcAtRisk: 9000m);

        var result = _classifier.Classify(mismatch, urgentDate);

        result.ActionSummary.Should().Contain("URGENT");
    }

    // ── Test 8: Deadline calculation crosses year boundary ─────────
    [Fact]
    public void Classify_PostTwentiethDecember_DeadlineIsJanuary20()
    {
        // December 25 is AFTER the 20th → next deadline is January 20, 2025
        var decemberDate = new DateOnly(2024, 12, 25);
        var mismatch = CreateMismatch(MismatchType.SupplierNotFiled, itcAtRisk: 5000m);

        var result = _classifier.Classify(mismatch, decemberDate);

        // Dec 25 → Jan 20 = 26 days
        result.DeadlineDate.Should().Contain("January 2025");
        result.DaysToDeadline.Should().Be(26);
    }

    // ── Test 9: GstinMismatch always requires CA review ───────────
    [Fact]
    public void Classify_GstinMismatch_AlwaysRequiresCaReview()
    {
        var mismatch = CreateMismatch(MismatchType.GstinMismatch, itcAtRisk: 500m);

        var result = _classifier.Classify(mismatch, TestDate);

        result.RequiresCaReview.Should().BeTrue();
        result.RecommendedAction.Should().Be(ResolutionAction.RequestSupplierAmendment);
    }

    // ── Helper ────────────────────────────────────────────────────

    private static MismatchRecord CreateMismatch(
        MismatchType type,
        decimal itcAtRisk)
    {
        var invoice = Invoice.Create(
            invoiceNumber: "INV-TEST-001",
            invoiceDate: new DateOnly(2024, 3, 5),
            supplierGstin: "27AABCU9603R1ZX",
            supplierName: "ABC Traders",
            buyerGstin: "29GGGGG1314R9Z6",
            taxableValue: itcAtRisk / 0.18m,
            igst: itcAtRisk,
            cgst: 0m,
            sgst: 0m,
            isFromPurchaseRegister: true
        );

        return MismatchRecord.Detect(
            reconciliationSessionId: Guid.NewGuid(),
            mismatchType: type,
            itcAmountAtRisk: itcAtRisk,
            riskScore: ItcRiskScore.Calculate(itcAtRisk, 0.5m),
            purchaseRegisterInvoice: invoice
        );
    }
}