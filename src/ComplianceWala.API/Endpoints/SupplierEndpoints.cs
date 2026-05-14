using ComplianceWala.Application.Interfaces;

namespace ComplianceWala.API.Endpoints;

public static class SupplierEndpoints
{
    public static void MapSupplierEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/suppliers")
            .WithTags("Suppliers")
            .WithOpenApi();

        // GET /api/suppliers/{gstin}/risk
        group.MapGet("/{gstin}/risk", async (
            string gstin,
            ISupplierProfileRepository repository,
            CancellationToken ct) =>
        {
            var profile = await repository.GetByGstinAsync(gstin, ct);
            if (profile is null)
                return Results.NotFound(
                    new { message = $"No risk data found for GSTIN {gstin}" });

            return Results.Ok(new
            {
                gstin = profile.Gstin,
                name = profile.Name,
                onTimeFilingRate = profile.CalculateOnTimeFilingRate(),
                monthsOfHistory = profile.FilingHistory.Count,
                filingHistory = profile.FilingHistory
            });
        })
        .WithName("GetSupplierRisk")
        .WithSummary("Get filing history and risk profile for a supplier GSTIN");
    }
}