using ComplianceWala.Application.Services;
using ComplianceWala.Domain.Entities;
using ComplianceWala.Domain.Enums;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace ComplianceWala.Tests.Services;

public class ReconciliationEngineTests
{
    private readonly ReconciliationEngine _engine;

    // ── Test constants ────────────────────────────────────────────
    private const string SupplierGstin = "27AABCU9603R1ZX";
    private const string BuyerGstin    = "29GGGGG1314R9Z6";
    private const string FilingPeriod  = "2024-03";

    public ReconciliationEngineTests()
    {
        _engine = new ReconciliationEngine(
            NullLogger<ReconciliationEngine>.Instance);
    }

    // ── Test 1: Perfect match — no mismatches ─────────────────────
    [Fact]
    public async Task ReconcileAsync_PerfectMatch_ReturnsZeroMismatches()
    {
        // Arrange
        var session = CreateSession();
        var invoice = CreateInvoice("INV-001", 100000m, igst: 18000m);

        session.LoadPurchaseRegisterInvoices(new[] { invoice });
        session.LoadGstr2bInvoices(new[] { invoice }); // Same invoice

        // Act
        var result = await _engine.ReconcileAsync(session, Enumerable.Empty<SupplierProfile>());

        // Assert
        result.TotalMismatches.Should().Be(0);
        result.MatchedInvoices.Should().Be(1);
        session.Status.Should().Be(SessionStatus.Completed);
    }

    // ── Test 2: Supplier not filed ────────────────────────────────
    [Fact]
    public async Task ReconcileAsync_InvoiceOnlyInPurchaseRegister_DetectsSupplierNotFiled()
    {
        // Arrange
        var session = CreateSession();
        var prInvoice = CreateInvoice("INV-002", 50000m, igst: 9000m);

        session.LoadPurchaseRegisterInvoices(new[] { prInvoice });
        session.LoadGstr2bInvoices(Enumerable.Empty<Invoice>());

        // Act
        var result = await _engine.ReconcileAsync(session, Enumerable.Empty<SupplierProfile>());

        // Assert
        result.TotalMismatches.Should().Be(1);
        session.Mismatches[0].MismatchType.Should().Be(MismatchType.SupplierNotFiled);
        session.Mismatches[0].ItcAmountAtRisk.Should().Be(9000m);
    }

    // ── Test 3: Amount difference ─────────────────────────────────
    [Fact]
    public async Task ReconcileAsync_AmountDifference_DetectsAmountMismatch()
    {
        // Arrange
        var session = CreateSession();
        var prInvoice    = CreateInvoice("INV-003", 100000m, igst: 18000m);
        var gstr2bInvoice = CreateInvoice("INV-003", 100000m, igst: 17500m); // ₹500 less

        session.LoadPurchaseRegisterInvoices(new[] { prInvoice });
        session.LoadGstr2bInvoices(new[] { gstr2bInvoice });

        // Act
        var result = await _engine.ReconcileAsync(session, Enumerable.Empty<SupplierProfile>());

        // Assert
        result.TotalMismatches.Should().Be(1);
        session.Mismatches[0].MismatchType.Should().Be(MismatchType.AmountDifference);
        session.Mismatches[0].ItcAmountAtRisk.Should().Be(500m); // Only difference
    }

    // ── Test 4: Rounding tolerance — NOT a mismatch ───────────────
    [Fact]
    public async Task ReconcileAsync_AmountWithin1Rupee_NotFlaggedAsMismatch()
    {
        // Arrange — ₹0.50 difference should be ignored (rounding)
        var session = CreateSession();
        var prInvoice    = CreateInvoice("INV-004", 100000m, igst: 18000.00m);
        var gstr2bInvoice = CreateInvoice("INV-004", 100000m, igst: 18000.50m);

        session.LoadPurchaseRegisterInvoices(new[] { prInvoice });
        session.LoadGstr2bInvoices(new[] { gstr2bInvoice });

        // Act
        var result = await _engine.ReconcileAsync(session, Enumerable.Empty<SupplierProfile>());

        // Assert — rounding differences must not generate noise
        result.TotalMismatches.Should().Be(0);
    }

