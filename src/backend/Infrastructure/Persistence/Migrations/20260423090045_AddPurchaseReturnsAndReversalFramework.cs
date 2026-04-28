using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPurchaseReturnsAndReversalFramework : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "reversal_document_id",
                table: "shortage_resolutions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "reversed_at",
                table: "shortage_resolutions",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "reversal_document_id",
                table: "purchase_receipts",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "reversed_at",
                table: "purchase_receipts",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "reversal_document_id",
                table: "purchase_orders",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "reversed_at",
                table: "purchase_orders",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "reversal_document_id",
                table: "payments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "reversed_at",
                table: "payments",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "document_reversals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    reversal_no = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    reversed_document_type = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    reversed_document_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    reversal_date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    reversal_reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    created_by = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    updated_by = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_document_reversals", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "purchase_returns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    return_no = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    supplier_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    reference_receipt_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    return_date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    created_by = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    updated_by = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    reversal_document_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    reversed_at = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_purchase_returns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_purchase_returns_purchase_receipts_reference_receipt_id",
                        column: x => x.reference_receipt_id,
                        principalTable: "purchase_receipts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_purchase_returns_suppliers_supplier_id",
                        column: x => x.supplier_id,
                        principalTable: "suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "purchase_return_lines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    purchase_return_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    line_no = table.Column<int>(type: "int", nullable: false),
                    item_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    component_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    warehouse_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    return_qty = table.Column<decimal>(type: "decimal(18,6)", nullable: false),
                    uom_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    base_qty = table.Column<decimal>(type: "decimal(18,6)", nullable: false),
                    reference_receipt_line_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    created_by = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    updated_by = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_purchase_return_lines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_purchase_return_lines_items_component_id",
                        column: x => x.component_id,
                        principalTable: "items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_purchase_return_lines_items_item_id",
                        column: x => x.item_id,
                        principalTable: "items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_purchase_return_lines_purchase_receipt_lines_reference_receipt_line_id",
                        column: x => x.reference_receipt_line_id,
                        principalTable: "purchase_receipt_lines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_purchase_return_lines_purchase_returns_purchase_return_id",
                        column: x => x.purchase_return_id,
                        principalTable: "purchase_returns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_purchase_return_lines_uoms_uom_id",
                        column: x => x.uom_id,
                        principalTable: "uoms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_purchase_return_lines_warehouses_warehouse_id",
                        column: x => x.warehouse_id,
                        principalTable: "warehouses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_shortage_resolutions_reversal_document_id",
                table: "shortage_resolutions",
                column: "reversal_document_id");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_receipts_reversal_document_id",
                table: "purchase_receipts",
                column: "reversal_document_id");

            migrationBuilder.CreateIndex(
                name: "IX_payments_reversal_document_id",
                table: "payments",
                column: "reversal_document_id");

            migrationBuilder.CreateIndex(
                name: "IX_document_reversals_reversal_no",
                table: "document_reversals",
                column: "reversal_no",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_document_reversals_reversed_document_type_reversed_document_id",
                table: "document_reversals",
                columns: new[] { "reversed_document_type", "reversed_document_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_purchase_return_lines_component_id",
                table: "purchase_return_lines",
                column: "component_id");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_return_lines_item_id",
                table: "purchase_return_lines",
                column: "item_id");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_return_lines_purchase_return_id_line_no",
                table: "purchase_return_lines",
                columns: new[] { "purchase_return_id", "line_no" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_purchase_return_lines_reference_receipt_line_id",
                table: "purchase_return_lines",
                column: "reference_receipt_line_id");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_return_lines_uom_id",
                table: "purchase_return_lines",
                column: "uom_id");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_return_lines_warehouse_id",
                table: "purchase_return_lines",
                column: "warehouse_id");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_returns_reference_receipt_id",
                table: "purchase_returns",
                column: "reference_receipt_id");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_returns_return_date",
                table: "purchase_returns",
                column: "return_date");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_returns_return_no",
                table: "purchase_returns",
                column: "return_no",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_purchase_returns_reversal_document_id",
                table: "purchase_returns",
                column: "reversal_document_id");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_returns_status",
                table: "purchase_returns",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_returns_supplier_id",
                table: "purchase_returns",
                column: "supplier_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "document_reversals");

            migrationBuilder.DropTable(
                name: "purchase_return_lines");

            migrationBuilder.DropTable(
                name: "purchase_returns");

            migrationBuilder.DropIndex(
                name: "IX_shortage_resolutions_reversal_document_id",
                table: "shortage_resolutions");

            migrationBuilder.DropIndex(
                name: "IX_purchase_receipts_reversal_document_id",
                table: "purchase_receipts");

            migrationBuilder.DropIndex(
                name: "IX_payments_reversal_document_id",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "reversal_document_id",
                table: "shortage_resolutions");

            migrationBuilder.DropColumn(
                name: "reversed_at",
                table: "shortage_resolutions");

            migrationBuilder.DropColumn(
                name: "reversal_document_id",
                table: "purchase_receipts");

            migrationBuilder.DropColumn(
                name: "reversed_at",
                table: "purchase_receipts");

            migrationBuilder.DropColumn(
                name: "reversal_document_id",
                table: "purchase_orders");

            migrationBuilder.DropColumn(
                name: "reversed_at",
                table: "purchase_orders");

            migrationBuilder.DropColumn(
                name: "reversal_document_id",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "reversed_at",
                table: "payments");
        }
    }
}
