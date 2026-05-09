using ComplianceWala.Infrastructure.Parsers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace ComplianceWala.Tests.Parsers;

public class Gstr1JsonParserTests
{
    private readonly Gstr1JsonParser _parser;

    public Gstr1JsonParserTests()
    {
        // NullLogger = logger that discards all messages — 
        // perfect for tests where logging output doesn't matter
        _parser = new Gstr1JsonParser(NullLogger<Gstr1JsonParser>.Instance);
    }

    [Fact]
    public async Task ParseAsync_ValidGstr1Json_ReturnsCorrectInvoiceCount()
    {
        // Arrange
        var json = GetValidGstr1Json();

        // Act
        var result = await _parser.ParseAsync(json);

        // Assert
        result.Invoices.Should().HaveCount(1);
        result.SupplierGstin.Should().Be("27AABCU9603R1ZX");
        result.FilingPeriod.Should().Be("2024-03");
    }

    [Fact]
    public async Task ParseAsync_ValidGstr1Json_CalculatesItcCorrectly()
    {
        var json = GetValidGstr1Json();
        var result = await _parser.ParseAsync(json);

        result.TotalItc.Should().Be(18000m);
        result.Invoices[0].TotalItc.Should().Be(18000m);
    }

    [Fact]
    public async Task ParseAsync_EmptyJson_ThrowsDomainException()
    {
        // Act
        var act = async () => await _parser.ParseAsync(string.Empty);

        // Assert
        await act.Should().ThrowAsync<Domain.Exceptions.DomainException>()
            .WithMessage("*empty*");
    }

    [Fact]
    public async Task ParseAsync_InvalidJson_ThrowsDomainException()
    {
        var act = async () => await _parser.ParseAsync("{ not valid json }}}");

        await act.Should().ThrowAsync<Domain.Exceptions.DomainException>()
            .WithMessage("*invalid*");
    }

    // ── Test Data ─────────────────────────────────────────────────
    
    private static string GetValidGstr1Json() => """
        {
          "gstin": "27AABCU9603R1ZX",
          "fp": "032024",
          "b2b": [
            {
              "ctin": "29GGGGG1314R9Z6",
              "inv": [
                {
                  "inum": "INV-2024-001",
                  "idt": "05-03-2024",
                  "val": 118000.00,
                  "pos": "29",
                  "rchrg": "N",
                  "itms": [
                    {
                      "num": 1,
                      "itm_det": {
                        "txval": 100000.00,
                        "rt": 18,
                        "iamt": 18000.00,
                        "camt": 0,
                        "samt": 0
                      }
                    }
                  ]
                }
              ]
            }
          ]
        }
        """;
}