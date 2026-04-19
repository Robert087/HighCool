using FluentValidation;

namespace ERP.Application.MasterData.Uoms;

public sealed class UpsertUomRequestValidator : AbstractValidator<UpsertUomRequest>
{
    public UpsertUomRequestValidator()
    {
        RuleFor(request => request.Code)
            .NotEmpty()
            .MaximumLength(16);

        RuleFor(request => request.Name)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(request => request.Precision)
            .InclusiveBetween(0, 6);
    }
}
