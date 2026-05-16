using System.Text.Json;
using ComplianceWala.Application.Interfaces;
using ComplianceWala.Domain.Entities;
using ComplianceWala.Domain.Exceptions;
using ComplianceWala.Infrastructure.Parsers.Dtos;
using Microsoft.Extensions.Logging;

namespace ComplianceWala.Infrastructure.Parsers;

public sealed class Gstr2bJsonParser : IGstr2bParser
{
    private readonly ILogger<Gstr2bJsonParser> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true
    };

    public Gstr2bJsonParser(ILogger<Gstr2bJsonParser> logger)
    {
        _logger = logger;
    }

    public Task<Gstr2bParseResult> ParseAsync(string jsonContent)
    {
        if (string.IsNullOrWhiteSpace(jsonContent))
            throw new DomainException("EMPTY_GSTR2B_JSON",
                "GSTR-2B JSON content cannot be empty.");

        Gstr2bRootDto root;
        try
        {
            root = JsonSerializer.Deserialize<Gstr2bRootDto>(jsonContent, JsonOptions)
                   ?? throw new DomainException("NULL_GSTR2B_JSON",
                       "GSTR-2B JSON deserialized to null.");
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to deserialize GSTR-2B JSON");
            throw new DomainException("INVALID_GSTR2B_FORMAT",
                $"GSTR-2B JSON format is invalid: {ex.Message}");
        }

        var data = root.Data;
        if (string.IsNullOrWhiteSpace(data.BuyerGstin))
            throw new DomainException("MISSING_BUYER_GSTIN",
                "GSTR-2B JSON is missing the buyer GSTIN.");

        var filingPeriod = ParseFilingPeriod(data.ReturnPeriod);
        var invoices = new List<Invoice>();
        var supplierFilingDates = new Dictionary<string, DateTime>();
        var skippedCount = 0;

        foreach (var supplier in data.DocData.Suppliers)
        {
            // ── Extract supplier filing date ──────────────────────
            // This powers our ITC risk score on Day 6
            if (DateTime.TryParseExact(
                    supplier.SupplierFilingDate,
                    "dd-MM-yyyy",
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None,
                    out var filingDate))
            {
                supplierFilingDates[supplier.SupplierGstin.ToUpperInvariant()] = filingDate;
            }
            else
            {
                _logger.LogWarning(
                    "Could not parse filing date '{Date}' for supplier {Gstin}",
                    supplier.SupplierFilingDate, supplier.SupplierGstin);
            }

            // ── Parse invoices for this supplier ──────────────────
            foreach (var invoiceDto in supplier.Invoices)
            {
                try
                {
                    var invoice = MapToInvoice(
                        invoiceDto,
                        supplier.SupplierGstin,
                        supplier.SupplierName,
                        data.BuyerGstin
                    );
                    invoices.Add(invoice);
                }
                catch (DomainException ex)
                {
                    _logger.LogWarning(
                        "Skipping GSTR-2B invoice {InvoiceNumber}: {Error}",
                        invoiceDto.InvoiceNumber, ex.Message);
                    skippedCount++;
                }
            }
        }

        _logger.LogInformation(
            "GSTR-2B parsed: {Count} invoices, {Suppliers} suppliers, {Skipped} skipped",
            invoices.Count, supplierFilingDates.Count, skippedCount);

        return Task.FromResult(new Gstr2bParseResult(
            BuyerGstin: data.BuyerGstin.Trim().ToUpperInvariant(),
            FilingPeriod: filingPeriod,
            Invoices: invoices.AsReadOnly(),
            SupplierFilingDates: supplierFilingDates,
            TotalInvoiceCount: invoices.Count,
            TotalItcAvailable: invoices.Sum(i => i.TotalItc)
        ));
    }

    private static Invoice MapToInvoice(
        Gstr2bInvoiceDto dto,
        string supplierGstin,
        string supplierName,
        string buyerGstin)
    {
        var invoiceDate = ParseInvoiceDate(dto.InvoiceDate, dto.InvoiceNumber);

        var taxableValue = dto.Items.Sum(i => i.TaxableValue);
        var igst = dto.Items.Sum(i => i.Igst);
        var cgst = dto.Items.Sum(i => i.Cgst);
        var sgst = dto.Items.Sum(i => i.Sgst);

        return Invoice.Create(
            invoiceNumber: dto.InvoiceNumber,
            invoiceDate: invoiceDate,
            supplierGstin: supplierGstin,
            supplierName: string.IsNullOrWhiteSpace(supplierName)
                ? supplierGstin  // Fallback to GSTIN if name missing
                : supplierName,
            buyerGstin: buyerGstin,
            taxableValue: taxableValue,
            igst: igst,
            cgst: cgst,
            sgst: sgst,
            isFromPurchaseRegister: false
        );
    }

    private static DateOnly ParseInvoiceDate(string dateString, string invoiceNumber)
    {
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
            $"Invoice {invoiceNumber} has unparseable date: '{dateString}'");
    }

    private static string ParseFilingPeriod(string period)
    {
        if (string.IsNullOrWhiteSpace(period) || period.Length != 6)
            throw new DomainException("INVALID_RETURN_PERIOD",
                $"Return period must be 6 characters (MMYYYY). Received: '{period}'");

        return $"{period[2..]}-{period[..2]}";  // "032024" → "2024-03"
    }
}