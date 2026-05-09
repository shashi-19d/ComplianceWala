using ComplianceWala.Domain.Entities;

namespace ComplianceWala.Application.Interfaces;

/// <summary>
/// Contract for parsing GSTR-1 JSON exports from the GST portal.
/// 
/// WHY AN INTERFACE HERE?
/// The reconciliation engine (Application layer) calls this parser.
/// By depending on this interface, the engine never imports anything 
/// from Infrastructure. We can swap JSON parsing for XML or API 
/// without touching the engine.
/// </summary>
public interface IGstr1Parser
{
    /// <summary>
    /// Parses raw GSTR-1 JSON content into a list of Invoice domain entities.
    /// Each invoice in the returned list has IsFromPurchaseRegister = false
    /// because GSTR-1 represents the SUPPLIER's declared sales.
    /// 
    /// We treat GSTR-1 as the "supplier side" for cross-referencing
    /// against the buyer's own purchase register.
    /// </summary>
    /// <param name="jsonContent">Raw JSON string from GST portal export</param>
    /// <returns>Parsed invoices and the filing period (e.g., "2024-03")</returns>
    /// <exception cref="Domain.Exceptions.DomainException">
    /// Thrown if JSON is structurally invalid or contains illegal GST values
    /// </exception>
    Task<Gstr1ParseResult> ParseAsync(string jsonContent);
}

/// <summary>
/// Result of parsing a GSTR-1 file.
/// Contains both the invoices and metadata about the filing.
/// </summary>
public record Gstr1ParseResult(
    string SupplierGstin,
    string FilingPeriod,
    IReadOnlyList<Invoice> Invoices,
    int TotalInvoiceCount,
    decimal TotalTaxableValue,
    decimal TotalItc
);