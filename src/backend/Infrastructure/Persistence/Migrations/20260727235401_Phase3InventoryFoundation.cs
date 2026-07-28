using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase3InventoryFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_warehouses_code",
                table: "warehouses");

            migrationBuilder.DropIndex(
                name: "IX_warehouses_name",
                table: "warehouses");

            migrationBuilder.DropIndex(
                name: "IX_uoms_code",
                table: "uoms");

            migrationBuilder.DropIndex(
                name: "IX_uoms_name",
                table: "uoms");

            migrationBuilder.DropIndex(
                name: "IX_uom_conversions_from_uom_id_to_uom_id_is_active",
                table: "uom_conversions");

            migrationBuilder.DropIndex(
                name: "IX_stock_ledger_entries_item_id_warehouse_id_transaction_date",
                table: "stock_ledger_entries");

            migrationBuilder.DropIndex(
                name: "IX_stock_ledger_entries_source_doc_type_source_doc_id",
                table: "stock_ledger_entries");

            migrationBuilder.DropIndex(
                name: "IX_stock_ledger_entries_transaction_type_transaction_date",
                table: "stock_ledger_entries");

            migrationBuilder.DropIndex(
                name: "IX_stock_ledger_entries_warehouse_id_transaction_date",
                table: "stock_ledger_entries");

            migrationBuilder.DropIndex(
                name: "IX_items_code",
                table: "items");

            migrationBuilder.DropIndex(
                name: "IX_items_name",
                table: "items");

            migrationBuilder.AddColumn<Guid>(
                name: "category_id",
                table: "items",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "default_warehouse_id",
                table: "items",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "minimum_stock_quantity",
                table: "items",
                type: "decimal(18,6)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "item_categories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    code = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    is_active = table.Column<bool>(type: "bit", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    created_by = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    updated_by = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_item_categories", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_warehouses_OrganizationId_code",
                table: "warehouses",
                columns: new[] { "OrganizationId", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_warehouses_OrganizationId_is_active_name",
                table: "warehouses",
                columns: new[] { "OrganizationId", "is_active", "name" });

            migrationBuilder.CreateIndex(
                name: "IX_warehouses_OrganizationId_name",
                table: "warehouses",
                columns: new[] { "OrganizationId", "name" });

            migrationBuilder.CreateIndex(
                name: "IX_uoms_OrganizationId_code",
                table: "uoms",
                columns: new[] { "OrganizationId", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_uoms_OrganizationId_is_active_name",
                table: "uoms",
                columns: new[] { "OrganizationId", "is_active", "name" });

            migrationBuilder.CreateIndex(
                name: "IX_uoms_OrganizationId_name",
                table: "uoms",
                columns: new[] { "OrganizationId", "name" });

            migrationBuilder.CreateIndex(
                name: "IX_uom_conversions_from_uom_id",
                table: "uom_conversions",
                column: "from_uom_id");

            migrationBuilder.CreateIndex(
                name: "IX_uom_conversions_OrganizationId_from_uom_id_to_uom_id_is_active",
                table: "uom_conversions",
                columns: new[] { "OrganizationId", "from_uom_id", "to_uom_id", "is_active" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_uom_conversions_OrganizationId_is_active",
                table: "uom_conversions",
                columns: new[] { "OrganizationId", "is_active" });

            migrationBuilder.CreateIndex(
                name: "IX_uom_conversions_OrganizationId_to_uom_id",
                table: "uom_conversions",
                columns: new[] { "OrganizationId", "to_uom_id" });

            migrationBuilder.CreateIndex(
                name: "IX_stock_ledger_entries_item_id",
                table: "stock_ledger_entries",
                column: "item_id");

            migrationBuilder.CreateIndex(
                name: "IX_stock_ledger_entries_OrganizationId_item_id_warehouse_id_transaction_date_created_at_Id",
                table: "stock_ledger_entries",
                columns: new[] { "OrganizationId", "item_id", "warehouse_id", "transaction_date", "created_at", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_stock_ledger_entries_OrganizationId_source_doc_type_source_doc_id",
                table: "stock_ledger_entries",
                columns: new[] { "OrganizationId", "source_doc_type", "source_doc_id" });

            migrationBuilder.CreateIndex(
                name: "IX_stock_ledger_entries_OrganizationId_transaction_type_transaction_date",
                table: "stock_ledger_entries",
                columns: new[] { "OrganizationId", "transaction_type", "transaction_date" });

            migrationBuilder.CreateIndex(
                name: "IX_stock_ledger_entries_OrganizationId_warehouse_id_transaction_date",
                table: "stock_ledger_entries",
                columns: new[] { "OrganizationId", "warehouse_id", "transaction_date" });

            migrationBuilder.CreateIndex(
                name: "IX_stock_ledger_entries_warehouse_id",
                table: "stock_ledger_entries",
                column: "warehouse_id");

            migrationBuilder.CreateIndex(
                name: "IX_items_category_id",
                table: "items",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "IX_items_default_warehouse_id",
                table: "items",
                column: "default_warehouse_id");

            migrationBuilder.CreateIndex(
                name: "IX_items_OrganizationId_base_uom_id",
                table: "items",
                columns: new[] { "OrganizationId", "base_uom_id" });

            migrationBuilder.CreateIndex(
                name: "IX_items_OrganizationId_category_id",
                table: "items",
                columns: new[] { "OrganizationId", "category_id" });

            migrationBuilder.CreateIndex(
                name: "IX_items_OrganizationId_code",
                table: "items",
                columns: new[] { "OrganizationId", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_items_OrganizationId_default_warehouse_id",
                table: "items",
                columns: new[] { "OrganizationId", "default_warehouse_id" });

            migrationBuilder.CreateIndex(
                name: "IX_items_OrganizationId_is_active_name",
                table: "items",
                columns: new[] { "OrganizationId", "is_active", "name" });

            migrationBuilder.CreateIndex(
                name: "IX_items_OrganizationId_name",
                table: "items",
                columns: new[] { "OrganizationId", "name" });

            migrationBuilder.CreateIndex(
                name: "IX_item_categories_OrganizationId_code",
                table: "item_categories",
                columns: new[] { "OrganizationId", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_item_categories_OrganizationId_is_active_name",
                table: "item_categories",
                columns: new[] { "OrganizationId", "is_active", "name" });

            migrationBuilder.CreateIndex(
                name: "IX_item_categories_OrganizationId_name",
                table: "item_categories",
                columns: new[] { "OrganizationId", "name" });

            migrationBuilder.AddForeignKey(
                name: "FK_items_item_categories_category_id",
                table: "items",
                column: "category_id",
                principalTable: "item_categories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_items_warehouses_default_warehouse_id",
                table: "items",
                column: "default_warehouse_id",
                principalTable: "warehouses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_items_item_categories_category_id",
                table: "items");

            migrationBuilder.DropForeignKey(
                name: "FK_items_warehouses_default_warehouse_id",
                table: "items");

            migrationBuilder.DropTable(
                name: "item_categories");

            migrationBuilder.DropIndex(
                name: "IX_warehouses_OrganizationId_code",
                table: "warehouses");

            migrationBuilder.DropIndex(
                name: "IX_warehouses_OrganizationId_is_active_name",
                table: "warehouses");

            migrationBuilder.DropIndex(
                name: "IX_warehouses_OrganizationId_name",
                table: "warehouses");

            migrationBuilder.DropIndex(
                name: "IX_uoms_OrganizationId_code",
                table: "uoms");

            migrationBuilder.DropIndex(
                name: "IX_uoms_OrganizationId_is_active_name",
                table: "uoms");

            migrationBuilder.DropIndex(
                name: "IX_uoms_OrganizationId_name",
                table: "uoms");

            migrationBuilder.DropIndex(
                name: "IX_uom_conversions_from_uom_id",
                table: "uom_conversions");

            migrationBuilder.DropIndex(
                name: "IX_uom_conversions_OrganizationId_from_uom_id_to_uom_id_is_active",
                table: "uom_conversions");

            migrationBuilder.DropIndex(
                name: "IX_uom_conversions_OrganizationId_is_active",
                table: "uom_conversions");

            migrationBuilder.DropIndex(
                name: "IX_uom_conversions_OrganizationId_to_uom_id",
                table: "uom_conversions");

            migrationBuilder.DropIndex(
                name: "IX_stock_ledger_entries_item_id",
                table: "stock_ledger_entries");

            migrationBuilder.DropIndex(
                name: "IX_stock_ledger_entries_OrganizationId_item_id_warehouse_id_transaction_date_created_at_Id",
                table: "stock_ledger_entries");

            migrationBuilder.DropIndex(
                name: "IX_stock_ledger_entries_OrganizationId_source_doc_type_source_doc_id",
                table: "stock_ledger_entries");

            migrationBuilder.DropIndex(
                name: "IX_stock_ledger_entries_OrganizationId_transaction_type_transaction_date",
                table: "stock_ledger_entries");

            migrationBuilder.DropIndex(
                name: "IX_stock_ledger_entries_OrganizationId_warehouse_id_transaction_date",
                table: "stock_ledger_entries");

            migrationBuilder.DropIndex(
                name: "IX_stock_ledger_entries_warehouse_id",
                table: "stock_ledger_entries");

            migrationBuilder.DropIndex(
                name: "IX_items_category_id",
                table: "items");

            migrationBuilder.DropIndex(
                name: "IX_items_default_warehouse_id",
                table: "items");

            migrationBuilder.DropIndex(
                name: "IX_items_OrganizationId_base_uom_id",
                table: "items");

            migrationBuilder.DropIndex(
                name: "IX_items_OrganizationId_category_id",
                table: "items");

            migrationBuilder.DropIndex(
                name: "IX_items_OrganizationId_code",
                table: "items");

            migrationBuilder.DropIndex(
                name: "IX_items_OrganizationId_default_warehouse_id",
                table: "items");

            migrationBuilder.DropIndex(
                name: "IX_items_OrganizationId_is_active_name",
                table: "items");

            migrationBuilder.DropIndex(
                name: "IX_items_OrganizationId_name",
                table: "items");

            migrationBuilder.DropColumn(
                name: "category_id",
                table: "items");

            migrationBuilder.DropColumn(
                name: "default_warehouse_id",
                table: "items");

            migrationBuilder.DropColumn(
                name: "minimum_stock_quantity",
                table: "items");

            migrationBuilder.CreateIndex(
                name: "IX_warehouses_code",
                table: "warehouses",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_warehouses_name",
                table: "warehouses",
                column: "name");

            migrationBuilder.CreateIndex(
                name: "IX_uoms_code",
                table: "uoms",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_uoms_name",
                table: "uoms",
                column: "name");

            migrationBuilder.CreateIndex(
                name: "IX_uom_conversions_from_uom_id_to_uom_id_is_active",
                table: "uom_conversions",
                columns: new[] { "from_uom_id", "to_uom_id", "is_active" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_stock_ledger_entries_item_id_warehouse_id_transaction_date",
                table: "stock_ledger_entries",
                columns: new[] { "item_id", "warehouse_id", "transaction_date" });

            migrationBuilder.CreateIndex(
                name: "IX_stock_ledger_entries_source_doc_type_source_doc_id",
                table: "stock_ledger_entries",
                columns: new[] { "source_doc_type", "source_doc_id" });

            migrationBuilder.CreateIndex(
                name: "IX_stock_ledger_entries_transaction_type_transaction_date",
                table: "stock_ledger_entries",
                columns: new[] { "transaction_type", "transaction_date" });

            migrationBuilder.CreateIndex(
                name: "IX_stock_ledger_entries_warehouse_id_transaction_date",
                table: "stock_ledger_entries",
                columns: new[] { "warehouse_id", "transaction_date" });

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
    }
}
