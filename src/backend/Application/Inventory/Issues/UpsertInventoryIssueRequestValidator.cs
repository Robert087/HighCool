using ERP.Domain.Inventory;
using FluentValidation;

namespace ERP.Application.Inventory.Issues;

public sealed class UpsertInventoryIssueRequestValidator : AbstractValidator<UpsertInventoryIssueRequest>
{
    public UpsertInventoryIssueRequestValidator()
    {
        RuleFor(request => request.IssueDate)
            .NotNull()
            .WithMessage("Issue date is required.");

        RuleFor(request => request.WarehouseId)
            .NotEmpty()
            .WithMessage("Warehouse is required.");

        RuleFor(request => request.Reason)
            .NotNull()
            .IsInEnum()
            .WithMessage("Issue reason is required.");

        RuleFor(request => request.ReferenceNo)
            .MaximumLength(64)
            .WithMessage("Reference number must be 64 characters or fewer.");

        RuleFor(request => request.RequestedBy)
            .MaximumLength(128)
            .WithMessage("Requested by must be 128 characters or fewer.");

        RuleFor(request => request.Notes)
            .MaximumLength(1000)
            .WithMessage("Notes must be 1000 characters or fewer.");

        RuleFor(request => request.Lines)
            .NotNull()
            .Must(lines => lines.Count > 0)
            .WithMessage("At least one issue line is required.")
            .Must(HaveUniqueLineNumbers)
            .WithMessage("Line numbers must be unique inside the inventory issue.")
            .Must(HaveUniqueItems)
            .WithMessage("The same item cannot appear more than once inside the inventory issue.");

        RuleForEach(request => request.Lines)
            .ChildRules(line =>
            {
                line.RuleFor(value => value.LineNo)
                    .GreaterThan(0)
                    .WithMessage("Line number must be greater than zero.");

                line.RuleFor(value => value.ItemId)
                    .NotEmpty()
                    .WithMessage("Item is required.");

                line.RuleFor(value => value.UomId)
                    .NotEmpty()
                    .WithMessage("UOM is required.");

                line.RuleFor(value => value.Quantity)
                    .GreaterThan(0m)
                    .WithMessage("Quantity must be greater than zero.");

                line.RuleFor(value => value.Notes)
                    .MaximumLength(500)
                    .WithMessage("Line notes must be 500 characters or fewer.");
            });
    }

    private static bool HaveUniqueLineNumbers(IReadOnlyList<UpsertInventoryIssueLineRequest> lines)
    {
        return lines.GroupBy(line => line.LineNo).All(group => group.Count() == 1);
    }

    private static bool HaveUniqueItems(IReadOnlyList<UpsertInventoryIssueLineRequest> lines)
    {
        return lines.GroupBy(line => line.ItemId).All(group => group.Count() == 1);
    }
}
