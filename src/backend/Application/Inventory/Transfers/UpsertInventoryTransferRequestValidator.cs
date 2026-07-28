using FluentValidation;

namespace ERP.Application.Inventory.Transfers;

public sealed class UpsertInventoryTransferRequestValidator : AbstractValidator<UpsertInventoryTransferRequest>
{
    public UpsertInventoryTransferRequestValidator()
    {
        RuleFor(entity => entity.TransferNo)
            .MaximumLength(32);

        RuleFor(entity => entity.TransferDate)
            .NotNull()
            .WithMessage("Transfer date is required.");

        RuleFor(entity => entity.SourceWarehouseId)
            .NotEmpty()
            .WithMessage("Source warehouse is required.");

        RuleFor(entity => entity.DestinationWarehouseId)
            .NotEmpty()
            .WithMessage("Destination warehouse is required.");

        RuleFor(entity => entity)
            .Must(entity => entity.SourceWarehouseId == Guid.Empty ||
                            entity.DestinationWarehouseId == Guid.Empty ||
                            entity.SourceWarehouseId != entity.DestinationWarehouseId)
            .WithMessage("Source warehouse and destination warehouse must be different.");

        RuleFor(entity => entity.Notes)
            .MaximumLength(1000);

        RuleFor(entity => entity.Lines)
            .NotEmpty()
            .WithMessage("At least one transfer line is required.");

        RuleFor(entity => entity.Lines)
            .Must(HaveUniqueLineNumbers)
            .WithMessage("Line numbers must be unique inside the inventory transfer.");

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

                line.RuleFor(item => item.Notes)
                    .MaximumLength(500);
            });
    }

    private static bool HaveUniqueLineNumbers(IReadOnlyList<UpsertInventoryTransferLineRequest> lines)
    {
        return lines.Select(line => line.LineNo).Distinct().Count() == lines.Count;
    }
}
