using ComplianceWala.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ComplianceWala.Infrastructure.Persistence.Configurations;

public class MismatchRecordConfiguration : IEntityTypeConfiguration<MismatchRecord>
{
    public void Configure(EntityTypeBuilder<MismatchRecord> builder)
    {
        builder.ToTable("mismatch_records");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.Id)
            .HasColumnName("id");

        builder.Property(m => m.ReconciliationSessionId)
            .HasColumnName("reconciliation_session_id")
            .IsRequired();

        builder.Property(m => m.MismatchType)
            .HasColumnName("mismatch_type")
            .IsRequired();

        builder.Property(m => m.ItcAmountAtRisk)
            .HasColumnName("itc_amount_at_risk")
            .HasPrecision(18, 2);

        builder.Property(m => m.AiExplanation)
            .HasColumnName("ai_explanation")
            .HasMaxLength(2000);

        builder.Property(m => m.AiExplanationHindi)
            .HasColumnName("ai_explanation_hindi")
            .HasMaxLength(2000);

        builder.Property(m => m.AiConfidenceScore)
            .HasColumnName("ai_confidence_score")
            .HasPrecision(4, 3);

        builder.Property(m => m.IsResolved)
            .HasColumnName("is_resolved");

        builder.Property(m => m.DetectedAt)
            .HasColumnName("detected_at");

        builder.Property(m => m.ResolvedAt)
            .HasColumnName("resolved_at");

        // ── ItcRiskScore as an Owned Entity ───────────────────────
        // An Owned Entity maps a Value Object's properties
        // as columns on the SAME table (no separate table needed).
        // ItcRiskScore columns: risk_score_amount, risk_score_probability, risk_score_level
        builder.OwnsOne(m => m.RiskScore, riskScore =>
        {
            riskScore.Property(r => r.AmountAtRisk)
                .HasColumnName("risk_score_amount")
                .HasPrecision(18, 2);

            riskScore.Property(r => r.BlockingProbability)
                .HasColumnName("risk_score_probability")
                .HasPrecision(4, 3);

            riskScore.Property(r => r.Level)
                .HasColumnName("risk_score_level");

            // Computed property — not stored
            riskScore.Ignore(r => r.ExpectedRecoverableAmount);
        });

        // Optional FK to purchase register invoice
        builder.HasOne(m => m.PurchaseRegisterInvoice)
            .WithMany()
            .HasForeignKey("purchase_register_invoice_id")
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);

        // Optional FK to GSTR-2B invoice
        builder.HasOne(m => m.Gstr2bInvoice)
            .WithMany()
            .HasForeignKey("gstr2b_invoice_id")
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(m => m.ReconciliationSessionId)
            .HasDatabaseName("ix_mismatch_records_session_id");

        builder.HasIndex(m => m.MismatchType)
            .HasDatabaseName("ix_mismatch_records_type");
    }
}