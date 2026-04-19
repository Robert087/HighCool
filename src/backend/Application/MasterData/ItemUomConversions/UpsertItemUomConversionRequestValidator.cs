using FluentValidation;

namespace ERP.Application.MasterData.ItemUomConversions;

public sealed class UpsertItemUomConversionRequestValidator : AbstractValidator<UpsertItemUomConversionRequest>
{
    public UpsertItemUomConversionRequestValidator()
    {
        RuleFor(request => request.ItemId)
            .NotEmpty();

        RuleFor(request => request.FromUomId)
            .NotEmpty();

        RuleFor(request => request.ToUomId)
            .NotEmpty();

        RuleFor(request => request.Factor)
            .GreaterThan(0m);

        RuleFor(request => request.MinFraction)
            .GreaterThanOrEqualTo(0m);

        RuleFor(request => request)
            .Must(request => request.FromUomId != request.ToUomId)
            .WithMessage("From UOM and To UOM must be different.");
    }
}
