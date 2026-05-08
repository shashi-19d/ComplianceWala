namespace ComplianceWala.Domain.Enums;

/// <summary>
/// Classifies the root cause of a GSTR-1 vs GSTR-2B reconciliation mismatch.
/// Each type maps to a different resolution action for the SMB owner.
/// </summary>
public enum MismatchType
{
    /// <summary>
    /// Invoice exists in buyer's books but supplier never filed GSTR-1.
    /// Resolution: Contact supplier to file immediately before deadline.
    /// </summary>
    SupplierNotFiled = 1,

    /// <summary>
    /// Same invoice has different dates in buyer's books vs GSTR-2B.
    /// Often happens when supplier files in a different GST period.
    /// Resolution: Accept — ITC will appear in the correct period's GSTR-2B.
    /// </summary>
    InvoiceDateMismatch = 2,

    /// <summary>
    /// GSTIN (tax registration number) on invoice doesn't match GST portal records.
    /// Resolution: Supplier must amend the invoice with correct GSTIN.
    /// </summary>
    GstinMismatch = 3,

    /// <summary>
    /// Invoice amounts differ between buyer's books and GSTR-2B.
    /// Often caused by credit notes or rounding differences.
    /// Resolution: Cross-check credit note or request supplier amendment.
    /// </summary>
    AmountDifference = 4,

    /// <summary>
    /// GST rate applied differs from what GSTR-2B shows.
    /// Caused by HSN code classification disagreements.
    /// Resolution: Verify correct HSN code and applicable GST rate.
    /// </summary>
    RateDifference = 5,

    /// <summary>
    /// Same invoice appears more than once in GSTR-2B.
    /// Resolution: Reject duplicate entry to avoid double ITC claim.
    /// </summary>
    DuplicateInvoice = 6
}