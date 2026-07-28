using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase5InventoryTransfers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "inventory_transfers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    transfer_no = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    transfer_date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    source_warehouse_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    destination_warehouse_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
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
                    table.PrimaryKey("PK_inventory_transfers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_inventory_transfers_warehouses_destination_warehouse_id",
                        column: x => x.destination_warehouse_id,
                        principalTable: "warehouses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_inventory_transfers_warehouses_source_warehouse_id",
                        column: x => x.source_warehouse_id,
                        principalTable: "warehouses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "inventory_transfer_lines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    inventory_transfer_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    line_no = table.Column<int>(type: "int", nullable: false),
                    item_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    uom_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    quantity = table.Column<decimal>(type: "decimal(18,6)", nullable: false),
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
                    table.PrimaryKey("PK_inventory_transfer_lines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_inventory_transfer_lines_inventory_transfers_inventory_transfer_id",
                        column: x => x.inventory_transfer_id,
                        principalTable: "inventory_transfers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_inventory_transfer_lines_items_item_id",
                        column: x => x.item_id,
                        principalTable: "items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_inventory_transfer_lines_uoms_uom_id",
                        column: x => x.uom_id,
                        principalTable: "uoms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_inventory_transfer_lines_inventory_transfer_id_line_no",
                table: "inventory_transfer_lines",
                columns: new[] { "inventory_transfer_id", "line_no" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_inventory_transfer_lines_item_id",
                table: "inventory_transfer_lines",
                column: "item_id");

            migrationBuilder.CreateIndex(
                name: "IX_inventory_transfer_lines_uom_id",
                table: "inventory_transfer_lines",
                column: "uom_id");

            migrationBuilder.CreateIndex(
                name: "IX_inventory_transfers_destination_warehouse_id",
                table: "inventory_transfers",
                column: "destination_warehouse_id");

            migrationBuilder.CreateIndex(
                name: "IX_inventory_transfers_OrganizationId_destination_warehouse_id_status_transfer_date",
                table: "inventory_transfers",
                columns: new[] { "OrganizationId", "destination_warehouse_id", "status", "transfer_date" });

            migrationBuilder.CreateIndex(
                name: "IX_inventory_transfers_OrganizationId_source_warehouse_id_status_transfer_date",
                table: "inventory_transfers",
                columns: new[] { "OrganizationId", "source_warehouse_id", "status", "transfer_date" });

            migrationBuilder.CreateIndex(
                name: "IX_inventory_transfers_OrganizationId_status_transfer_date",
                table: "inventory_transfers",
                columns: new[] { "OrganizationId", "status", "transfer_date" });

            migrationBuilder.CreateIndex(
                name: "IX_inventory_transfers_OrganizationId_transfer_no",
                table: "inventory_transfers",
                columns: new[] { "OrganizationId", "transfer_no" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_inventory_transfers_source_warehouse_id",
                table: "inventory_transfers",
                column: "source_warehouse_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "inventory_transfer_lines");

            migrationBuilder.DropTable(
                name: "inventory_transfers");
        }
    }
}
