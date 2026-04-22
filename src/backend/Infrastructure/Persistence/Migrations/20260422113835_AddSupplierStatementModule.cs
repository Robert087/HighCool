using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSupplierStatementModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_supplier_statement_entries_source_doc_id_source_line_id_effect_type",
                table: "supplier_statement_entries");

            migrationBuilder.RenameColumn(
                name: "transaction_date",
                table: "supplier_statement_entries",
                newName: "entry_date");

            migrationBuilder.RenameColumn(
                name: "amount_delta",
                table: "supplier_statement_entries",
                newName: "debit");

            migrationBuilder.RenameIndex(
                name: "IX_supplier_statement_entries_supplier_id_transaction_date",
                table: "supplier_statement_entries",
                newName: "IX_supplier_statement_entries_supplier_id_entry_date");

            migrationBuilder.AddColumn<decimal>(
                name: "credit",
                table: "supplier_statement_entries",
                type: "decimal(18,6)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.Sql(
                """
                update supplier_statement_entries
                set
                    credit = case
                        when debit > 0 then debit
                        else 0
                    end,
                    debit = case
                        when debit < 0 then abs(debit)
                        else 0
                    end;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_supplier_statement_entries_source_doc_type_source_doc_id_source_line_id_effect_type",
                table: "supplier_statement_entries",
                columns: new[] { "source_doc_type", "source_doc_id", "source_line_id", "effect_type" },
                unique: true,
                filter: "[source_line_id] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_supplier_statement_entries_source_doc_type_source_doc_id_source_line_id_effect_type",
                table: "supplier_statement_entries");

            migrationBuilder.DropColumn(
                name: "credit",
                table: "supplier_statement_entries");

            migrationBuilder.RenameColumn(
                name: "entry_date",
                table: "supplier_statement_entries",
                newName: "transaction_date");

            migrationBuilder.RenameColumn(
                name: "debit",
                table: "supplier_statement_entries",
                newName: "amount_delta");

            migrationBuilder.RenameIndex(
                name: "IX_supplier_statement_entries_supplier_id_entry_date",
                table: "supplier_statement_entries",
                newName: "IX_supplier_statement_entries_supplier_id_transaction_date");

            migrationBuilder.CreateIndex(
                name: "IX_supplier_statement_entries_source_doc_id_source_line_id_effect_type",
                table: "supplier_statement_entries",
                columns: new[] { "source_doc_id", "source_line_id", "effect_type" },
                unique: true,
                filter: "[source_line_id] IS NOT NULL");
        }
    }
}
