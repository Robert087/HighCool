using FluentValidation;

namespace ERP.Application.Inventory.Monitoring;

public sealed class UpdateReorderSettingsRequestValidator : AbstractValidator<UpdateReorderSettingsRequest>
{
    public UpdateReorderSettingsRequestValidator()
    {
        RuleFor(request => request.MinimumStock)
            .GreaterThanOrEqualTo(0m);

        RuleFor(request => request.MaximumStock)
            .GreaterThan(0m);

        RuleFor(request => request.ReorderPoint)
            .GreaterThanOrEqualTo(request => request.MinimumStock);

        RuleFor(request => request.MaximumStock)
            .GreaterThanOrEqualTo(request => request.ReorderPoint);

        RuleFor(request => request.ReorderQuantity)
            .GreaterThan(0m);

        RuleFor(request => request.SafetyStock)
            .GreaterThanOrEqualTo(0m)
            .When(request => request.SafetyStock.HasValue);

        RuleFor(request => request.LeadTimeDays)
            .GreaterThanOrEqualTo(0)
            .When(request => request.LeadTimeDays.HasValue);
    }
}
