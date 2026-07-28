using FluentValidation;

namespace ERP.Application.Inventory.Counts;

public sealed class UpsertInventoryCountRequestValidator : AbstractValidator<UpsertInventoryCountRequest>
{
    public UpsertInventoryCountRequestValidator()
    {
        RuleFor(request => request.CountDate)
            .NotNull()
            .WithMessage("Count date is required.");

        RuleFor(request => request.WarehouseId)
            .NotEmpty()
            .WithMessage("Warehouse is required.");

        RuleFor(request => request.Notes)
            .MaximumLength(1000)
            .WithMessage("Notes must be 1000 characters or fewer.");

        RuleFor(request => request.Lines)
            .NotNull()
            .Must(lines => lines.Count > 0)
            .WithMessage("At least one count line is required.")
            .Must(HaveUniqueLineNumbers)
            .WithMessage("Line numbers must be unique inside the inventory count.")
            .Must(HaveUniqueItems)
            .WithMessage("The same item cannot appear more than once inside the inventory count.");

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

                line.RuleFor(value => value.CountedQty)
                    .GreaterThanOrEqualTo(0m)
                    .WithMessage("Counted quantity cannot be negative.");

                line.RuleFor(value => value.Notes)
                    .MaximumLength(500)
                    .WithMessage("Line notes must be 500 characters or fewer.");
            });
    }

    private static bool HaveUniqueLineNumbers(IReadOnlyList<UpsertInventoryCountLineRequest> lines)
    {
        return lines.GroupBy(line => line.LineNo).All(group => group.Count() == 1);
    }

    private static bool HaveUniqueItems(IReadOnlyList<UpsertInventoryCountLineRequest> lines)
    {
        return lines.GroupBy(line => line.ItemId).All(group => group.Count() == 1);
    }
}
