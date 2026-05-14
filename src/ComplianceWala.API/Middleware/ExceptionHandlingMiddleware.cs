using System.Net;
using System.Text.Json;
using ComplianceWala.Domain.Exceptions;

namespace ComplianceWala.API.Middleware;

/// <summary>
/// Global exception handler — catches all unhandled exceptions
/// and returns consistent JSON error responses.
///
/// WHY MIDDLEWARE AND NOT try/catch IN EVERY ENDPOINT?
/// Centralised handling means every endpoint automatically gets
/// consistent error format. No developer can forget to handle errors.
/// One place to change error response structure across the entire API.
/// </summary>
public sealed class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (DomainException ex)
        {
            // Known business rule violation — 400 Bad Request
            _logger.LogWarning(
                "Domain exception: {ErrorCode} — {Message}",
                ex.ErrorCode, ex.Message);

            await WriteErrorResponseAsync(
                context,
                HttpStatusCode.BadRequest,
                ex.ErrorCode,
                ex.Message);
        }
        catch (Exception ex)
        {
            // Unknown error — 500 Internal Server Error
            // Log full stack trace, but never expose it to client
            _logger.LogError(ex,
                "Unhandled exception on {Method} {Path}",
                context.Request.Method,
                context.Request.Path);

            await WriteErrorResponseAsync(
                context,
                HttpStatusCode.InternalServerError,
                "INTERNAL_ERROR",
                "An unexpected error occurred. Please try again.");
        }
    }

    private static async Task WriteErrorResponseAsync(
        HttpContext context,
        HttpStatusCode statusCode,
        string errorCode,
        string message)
    {
        context.Response.StatusCode = (int)statusCode;
        context.Response.ContentType = "application/json";

        var response = new
        {
            errorCode,
            message,
            timestamp = DateTime.UtcNow
        };

        await context.Response.WriteAsync(
            JsonSerializer.Serialize(response, JsonOptions));
    }
}