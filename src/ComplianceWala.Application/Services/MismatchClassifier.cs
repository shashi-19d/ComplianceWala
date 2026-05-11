using ComplianceWala.Application.DTOs;
using ComplianceWala.Application.Interfaces;
using ComplianceWala.Domain.Entities;
using ComplianceWala.Domain.Enums;

namespace ComplianceWala.Application.Services;

/// <summary>
/// Deterministic rule engine for mismatch classification.
/// 
/// RULE EXECUTION ORDER (each mismatch goes through all rules):
/// Rule 1: Determine GST deadline from reconciliation date
/// Rule 2: Calculate days remaining to deadline
/// Rule 3: Determine base priority from MismatchType
/// Rule 4: Escalate priority based on ITC amount
/// Rule 5: Escalate priority based on deadline urgency
/// Rule 6: Determine recommended action
/// Rule 7: Determine CA review requirement
/// Rule 8: Generate action summary text
/// </summary>
public sealed class MismatchClassifier : IMismatchClassifier
{
    // ITC amount thresholds for priority escalation
    private const decimal CriticalItcThreshold = 100_000m;  // ₹1 lakh
    private const decimal HighItcThreshold     =  25_000m;  // ₹25,000

    // CA review required above this amount
    private const decimal CaReviewThreshold    = 100_000m;  // ₹1 lakh

    public MismatchClassification Classify(
        MismatchRecord mismatch,
        DateOnly reconciliationDate)
    {
        // ── Rule 1 & 2: Deadline calculation ─────────────────────
        var deadline = CalculateGstr3bDeadline(reconciliationDate);
        var daysToDeadline = deadline.DayNumber - reconciliationDate.DayNumber;

        // ── Rule 3: Base priority from mismatch type ──────────────
        var priority = GetBasePriority(mismatch.MismatchType);

        // ── Rule 4: Escalate on ITC amount ────────────────────────
        priority = EscalateOnAmount(priority, mismatch.ItcAmountAtRisk);

        // ── Rule 5: Escalate on deadline urgency ──────────────────
        priority = EscalateOnDeadline(priority, daysToDeadline);

        // ── Rule 6: Recommended action ────────────────────────────
        var action = DetermineAction(
            mismatch.MismatchType,
            mismatch.ItcAmountAtRisk,
            daysToDeadline);

        // ── Rule 7: CA review requirement ─────────────────────────
        var requiresCa = RequiresCaReview(
            mismatch.MismatchType,
            mismatch.ItcAmountAtRisk);

        // ── Rule 8: Action summary text ───────────────────────────
        var invoice = mismatch.PurchaseRegisterInvoice
                   ?? mismatch.Gstr2bInvoice!;

        var actionSummary = GenerateActionSummary(
            mismatch.MismatchType,
            invoice.SupplierName,
            invoice.InvoiceNumber,
            mismatch.ItcAmountAtRisk,
            daysToDeadline);

        return new MismatchClassification(
            MismatchId:        mismatch.Id,
            SessionId:         mismatch.ReconciliationSessionId,
            MismatchType:      mismatch.MismatchType,
            Priority:          priority,
            RecommendedAction: action,
            ItcAmountAtRisk:   mismatch.ItcAmountAtRisk,
            DaysToDeadline:    daysToDeadline,
            DeadlineDate:      deadline.ToString("dd MMMM yyyy"),
            RequiresCaReview:  requiresCa,
            ActionSummary:     actionSummary,
            SupplierGstin:     invoice.SupplierGstin,
            SupplierName:      invoice.SupplierName,
            InvoiceNumber:     invoice.InvoiceNumber
        );
    }

    public IReadOnlyList<MismatchClassification> ClassifyAll(
        IEnumerable<MismatchRecord> mismatches,
        DateOnly reconciliationDate)
    {
        return mismatches
            .Select(m => Classify(m, reconciliationDate))
            .OrderBy(c => c.Priority)      // Critical (1) first
            .ThenByDescending(c => c.ItcAmountAtRisk) // Highest risk within same priority
            .ToList()
            .AsReadOnly();
    }

    // ── Rule Implementations ──────────────────────────────────────

    /// <summary>
    /// GST rule: GSTR-3B is due on 20th of the month following
    /// the filing period. We calculate which period we're in
    /// and return the upcoming 20th deadline.
    /// </summary>
    private static DateOnly CalculateGstr3bDeadline(DateOnly reconciliationDate)
    {
        // GST rule: GSTR-3B is due on the 20th of the current month.
        // If we are still before the 20th → this month's 20th is the deadline.
        // If we are on/past the 20th → next month's 20th is the deadline.
        if (reconciliationDate.Day <= 20)
            return new DateOnly(
                reconciliationDate.Year,
                reconciliationDate.Month,
                20);

        var nextMonth = reconciliationDate.Month == 12 ? 1 : reconciliationDate.Month + 1;
        var nextYear = reconciliationDate.Month == 12
            ? reconciliationDate.Year + 1
            : reconciliationDate.Year;

        return new DateOnly(nextYear, nextMonth, 20);
    }

