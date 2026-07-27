using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOrganizationFeatureGatesPhase2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "EnableEmployeeAdvances",
                table: "Organizations",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "EnableEmployees",
                table: "Organizations",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "EnableExpenses",
                table: "Organizations",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "EnableInventoryCounts",
                table: "Organizations",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "EnableInventoryIssues",
                table: "Organizations",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "EnableLowStockAlerts",
                table: "Organizations",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "EnableNotifications",
                table: "Organizations",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "EnablePriceLists",
                table: "Organizations",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "EnableReports",
                table: "Organizations",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "EnableSalaries",
                table: "Organizations",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "EnableSales",
                table: "Organizations",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EnableEmployeeAdvances",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "EnableEmployees",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "EnableExpenses",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "EnableInventoryCounts",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "EnableInventoryIssues",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "EnableLowStockAlerts",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "EnableNotifications",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "EnablePriceLists",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "EnableReports",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "EnableSalaries",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "EnableSales",
                table: "Organizations");
        }
    }
}
