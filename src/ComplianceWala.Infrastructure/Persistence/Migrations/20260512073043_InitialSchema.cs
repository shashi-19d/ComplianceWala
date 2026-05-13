using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ComplianceWala.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ReconciliationSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    BusinessGstin = table.Column<string>(type: "TEXT", nullable: false),
                    FilingPeriod = table.Column<string>(type: "TEXT", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReconciliationSessions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "supplier_profiles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    gstin = table.Column<string>(type: "TEXT", maxLength: 15, nullable: false),
                    name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    last_updated = table.Column<DateTime>(type: "TEXT", nullable: false),
                    filing_history = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_supplier_profiles", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "invoices",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    invoice_number = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    invoice_date = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    supplier_gstin = table.Column<string>(type: "TEXT", maxLength: 15, nullable: false),
                    supplier_name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    buyer_gstin = table.Column<string>(type: "TEXT", maxLength: 15, nullable: false),
                    taxable_value = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    igst = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    cgst = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    sgst = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    is_from_purchase_register = table.Column<bool>(type: "INTEGER", nullable: false),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ReconciliationSessionId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ReconciliationSessionId1 = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_invoices", x => x.id);
                    table.ForeignKey(
                        name: "FK_invoices_ReconciliationSessions_ReconciliationSessionId",
                        column: x => x.ReconciliationSessionId,
                        principalTable: "ReconciliationSessions",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_invoices_ReconciliationSessions_ReconciliationSessionId1",
                        column: x => x.ReconciliationSessionId1,
                        principalTable: "ReconciliationSessions",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "mismatch_records",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    reconciliation_session_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    purchase_register_invoice_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    gstr2b_invoice_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    mismatch_type = table.Column<int>(type: "INTEGER", nullable: false),
                    itc_amount_at_risk = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    risk_score_amount = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    risk_score_probability = table.Column<decimal>(type: "TEXT", precision: 4, scale: 3, nullable: false),
                    risk_score_level = table.Column<int>(type: "INTEGER", nullable: false),
                    ai_explanation = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    ai_explanation_hindi = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    ai_confidence_score = table.Column<decimal>(type: "TEXT", precision: 4, scale: 3, nullable: false),
                    is_resolved = table.Column<bool>(type: "INTEGER", nullable: false),
                    detected_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    resolved_at = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mismatch_records", x => x.id);
                    table.ForeignKey(
                        name: "FK_mismatch_records_ReconciliationSessions_reconciliation_session_id",
                        column: x => x.reconciliation_session_id,
                        principalTable: "ReconciliationSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_mismatch_records_invoices_gstr2b_invoice_id",
                        column: x => x.gstr2b_invoice_id,
                        principalTable: "invoices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_mismatch_records_invoices_purchase_register_invoice_id",
                        column: x => x.purchase_register_invoice_id,
                        principalTable: "invoices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "ix_invoices_invoice_number",
                table: "invoices",
                column: "invoice_number");

            migrationBuilder.CreateIndex(
                name: "IX_invoices_ReconciliationSessionId",
                table: "invoices",
                column: "ReconciliationSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_invoices_ReconciliationSessionId1",
                table: "invoices",
                column: "ReconciliationSessionId1");

            migrationBuilder.CreateIndex(
                name: "ix_invoices_supplier_gstin",
                table: "invoices",
                column: "supplier_gstin");

            migrationBuilder.CreateIndex(
                name: "IX_mismatch_records_gstr2b_invoice_id",
                table: "mismatch_records",
                column: "gstr2b_invoice_id");

            migrationBuilder.CreateIndex(
                name: "IX_mismatch_records_purchase_register_invoice_id",
                table: "mismatch_records",
                column: "purchase_register_invoice_id");

            migrationBuilder.CreateIndex(
                name: "ix_mismatch_records_session_id",
                table: "mismatch_records",
                column: "reconciliation_session_id");

            migrationBuilder.CreateIndex(
                name: "ix_mismatch_records_type",
                table: "mismatch_records",
                column: "mismatch_type");

            migrationBuilder.CreateIndex(
                name: "ix_supplier_profiles_gstin",
                table: "supplier_profiles",
                column: "gstin",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "mismatch_records");

            migrationBuilder.DropTable(
                name: "supplier_profiles");

            migrationBuilder.DropTable(
                name: "invoices");

            migrationBuilder.DropTable(
                name: "ReconciliationSessions");
        }
    }
}
