using System.Diagnostics;
using ComplianceWala.Application.Interfaces;
using ComplianceWala.Domain.Entities;
using ComplianceWala.Domain.Enums;
using ComplianceWala.Domain.Exceptions;
using ComplianceWala.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace ComplianceWala.Application.Services;

/// <summary>
/// Core reconciliation engine.
/// Implements the GSTR-1 vs GSTR-2B diff algorithm.
///
/// ALGORITHM OVERVIEW:
/// Phase 1 — Build lookup indexes (O(n) setup)
/// Phase 2 — Match and compare invoices (O(n) per invoice)
/// Phase 3 — Detect orphan GSTR-2B entries (O(n))
/// Phase 4 — Detect duplicates in GSTR-2B (O(n))
/// Total: O(n) — linear time regardless of invoice count
/// </summary>
public sealed class ReconciliationEngine : IReconciliationEngine
{
    // Tolerance for amount comparison.
    // ₹1 difference = rounding, not a real mismatch.
    // ₹1.01+ difference = real discrepancy worth flagging.
    private const decimal AmountToleranceRupees = 1.00m;

    // If invoice dates differ by more than this, flag as date mismatch.
    // Within 30 days = supplier filed in adjacent GST period (acceptable).
    // Beyond 30 days = genuine date error on the invoice.
    private const int DateToleranceDays = 30;

    private readonly ILogger<ReconciliationEngine> _logger;

    public ReconciliationEngine(ILogger<ReconciliationEngine> logger)
    {
        _logger = logger;
    }

    public async Task<ReconciliationResult> ReconcileAsync(
        ReconciliationSession session,
        IEnumerable<SupplierProfile> supplierProfiles,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        // ── Guard: session must be ready ─────────────────────────
        if (session.Status != SessionStatus.ReadyForReconciliation)
            throw new DomainException("INVALID_SESSION_STATE",
                $"Session must be in 'ReadyForReconciliation' state. " +
                $"Current state: {session.Status}");

        _logger.LogInformation(
            "Starting reconciliation for session {SessionId}, period {Period}",
            session.Id, session.FilingPeriod);

        // ── Transition session state ──────────────────────────────
        session.StartReconciliation();

        // ── Build supplier profile lookup (O(1) access by GSTIN) ─
        var supplierLookup = supplierProfiles
            .ToDictionary(s => s.Gstin, s => s);

        // ── PHASE 1: Build GSTR-2B index ─────────────────────────
        // Key: normalized invoice number
        // Value: list of GSTR-2B invoices with that number
        //        (list because duplicates may exist — same invoice filed twice)
        var gstr2bIndex = BuildGstr2bIndex(session.Gstr2bInvoices);

        // Track which GSTR-2B invoices were matched
        // Key: invoice number, Value: match count
        var gstr2bMatchCount = gstr2bIndex.Keys
            .ToDictionary(k => k, _ => 0);

        // ── PHASE 2: Walk purchase register, match against GSTR-2B ─
        var matchedCount = 0;

        foreach (var prInvoice in session.PurchaseRegisterInvoices)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var normalizedNumber = NormalizeInvoiceNumber(prInvoice.InvoiceNumber);

            if (!gstr2bIndex.TryGetValue(normalizedNumber, out var gstr2bMatches))
            {
                // ── MISMATCH TYPE 1: SupplierNotFiled ─────────────
                // Invoice in buyer's books but completely absent from GSTR-2B
                // Most common mismatch — supplier simply didn't file GSTR-1
                var riskScore = GetRiskScore(
                    prInvoice.SupplierGstin,
                    prInvoice.TotalItc,
                    supplierLookup);

                var mismatch = MismatchRecord.Detect(
                    reconciliationSessionId: session.Id,
                    mismatchType: MismatchType.SupplierNotFiled,
                    itcAmountAtRisk: prInvoice.TotalItc,
                    riskScore: riskScore,
                    purchaseRegisterInvoice: prInvoice,
                    gstr2bInvoice: null
                );

                session.AddMismatch(mismatch);
                continue;
            }

            // Invoice exists in GSTR-2B — increment match count
            gstr2bMatchCount[normalizedNumber]++;
            matchedCount++;

            // Use the first match for comparison
            var gstr2bInvoice = gstr2bMatches[0];

            // ── PHASE 2a: Compare matched invoices field by field ──
            var fieldMismatches = CompareInvoices(
                prInvoice,
                gstr2bInvoice,
                session.Id,
                supplierLookup);

            foreach (var mismatch in fieldMismatches)
                session.AddMismatch(mismatch);
        }

