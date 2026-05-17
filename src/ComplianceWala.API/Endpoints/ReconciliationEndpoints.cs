using ComplianceWala.Application.DTOs.Requests;
using ComplianceWala.Application.Interfaces;

namespace ComplianceWala.API.Endpoints;

/// <summary>
/// Minimal API endpoints for GST reconciliation workflow.
///
/// ENDPOINT DESIGN: Each endpoint does exactly three things:
/// 1. Receive HTTP request
/// 2. Call orchestrator
/// 3. Return HTTP response
/// Zero business logic here.
/// </summary>
public static class ReconciliationEndpoints
{
    public static void MapReconciliationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/reconciliation")
    .WithTags("Reconciliation");

        // POST /api/reconciliation/sessions
        group.MapPost("/sessions", async (
            CreateSessionRequest request,
            IReconciliationOrchestrator orchestrator,
            CancellationToken ct) =>
        {
            var result = await orchestrator.CreateSessionAsync(request, ct);
            return Results.Created($"/api/reconciliation/sessions/{result.SessionId}", result);
        })
        .WithName("CreateSession")
        .WithSummary("Create a new GST reconciliation session");

        // POST /api/reconciliation/sessions/{id}/purchase-register
        group.MapPost("/sessions/{id:guid}/purchase-register", async (
            Guid id,
            UploadGstrRequest request,
            IReconciliationOrchestrator orchestrator,
            CancellationToken ct) =>
        {
            var result = await orchestrator.UploadPurchaseRegisterAsync(id, request, ct);
            return Results.Ok(result);
        })
        .WithName("UploadPurchaseRegister")
        .WithSummary("Upload purchase register (GSTR-1 format JSON)");

        // POST /api/reconciliation/sessions/{id}/gstr2b
        group.MapPost("/sessions/{id:guid}/gstr2b", async (
            Guid id,
            UploadGstrRequest request,
            IReconciliationOrchestrator orchestrator,
            CancellationToken ct) =>
        {
            var result = await orchestrator.UploadGstr2bAndReconcileAsync(id, request, ct);
            return Results.Ok(result);
        })
        .WithName("UploadGstr2bAndReconcile")
        .WithSummary("Upload GSTR-2B and trigger full reconciliation pipeline");

        // GET /api/reconciliation/sessions/{id}
        group.MapGet("/sessions/{id:guid}", async (
            Guid id,
            IReconciliationOrchestrator orchestrator,
            CancellationToken ct) =>
        {
            var result = await orchestrator.GetSessionResultAsync(id, ct);
            return result is null
                ? Results.NotFound(new { message = $"Session {id} not found" })
                : Results.Ok(result);
        })
        .WithName("GetSessionResult")
        .WithSummary("Get reconciliation results for a completed session");


        // POST /api/reconciliation/alerts/scan
        // Manual trigger for testing — same job the scheduler runs
        group.MapPost("/alerts/scan", async (
            IDeadlineAlertService alertService,
            CancellationToken ct) =>
        {
            var summary = await alertService.ScanAndAlertAsync(ct);
            return Results.Ok(summary);
        })
        .WithName("TriggerDeadlineScan")
        .WithSummary("Manually trigger deadline alert scan (dev/test use)");
    }


}