using System.Diagnostics;
using System.Text.Json;
using ComplianceWala.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace ComplianceWala.Infrastructure.AI;

public sealed class AiNarrativeService : IAiNarrativeService
{
    private const string ModelName = "phi3:mini";

    private readonly OllamaClient _ollamaClient;
    private readonly ILogger<AiNarrativeService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public AiNarrativeService(
        OllamaClient ollamaClient,
        ILogger<AiNarrativeService> logger)
    {
        _ollamaClient = ollamaClient;
        _logger = logger;
    }

    public async Task<AiNarrativeResult> GenerateNarrativeAsync(
        MismatchNarrativeContext context,
        CancellationToken ct = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var prompt = PromptTemplates.BuildMismatchPrompt(context);

        _logger.LogInformation(
            "Generating AI narrative for mismatch {MismatchId} " +
            "(type: {Type}, supplier: {Supplier})",
            context.MismatchId, context.MismatchTypeName, context.SupplierName);

        var rawResponse = await _ollamaClient.GenerateAsync(ModelName, prompt, ct);

        stopwatch.Stop();

        // ── Parse AI response ─────────────────────────────────────
        if (rawResponse is not null)
        {
            var parsed = TryParseNarrativeResponse(rawResponse);
            if (parsed is not null)
            {
                _logger.LogInformation(
                    "AI narrative generated for {MismatchId} in {Ms}ms " +
                    "(confidence: {Confidence:P0})",
                    context.MismatchId, stopwatch.ElapsedMilliseconds,
                    parsed.Confidence);

                return new AiNarrativeResult(
                    MismatchId:         context.MismatchId,
                    ExplanationEnglish: parsed.English,
                    ExplanationHindi:   parsed.Hindi,
                    ConfidenceScore:    (decimal)parsed.Confidence,
                    UsedFallback:       false,
                    GenerationTimeMs:   stopwatch.ElapsedMilliseconds
                );
            }

            _logger.LogWarning(
                "AI response for {MismatchId} was not valid JSON. " +
                "Raw: {Raw}",
                context.MismatchId,
                rawResponse.Length > 200
                    ? rawResponse[..200] + "..."
                    : rawResponse);
        }

        // ── Fallback: use rule-generated text ─────────────────────
        // AI is unavailable or returned unparseable response.
        // The system MUST still work — fallback to deterministic text.
        _logger.LogWarning(
            "Using fallback narrative for mismatch {MismatchId}", context.MismatchId);

        return BuildFallbackResult(context, stopwatch.ElapsedMilliseconds);
    }

    public async Task<IReadOnlyList<AiNarrativeResult>> GenerateBatchNarrativesAsync(
        IReadOnlyList<MismatchNarrativeContext> contexts,
        CancellationToken ct = default)
    {
        // Process sequentially — Ollama on 8GB RAM can't handle parallel requests
        // A production system with a GPU server would use Task.WhenAll
        var results = new List<AiNarrativeResult>(contexts.Count);

        foreach (var context in contexts)
        {
            ct.ThrowIfCancellationRequested();
            var result = await GenerateNarrativeAsync(context, ct);
            results.Add(result);
        }

        _logger.LogInformation(
            "Batch complete: {Total} narratives, {Fallbacks} fallbacks used",
            results.Count,
            results.Count(r => r.UsedFallback));

        return results.AsReadOnly();
    }

    // ── Private Helpers ───────────────────────────────────────────

    private NarrativeJsonResponse? TryParseNarrativeResponse(string rawResponse)
    {
        try
        {
            // Strip markdown code fences if model wraps JSON in ```json
            var cleaned = rawResponse.Trim();
            if (cleaned.StartsWith("```"))
            {
                var start = cleaned.IndexOf('\n') + 1;
                var end = cleaned.LastIndexOf("```");
                if (end > start)
                    cleaned = cleaned[start..end].Trim();
            }

            return JsonSerializer.Deserialize<NarrativeJsonResponse>(
                cleaned, JsonOptions);
        }
        catch (JsonException ex)
        {
            _logger.LogDebug(ex, "Failed to parse AI response as JSON");
            return null;
        }
    }

    private static AiNarrativeResult BuildFallbackResult(
        MismatchNarrativeContext context,
        long elapsedMs)
    {
        // Fallback Hindi is a templated translation of the English summary
        var fallbackHindi = $"सप्लायर {context.SupplierName} के " +
            $"इनवॉयस {context.InvoiceNumber} में गड़बड़ी है। " +
            $"₹{context.ItcAmountAtRisk:N0} का ITC खतरे में है। " +
            $"तुरंत कार्रवाई करें।";

        return new AiNarrativeResult(
            MismatchId:         context.MismatchId,
            ExplanationEnglish: context.RuleGeneratedSummary,
            ExplanationHindi:   fallbackHindi,
            ConfidenceScore:    0m,   // 0 = fallback, not AI-generated
            UsedFallback:       true,
            GenerationTimeMs:   elapsedMs
        );
    }

    // ── Internal DTO for JSON parsing ─────────────────────────────
    private record NarrativeJsonResponse(
        string English,
        string Hindi,
        float Confidence);
}