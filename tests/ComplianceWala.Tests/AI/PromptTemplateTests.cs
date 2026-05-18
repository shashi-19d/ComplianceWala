using ComplianceWala.Application.Interfaces;
using ComplianceWala.Infrastructure.AI;
using FluentAssertions;

namespace ComplianceWala.Tests.AI;

public class PromptTemplateTests
{
    private static MismatchNarrativeContext CreateContext(
        int daysToDeadline = 15,
        decimal itcAtRisk = 45000m) =>
        new(
            MismatchId:           Guid.NewGuid(),
            MismatchTypeName:     "SupplierNotFiled",
            SupplierName:         "ABC Traders Pvt Ltd",
            SupplierGstin:        "27AABCU9603R1ZX",
            InvoiceNumber:        "INV-2024-001",
            ItcAmountAtRisk:      itcAtRisk,
            DaysToDeadline:       daysToDeadline,
            RecommendedActionName:"ContactSupplierToFile",
            RuleGeneratedSummary: "Contact ABC Traders to file GSTR-1"
        );

    [Fact]
    public void BuildMismatchPrompt_ContainsSupplierName()
    {
        var context = CreateContext();
        var prompt = PromptTemplates.BuildMismatchPrompt(context);
        prompt.Should().Contain("ABC Traders Pvt Ltd");
    }

    [Fact]
    public void BuildMismatchPrompt_ContainsItcAmount()
    {
        var context = CreateContext(itcAtRisk: 45000m);
        var prompt = PromptTemplates.BuildMismatchPrompt(context);
        prompt.Should().Contain("45,000");
    }

    [Fact]
    public void BuildMismatchPrompt_CriticalDeadline_ContainsUrgentLanguage()
    {
        var context = CreateContext(daysToDeadline: 2);
        var prompt = PromptTemplates.BuildMismatchPrompt(context);
        prompt.Should().Contain("CRITICAL");
    }

    [Fact]
    public void BuildMismatchPrompt_UrgentDeadline_ContainsUrgentLanguage()
    {
        var context = CreateContext(daysToDeadline: 7);
        var prompt = PromptTemplates.BuildMismatchPrompt(context);
        prompt.Should().Contain("URGENT");
    }

    [Fact]
    public void BuildMismatchPrompt_NormalDeadline_DoesNotContainUrgent()
    {
        var context = CreateContext(daysToDeadline: 20);
        var prompt = PromptTemplates.BuildMismatchPrompt(context);
        prompt.Should().NotContain("URGENT");
        prompt.Should().NotContain("CRITICAL");
    }

    [Fact]
    public void BuildMismatchPrompt_ContainsJsonFormatInstruction()
    {
        var context = CreateContext();
        var prompt = PromptTemplates.BuildMismatchPrompt(context);
        prompt.Should().Contain("confidence");
        prompt.Should().Contain("english");
        prompt.Should().Contain("hindi");
    }

    [Fact]
    public void BuildMismatchPrompt_ContainsInvoiceNumber()
    {
        var context = CreateContext();
        var prompt = PromptTemplates.BuildMismatchPrompt(context);
        prompt.Should().Contain("INV-2024-001");
    }
}