using ComplianceWala.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ComplianceWala.Infrastructure.Persistence.Configurations;

public class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
{
    public void Configure(EntityTypeBuilder<Invoice> builder)
    {
        builder.ToTable("invoices");

        builder.HasKey(i => i.Id);

        builder.Property(i => i.Id)
            .HasColumnName("id");

        builder.Property(i => i.InvoiceNumber)
            .HasColumnName("invoice_number")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(i => i.InvoiceDate)
            .HasColumnName("invoice_date")
            .IsRequired();

        builder.Property(i => i.SupplierGstin)
            .HasColumnName("supplier_gstin")
            .HasMaxLength(15)
            .IsRequired();

        builder.Property(i => i.SupplierName)
            .HasColumnName("supplier_name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(i => i.BuyerGstin)
            .HasColumnName("buyer_gstin")
            .HasMaxLength(15)
            .IsRequired();

        builder.Property(i => i.TaxableValue)
            .HasColumnName("taxable_value")
            .HasPrecision(18, 2);

        builder.Property(i => i.Igst)
            .HasColumnName("igst")
            .HasPrecision(18, 2);

        builder.Property(i => i.Cgst)
            .HasColumnName("cgst")
            .HasPrecision(18, 2);

        builder.Property(i => i.Sgst)
            .HasColumnName("sgst")
            .HasPrecision(18, 2);

        builder.Property(i => i.IsFromPurchaseRegister)
            .HasColumnName("is_from_purchase_register");

        builder.Property(i => i.CreatedAt)
            .HasColumnName("created_at");

        // TotalItc is computed — not stored in DB
        // EF Core ignores computed properties by default if no setter
        // but we explicitly ignore to be safe
        builder.Ignore(i => i.TotalItc);

        // Indexes for common query patterns
        builder.HasIndex(i => i.SupplierGstin)
            .HasDatabaseName("ix_invoices_supplier_gstin");

        builder.HasIndex(i => i.InvoiceNumber)
            .HasDatabaseName("ix_invoices_invoice_number");
    }
}