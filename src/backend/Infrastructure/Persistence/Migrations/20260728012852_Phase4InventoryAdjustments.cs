using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase4InventoryAdjustments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "inventory_adjustments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    adjustment_no = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    adjustment_date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    warehouse_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    reason = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    created_by = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    updated_by = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PostedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PostedBy = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    CanceledAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CanceledBy = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    reversal_document_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    reversed_at = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inventory_adjustments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_inventory_adjustments_warehouses_warehouse_id",
                        column: x => x.warehouse_id,
                        principalTable: "warehouses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "inventory_adjustment_lines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    inventory_adjustment_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    line_no = table.Column<int>(type: "int", nullable: false),
                    item_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    uom_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    quantity = table.Column<decimal>(type: "decimal(18,6)", nullable: false),
                    adjustment_type = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    base_qty = table.Column<decimal>(type: "decimal(18,6)", nullable: false),
                    notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    created_by = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    updated_by = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inventory_adjustment_lines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_inventory_adjustment_lines_inventory_adjustments_inventory_adjustment_id",
                        column: x => x.inventory_adjustment_id,
                        principalTable: "inventory_adjustments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_inventory_adjustment_lines_items_item_id",
                        column: x => x.item_id,
                        principalTable: "items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_inventory_adjustment_lines_uoms_uom_id",
                        column: x => x.uom_id,
                        principalTable: "uoms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_inventory_adjustment_lines_inventory_adjustment_id_line_no",
                table: "inventory_adjustment_lines",
                columns: new[] { "inventory_adjustment_id", "line_no" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_inventory_adjustment_lines_item_id",
                table: "inventory_adjustment_lines",
                column: "item_id");

            migrationBuilder.CreateIndex(
                name: "IX_inventory_adjustment_lines_uom_id",
                table: "inventory_adjustment_lines",
                column: "uom_id");

            migrationBuilder.CreateIndex(
                name: "IX_inventory_adjustments_OrganizationId_adjustment_no",
                table: "inventory_adjustments",
                columns: new[] { "OrganizationId", "adjustment_no" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_inventory_adjustments_OrganizationId_reason",
                table: "inventory_adjustments",
                columns: new[] { "OrganizationId", "reason" });

            migrationBuilder.CreateIndex(
                name: "IX_inventory_adjustments_OrganizationId_status_adjustment_date",
                table: "inventory_adjustments",
                columns: new[] { "OrganizationId", "status", "adjustment_date" });

            migrationBuilder.CreateIndex(
                name: "IX_inventory_adjustments_OrganizationId_warehouse_id_status_adjustment_date",
                table: "inventory_adjustments",
                columns: new[] { "OrganizationId", "warehouse_id", "status", "adjustment_date" });

            migrationBuilder.CreateIndex(
                name: "IX_inventory_adjustments_warehouse_id",
                table: "inventory_adjustments",
                column: "warehouse_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "inventory_adjustment_lines");

            migrationBuilder.DropTable(
                name: "inventory_adjustments");
        }
    }
}
