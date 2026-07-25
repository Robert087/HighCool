using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDesktopFoundationBatch2Safety : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "installation_id",
                table: "application_database_metadata",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "application_database_restore_journal",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    started_at_utc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    completed_at_utc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    selected_backup_id = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    safety_backup_id = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    original_schema_version = table.Column<int>(type: "int", nullable: true),
                    restored_schema_version = table.Column<int>(type: "int", nullable: true),
                    status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    application_version = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    installation_id = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    failure_code = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    failure_message = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    created_by = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    updated_by = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_application_database_restore_journal", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "application_database_upgrade_journal",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    started_at_utc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    completed_at_utc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    from_schema_version = table.Column<int>(type: "int", nullable: false),
                    target_schema_version = table.Column<int>(type: "int", nullable: false),
                    pre_upgrade_backup_id = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    application_version = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    failure_code = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    failure_message = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    installation_id = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    created_by = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    updated_by = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_application_database_upgrade_journal", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_application_database_restore_journal_safety_backup_id",
                table: "application_database_restore_journal",
                column: "safety_backup_id");

            migrationBuilder.CreateIndex(
                name: "IX_application_database_restore_journal_selected_backup_id",
                table: "application_database_restore_journal",
                column: "selected_backup_id");

            migrationBuilder.CreateIndex(
                name: "IX_application_database_restore_journal_started_at_utc",
                table: "application_database_restore_journal",
                column: "started_at_utc");

            migrationBuilder.CreateIndex(
                name: "IX_application_database_restore_journal_status",
                table: "application_database_restore_journal",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_application_database_upgrade_journal_pre_upgrade_backup_id",
                table: "application_database_upgrade_journal",
                column: "pre_upgrade_backup_id");

            migrationBuilder.CreateIndex(
                name: "IX_application_database_upgrade_journal_started_at_utc",
                table: "application_database_upgrade_journal",
                column: "started_at_utc");

            migrationBuilder.CreateIndex(
                name: "IX_application_database_upgrade_journal_status",
                table: "application_database_upgrade_journal",
                column: "status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "application_database_restore_journal");

            migrationBuilder.DropTable(
                name: "application_database_upgrade_journal");

            migrationBuilder.DropColumn(
                name: "installation_id",
                table: "application_database_metadata");
        }
    }
}
