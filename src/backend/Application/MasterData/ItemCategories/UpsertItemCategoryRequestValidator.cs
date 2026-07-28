using FluentValidation;

namespace ERP.Application.MasterData.ItemCategories;

public sealed class UpsertItemCategoryRequestValidator : AbstractValidator<UpsertItemCategoryRequest>
{
    public UpsertItemCategoryRequestValidator()
    {
        RuleFor(request => request.Code)
            .NotEmpty()
            .MaximumLength(32);

        RuleFor(request => request.Name)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(request => request.Description)
            .MaximumLength(500);
    }
}
