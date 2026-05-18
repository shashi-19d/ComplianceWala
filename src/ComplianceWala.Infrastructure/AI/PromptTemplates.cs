using ComplianceWala.Application.Interfaces;

namespace ComplianceWala.Infrastructure.AI;

/// <summary>
/// Prompt engineering for GST mismatch explanations.
///
/// PROMPT DESIGN PRINCIPLES:
/// 1. Role assignment — "You are a GST compliance assistant"
///    Models perform better with explicit role context
/// 2. Constraint injection — "Do NOT advise illegal actions"
///    Prevents hallucinated advice that could harm users
/// 3. Output format specification — exact JSON structure required
///    Makes parsing deterministic, avoids prose extraction hacks
/// 4. Few-shot example — one example trains the output style
///    without fine-tuning
/// 5. Context grounding — all numbers passed explicitly
///    Prevents the model from inventing figures
/// </summary>
public static class PromptTemplates
{
    private const string SystemPrompt = """
        You are a GST compliance assistant helping Indian small business owners 
        understand their GST reconciliation mismatches.
        
        Your explanations must be:
        - Simple enough for a non-accountant to understand
        - Accurate (never invent GST rules or amounts)
        - Action-oriented (what should they do right now)
        - Empathetic (these are real financial risks for small businesses)
        
        RULES:
        - Never advise illegal actions (e.g., claiming blocked ITC anyway)
        - Never invent tax amounts not provided in the context
        - Always mention the specific rupee amount at risk
        - Always mention the deadline urgency if days <= 10
        - Hindi must be grammatically correct Devanagari script
        
        Respond ONLY with valid JSON. No markdown, no backticks, no preamble.
        """;

    public static string BuildMismatchPrompt(MismatchNarrativeContext context)
    {
        var urgencyNote = context.DaysToDeadline <= 3
            ? $"CRITICAL: Only {context.DaysToDeadline} days remain before the GSTR-3B deadline."
            : context.DaysToDeadline <= 10
                ? $"URGENT: {context.DaysToDeadline} days remain before the GSTR-3B deadline."
                : $"{context.DaysToDeadline} days remain before the GSTR-3B deadline.";

        return $@"
            {SystemPrompt}
            
            MISMATCH DETAILS:
            - Type: {context.MismatchTypeName}
            - Supplier: {context.SupplierName} (GSTIN: {context.SupplierGstin})
            - Invoice Number: {context.InvoiceNumber}
            - ITC Amount at Risk: ₹{context.ItcAmountAtRisk:N0}
            - Deadline Status: {urgencyNote}
            - Recommended Action: {context.RecommendedActionName}
            - System Summary: {context.RuleGeneratedSummary}
            
            EXAMPLE OUTPUT FORMAT (follow exactly):
            {{
              ""english"": ""Your supplier [Name] has not filed their GSTR-1 for [period]. This means ₹[amount] of your Input Tax Credit is currently blocked. You must contact them immediately and request they file before [date]. If they don't file, you cannot claim this credit."",
              ""hindi"": ""आपके सप्लायर [नाम] ने GSTR-1 नहीं भरा है। इससे आपका ₹[राशि] ITC अटका हुआ है। उन्हें तुरंत संपर्क करें।"",
              ""confidence"": 0.92
            }}
            
            Now generate the explanation for the mismatch above.
            Respond with ONLY the JSON object. Nothing else.
            ";
    }
}