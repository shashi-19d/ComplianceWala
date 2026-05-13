using ComplianceWala.Domain.Entities;
using ComplianceWala.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;

namespace ComplianceWala.Infrastructure.Persistence;

/// <summary>
/// EF Core DbContext for ComplianceWala.
/// 
/// DESIGN DECISIONS:
/// 1. Uses Fluent API configurations in separate IEntityTypeConfiguration classes
///    to keep this file clean and configurations focused.
/// 2. Snake_case table/column names to match PostgreSQL conventions.
/// 3. All domain entities configured — no data annotations on domain entities.
/// </summary>
public sealed class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options) { }

    public DbSet<ReconciliationSession> ReconciliationSessions => Set<ReconciliationSession>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<MismatchRecord> MismatchRecords => Set<MismatchRecord>();
    public DbSet<SupplierProfile> SupplierProfiles => Set<SupplierProfile>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Apply all IEntityTypeConfiguration classes in this assembly
        // This scans and applies InvoiceConfiguration, SupplierProfileConfiguration, etc.
        // Adding a new entity = add a new Configuration class. No changes here.
        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(AppDbContext).Assembly);

        base.OnModelCreating(modelBuilder);
    }
}