using FluentValidation;

namespace ERP.Application.MasterData.Warehouses;

public sealed class UpsertWarehouseRequestValidator : AbstractValidator<UpsertWarehouseRequest>
{
    public UpsertWarehouseRequestValidator()
    {
        RuleFor(request => request.Code)
            .NotEmpty()
            .MaximumLength(32);

        RuleFor(request => request.Name)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(request => request.Location)
            .MaximumLength(300);
    }
}
