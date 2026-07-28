using ERP.Domain.Inventory;
using FluentValidation;

namespace ERP.Application.Inventory.Adjustments;

public sealed class UpsertInventoryAdjustmentRequestValidator : AbstractValidator<UpsertInventoryAdjustmentRequest>
{
    public UpsertInventoryAdjustmentRequestValidator()
    {
        RuleFor(entity => entity.AdjustmentNo)
            .MaximumLength(32);

        RuleFor(entity => entity.AdjustmentDate)
            .NotNull()
            .WithMessage("Adjustment date is required.");

        RuleFor(entity => entity.WarehouseId)
            .NotEmpty()
            .WithMessage("Warehouse is required.");

        RuleFor(entity => entity.Reason)
            .NotEmpty()
            .WithMessage("Reason is required.")
            .MaximumLength(300);

        RuleFor(entity => entity.Notes)
            .MaximumLength(1000);

        RuleFor(entity => entity.Lines)
            .NotEmpty()
            .WithMessage("At least one adjustment line is required.");

        RuleFor(entity => entity.Lines)
            .Must(HaveUniqueLineNumbers)
            .WithMessage("Line numbers must be unique inside the inventory adjustment.");

        RuleForEach(entity => entity.Lines)
            .ChildRules(line =>
            {
                line.RuleFor(item => item.LineNo)
                    .GreaterThan(0)
                    .WithMessage("Line number must be greater than zero.");

                line.RuleFor(item => item.ItemId)
                    .NotEmpty()
                    .WithMessage("Item is required.");

                line.RuleFor(item => item.UomId)
                    .NotEmpty()
                    .WithMessage("UOM is required.");

                line.RuleFor(item => item.Quantity)
                    .GreaterThan(0m)
                    .WithMessage("Quantity must be greater than zero.");

                line.RuleFor(item => item.AdjustmentType)
                    .Must(value => value is InventoryAdjustmentType.Increase or InventoryAdjustmentType.Decrease)
                    .WithMessage("Adjustment type is required.");

                line.RuleFor(item => item.Notes)
                    .MaximumLength(500);
            });
    }

    private static bool HaveUniqueLineNumbers(IReadOnlyList<UpsertInventoryAdjustmentLineRequest> lines)
    {
        return lines.Select(line => line.LineNo).Distinct().Count() == lines.Count;
    }
}
