using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase8InventoryMonitoring : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "enable_inventory_monitoring",
                table: "items",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "lead_time_days",
                table: "items",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "maximum_stock_quantity",
                table: "items",
                type: "decimal(18,6)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "reorder_point_quantity",
                table: "items",
                type: "decimal(18,6)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "reorder_quantity",
                table: "items",
                type: "decimal(18,6)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "safety_stock_quantity",
                table: "items",
                type: "decimal(18,6)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_items_OrganizationId_enable_inventory_monitoring_category_id",
                table: "items",
                columns: new[] { "OrganizationId", "enable_inventory_monitoring", "category_id" });

            migrationBuilder.CreateIndex(
                name: "IX_items_OrganizationId_enable_inventory_monitoring_reorder_point_quantity",
                table: "items",
                columns: new[] { "OrganizationId", "enable_inventory_monitoring", "reorder_point_quantity" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_items_OrganizationId_enable_inventory_monitoring_category_id",
                table: "items");

            migrationBuilder.DropIndex(
                name: "IX_items_OrganizationId_enable_inventory_monitoring_reorder_point_quantity",
                table: "items");

            migrationBuilder.DropColumn(
                name: "enable_inventory_monitoring",
                table: "items");

            migrationBuilder.DropColumn(
                name: "lead_time_days",
                table: "items");

            migrationBuilder.DropColumn(
                name: "maximum_stock_quantity",
                table: "items");

            migrationBuilder.DropColumn(
                name: "reorder_point_quantity",
                table: "items");

            migrationBuilder.DropColumn(
                name: "reorder_quantity",
                table: "items");

            migrationBuilder.DropColumn(
                name: "safety_stock_quantity",
                table: "items");
        }
    }
}
