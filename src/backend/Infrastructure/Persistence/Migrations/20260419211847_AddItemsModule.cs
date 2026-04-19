using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddItemsModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "items",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    code = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    base_uom_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    is_active = table.Column<bool>(type: "bit", nullable: false),
                    is_sellable = table.Column<bool>(type: "bit", nullable: false),
                    is_component = table.Column<bool>(type: "bit", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    created_by = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    updated_by = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_items_uoms_base_uom_id",
                        column: x => x.base_uom_id,
                        principalTable: "uoms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "item_components",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    parent_item_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    component_item_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    quantity = table.Column<decimal>(type: "decimal(18,6)", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    created_by = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    updated_by = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_item_components", x => x.Id);
                    table.ForeignKey(
                        name: "FK_item_components_items_component_item_id",
                        column: x => x.component_item_id,
                        principalTable: "items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_item_components_items_parent_item_id",
                        column: x => x.parent_item_id,
                        principalTable: "items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "item_uom_conversions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    item_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    from_uom_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    to_uom_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    factor = table.Column<decimal>(type: "decimal(18,6)", nullable: false),
                    rounding_mode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    min_fraction = table.Column<decimal>(type: "decimal(18,6)", nullable: false),
                    is_active = table.Column<bool>(type: "bit", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    created_by = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    updated_by = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_item_uom_conversions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_item_uom_conversions_items_item_id",
                        column: x => x.item_id,
                        principalTable: "items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_item_uom_conversions_uoms_from_uom_id",
                        column: x => x.from_uom_id,
                        principalTable: "uoms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_item_uom_conversions_uoms_to_uom_id",
                        column: x => x.to_uom_id,
                        principalTable: "uoms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_item_components_component_item_id",
                table: "item_components",
                column: "component_item_id");

            migrationBuilder.CreateIndex(
                name: "IX_item_components_parent_item_id_component_item_id",
                table: "item_components",
                columns: new[] { "parent_item_id", "component_item_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_item_uom_conversions_from_uom_id",
                table: "item_uom_conversions",
                column: "from_uom_id");

            migrationBuilder.CreateIndex(
                name: "IX_item_uom_conversions_item_id_from_uom_id_to_uom_id_is_active",
                table: "item_uom_conversions",
                columns: new[] { "item_id", "from_uom_id", "to_uom_id", "is_active" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_item_uom_conversions_to_uom_id",
                table: "item_uom_conversions",
                column: "to_uom_id");

            migrationBuilder.CreateIndex(
                name: "IX_items_base_uom_id",
                table: "items",
                column: "base_uom_id");

            migrationBuilder.CreateIndex(
                name: "IX_items_code",
                table: "items",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_items_name",
                table: "items",
                column: "name");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "item_components");

            migrationBuilder.DropTable(
                name: "item_uom_conversions");

            migrationBuilder.DropTable(
                name: "items");
        }
    }
}
