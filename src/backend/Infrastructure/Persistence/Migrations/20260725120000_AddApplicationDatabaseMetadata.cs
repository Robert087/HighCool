using System;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Persistence.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260725120000_AddApplicationDatabaseMetadata")]
public partial class AddApplicationDatabaseMetadata : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "application_database_metadata",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                application_version = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                database_schema_version = table.Column<int>(type: "int", nullable: false),
                database_created_at_utc = table.Column<DateTime>(type: "datetime2", nullable: false),
                last_successful_schema_upgrade_at_utc = table.Column<DateTime>(type: "datetime2", nullable: false),
                created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                created_by = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                updated_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                updated_by = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_application_database_metadata", x => x.Id);
            });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "application_database_metadata");
    }
}
