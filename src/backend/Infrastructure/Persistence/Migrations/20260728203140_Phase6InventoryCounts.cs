using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase6InventoryCounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "inventory_counts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    count_no = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    count_date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    snapshot_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    warehouse_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    version = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_inventory_counts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_inventory_counts_warehouses_warehouse_id",
                        column: x => x.warehouse_id,
                        principalTable: "warehouses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "inventory_count_lines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    inventory_count_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    line_no = table.Column<int>(type: "int", nullable: false),
                    item_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    uom_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    system_qty = table.Column<decimal>(type: "decimal(18,6)", nullable: false),
                    counted_qty = table.Column<decimal>(type: "decimal(18,6)", nullable: false),
                    variance_qty = table.Column<decimal>(type: "decimal(18,6)", nullable: false),
                    base_system_qty = table.Column<decimal>(type: "decimal(18,6)", nullable: false),
                    base_counted_qty = table.Column<decimal>(type: "decimal(18,6)", nullable: false),
                    base_variance_qty = table.Column<decimal>(type: "decimal(18,6)", nullable: false),
                    notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    created_by = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    updated_by = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inventory_count_lines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_inventory_count_lines_inventory_counts_inventory_count_id",
                        column: x => x.inventory_count_id,
                        principalTable: "inventory_counts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_inventory_count_lines_items_item_id",
                        column: x => x.item_id,
                        principalTable: "items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_inventory_count_lines_uoms_uom_id",
                        column: x => x.uom_id,
                        principalTable: "uoms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_inventory_count_lines_inventory_count_id_item_id",
                table: "inventory_count_lines",
                columns: new[] { "inventory_count_id", "item_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_inventory_count_lines_inventory_count_id_line_no",
                table: "inventory_count_lines",
                columns: new[] { "inventory_count_id", "line_no" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_inventory_count_lines_item_id",
                table: "inventory_count_lines",
                column: "item_id");

            migrationBuilder.CreateIndex(
                name: "IX_inventory_count_lines_uom_id",
                table: "inventory_count_lines",
                column: "uom_id");

            migrationBuilder.CreateIndex(
                name: "IX_inventory_counts_OrganizationId_count_no",
                table: "inventory_counts",
                columns: new[] { "OrganizationId", "count_no" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_inventory_counts_OrganizationId_status_count_date",
                table: "inventory_counts",
                columns: new[] { "OrganizationId", "status", "count_date" });

            migrationBuilder.CreateIndex(
                name: "IX_inventory_counts_OrganizationId_warehouse_id_status_count_date",
                table: "inventory_counts",
                columns: new[] { "OrganizationId", "warehouse_id", "status", "count_date" });

            migrationBuilder.CreateIndex(
                name: "IX_inventory_counts_warehouse_id",
                table: "inventory_counts",
                column: "warehouse_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "inventory_count_lines");

            migrationBuilder.DropTable(
                name: "inventory_counts");
        }
    }
}