    /// <summary>
    /// Base priority rules by mismatch type.
    /// DuplicateInvoice starts Critical — claiming it is fraud.
    /// SupplierNotFiled starts High — common but deadline-sensitive.
    /// Others start Medium or Low.
    /// </summary>
    private static MismatchPriority GetBasePriority(MismatchType type) => type switch
    {
        MismatchType.DuplicateInvoice    => MismatchPriority.Critical,
        MismatchType.GstinMismatch       => MismatchPriority.High,
        MismatchType.SupplierNotFiled    => MismatchPriority.High,
        MismatchType.RateDifference      => MismatchPriority.High,
        MismatchType.AmountDifference    => MismatchPriority.Medium,
        MismatchType.InvoiceDateMismatch => MismatchPriority.Low,
        _ => MismatchPriority.Medium
    };

    /// <summary>
    /// Escalate priority if ITC amount is large enough to warrant urgency.
    /// A ₹2 lakh mismatch deserves more attention than a ₹500 one,
    /// even if the deadline is far away.
    /// </summary>
    private static MismatchPriority EscalateOnAmount(
        MismatchPriority current,
        decimal itcAtRisk)
    {
        if (itcAtRisk >= CriticalItcThreshold)
            return TakeHigherPriority(current, MismatchPriority.Critical);

        if (itcAtRisk >= HighItcThreshold)
            return TakeHigherPriority(current, MismatchPriority.High);

        return current;
    }

    /// <summary>
    /// Escalate priority as deadline approaches.
    /// ≤3 days → Critical regardless of original priority.
    /// 4-10 days → at least High.
    /// >20 days → no escalation.
    /// </summary>
    private static MismatchPriority EscalateOnDeadline(
        MismatchPriority current,
        int daysToDeadline)
    {
        if (daysToDeadline <= 3)
            return MismatchPriority.Critical;

        if (daysToDeadline <= 10)
            return TakeHigherPriority(current, MismatchPriority.High);

        return current;
    }

    /// <summary>
    /// Priority enum: lower int value = higher urgency (Critical=1).
    /// "Higher priority" means the lower integer.
    /// </summary>
    private static MismatchPriority TakeHigherPriority(
        MismatchPriority a,
        MismatchPriority b)
        => (MismatchPriority)Math.Min((int)a, (int)b);

    private static ResolutionAction DetermineAction(
        MismatchType type,
        decimal itcAtRisk,
        int daysToDeadline) => type switch
    {
        MismatchType.SupplierNotFiled    => ResolutionAction.ContactSupplierToFile,
        MismatchType.DuplicateInvoice    => ResolutionAction.RejectDuplicateEntry,
        MismatchType.InvoiceDateMismatch => ResolutionAction.AcceptAndDeferToNextPeriod,
        MismatchType.GstinMismatch       => ResolutionAction.RequestSupplierAmendment,
        MismatchType.RateDifference      => itcAtRisk >= CaReviewThreshold
                                            ? ResolutionAction.EscalateToCa
                                            : ResolutionAction.VerifyHsnClassificationWithCa,
        MismatchType.AmountDifference    => ResolutionAction.RequestSupplierAmendment,
        _ => ResolutionAction.EscalateToCa
    };

    private static bool RequiresCaReview(MismatchType type, decimal itcAtRisk)
    {
        // Always involve CA for large amounts
        if (itcAtRisk >= CaReviewThreshold)
            return true;

        // These types are legally complex regardless of amount
        return type is MismatchType.RateDifference
                    or MismatchType.GstinMismatch
                    or MismatchType.DuplicateInvoice;
    }

    private static string GenerateActionSummary(
        MismatchType type,
        string supplierName,
        string invoiceNumber,
        decimal itcAtRisk,
        int daysToDeadline)
    {
        var urgency = daysToDeadline <= 3
            ? "URGENT: "
            : string.Empty;

        return type switch
        {
            MismatchType.SupplierNotFiled =>
                $"{urgency}Contact {supplierName} to file their GSTR-1 for invoice " +
                $"{invoiceNumber}. ₹{itcAtRisk:N0} ITC at risk. " +
                $"{daysToDeadline} days to GSTR-3B deadline.",

            MismatchType.AmountDifference =>
                $"Request {supplierName} to amend invoice {invoiceNumber}. " +
                $"Amount difference of ₹{itcAtRisk:N0} detected between your books and GSTR-2B.",

            MismatchType.GstinMismatch =>
                $"{urgency}GSTIN mismatch on invoice {invoiceNumber} from {supplierName}. " +
                $"Request amendment. ITC of ₹{itcAtRisk:N0} blocked until corrected.",

            MismatchType.InvoiceDateMismatch =>
                $"Invoice {invoiceNumber} from {supplierName} has a date discrepancy. " +
                $"ITC may appear in next month's GSTR-2B. Monitor next cycle.",

            MismatchType.DuplicateInvoice =>
                $"Invoice {invoiceNumber} appears multiple times in GSTR-2B. " +
                $"DO NOT claim duplicate ITC — this constitutes fraud under GST law.",

            MismatchType.RateDifference =>
                $"GST rate mismatch on invoice {invoiceNumber} from {supplierName}. " +
                $"Verify correct HSN code with your CA. ₹{itcAtRisk:N0} ITC in dispute.",

            _ => $"Review invoice {invoiceNumber} from {supplierName} with your CA."
        };
    }
}