using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace ComplianceWala.Infrastructure.AI;

/// <summary>
/// Thin HTTP client for the Ollama local AI API.
/// Handles only transport concerns — no business logic.
///
/// Ollama API docs: https://github.com/ollama/ollama/blob/main/docs/api.md
/// </summary>
public sealed class OllamaClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<OllamaClient> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public OllamaClient(HttpClient httpClient, ILogger<OllamaClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    /// <summary>
    /// Sends a prompt to the Ollama model and returns the response text.
    /// Returns null if Ollama is unavailable or times out.
    /// </summary>
    public async Task<string?> GenerateAsync(
        string model,
        string prompt,
        CancellationToken ct = default)
    {
        var request = new OllamaGenerateRequest(
            Model: model,
            Prompt: prompt,
            Stream: false,  // Wait for complete response — no streaming
            Options: new OllamaOptions(
                Temperature: 0.3f,  // Low = more deterministic, fewer hallucinations
                NumPredict: 500     // Max tokens in response
            )
        );

        try
        {
            var response = await _httpClient.PostAsJsonAsync(
                "/api/generate",
                request,
                ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Ollama returned {StatusCode} for model {Model}",
                    response.StatusCode, model);
                return null;
            }

            var result = await response.Content
                .ReadFromJsonAsync<OllamaGenerateResponse>(JsonOptions, ct);

            return result?.Response;
        }
        catch (TaskCanceledException)
        {
            _logger.LogWarning("Ollama request timed out for model {Model}", model);
            return null;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex,
                "Ollama is unavailable. Is 'ollama serve' running?");
            return null;
        }
    }

    // ── Request/Response DTOs ─────────────────────────────────────
    // These are private — only OllamaClient knows about Ollama's API shape

    private record OllamaGenerateRequest(
        string Model,
        string Prompt,
        bool Stream,
        OllamaOptions Options);

    private record OllamaOptions(
        float Temperature,
        int NumPredict);

    private record OllamaGenerateResponse(
        string Response,
        bool Done,
        long TotalDuration);
}