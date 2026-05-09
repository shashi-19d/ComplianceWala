using ComplianceWala.Domain.Entities;

namespace ComplianceWala.Application.Interfaces;

/// <summary>
/// Contract for parsing GSTR-2B JSON exports from the GST portal.
/// GSTR-2B is the auto-generated ITC statement — the buyer's view.
/// </summary>
public interface IGstr2bParser
{
    /// <summary>
    /// Parses raw GSTR-2B JSON content into Invoice domain entities.
    /// Each invoice has IsFromPurchaseRegister = false (this is the 
    /// government's version, not the buyer's books).
    /// Also extracts supplier filing dates for risk scoring.
    /// </summary>
    Task<Gstr2bParseResult> ParseAsync(string jsonContent);
}

/// <summary>
/// Result of parsing a GSTR-2B file.
/// Includes supplier filing metadata needed for ITC risk scoring.
/// </summary>
public record Gstr2bParseResult(
    string BuyerGstin,
    string FilingPeriod,
    IReadOnlyList<Invoice> Invoices,

    /// <summary>
    /// Maps SupplierGSTIN → Date they filed GSTR-1.
    /// Used by SupplierProfile to update filing history.
    /// Key: "27AABCU9603R1ZX", Value: DateTime of filing
    /// </summary>
    IReadOnlyDictionary<string, DateTime> SupplierFilingDates,

    int TotalInvoiceCount,
    decimal TotalItcAvailable
);