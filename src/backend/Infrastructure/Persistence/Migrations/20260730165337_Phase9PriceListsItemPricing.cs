using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase9PriceListsItemPricing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "price_lists",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    code = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    type = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    is_default = table.Column<bool>(type: "bit", nullable: false),
                    is_active = table.Column<bool>(type: "bit", nullable: false),
                    description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    version = table.Column<int>(type: "int", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    created_by = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    updated_by = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_price_lists", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "item_prices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    price_list_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    item_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    uom_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    rate = table.Column<decimal>(type: "decimal(18,6)", nullable: false),
                    minimum_quantity = table.Column<decimal>(type: "decimal(18,6)", nullable: false),
                    valid_from = table.Column<DateTime>(type: "datetime2", nullable: false),
                    valid_to = table.Column<DateTime>(type: "datetime2", nullable: true),
                    is_active = table.Column<bool>(type: "bit", nullable: false),
                    notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    version = table.Column<int>(type: "int", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    created_by = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    updated_by = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_item_prices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_item_prices_items_item_id",
                        column: x => x.item_id,
                        principalTable: "items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_item_prices_price_lists_price_list_id",
                        column: x => x.price_list_id,
                        principalTable: "price_lists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_item_prices_uoms_uom_id",
                        column: x => x.uom_id,
                        principalTable: "uoms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_item_prices_item_id",
                table: "item_prices",
                column: "item_id");

            migrationBuilder.CreateIndex(
                name: "IX_item_prices_OrganizationId_currency",
                table: "item_prices",
                columns: new[] { "OrganizationId", "currency" });

            migrationBuilder.CreateIndex(
                name: "IX_item_prices_OrganizationId_is_active_valid_from_valid_to",
                table: "item_prices",
                columns: new[] { "OrganizationId", "is_active", "valid_from", "valid_to" });

            migrationBuilder.CreateIndex(
                name: "IX_item_prices_OrganizationId_item_id_uom_id",
                table: "item_prices",
                columns: new[] { "OrganizationId", "item_id", "uom_id" });

            migrationBuilder.CreateIndex(
                name: "IX_item_prices_OrganizationId_price_list_id_item_id_uom_id_is_active_valid_from_valid_to_minimum_quantity",
                table: "item_prices",
                columns: new[] { "OrganizationId", "price_list_id", "item_id", "uom_id", "is_active", "valid_from", "valid_to", "minimum_quantity" });

            migrationBuilder.CreateIndex(
                name: "IX_item_prices_OrganizationId_price_list_id_item_id_uom_id_minimum_quantity_is_active",
                table: "item_prices",
                columns: new[] { "OrganizationId", "price_list_id", "item_id", "uom_id", "minimum_quantity", "is_active" });

            migrationBuilder.CreateIndex(
                name: "IX_item_prices_price_list_id",
                table: "item_prices",
                column: "price_list_id");

            migrationBuilder.CreateIndex(
                name: "IX_item_prices_uom_id",
                table: "item_prices",
                column: "uom_id");

            migrationBuilder.CreateIndex(
                name: "IX_price_lists_OrganizationId_code",
                table: "price_lists",
                columns: new[] { "OrganizationId", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_price_lists_OrganizationId_currency",
                table: "price_lists",
                columns: new[] { "OrganizationId", "currency" });

            migrationBuilder.CreateIndex(
                name: "IX_price_lists_OrganizationId_name",
                table: "price_lists",
                columns: new[] { "OrganizationId", "name" });

            migrationBuilder.CreateIndex(
                name: "IX_price_lists_OrganizationId_type_is_active_name",
                table: "price_lists",
                columns: new[] { "OrganizationId", "type", "is_active", "name" });

            migrationBuilder.CreateIndex(
                name: "IX_price_lists_OrganizationId_type_is_default_is_active",
                table: "price_lists",
                columns: new[] { "OrganizationId", "type", "is_default", "is_active" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "item_prices");

            migrationBuilder.DropTable(
                name: "price_lists");
        }
    }
}
