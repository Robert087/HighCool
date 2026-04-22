using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSupplierPaymentsModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "supplier_payable_amount",
                table: "purchase_receipts",
                type: "decimal(18,6)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "payments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    payment_no = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    party_type = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    party_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    direction = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    amount = table.Column<decimal>(type: "decimal(18,6)", nullable: false),
                    payment_date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    currency = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                    exchange_rate = table.Column<decimal>(type: "decimal(18,6)", nullable: true),
                    payment_method = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    reference_note = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    created_by = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    updated_by = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_payments_suppliers_party_id",
                        column: x => x.party_id,
                        principalTable: "suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "payment_allocations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    payment_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    target_doc_type = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    target_doc_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    target_line_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    allocated_amount = table.Column<decimal>(type: "decimal(18,6)", nullable: false),
                    allocation_order = table.Column<int>(type: "int", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    created_by = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    updated_by = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payment_allocations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_payment_allocations_payments_payment_id",
                        column: x => x.payment_id,
                        principalTable: "payments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_payment_allocations_payment_id_allocation_order",
                table: "payment_allocations",
                columns: new[] { "payment_id", "allocation_order" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_payment_allocations_target_doc_type_target_doc_id_target_line_id",
                table: "payment_allocations",
                columns: new[] { "target_doc_type", "target_doc_id", "target_line_id" });

            migrationBuilder.CreateIndex(
                name: "IX_payments_direction",
                table: "payments",
                column: "direction");

            migrationBuilder.CreateIndex(
                name: "IX_payments_party_id",
                table: "payments",
                column: "party_id");

            migrationBuilder.CreateIndex(
                name: "IX_payments_party_type_party_id_payment_date",
                table: "payments",
                columns: new[] { "party_type", "party_id", "payment_date" });

            migrationBuilder.CreateIndex(
                name: "IX_payments_payment_method",
                table: "payments",
                column: "payment_method");

            migrationBuilder.CreateIndex(
                name: "IX_payments_payment_no",
                table: "payments",
                column: "payment_no",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_payments_status",
                table: "payments",
                column: "status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "payment_allocations");

            migrationBuilder.DropTable(
                name: "payments");

            migrationBuilder.DropColumn(
                name: "supplier_payable_amount",
                table: "purchase_receipts");
        }
    }
}
