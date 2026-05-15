using ComplianceWala.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ComplianceWala.Infrastructure.Persistence.Configurations;

public class ReconciliationSessionConfiguration : IEntityTypeConfiguration<ReconciliationSession>
{
    public void Configure(EntityTypeBuilder<ReconciliationSession> builder)
    {
        builder.ToTable("reconciliation_sessions");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id)
            .HasColumnName("id");

        builder.Property(s => s.BusinessGstin)
            .HasColumnName("business_gstin")
            .HasMaxLength(15)
            .IsRequired();

        builder.Property(s => s.FilingPeriod)
            .HasColumnName("filing_period")
            .HasMaxLength(7)
            .IsRequired();

        builder.Property(s => s.Status)
            .HasColumnName("status")
            .IsRequired();

        builder.Property(s => s.CreatedAt)
            .HasColumnName("created_at");

        builder.Property(s => s.CompletedAt)
            .HasColumnName("completed_at");

        // ── Fix: Explicit FK names prevent shadow property collision ──
        // Two many-to-many relationships to same Invoice type
        // requires explicitly named join table FKs
        builder.HasMany(s => s.PurchaseRegisterInvoices)
            .WithMany()
            .UsingEntity<Dictionary<string, object>>(
                "session_purchase_invoices",
                right => right
                    .HasOne<Invoice>()
                    .WithMany()
                    .HasForeignKey("invoice_id")
                    .OnDelete(DeleteBehavior.Cascade),
                left => left
                    .HasOne<ReconciliationSession>()
                    .WithMany()
                    .HasForeignKey("session_id")
                    .OnDelete(DeleteBehavior.Cascade),
                join =>
                {
                    join.HasKey("session_id", "invoice_id");
                    join.HasIndex("invoice_id")
                        .HasDatabaseName("ix_session_purchase_invoices_invoice_id");
                });

        builder.HasMany(s => s.Gstr2bInvoices)
            .WithMany()
            .UsingEntity<Dictionary<string, object>>(
                "session_gstr2b_invoices",
                right => right
                    .HasOne<Invoice>()
                    .WithMany()
                    .HasForeignKey("invoice_id")
                    .OnDelete(DeleteBehavior.Cascade),
                left => left
                    .HasOne<ReconciliationSession>()
                    .WithMany()
                    .HasForeignKey("session_id")
                    .OnDelete(DeleteBehavior.Cascade),
                join =>
                {
                    join.HasKey("session_id", "invoice_id");
                    join.HasIndex("invoice_id")
                        .HasDatabaseName("ix_session_gstr2b_invoices_invoice_id");
                });

        builder.HasMany(s => s.Mismatches)
            .WithOne()
            .HasForeignKey(m => m.ReconciliationSessionId)
            .OnDelete(DeleteBehavior.Cascade);

        // Computed — not stored
        builder.Ignore(s => s.TotalItcInBooks);
        builder.Ignore(s => s.TotalItcInGstr2b);
        builder.Ignore(s => s.TotalItcAtRisk);
        builder.Ignore(s => s.TotalMismatches);
        builder.Ignore(s => s.ResolvedMismatches);

        builder.HasIndex(s => s.BusinessGstin)
            .HasDatabaseName("ix_reconciliation_sessions_gstin");

        builder.HasIndex(s => new { s.BusinessGstin, s.FilingPeriod })
            .HasDatabaseName("ix_reconciliation_sessions_gstin_period");
    }
}