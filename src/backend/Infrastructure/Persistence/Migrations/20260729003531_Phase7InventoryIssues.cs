using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase7InventoryIssues : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "inventory_issues",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    issue_no = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    issue_date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    warehouse_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    reason = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    reference_no = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    requested_by = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
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
                    table.PrimaryKey("PK_inventory_issues", x => x.Id);
                    table.ForeignKey(
                        name: "FK_inventory_issues_warehouses_warehouse_id",
                        column: x => x.warehouse_id,
                        principalTable: "warehouses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "inventory_issue_lines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    inventory_issue_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
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
                    table.PrimaryKey("PK_inventory_issue_lines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_inventory_issue_lines_inventory_issues_inventory_issue_id",
                        column: x => x.inventory_issue_id,
                        principalTable: "inventory_issues",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_inventory_issue_lines_items_item_id",
                        column: x => x.item_id,
                        principalTable: "items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_inventory_issue_lines_uoms_uom_id",
                        column: x => x.uom_id,
                        principalTable: "uoms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_inventory_issue_lines_inventory_issue_id_item_id",
                table: "inventory_issue_lines",
                columns: new[] { "inventory_issue_id", "item_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_inventory_issue_lines_inventory_issue_id_line_no",
                table: "inventory_issue_lines",
                columns: new[] { "inventory_issue_id", "line_no" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_inventory_issue_lines_item_id",
                table: "inventory_issue_lines",
                column: "item_id");

            migrationBuilder.CreateIndex(
                name: "IX_inventory_issue_lines_uom_id",
                table: "inventory_issue_lines",
                column: "uom_id");

            migrationBuilder.CreateIndex(
                name: "IX_inventory_issues_OrganizationId_issue_no",
                table: "inventory_issues",
                columns: new[] { "OrganizationId", "issue_no" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_inventory_issues_OrganizationId_reason_issue_date",
                table: "inventory_issues",
                columns: new[] { "OrganizationId", "reason", "issue_date" });

            migrationBuilder.CreateIndex(
                name: "IX_inventory_issues_OrganizationId_status_issue_date",
                table: "inventory_issues",
                columns: new[] { "OrganizationId", "status", "issue_date" });

            migrationBuilder.CreateIndex(
                name: "IX_inventory_issues_OrganizationId_warehouse_id_status_issue_date",
                table: "inventory_issues",
                columns: new[] { "OrganizationId", "warehouse_id", "status", "issue_date" });

            migrationBuilder.CreateIndex(
                name: "IX_inventory_issues_warehouse_id",
                table: "inventory_issues",
                column: "warehouse_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "inventory_issue_lines");

            migrationBuilder.DropTable(
                name: "inventory_issues");
        }
    }
}
