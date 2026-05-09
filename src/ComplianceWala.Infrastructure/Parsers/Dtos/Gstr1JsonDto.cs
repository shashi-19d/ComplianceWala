using System.Text.Json.Serialization;

namespace ComplianceWala.Infrastructure.Parsers.Dtos;

/// <summary>
/// DTO that mirrors the exact JSON structure of GSTR-1 government export.
/// 
/// DTO RULE: These classes exist ONLY to deserialize JSON.
/// They have NO business logic. NO validation. NO methods.
/// They are data bags. The Parser converts them into rich domain entities.
/// 
/// JsonPropertyName attributes map C# PascalCase to government's 
/// lowercase_underscore JSON keys.
/// </summary>
internal sealed class Gstr1RootDto
{
    [JsonPropertyName("gstin")]
    public string SupplierGstin { get; init; } = string.Empty;

    /// <summary>Filing period in MMYYYY format. Example: "032024" = March 2024</summary>
    [JsonPropertyName("fp")]
    public string FilingPeriod { get; init; } = string.Empty;

    /// <summary>B2B = Business to Business invoice list</summary>
    [JsonPropertyName("b2b")]
    public List<Gstr1BuyerDto> Buyers { get; init; } = new();
}

internal sealed class Gstr1BuyerDto
{
    /// <summary>Customer/Buyer GSTIN</summary>
    [JsonPropertyName("ctin")]
    public string BuyerGstin { get; init; } = string.Empty;

    [JsonPropertyName("inv")]
    public List<Gstr1InvoiceDto> Invoices { get; init; } = new();
}

internal sealed class Gstr1InvoiceDto
{
    /// <summary>Invoice number as printed on document</summary>
    [JsonPropertyName("inum")]
    public string InvoiceNumber { get; init; } = string.Empty;

    /// <summary>Invoice date in DD-MM-YYYY format</summary>
    [JsonPropertyName("idt")]
    public string InvoiceDate { get; init; } = string.Empty;

    /// <summary>Total invoice value including GST</summary>
    [JsonPropertyName("val")]
    public decimal TotalValue { get; init; }

    [JsonPropertyName("itms")]
    public List<Gstr1ItemDto> Items { get; init; } = new();
}

internal sealed class Gstr1ItemDto
{
    [JsonPropertyName("itm_det")]
    public Gstr1ItemDetailDto Detail { get; init; } = new();
}

internal sealed class Gstr1ItemDetailDto
{
    /// <summary>Taxable value before GST</summary>
    [JsonPropertyName("txval")]
    public decimal TaxableValue { get; init; }

    [JsonPropertyName("rt")]
    public decimal Rate { get; init; }

    /// <summary>Integrated GST (inter-state)</summary>
    [JsonPropertyName("iamt")]
    public decimal Igst { get; init; }

    /// <summary>Central GST (intra-state, half)</summary>
    [JsonPropertyName("camt")]
    public decimal Cgst { get; init; }

    /// <summary>State GST (intra-state, half)</summary>
    [JsonPropertyName("samt")]
    public decimal Sgst { get; init; }
}