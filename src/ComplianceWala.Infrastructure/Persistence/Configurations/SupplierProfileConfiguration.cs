using System.Text.Json;
using ComplianceWala.Domain.Entities;
using ComplianceWala.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace ComplianceWala.Infrastructure.Persistence.Configurations;

public class SupplierProfileConfiguration : IEntityTypeConfiguration<SupplierProfile>
{
    public void Configure(EntityTypeBuilder<SupplierProfile> builder)
    {
        builder.ToTable("supplier_profiles");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id)
            .HasColumnName("id");

        builder.Property(s => s.Gstin)
            .HasColumnName("gstin")
            .HasMaxLength(15)
            .IsRequired();

        builder.Property(s => s.Name)
            .HasColumnName("name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(s => s.LastUpdated)
            .HasColumnName("last_updated");

        // ── The tricky part: Dictionary<string, FilingStatus> ─────
        // _filingHistory is a private field in SupplierProfile.
        // We store it as JSON in a single PostgreSQL column.
        //
        // EF Core accesses private backing fields via the field name.
        // The ValueConverter serializes Dictionary ↔ JSON string.

        var filingHistoryConverter = new ValueConverter<Dictionary<string, FilingStatus>, string>(
            // C# → Database: serialize to JSON
            dict => JsonSerializer.Serialize(dict,
                (JsonSerializerOptions?)null),
            // Database → C#: deserialize from JSON
            json => JsonSerializer.Deserialize<Dictionary<string, FilingStatus>>(
                        json,
                        (JsonSerializerOptions?)null)
                    ?? new Dictionary<string, FilingStatus>()
        );

        builder.Property<Dictionary<string, FilingStatus>>("_filingHistory")
            .HasColumnName("filing_history")
            .HasColumnType("TEXT")
            .HasConversion(filingHistoryConverter)
            .IsRequired();

        // Unique constraint — one profile per GSTIN
        builder.HasIndex(s => s.Gstin)
            .IsUnique()
            .HasDatabaseName("ix_supplier_profiles_gstin");
    }
}