using FluentValidation;

namespace ERP.Application.MasterData.Items;

public sealed class UpsertItemRequestValidator : AbstractValidator<UpsertItemRequest>
{
    public UpsertItemRequestValidator()
    {
        RuleFor(request => request.Code)
            .NotEmpty()
            .MaximumLength(32);

        RuleFor(request => request.Name)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(request => request.BaseUomId)
            .NotEmpty();

        RuleFor(request => request.MinimumStockQuantity)
            .GreaterThanOrEqualTo(0m);

        RuleFor(request => request.Components)
            .NotNull();

        RuleForEach(request => request.Components)
            .ChildRules(component =>
            {
                component.RuleFor(row => row.ComponentItemId)
                    .NotEmpty();

                component.RuleFor(row => row.UomId)
                    .NotEmpty();

                component.RuleFor(row => row.Quantity)
                    .GreaterThan(0m);
            });

        RuleFor(request => request)
            .Must(request => !request.HasComponents || request.Components.Count > 0)
            .WithMessage("Items marked as having components must include at least one component row.");

        RuleFor(request => request)
            .Must(request => request.HasComponents || request.Components.Count == 0)
            .WithMessage("Component rows can only be provided when the item is marked as having components.");
    }
}