    // ── Test 5: Case-insensitive invoice number matching ──────────
    [Fact]
    public async Task ReconcileAsync_InvoiceNumberCaseDifference_StillMatches()
    {
        // Real scenario: buyer books say "inv-005", supplier files "INV-005"
        var session = CreateSession();
        var prInvoice    = CreateInvoice("inv-005", 100000m, igst: 18000m);
        var gstr2bInvoice = CreateInvoice("INV-005", 100000m, igst: 18000m);

        session.LoadPurchaseRegisterInvoices(new[] { prInvoice });
        session.LoadGstr2bInvoices(new[] { gstr2bInvoice });

        var result = await _engine.ReconcileAsync(session, Enumerable.Empty<SupplierProfile>());

        // Must match — case difference is NOT a business mismatch
        result.TotalMismatches.Should().Be(0);
        result.MatchedInvoices.Should().Be(1);
    }

    // ── Test 6: Multiple mismatches in one session ─────────────────
    [Fact]
    public async Task ReconcileAsync_MultipleMismatchTypes_DetectsAll()
    {
        var session = CreateSession();

        var prInvoices = new[]
        {
            CreateInvoice("INV-010", 100000m, igst: 18000m),  // Will match fine
            CreateInvoice("INV-011", 50000m,  igst: 9000m),   // Supplier not filed
            CreateInvoice("INV-012", 75000m,  igst: 13500m),  // Amount difference
        };

        var gstr2bInvoices = new[]
        {
            CreateInvoice("INV-010", 100000m, igst: 18000m),  // Perfect match
            CreateInvoice("INV-012", 75000m,  igst: 12000m),  // ₹1500 difference
        };

        session.LoadPurchaseRegisterInvoices(prInvoices);
        session.LoadGstr2bInvoices(gstr2bInvoices);

        var result = await _engine.ReconcileAsync(session, Enumerable.Empty<SupplierProfile>());

        result.TotalMismatches.Should().Be(2); // SupplierNotFiled + AmountDifference
        result.TotalItcAtRisk.Should().BeGreaterThan(0);
    }

    // ── Test 7: ITC at risk calculation ───────────────────────────
    [Fact]
    public async Task ReconcileAsync_SupplierNotFiled_FullItcAmountAtRisk()
    {
        var session = CreateSession();
        var prInvoice = CreateInvoice("INV-020", 500000m, igst: 90000m);

        session.LoadPurchaseRegisterInvoices(new[] { prInvoice });
        session.LoadGstr2bInvoices(Enumerable.Empty<Invoice>());

        var result = await _engine.ReconcileAsync(session, Enumerable.Empty<SupplierProfile>());

        // Full ₹90,000 ITC is at risk when supplier hasn't filed
        result.TotalItcAtRisk.Should().Be(90000m);
    }

    // ── Test 8: Empty purchase register ───────────────────────────
    [Fact]
    public async Task ReconcileAsync_EmptyGstr2b_AllInvoicesFlaggedAsSupplierNotFiled()
    {
        var session = CreateSession();
        var prInvoices = new[]
        {
            CreateInvoice("INV-030", 100000m, igst: 18000m),
            CreateInvoice("INV-031", 200000m, igst: 36000m),
        };

        session.LoadPurchaseRegisterInvoices(prInvoices);
        session.LoadGstr2bInvoices(Enumerable.Empty<Invoice>());

        var result = await _engine.ReconcileAsync(session, Enumerable.Empty<SupplierProfile>());

        result.TotalMismatches.Should().Be(2);
        session.Mismatches.Should()
            .OnlyContain(m => m.MismatchType == MismatchType.SupplierNotFiled);
        result.TotalItcAtRisk.Should().Be(54000m); // 18000 + 36000
    }

    // ── Helpers ───────────────────────────────────────────────────

    private static ReconciliationSession CreateSession()
    {
        return ReconciliationSession.Create(BuyerGstin, 2024, 3);
    }

    private static Invoice CreateInvoice(
        string invoiceNumber,
        decimal taxableValue,
        decimal igst = 0m,
        decimal cgst = 0m,
        decimal sgst = 0m)
    {
        return Invoice.Create(
            invoiceNumber: invoiceNumber,
            invoiceDate: new DateOnly(2024, 3, 5),
            supplierGstin: SupplierGstin,
            supplierName: "ABC Traders",
            buyerGstin: BuyerGstin,
            taxableValue: taxableValue,
            igst: igst,
            cgst: cgst,
            sgst: sgst,
            isFromPurchaseRegister: true
        );
    }
}