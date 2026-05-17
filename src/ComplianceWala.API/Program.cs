using ComplianceWala.API.Endpoints;
using ComplianceWala.API.Middleware;
using ComplianceWala.Application.Interfaces;
using ComplianceWala.Application.Services;
using ComplianceWala.Infrastructure.Parsers;
using ComplianceWala.Infrastructure.Persistence;
using ComplianceWala.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using ComplianceWala.Infrastructure.BackgroundJobs;
using Quartz;

var builder = WebApplication.CreateBuilder(args);

// ── Database ──────────────────────────────────────────────────────
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        sqlite => sqlite.MigrationsAssembly(
            typeof(AppDbContext).Assembly.FullName)));

// ── Repositories ──────────────────────────────────────────────────
builder.Services.AddScoped<IReconciliationSessionRepository,
    ReconciliationSessionRepository>();
builder.Services.AddScoped<ISupplierProfileRepository,
    SupplierProfileRepository>();

// ── Application Services ──────────────────────────────────────────
builder.Services.AddScoped<IReconciliationOrchestrator,
    ReconciliationOrchestrator>();
builder.Services.AddScoped<ISupplierRiskService,
    SupplierRiskService>();
builder.Services.AddScoped<IReconciliationEngine,
    ReconciliationEngine>();

// ── Parsers ───────────────────────────────────────────────────────
builder.Services.AddScoped<IGstr1Parser, Gstr1JsonParser>();
builder.Services.AddScoped<IGstr2bParser, Gstr2bJsonParser>();

// ── Classifier (stateless — Singleton is safe and efficient) ──────
builder.Services.AddSingleton<IMismatchClassifier, MismatchClassifier>();

// ── API Documentation ─────────────────────────────────────────────
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new()
    {
        Title = "ComplianceWala API",
        Version = "v1",
        Description = "AI-Powered GST Reconciliation Engine for Indian SMBs"
    });
});


// ── Application Services ──────────────────────────────────────────
builder.Services.AddScoped<IReconciliationOrchestrator, ReconciliationOrchestrator>();
builder.Services.AddScoped<ISupplierRiskService, SupplierRiskService>();
builder.Services.AddScoped<IReconciliationEngine, ReconciliationEngine>();
builder.Services.AddScoped<IDeadlineAlertService, DeadlineAlertService>(); // ← ADD THIS

// ── Background Jobs ───────────────────────────────────────────────
builder.Services.AddQuartz(q =>
{
    var jobKey = new JobKey("DeadlineAlertJob");

    q.AddJob<DeadlineAlertJob>(opts => opts
        .WithIdentity(jobKey)
        .WithDescription("Scans reconciliation sessions for approaching GST deadlines"));

    q.AddTrigger(opts => opts
        .ForJob(jobKey)
        .WithIdentity("DeadlineAlertJob-trigger")
        .WithDescription("Runs daily at 8:00 AM UTC")
        // Cron: seconds minutes hours day month weekday
        // "0 0 8 * * ?" = at 8:00:00 AM every day
        .WithCronSchedule("0 0 8 * * ?"));
});

builder.Services.AddQuartzHostedService(q =>
    q.WaitForJobsToComplete = true);  // Graceful shutdown waits for running jobs

var app = builder.Build();

// ── Middleware Pipeline (ORDER MATTERS) ───────────────────────────
// Exception handler must be FIRST — wraps everything else
app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "ComplianceWala v1"));
}

// ── Endpoints ─────────────────────────────────────────────────────
app.MapReconciliationEndpoints();
app.MapSupplierEndpoints();

app.MapGet("/health", () => Results.Ok(new
{
    status = "healthy",
    timestamp = DateTime.UtcNow,
    version = "1.0.0"
}));

// ── Auto-apply migrations on startup (dev only) ───────────────────
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

app.Run();