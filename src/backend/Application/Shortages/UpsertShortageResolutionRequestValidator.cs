using ERP.Domain.Shortages;
using FluentValidation;

namespace ERP.Application.Shortages;

public sealed class UpsertShortageResolutionRequestValidator : AbstractValidator<UpsertShortageResolutionRequest>
{
    public UpsertShortageResolutionRequestValidator()
    {
        RuleFor(request => request.ResolutionNo)
            .MaximumLength(32);

        RuleFor(request => request.SupplierId)
            .NotEmpty();

        RuleFor(request => request.ResolutionType)
            .NotNull()
            .IsInEnum();

        RuleFor(request => request.ResolutionDate)
            .NotNull();

        RuleFor(request => request.TotalQty)
            .GreaterThanOrEqualTo(0m)
            .When(request => request.TotalQty.HasValue);

        RuleFor(request => request.TotalAmount)
            .GreaterThanOrEqualTo(0m)
            .When(request => request.TotalAmount.HasValue);

        RuleFor(request => request.Currency)
            .MaximumLength(16);

        RuleFor(request => request.Notes)
            .MaximumLength(1000);

        RuleFor(request => request.Allocations)
            .NotNull();

        RuleFor(request => request.Allocations)
            .Must(HaveUniqueShortageRows)
            .WithMessage("Duplicate shortage rows are not allowed inside the same resolution.");

        RuleForEach(request => request.Allocations)
            .ChildRules(allocation =>
            {
                allocation.RuleFor(entry => entry.ShortageLedgerId)
                    .NotEmpty();

                allocation.RuleFor(entry => entry.SequenceNo)
                    .GreaterThan(0);

                allocation.RuleFor(entry => entry.AllocationMethod)
                    .MaximumLength(32);

                allocation.RuleFor(entry => entry.AllocatedQty)
                    .GreaterThan(0m)
                    .When(entry => entry.AllocatedQty.HasValue);

                allocation.RuleFor(entry => entry.AllocatedAmount)
                    .GreaterThan(0m)
                    .When(entry => entry.AllocatedAmount.HasValue);

                allocation.RuleFor(entry => entry.ValuationRate)
                    .GreaterThan(0m)
                    .When(entry => entry.ValuationRate.HasValue);
            });

        When(request => request.ResolutionType == ShortageResolutionType.Physical, () =>
        {
            RuleForEach(request => request.Allocations)
                .Must(entry => entry.AllocatedQty.HasValue && entry.AllocatedQty.Value > 0m)
                .WithMessage("Physical resolutions require quantity allocations.");

            RuleForEach(request => request.Allocations)
                .Must(entry => !entry.AllocatedAmount.HasValue)
                .WithMessage("Physical resolutions cannot store amount allocations.");

            RuleForEach(request => request.Allocations)
                .Must(entry => !entry.ValuationRate.HasValue)
                .WithMessage("Physical resolutions do not require a valuation rate.");
        });

        When(request => request.ResolutionType == ShortageResolutionType.Financial, () =>
        {
            RuleForEach(request => request.Allocations)
                .Must(entry => entry.AllocatedQty.HasValue && entry.AllocatedQty.Value > 0m)
                .WithMessage("Financial resolutions require resolved quantity.");

            RuleForEach(request => request.Allocations)
                .Must(entry => entry.ValuationRate.HasValue && entry.ValuationRate.Value > 0m)
                .WithMessage("Financial resolutions require a valuation rate.");
        });
    }

    private static bool HaveUniqueShortageRows(IReadOnlyList<UpsertShortageResolutionAllocationRequest> allocations)
    {
        return allocations
            .Where(entry => entry.ShortageLedgerId != Guid.Empty)
            .Select(entry => entry.ShortageLedgerId)
            .Distinct()
            .Count() == allocations.Count(entry => entry.ShortageLedgerId != Guid.Empty);
    }
}