        // ── PHASE 3: Detect duplicates in GSTR-2B ─────────────────
        // If a GSTR-2B invoice was matched more than once,
        // it means the supplier filed the same invoice twice
        foreach (var (invoiceNumber, matchCount) in gstr2bMatchCount)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (matchCount > 1)
            {
                var duplicateInvoice = gstr2bIndex[invoiceNumber][0];
                var riskScore = ItcRiskScore.Zero(); // Duplicate = no new ITC at risk

                var mismatch = MismatchRecord.Detect(
                    reconciliationSessionId: session.Id,
                    mismatchType: MismatchType.DuplicateInvoice,
                    itcAmountAtRisk: 0m,
                    riskScore: riskScore,
                    purchaseRegisterInvoice: null,
                    gstr2bInvoice: duplicateInvoice
                );

                session.AddMismatch(mismatch);

                _logger.LogWarning(
                    "Duplicate invoice detected: {InvoiceNumber} appears {Count} times in GSTR-2B",
                    invoiceNumber, matchCount);
            }
        }

        // ── Complete session ───────────────────────────────────────
        session.CompleteReconciliation();
        stopwatch.Stop();

        _logger.LogInformation(
            "Reconciliation complete. Session {SessionId}: {Mismatches} mismatches, " +
            "₹{ItcAtRisk:N0} ITC at risk, {Duration}ms",
            session.Id,
            session.TotalMismatches,
            session.TotalItcAtRisk,
            stopwatch.ElapsedMilliseconds);

        await Task.CompletedTask;

        return new ReconciliationResult(
            SessionId: session.Id,
            FilingPeriod: session.FilingPeriod,
            TotalPurchaseRegisterInvoices: session.PurchaseRegisterInvoices.Count,
            TotalGstr2bInvoices: session.Gstr2bInvoices.Count,
            TotalMismatches: session.TotalMismatches,
            MatchedInvoices: matchedCount,
            TotalItcInBooks: session.TotalItcInBooks,
            TotalItcInGstr2b: session.TotalItcInGstr2b,
            TotalItcAtRisk: session.TotalItcAtRisk,
            ProcessingTime: stopwatch.Elapsed
        );
    }

    // ── Private: Compare two matched invoices ─────────────────────

    private List<MismatchRecord> CompareInvoices(
        Invoice prInvoice,
        Invoice gstr2bInvoice,
        Guid sessionId,
        Dictionary<string, SupplierProfile> supplierLookup)
    {
        var mismatches = new List<MismatchRecord>();

        // ── Check 1: GSTIN Mismatch ───────────────────────────────
        if (!string.Equals(
                prInvoice.SupplierGstin,
                gstr2bInvoice.SupplierGstin,
                StringComparison.OrdinalIgnoreCase))
        {
            mismatches.Add(MismatchRecord.Detect(
                sessionId,
                MismatchType.GstinMismatch,
                itcAmountAtRisk: prInvoice.TotalItc,
                riskScore: GetRiskScore(prInvoice.SupplierGstin, prInvoice.TotalItc, supplierLookup),
                purchaseRegisterInvoice: prInvoice,
                gstr2bInvoice: gstr2bInvoice
            ));
        }

        // ── Check 2: Amount Difference ────────────────────────────
        var itcDifference = Math.Abs(prInvoice.TotalItc - gstr2bInvoice.TotalItc);
        if (itcDifference > AmountToleranceRupees)
        {
            mismatches.Add(MismatchRecord.Detect(
                sessionId,
                MismatchType.AmountDifference,
                itcAmountAtRisk: itcDifference,  // Only the DIFFERENCE is at risk
                riskScore: GetRiskScore(prInvoice.SupplierGstin, itcDifference, supplierLookup),
                purchaseRegisterInvoice: prInvoice,
                gstr2bInvoice: gstr2bInvoice
            ));
        }

        // ── Check 3: Invoice Date Mismatch ────────────────────────
        var daysDifference = Math.Abs(
            prInvoice.InvoiceDate.DayNumber - gstr2bInvoice.InvoiceDate.DayNumber);

        if (daysDifference > DateToleranceDays)
        {
            // Date mismatch doesn't necessarily block ITC
            // It may just mean different GST period — flag with low ITC risk
            mismatches.Add(MismatchRecord.Detect(
                sessionId,
                MismatchType.InvoiceDateMismatch,
                itcAmountAtRisk: 0m,  // ITC not necessarily lost
                riskScore: ItcRiskScore.Calculate(0m, 0.20m),  // Low fixed risk
                purchaseRegisterInvoice: prInvoice,
                gstr2bInvoice: gstr2bInvoice
            ));
        }

        // ── Check 4: Rate/GST Type Difference ─────────────────────
        // If one has IGST and other has CGST+SGST — tax type mismatch
        var prIsInterState   = prInvoice.Igst > 0;
        var gstr2bIsInterState = gstr2bInvoice.Igst > 0;

        if (prIsInterState != gstr2bIsInterState)
        {
            mismatches.Add(MismatchRecord.Detect(
                sessionId,
                MismatchType.RateDifference,
                itcAmountAtRisk: prInvoice.TotalItc,
                riskScore: GetRiskScore(prInvoice.SupplierGstin, prInvoice.TotalItc, supplierLookup),
                purchaseRegisterInvoice: prInvoice,
                gstr2bInvoice: gstr2bInvoice
            ));
        }

        return mismatches;
    }

    // ── Private: Build GSTR-2B index ─────────────────────────────

    private static Dictionary<string, List<Invoice>> BuildGstr2bIndex(
        IReadOnlyList<Invoice> gstr2bInvoices)
    {
        var index = new Dictionary<string, List<Invoice>>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var invoice in gstr2bInvoices)
        {
            var key = NormalizeInvoiceNumber(invoice.InvoiceNumber);

            if (!index.ContainsKey(key))
                index[key] = new List<Invoice>();

            index[key].Add(invoice);
        }

        return index;
    }

    // ── Private: Normalize invoice number for matching ────────────
    // "INV-001", "inv-001", "INV/001", " INV-001 " all normalize to "INV001"
    // Removes spaces, slashes, hyphens, converts to uppercase
    private static string NormalizeInvoiceNumber(string invoiceNumber)
    {
        return invoiceNumber
            .Trim()
            .ToUpperInvariant()
            .Replace("-", "")
            .Replace("/", "")
            .Replace(" ", "")
            .Replace("\\", "");
    }

    // ── Private: Get ITC risk from supplier profile ───────────────
    private static ItcRiskScore GetRiskScore(
        string supplierGstin,
        decimal itcAmount,
        Dictionary<string, SupplierProfile> supplierLookup)
    {
        if (supplierLookup.TryGetValue(supplierGstin, out var profile))
            return profile.CalculateItcRisk(itcAmount);

        // Unknown supplier — no filing history — moderate default risk
        return ItcRiskScore.Calculate(itcAmount, 0.30m);
    }
}