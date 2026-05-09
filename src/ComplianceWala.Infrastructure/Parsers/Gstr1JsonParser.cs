using System.Text.Json;
using ComplianceWala.Application.Interfaces;
using ComplianceWala.Domain.Entities;
using ComplianceWala.Domain.Exceptions;
using ComplianceWala.Infrastructure.Parsers.Dtos;
using Microsoft.Extensions.Logging;

namespace ComplianceWala.Infrastructure.Parsers;

/// <summary>
/// Parses GSTR-1 JSON exports from the GST portal into Invoice domain entities.
/// 
/// PARSING STRATEGY:
/// 1. Deserialize JSON → DTO (dumb data container)
/// 2. Validate DTO structure (infrastructure concern)
/// 3. Map DTO → Domain Entity via Invoice.Create() (domain concern)
/// 4. Domain entity's factory handles business rule validation
/// 
/// Errors here: structural (bad JSON, missing fields)
/// Errors in Invoice.Create(): business rules (negative amounts, invalid GSTIN)
/// </summary>
public sealed class Gstr1JsonParser : IGstr1Parser
{
    private readonly ILogger<Gstr1JsonParser> _logger;

    // JSON options configured once, reused for performance
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true
    };

    public Gstr1JsonParser(ILogger<Gstr1JsonParser> logger)
    {
        _logger = logger;
    }

    public async Task<Gstr1ParseResult> ParseAsync(string jsonContent)
    {
        // ── Step 1: Guard against null/empty input ────────────────
        if (string.IsNullOrWhiteSpace(jsonContent))
            throw new DomainException("EMPTY_GSTR1_JSON",
                "GSTR-1 JSON content cannot be empty.");

        // ── Step 2: Deserialize JSON to DTO ───────────────────────
        // We wrap in try-catch because JSON format errors are 
        // infrastructure problems, not domain problems
        Gstr1RootDto root;
        try
        {
            root = JsonSerializer.Deserialize<Gstr1RootDto>(jsonContent, JsonOptions)
                   ?? throw new DomainException("NULL_GSTR1_JSON",
                       "GSTR-1 JSON deserialized to null. File may be empty or corrupt.");
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to deserialize GSTR-1 JSON");
            throw new DomainException("INVALID_GSTR1_FORMAT",
                $"GSTR-1 JSON format is invalid: {ex.Message}");
        }

        // ── Step 3: Validate root-level required fields ───────────
        if (string.IsNullOrWhiteSpace(root.SupplierGstin))
            throw new DomainException("MISSING_SUPPLIER_GSTIN",
                "GSTR-1 JSON is missing the supplier GSTIN ('gstin' field).");

        if (string.IsNullOrWhiteSpace(root.FilingPeriod))
            throw new DomainException("MISSING_FILING_PERIOD",
                "GSTR-1 JSON is missing the filing period ('fp' field).");

        // ── Step 4: Parse filing period ───────────────────────────
        // Government format: "032024" = March 2024
        var filingPeriod = ParseFilingPeriod(root.FilingPeriod);

        // ── Step 5: Map each invoice ──────────────────────────────
        var invoices = new List<Invoice>();
        var skippedCount = 0;

        foreach (var buyer in root.Buyers)
        {
            if (string.IsNullOrWhiteSpace(buyer.BuyerGstin))
            {
                _logger.LogWarning("Skipping B2B entry with missing buyer GSTIN");
                skippedCount++;
                continue;
            }

            foreach (var invoiceDto in buyer.Invoices)
            {
                try
                {
                    var invoice = MapToInvoice(
                        invoiceDto,
                        root.SupplierGstin,
                        supplierName: root.SupplierGstin, // GSTR-1 doesn't have name
                        buyer.BuyerGstin,
                        isFromPurchaseRegister: false
                    );
                    invoices.Add(invoice);
                }
                catch (DomainException ex)
                {
                    // Log and skip bad invoices — don't fail the entire file
                    // A real GST file may have 10,000 invoices; one bad one 
                    // shouldn't block the rest
                    _logger.LogWarning(
                        "Skipping invoice {InvoiceNumber} due to domain error: {Error}",
                        invoiceDto.InvoiceNumber, ex.Message);
                    skippedCount++;
                }
            }
        }

        _logger.LogInformation(
            "GSTR-1 parsed: {SuccessCount} invoices loaded, {SkippedCount} skipped",
            invoices.Count, skippedCount);

        // ── Step 6: Build and return result ──────────────────────
        return new Gstr1ParseResult(
            SupplierGstin: root.SupplierGstin.Trim().ToUpperInvariant(),
            FilingPeriod: filingPeriod,
            Invoices: invoices.AsReadOnly(),
            TotalInvoiceCount: invoices.Count,
            TotalTaxableValue: invoices.Sum(i => i.TaxableValue),
            TotalItc: invoices.Sum(i => i.TotalItc)
        );

        // Wrapped in local function — only this method needs it
        // 'await' makes ParseAsync truly async; for now parsing is sync
        // but the signature is async for future API-based GST fetching
        await Task.CompletedTask;
    }

    // ── Private Helpers ───────────────────────────────────────────

    private static Invoice MapToInvoice(
        Gstr1InvoiceDto dto,
        string supplierGstin,
        string supplierName,
        string buyerGstin,
        bool isFromPurchaseRegister)
    {
        // Parse date from DD-MM-YYYY government format
        var invoiceDate = ParseInvoiceDate(dto.InvoiceDate, dto.InvoiceNumber);

        // Sum across multiple line items (one invoice can have multiple HSN lines)
        var taxableValue = dto.Items.Sum(i => i.Detail.TaxableValue);
        var igst = dto.Items.Sum(i => i.Detail.Igst);
        var cgst = dto.Items.Sum(i => i.Detail.Cgst);
        var sgst = dto.Items.Sum(i => i.Detail.Sgst);

        // Invoice.Create() enforces all domain rules
        return Invoice.Create(
            invoiceNumber: dto.InvoiceNumber,
            invoiceDate: invoiceDate,
            supplierGstin: supplierGstin,
            supplierName: supplierName,
            buyerGstin: buyerGstin,
            taxableValue: taxableValue,
            igst: igst,
            cgst: cgst,
            sgst: sgst,
            isFromPurchaseRegister: isFromPurchaseRegister
        );
    }

    private static DateOnly ParseInvoiceDate(string dateString, string invoiceNumber)
    {
        // Government format: "05-03-2024" = 5th March 2024 (DD-MM-YYYY)
        if (DateOnly.TryParseExact(
                dateString,
                "dd-MM-yyyy",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None,
                out var date))
        {
            return date;
        }

        throw new DomainException("INVALID_INVOICE_DATE",
            $"Invoice {invoiceNumber} has unparseable date: '{dateString}'. " +
            $"Expected format: DD-MM-YYYY");
    }

    private static string ParseFilingPeriod(string fp)
    {
        // Government format: "032024" = March 2024
        // We convert to "2024-03" for our internal consistency
        if (fp.Length != 6)
            throw new DomainException("INVALID_FILING_PERIOD",
                $"Filing period must be 6 characters (MMYYYY). Received: '{fp}'");

        var month = fp[..2];   // First 2 chars
        var year  = fp[2..];   // Last 4 chars

        return $"{year}-{month}";  // "2024-03"
    }
}