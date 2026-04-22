using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ExtendSupplierMasterDataFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "address",
                table: "suppliers",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "area",
                table: "suppliers",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "city",
                table: "suppliers",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "credit_limit",
                table: "suppliers",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "notes",
                table: "suppliers",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "payment_terms",
                table: "suppliers",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "tax_number",
                table: "suppliers",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "address",
                table: "suppliers");

            migrationBuilder.DropColumn(
                name: "area",
                table: "suppliers");

            migrationBuilder.DropColumn(
                name: "city",
                table: "suppliers");

            migrationBuilder.DropColumn(
                name: "credit_limit",
                table: "suppliers");

            migrationBuilder.DropColumn(
                name: "notes",
                table: "suppliers");

            migrationBuilder.DropColumn(
                name: "payment_terms",
                table: "suppliers");

            migrationBuilder.DropColumn(
                name: "tax_number",
                table: "suppliers");
        }
    }
}
