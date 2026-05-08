using ComplianceWala.Domain.Exceptions;

namespace ComplianceWala.Domain.Entities;

/// <summary>
/// Represents a single GST invoice — either from the buyer's purchase register
/// or extracted from GSTR-1 / GSTR-2B government data.
/// 
/// This is the atomic unit of reconciliation. Every mismatch starts here.
/// </summary>
public class Invoice
{
    // ── Identity ──────────────────────────────────────────────────
    public Guid Id { get; private set; }

    // ── Core GST Invoice Fields ───────────────────────────────────
    
    /// <summary>
    /// Invoice number as printed on the physical/digital invoice.
    /// This is the PRIMARY MATCHING KEY during reconciliation.
    /// Same invoice number in buyer's books AND GSTR-2B = potential match.
    /// </summary>
    public string InvoiceNumber { get; private set; }

    public DateOnly InvoiceDate { get; private set; }

    /// <summary>
    /// GSTIN of the supplier who issued this invoice.
    /// Format: 2-digit state code + 10-digit PAN + 1 entity + 1 check + Z
    /// Example: 27AABCU9603R1ZX
    /// </summary>
    public string SupplierGstin { get; private set; }

    public string SupplierName { get; private set; }

    /// <summary>GSTIN of the buyer (your user's business).</summary>
    public string BuyerGstin { get; private set; }

    // ── Financial Fields ──────────────────────────────────────────
    
    /// <summary>Invoice value BEFORE GST is added.</summary>
    public decimal TaxableValue { get; private set; }

    /// <summary>
    /// Integrated GST — applies to inter-state transactions.
    /// Example: Mumbai supplier → Bangalore buyer = IGST applies.
    /// </summary>
    public decimal Igst { get; private set; }

    /// <summary>Central GST — applies to intra-state transactions (half of total GST).</summary>
    public decimal Cgst { get; private set; }

    /// <summary>State GST — applies to intra-state transactions (half of total GST).</summary>
    public decimal Sgst { get; private set; }

    /// <summary>
    /// Total ITC this invoice represents.
    /// = IGST + CGST + SGST
    /// This is the money the buyer can claim back from the government.
    /// </summary>
    public decimal TotalItc => Igst + Cgst + Sgst;

    // ── Source Tracking ───────────────────────────────────────────
    
    /// <summary>
    /// TRUE  = this invoice came from the buyer's own books (purchase register)
    /// FALSE = this invoice came from government's GSTR-2B
    /// During reconciliation we match one against the other.
    /// </summary>
    public bool IsFromPurchaseRegister { get; private set; }

    public DateTime CreatedAt { get; private set; }

    // ── Constructor ───────────────────────────────────────────────
    
    private Invoice() { }  // Required by EF Core — never call directly

    public static Invoice Create(
        string invoiceNumber,
        DateOnly invoiceDate,
        string supplierGstin,
        string supplierName,
        string buyerGstin,
        decimal taxableValue,
        decimal igst,
        decimal cgst,
        decimal sgst,
        bool isFromPurchaseRegister)
    {
        // ── Domain Rule Validations ───────────────────────────────
        
        if (string.IsNullOrWhiteSpace(invoiceNumber))
            throw new DomainException("INVALID_INVOICE_NUMBER",
                "Invoice number cannot be empty.");

        if (string.IsNullOrWhiteSpace(supplierGstin) || supplierGstin.Length != 15)
            throw new DomainException("INVALID_GSTIN",
                $"Supplier GSTIN must be exactly 15 characters. Received: '{supplierGstin}'");

        if (taxableValue < 0)
            throw new DomainException("NEGATIVE_TAXABLE_VALUE",
                $"Taxable value cannot be negative. Received: {taxableValue}");

        if (igst < 0 || cgst < 0 || sgst < 0)
            throw new DomainException("NEGATIVE_TAX_AMOUNT",
                "GST amounts (IGST/CGST/SGST) cannot be negative.");

        // ── Business Rule: IGST and CGST/SGST are mutually exclusive ──
        // Inter-state = only IGST applies
        // Intra-state = only CGST + SGST applies (never all three)
        if (igst > 0 && (cgst > 0 || sgst > 0))
            throw new DomainException("INVALID_GST_COMBINATION",
                "An invoice cannot have both IGST and CGST/SGST. " +
                "IGST applies to inter-state; CGST+SGST to intra-state transactions.");

        return new Invoice
        {
            Id = Guid.NewGuid(),
            InvoiceNumber = invoiceNumber.Trim().ToUpperInvariant(),
            InvoiceDate = invoiceDate,
            SupplierGstin = supplierGstin.Trim().ToUpperInvariant(),
            SupplierName = supplierName.Trim(),
            BuyerGstin = buyerGstin.Trim().ToUpperInvariant(),
            TaxableValue = taxableValue,
            Igst = igst,
            Cgst = cgst,
            Sgst = sgst,
            IsFromPurchaseRegister = isFromPurchaseRegister,
            CreatedAt = DateTime.UtcNow
        };
    }
}