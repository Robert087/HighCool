using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Infrastructure.Persistence.Migrations
{
    public partial class CorrectShortageResolutionMixedSettlement : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "resolved_qty",
                table: "shortage_ledger_entries",
                newName: "resolved_physical_qty");

            migrationBuilder.AddColumn<decimal>(
                name: "resolved_financial_qty_equivalent",
                table: "shortage_ledger_entries",
                type: "decimal(18,6)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "allocation_type",
                table: "shortage_resolution_allocations",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Physical");

            migrationBuilder.AddColumn<decimal>(
                name: "financial_qty_equivalent",
                table: "shortage_resolution_allocations",
                type: "decimal(18,6)",
                nullable: true);

            migrationBuilder.Sql(
                """
                update shortage_resolution_allocations
                set allocation_type = case
                    when exists (
                        select 1
                        from shortage_resolutions
                        where shortage_resolutions.id = shortage_resolution_allocations.resolution_id
                          and shortage_resolutions.resolution_type = 'Financial'
                    ) then 'Financial'
                    else 'Physical'
                end;
                """);

            migrationBuilder.Sql(
                """
                update shortage_resolution_allocations
                set financial_qty_equivalent = round(
                    allocated_amount / nullif(
                        coalesce(
                            valuation_rate,
                            (
                                select sle.shortage_value / nullif(sle.shortage_qty, 0)
                                from shortage_ledger_entries sle
                                where sle.id = shortage_resolution_allocations.shortage_ledger_id
                                  and sle.shortage_value is not null
                                  and sle.shortage_qty > 0
                            )
                        ),
                        0
                    ),
                    6
                )
                where allocation_type = 'Financial'
                  and allocated_amount is not null;
                """);

            migrationBuilder.Sql(
                """
                update shortage_ledger_entries
                set
                    resolved_physical_qty = round(
                        coalesce(
                            (
                                select sum(coalesce(a.allocated_qty, 0))
                                from shortage_resolution_allocations a
                                join shortage_resolutions r on r.id = a.resolution_id
                                where a.shortage_ledger_id = shortage_ledger_entries.id
                                  and a.allocation_type = 'Physical'
                                  and r.status = 'Posted'
                            ),
                            case
                                when coalesce(resolved_amount, 0) > 0 then 0
                                else coalesce(resolved_physical_qty, 0)
                            end
                        ),
                        6
                    ),
                    resolved_financial_qty_equivalent = round(
                        coalesce(
                            (
                                select sum(coalesce(a.financial_qty_equivalent, 0))
                                from shortage_resolution_allocations a
                                join shortage_resolutions r on r.id = a.resolution_id
                                where a.shortage_ledger_id = shortage_ledger_entries.id
                                  and a.allocation_type = 'Financial'
                                  and r.status = 'Posted'
                            ),
                            case
                                when coalesce(resolved_amount, 0) > 0 and shortage_value is not null and shortage_qty > 0
                                    then resolved_amount / (shortage_value / shortage_qty)
                                else 0
                            end
                        ),
                        6
                    ),
                    resolved_amount = round(
                        coalesce(
                            (
                                select sum(coalesce(a.allocated_amount, 0))
                                from shortage_resolution_allocations a
                                join shortage_resolutions r on r.id = a.resolution_id
                                where a.shortage_ledger_id = shortage_ledger_entries.id
                                  and a.allocation_type = 'Financial'
                                  and r.status = 'Posted'
                            ),
                            coalesce(resolved_amount, 0)
                        ),
                        6
                    );
                """);

            migrationBuilder.Sql(
                """
                update shortage_ledger_entries
                set
                    open_qty = round(
                        case
                            when shortage_qty - (coalesce(resolved_physical_qty, 0) + coalesce(resolved_financial_qty_equivalent, 0)) < 0 then 0
                            else shortage_qty - (coalesce(resolved_physical_qty, 0) + coalesce(resolved_financial_qty_equivalent, 0))
                        end,
                        6
                    ),
                    open_amount = case
                        when shortage_value is null or shortage_qty <= 0 then null
                        else round(
                            case
                                when shortage_qty - (coalesce(resolved_physical_qty, 0) + coalesce(resolved_financial_qty_equivalent, 0)) < 0 then 0
                                else shortage_qty - (coalesce(resolved_physical_qty, 0) + coalesce(resolved_financial_qty_equivalent, 0))
                            end * (shortage_value / shortage_qty),
                            6
                        )
                    end,
                    status = case
                        when status = 'Canceled' then 'Canceled'
                        when round(
                            case
                                when shortage_qty - (coalesce(resolved_physical_qty, 0) + coalesce(resolved_financial_qty_equivalent, 0)) < 0 then 0
                                else shortage_qty - (coalesce(resolved_physical_qty, 0) + coalesce(resolved_financial_qty_equivalent, 0))
                            end,
                            6
                        ) = 0 and shortage_qty > 0 then 'Resolved'
                        when round(coalesce(resolved_physical_qty, 0) + coalesce(resolved_financial_qty_equivalent, 0), 6) > 0 then 'PartiallyResolved'
                        else 'Open'
                    end;
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "allocation_type",
                table: "shortage_resolution_allocations");

            migrationBuilder.DropColumn(
                name: "financial_qty_equivalent",
                table: "shortage_resolution_allocations");

            migrationBuilder.DropColumn(
                name: "resolved_financial_qty_equivalent",
                table: "shortage_ledger_entries");

            migrationBuilder.RenameColumn(
                name: "resolved_physical_qty",
                table: "shortage_ledger_entries",
                newName: "resolved_qty");
        }
    }
}
