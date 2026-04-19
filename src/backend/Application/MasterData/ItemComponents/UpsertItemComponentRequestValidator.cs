using FluentValidation;

namespace ERP.Application.MasterData.ItemComponents;

public sealed class UpsertItemComponentRequestValidator : AbstractValidator<UpsertItemComponentRequest>
{
    public UpsertItemComponentRequestValidator()
    {
        RuleFor(request => request.ParentItemId)
            .NotEmpty();

        RuleFor(request => request.ComponentItemId)
            .NotEmpty();

        RuleFor(request => request.Quantity)
            .GreaterThan(0m);

        RuleFor(request => request)
            .Must(request => request.ParentItemId != request.ComponentItemId)
            .WithMessage("Parent item and component item must be different.");
    }
}
