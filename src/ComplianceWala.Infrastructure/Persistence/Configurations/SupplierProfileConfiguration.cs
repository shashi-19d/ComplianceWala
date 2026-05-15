using System.Text.Json;
using ComplianceWala.Domain.Entities;
using ComplianceWala.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
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

        // ── Fix: ValueConverter + ValueComparer for Dictionary ────
        var jsonOptions = new JsonSerializerOptions();

        var converter = new ValueConverter<Dictionary<string, FilingStatus>, string>(
            dict => JsonSerializer.Serialize(dict, jsonOptions),
            json => JsonSerializer.Deserialize<Dictionary<string, FilingStatus>>(
                        json, jsonOptions)
                    ?? new Dictionary<string, FilingStatus>()
        );

        // ValueComparer is REQUIRED when using a converter on a collection type.
        // Without it, EF Core cannot detect when dictionary contents change
        // and will never save updates to filing history.
        var comparer = new ValueComparer<Dictionary<string, FilingStatus>>(
            (a, b) => JsonSerializer.Serialize(a, jsonOptions)
                   == JsonSerializer.Serialize(b, jsonOptions),
            dict => JsonSerializer.Serialize(dict, jsonOptions).GetHashCode(),
            dict => JsonSerializer.Deserialize<Dictionary<string, FilingStatus>>(
                        JsonSerializer.Serialize(dict, jsonOptions), jsonOptions)
                    ?? new Dictionary<string, FilingStatus>()
        );

        builder
            .Property<Dictionary<string, FilingStatus>>("_filingHistory")
            .HasColumnName("filing_history")
            .HasColumnType("TEXT")
            .HasConversion(converter, comparer)
            .IsRequired();

        builder.HasIndex(s => s.Gstin)
            .IsUnique()
            .HasDatabaseName("ix_supplier_profiles_gstin");
    }
}