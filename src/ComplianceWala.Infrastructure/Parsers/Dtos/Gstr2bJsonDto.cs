using System.Text.Json.Serialization;

namespace ComplianceWala.Infrastructure.Parsers.Dtos;

/// <summary>
/// DTO mirroring GSTR-2B government JSON structure.
/// Note the extra nesting: data → docdata → b2b
/// This is exactly how the GST portal exports the file.
/// </summary>
internal sealed class Gstr2bRootDto
{
    [JsonPropertyName("data")]
    public Gstr2bDataDto Data { get; init; } = new();
}

internal sealed class Gstr2bDataDto
{
    /// <summary>Buyer's GSTIN (this file belongs to the buyer)</summary>
    [JsonPropertyName("gstin")]
    public string BuyerGstin { get; init; } = string.Empty;

    /// <summary>Return period in MMYYYY format</summary>
    [JsonPropertyName("rtnprd")]
    public string ReturnPeriod { get; init; } = string.Empty;

    [JsonPropertyName("docdata")]
    public Gstr2bDocDataDto DocData { get; init; } = new();
}

internal sealed class Gstr2bDocDataDto
{
    [JsonPropertyName("b2b")]
    public List<Gstr2bSupplierDto> Suppliers { get; init; } = new();
}

internal sealed class Gstr2bSupplierDto
{
    /// <summary>Supplier's GSTIN</summary>
    [JsonPropertyName("ctin")]
    public string SupplierGstin { get; init; } = string.Empty;

    /// <summary>Supplier's trade name</summary>
    [JsonPropertyName("trdnm")]
    public string SupplierName { get; init; } = string.Empty;

    /// <summary>
    /// Date supplier filed GSTR-1 (DD-MM-YYYY).
    /// Critical for ITC risk scoring — did they file on time?
    /// </summary>
    [JsonPropertyName("supfildt")]
    public string SupplierFilingDate { get; init; } = string.Empty;

    [JsonPropertyName("inv")]
    public List<Gstr2bInvoiceDto> Invoices { get; init; } = new();
}

internal sealed class Gstr2bInvoiceDto
{
    [JsonPropertyName("inum")]
    public string InvoiceNumber { get; init; } = string.Empty;

    /// <summary>Invoice date in DD-MM-YYYY format</summary>
    [JsonPropertyName("dt")]
    public string InvoiceDate { get; init; } = string.Empty;

    [JsonPropertyName("val")]
    public decimal TotalValue { get; init; }

    [JsonPropertyName("itms")]
    public List<Gstr2bItemDto> Items { get; init; } = new();
}

internal sealed class Gstr2bItemDto
{
    [JsonPropertyName("txval")]
    public decimal TaxableValue { get; init; }

    [JsonPropertyName("igst")]
    public decimal Igst { get; init; }

    [JsonPropertyName("cgst")]
    public decimal Cgst { get; init; }

    [JsonPropertyName("sgst")]
    public decimal Sgst { get; init; }
}