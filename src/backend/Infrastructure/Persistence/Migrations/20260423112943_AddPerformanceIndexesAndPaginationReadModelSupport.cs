using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPerformanceIndexesAndPaginationReadModelSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_stock_ledger_entries_warehouse_id",
                table: "stock_ledger_entries");

            migrationBuilder.DropIndex(
                name: "IX_purchase_returns_supplier_id",
                table: "purchase_returns");

            migrationBuilder.DropIndex(
                name: "IX_purchase_receipts_supplier_id",
                table: "purchase_receipts");

            migrationBuilder.DropIndex(
                name: "IX_purchase_receipts_warehouse_id",
                table: "purchase_receipts");

            migrationBuilder.DropIndex(
                name: "IX_purchase_orders_supplier_id",
                table: "purchase_orders");

            migrationBuilder.CreateIndex(
                name: "IX_supplier_statement_entries_supplier_id_effect_type_entry_date",
                table: "supplier_statement_entries",
                columns: new[] { "supplier_id", "effect_type", "entry_date" });

            migrationBuilder.CreateIndex(
                name: "IX_supplier_statement_entries_supplier_id_source_doc_type_entry_date",
                table: "supplier_statement_entries",
                columns: new[] { "supplier_id", "source_doc_type", "entry_date" });

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
                name: "IX_shortage_resolutions_supplier_id_status_resolution_date",
                table: "shortage_resolutions",
                columns: new[] { "supplier_id", "status", "resolution_date" });

            migrationBuilder.CreateIndex(
                name: "IX_shortage_resolution_allocations_shortage_ledger_id_allocation_type",
                table: "shortage_resolution_allocations",
                columns: new[] { "shortage_ledger_id", "allocation_type" });

            migrationBuilder.CreateIndex(
                name: "IX_shortage_ledger_entries_status_component_item_id",
                table: "shortage_ledger_entries",
                columns: new[] { "status", "component_item_id" });

            migrationBuilder.CreateIndex(
                name: "IX_shortage_ledger_entries_status_item_id",
                table: "shortage_ledger_entries",
                columns: new[] { "status", "item_id" });

            migrationBuilder.CreateIndex(
                name: "IX_purchase_returns_supplier_id_status_return_date",
                table: "purchase_returns",
                columns: new[] { "supplier_id", "status", "return_date" });

            migrationBuilder.CreateIndex(
                name: "IX_purchase_receipts_purchase_order_id_receipt_date",
                table: "purchase_receipts",
                columns: new[] { "purchase_order_id", "receipt_date" });

            migrationBuilder.CreateIndex(
                name: "IX_purchase_receipts_supplier_id_status_receipt_date",
                table: "purchase_receipts",
                columns: new[] { "supplier_id", "status", "receipt_date" });

            migrationBuilder.CreateIndex(
                name: "IX_purchase_receipts_warehouse_id_receipt_date",
                table: "purchase_receipts",
                columns: new[] { "warehouse_id", "receipt_date" });

            migrationBuilder.CreateIndex(
                name: "IX_purchase_receipt_lines_purchase_receipt_id_item_id",
                table: "purchase_receipt_lines",
                columns: new[] { "purchase_receipt_id", "item_id" });

            migrationBuilder.CreateIndex(
                name: "IX_purchase_orders_supplier_id_order_date",
                table: "purchase_orders",
                columns: new[] { "supplier_id", "order_date" });

            migrationBuilder.CreateIndex(
                name: "IX_purchase_orders_supplier_id_status_order_date",
                table: "purchase_orders",
                columns: new[] { "supplier_id", "status", "order_date" });

            migrationBuilder.CreateIndex(
                name: "IX_purchase_order_lines_purchase_order_id_item_id",
                table: "purchase_order_lines",
                columns: new[] { "purchase_order_id", "item_id" });

            migrationBuilder.CreateIndex(
                name: "IX_payments_party_type_party_id_direction_status_payment_date",
                table: "payments",
                columns: new[] { "party_type", "party_id", "direction", "status", "payment_date" });

            migrationBuilder.CreateIndex(
                name: "IX_payment_allocations_payment_id",
                table: "payment_allocations",
                column: "payment_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_supplier_statement_entries_supplier_id_effect_type_entry_date",
                table: "supplier_statement_entries");

            migrationBuilder.DropIndex(
                name: "IX_supplier_statement_entries_supplier_id_source_doc_type_entry_date",
                table: "supplier_statement_entries");

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
                name: "IX_shortage_resolutions_supplier_id_status_resolution_date",
                table: "shortage_resolutions");

            migrationBuilder.DropIndex(
                name: "IX_shortage_resolution_allocations_shortage_ledger_id_allocation_type",
                table: "shortage_resolution_allocations");

            migrationBuilder.DropIndex(
                name: "IX_shortage_ledger_entries_status_component_item_id",
                table: "shortage_ledger_entries");

            migrationBuilder.DropIndex(
                name: "IX_shortage_ledger_entries_status_item_id",
                table: "shortage_ledger_entries");

            migrationBuilder.DropIndex(
                name: "IX_purchase_returns_supplier_id_status_return_date",
                table: "purchase_returns");

            migrationBuilder.DropIndex(
                name: "IX_purchase_receipts_purchase_order_id_receipt_date",
                table: "purchase_receipts");

            migrationBuilder.DropIndex(
                name: "IX_purchase_receipts_supplier_id_status_receipt_date",
                table: "purchase_receipts");

            migrationBuilder.DropIndex(
                name: "IX_purchase_receipts_warehouse_id_receipt_date",
                table: "purchase_receipts");

            migrationBuilder.DropIndex(
                name: "IX_purchase_receipt_lines_purchase_receipt_id_item_id",
                table: "purchase_receipt_lines");

            migrationBuilder.DropIndex(
                name: "IX_purchase_orders_supplier_id_order_date",
                table: "purchase_orders");

            migrationBuilder.DropIndex(
                name: "IX_purchase_orders_supplier_id_status_order_date",
                table: "purchase_orders");

            migrationBuilder.DropIndex(
                name: "IX_purchase_order_lines_purchase_order_id_item_id",
                table: "purchase_order_lines");

            migrationBuilder.DropIndex(
                name: "IX_payments_party_type_party_id_direction_status_payment_date",
                table: "payments");

            migrationBuilder.DropIndex(
                name: "IX_payment_allocations_payment_id",
                table: "payment_allocations");

            migrationBuilder.CreateIndex(
                name: "IX_stock_ledger_entries_warehouse_id",
                table: "stock_ledger_entries",
                column: "warehouse_id");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_returns_supplier_id",
                table: "purchase_returns",
                column: "supplier_id");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_receipts_supplier_id",
                table: "purchase_receipts",
                column: "supplier_id");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_receipts_warehouse_id",
                table: "purchase_receipts",
                column: "warehouse_id");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_orders_supplier_id",
                table: "purchase_orders",
                column: "supplier_id");
        }
    }
}
