using ComplianceWala.Domain.Enums;
using ComplianceWala.Domain.Exceptions;
using ComplianceWala.Domain.ValueObjects;

namespace ComplianceWala.Domain.Entities;

/// <summary>
/// AGGREGATE ROOT — The central entity of ComplianceWala's domain.
/// 
/// Represents one complete GST reconciliation run for a specific business
/// for a specific GST filing period (month + year).
/// 
/// All operations flow through this entity:
///   Upload GSTR-1 → attach to session
///   Upload GSTR-2B → attach to session  
///   Run reconciliation → session generates MismatchRecords
///   Request AI explanations → session updates MismatchRecords
///
/// You NEVER create MismatchRecord directly — only via session.AddMismatch().
/// </summary>
public class ReconciliationSession
{
    // ── Identity ──────────────────────────────────────────────────
    public Guid Id { get; private set; }

    /// <summary>GSTIN of the business running this reconciliation.</summary>
    public string BusinessGstin { get; private set; }

    /// <summary>
    /// The GST filing period being reconciled.
    /// Format: "2024-03" = March 2024 GSTR-1 vs GSTR-2B reconciliation.
    /// </summary>
    public string FilingPeriod { get; private set; }

    // ── Invoice Collections ───────────────────────────────────────
    
    private readonly List<Invoice> _purchaseRegisterInvoices = new();
    private readonly List<Invoice> _gstr2bInvoices = new();
    private readonly List<MismatchRecord> _mismatches = new();

    public IReadOnlyList<Invoice> PurchaseRegisterInvoices => _purchaseRegisterInvoices;
    public IReadOnlyList<Invoice> Gstr2bInvoices => _gstr2bInvoices;
    public IReadOnlyList<MismatchRecord> Mismatches => _mismatches;

    // ── Summary Metrics (computed) ────────────────────────────────
    
    public decimal TotalItcInBooks =>
        _purchaseRegisterInvoices.Sum(i => i.TotalItc);

    public decimal TotalItcInGstr2b =>
        _gstr2bInvoices.Sum(i => i.TotalItc);

    public decimal TotalItcAtRisk =>
        _mismatches.Sum(m => m.ItcAmountAtRisk);

    public int TotalMismatches => _mismatches.Count;
    public int ResolvedMismatches => _mismatches.Count(m => m.IsResolved);

    // ── Status ────────────────────────────────────────────────────
    public SessionStatus Status { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }

    // ── Constructor ───────────────────────────────────────────────
    private ReconciliationSession() { }

    public static ReconciliationSession Create(string businessGstin, int year, int month)
    {
        if (string.IsNullOrWhiteSpace(businessGstin) || businessGstin.Length != 15)
            throw new DomainException("INVALID_GSTIN",
                $"Business GSTIN must be 15 characters. Received: '{businessGstin}'");

        if (year < 2017)
            throw new DomainException("INVALID_YEAR",
                "GST did not exist before July 2017.");

        if (month < 1 || month > 12)
            throw new DomainException("INVALID_MONTH",
                $"Month must be 1–12. Received: {month}");

        return new ReconciliationSession
        {
            Id = Guid.NewGuid(),
            BusinessGstin = businessGstin.Trim().ToUpperInvariant(),
            FilingPeriod = $"{year}-{month:D2}",
            Status = SessionStatus.AwaitingUploads,
            CreatedAt = DateTime.UtcNow
        };
    }

    // ── Behavior Methods ──────────────────────────────────────────

    public void LoadPurchaseRegisterInvoices(IEnumerable<Invoice> invoices)
    {
        if (Status != SessionStatus.AwaitingUploads)
            throw new DomainException("INVALID_SESSION_STATE",
                $"Cannot load invoices. Session is in '{Status}' state.");

        var invoiceList = invoices.ToList();
        if (invoiceList.Count == 0)
            throw new DomainException("EMPTY_INVOICE_LIST",
                "Purchase register must contain at least one invoice.");

        _purchaseRegisterInvoices.AddRange(invoiceList);
    }

    public void LoadGstr2bInvoices(IEnumerable<Invoice> invoices)
    {
        if (Status != SessionStatus.AwaitingUploads)
            throw new DomainException("INVALID_SESSION_STATE",
                $"Cannot load GSTR-2B invoices. Session is in '{Status}' state.");

        _gstr2bInvoices.AddRange(invoices);
        Status = SessionStatus.ReadyForReconciliation;
    }

    /// <summary>
    /// Called by the reconciliation engine (Day 4) 
    /// to register a detected mismatch.
    /// </summary>
    public void AddMismatch(MismatchRecord mismatch)
    {
        if (Status != SessionStatus.Reconciling)
            throw new DomainException("INVALID_SESSION_STATE",
                "Cannot add mismatches. Session must be in 'Reconciling' state.");

        _mismatches.Add(mismatch);
    }

    public void StartReconciliation()
    {
        if (Status != SessionStatus.ReadyForReconciliation)
            throw new DomainException("INVALID_SESSION_STATE",
                "Both purchase register and GSTR-2B must be loaded before reconciling.");

        Status = SessionStatus.Reconciling;
    }

    public void CompleteReconciliation()
    {
        if (Status != SessionStatus.Reconciling)
            throw new DomainException("INVALID_SESSION_STATE",
                "Cannot complete — session is not in Reconciling state.");

        Status = SessionStatus.Completed;
        CompletedAt = DateTime.UtcNow;
    }
}