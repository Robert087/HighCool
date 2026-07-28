using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase4InventoryAdjustmentHardening : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ledger_operation_key",
                table: "stock_ledger_entries",
                type: "nvarchar(160)",
                maxLength: 160,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "version",
                table: "inventory_adjustments",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "document_number_sequences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    document_type = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    prefix = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    next_value = table.Column<int>(type: "int", nullable: false),
                    padding_length = table.Column<int>(type: "int", nullable: false),
                    version = table.Column<int>(type: "int", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    created_by = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    updated_by = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_document_number_sequences", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_stock_ledger_entries_OrganizationId_ledger_operation_key",
                table: "stock_ledger_entries",
                columns: new[] { "OrganizationId", "ledger_operation_key" },
                unique: true,
                filter: "[ledger_operation_key] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_document_number_sequences_OrganizationId_document_type",
                table: "document_number_sequences",
                columns: new[] { "OrganizationId", "document_type" },
                unique: true);

            if (migrationBuilder.ActiveProvider == "Microsoft.EntityFrameworkCore.Sqlite")
            {
                migrationBuilder.Sql(
                    """
                    INSERT INTO "document_number_sequences"
                        ("Id", "document_type", "prefix", "next_value", "padding_length", "version", "created_at", "created_by", "OrganizationId")
                    SELECT
                        lower(hex(randomblob(4)) || '-' || hex(randomblob(2)) || '-' || hex(randomblob(2)) || '-' || hex(randomblob(2)) || '-' || hex(randomblob(6))),
                        'InventoryAdjustment',
                        'ADJ-',
                        COALESCE(MAX(CASE
                            WHEN length(substr("adjustment_no", 5)) = 6
                                 AND substr("adjustment_no", 1, 4) = 'ADJ-'
                                 AND substr("adjustment_no", 5) NOT GLOB '*[^0-9]*'
                            THEN CAST(substr("adjustment_no", 5) AS INTEGER)
                            ELSE 0
                        END), 0) + 1,
                        6,
                        0,
                        datetime('now'),
                        'migration',
                        "OrganizationId"
                    FROM "inventory_adjustments"
                    GROUP BY "OrganizationId";
                    """);
            }
            else
            {
                migrationBuilder.Sql(
                    """
                    INSERT INTO [document_number_sequences]
                        ([Id], [document_type], [prefix], [next_value], [padding_length], [version], [created_at], [created_by], [OrganizationId])
                    SELECT
                        NEWID(),
                        N'InventoryAdjustment',
                        N'ADJ-',
                        COALESCE(MAX(CASE
                            WHEN LEN(SUBSTRING([adjustment_no], 5, 64)) = 6
                                 AND SUBSTRING([adjustment_no], 1, 4) = N'ADJ-'
                                 AND TRY_CONVERT(int, SUBSTRING([adjustment_no], 5, 64)) IS NOT NULL
                            THEN TRY_CONVERT(int, SUBSTRING([adjustment_no], 5, 64))
                            ELSE 0
                        END), 0) + 1,
                        6,
                        0,
                        SYSUTCDATETIME(),
                        N'migration',
                        [OrganizationId]
                    FROM [inventory_adjustments]
                    GROUP BY [OrganizationId];
                    """);
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "document_number_sequences");

            migrationBuilder.DropIndex(
                name: "IX_stock_ledger_entries_OrganizationId_ledger_operation_key",
                table: "stock_ledger_entries");

            migrationBuilder.DropColumn(
                name: "ledger_operation_key",
                table: "stock_ledger_entries");

            migrationBuilder.DropColumn(
                name: "version",
                table: "inventory_adjustments");
        }
    }
}
