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

        RuleFor(request => request)
            .Must(request => request.IsSellable || request.IsComponent)
            .WithMessage("Item must be sellable, component, or both.");
    }
}
