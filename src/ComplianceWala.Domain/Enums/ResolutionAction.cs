namespace ComplianceWala.Domain.Enums;

/// <summary>
/// The specific action an SMB owner or their CA must take 
/// to resolve a detected mismatch before the GST deadline.
/// Each action maps to a different workflow in the frontend.
/// </summary>
public enum ResolutionAction
{
    /// <summary>
    /// Contact the supplier immediately and request they file GSTR-1.
    /// Used for: SupplierNotFiled
    /// Urgency: Depends on days remaining to GSTR-3B deadline.
    /// </summary>
    ContactSupplierToFile = 1,

    /// <summary>
    /// The invoice will appear in next month's GSTR-2B due to timing.
    /// No action needed now — ITC will be claimable next period.
    /// Used for: InvoiceDateMismatch (within adjacent GST period)
    /// </summary>
    AcceptAndDeferToNextPeriod = 2,

    /// <summary>
    /// Ask supplier to file an amendment (GSTR-1A) to correct the error.
    /// Used for: GstinMismatch, AmountDifference, RateDifference
    /// </summary>
    RequestSupplierAmendment = 3,

    /// <summary>
    /// Do NOT claim ITC for this invoice entry.
    /// Claiming a duplicate entry constitutes ITC fraud under GST law.
    /// Used for: DuplicateInvoice
    /// </summary>
    RejectDuplicateEntry = 4,

    /// <summary>
    /// Verify the correct HSN code and applicable GST rate with your CA.
    /// Used for: RateDifference where HSN classification is ambiguous.
    /// </summary>
    VerifyHsnClassificationWithCa = 5,

    /// <summary>
    /// Mismatch requires professional CA review — amount or complexity 
    /// exceeds what an automated rule can resolve safely.
    /// </summary>
    EscalateToCa = 6
}