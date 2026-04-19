using FluentValidation;

namespace ERP.Application.MasterData.Suppliers;

public sealed class UpsertSupplierRequestValidator : AbstractValidator<UpsertSupplierRequest>
{
    public UpsertSupplierRequestValidator()
    {
        RuleFor(request => request.Code)
            .NotEmpty()
            .MaximumLength(32);

        RuleFor(request => request.Name)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(request => request.StatementName)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(request => request.Phone)
            .MaximumLength(50);

        RuleFor(request => request.Email)
            .MaximumLength(200)
            .EmailAddress()
            .When(request => !string.IsNullOrWhiteSpace(request.Email));
    }
}
