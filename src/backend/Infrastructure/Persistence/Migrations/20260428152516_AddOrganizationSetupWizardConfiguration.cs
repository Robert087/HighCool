using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOrganizationSetupWizardConfiguration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AllowDirectPurchaseReceipt",
                table: "Organizations",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "AllowNegativeStock",
                table: "Organizations",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "AllowOverReceipt",
                table: "Organizations",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "AllowPartialReceipt",
                table: "Organizations",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "EnableBatchTracking",
                table: "Organizations",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "EnableComponentsBom",
                table: "Organizations",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "EnableExpiryTracking",
                table: "Organizations",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "EnableInventory",
                table: "Organizations",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "EnableMultipleWarehouses",
                table: "Organizations",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "EnablePostingWorkflow",
                table: "Organizations",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "EnableProcurement",
                table: "Organizations",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "EnablePurchaseOrders",
                table: "Organizations",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "EnablePurchaseReceipts",
                table: "Organizations",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "EnableReversals",
                table: "Organizations",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "EnableSerialTracking",
                table: "Organizations",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "EnableShortageManagement",
                table: "Organizations",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "EnableStockAdjustments",
                table: "Organizations",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "EnableStockTransfers",
                table: "Organizations",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "EnableSupplierFinancials",
                table: "Organizations",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "EnableSupplierManagement",
                table: "Organizations",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "EnableUom",
                table: "Organizations",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "EnableUomConversion",
                table: "Organizations",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "EnableWarehouses",
                table: "Organizations",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "LockPostedDocuments",
                table: "Organizations",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "OverReceiptTolerancePercent",
                table: "Organizations",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "RequireApprovalBeforePosting",
                table: "Organizations",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "RequirePoBeforeReceipt",
                table: "Organizations",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "RequireReasonForCancelOrReversal",
                table: "Organizations",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "SetupCompleted",
                table: "Organizations",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "SetupCompletedAt",
                table: "Organizations",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SetupCompletedBy",
                table: "Organizations",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SetupStep",
                table: "Organizations",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SetupVersion",
                table: "Organizations",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AllowDirectPurchaseReceipt",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "AllowNegativeStock",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "AllowOverReceipt",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "AllowPartialReceipt",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "EnableBatchTracking",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "EnableComponentsBom",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "EnableExpiryTracking",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "EnableInventory",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "EnableMultipleWarehouses",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "EnablePostingWorkflow",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "EnableProcurement",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "EnablePurchaseOrders",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "EnablePurchaseReceipts",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "EnableReversals",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "EnableSerialTracking",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "EnableShortageManagement",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "EnableStockAdjustments",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "EnableStockTransfers",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "EnableSupplierFinancials",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "EnableSupplierManagement",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "EnableUom",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "EnableUomConversion",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "EnableWarehouses",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "LockPostedDocuments",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "OverReceiptTolerancePercent",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "RequireApprovalBeforePosting",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "RequirePoBeforeReceipt",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "RequireReasonForCancelOrReversal",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "SetupCompleted",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "SetupCompletedAt",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "SetupCompletedBy",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "SetupStep",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "SetupVersion",
                table: "Organizations");
        }
    }
}
