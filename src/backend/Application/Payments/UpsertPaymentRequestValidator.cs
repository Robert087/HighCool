using ERP.Domain.Payments;
using FluentValidation;

namespace ERP.Application.Payments;

public sealed class UpsertPaymentRequestValidator : AbstractValidator<UpsertPaymentRequest>
{
    public UpsertPaymentRequestValidator()
    {
        RuleFor(request => request.PaymentNo)
            .MaximumLength(32);

        RuleFor(request => request.PartyType)
            .IsInEnum();

        RuleFor(request => request.PartyId)
            .NotEmpty();

        RuleFor(request => request.Direction)
            .IsInEnum();

        RuleFor(request => request.Amount)
            .GreaterThan(0m);

        RuleFor(request => request.PaymentDate)
            .NotNull();

        RuleFor(request => request.Currency)
            .MaximumLength(16);

        RuleFor(request => request.ExchangeRate)
            .GreaterThan(0m)
            .When(request => request.ExchangeRate.HasValue);

        RuleFor(request => request.ReferenceNote)
            .MaximumLength(128);

        RuleFor(request => request.Notes)
            .MaximumLength(1000);

        RuleFor(request => request.Allocations)
            .NotNull();

        RuleFor(request => request.Allocations)
            .Must(HaveUniqueAllocationOrder)
            .WithMessage("Allocation order values must be unique inside the payment.");

        RuleForEach(request => request.Allocations)
            .ChildRules(allocation =>
            {
                allocation.RuleFor(entry => entry.TargetDocType)
                    .IsInEnum();

                allocation.RuleFor(entry => entry.TargetDocId)
                    .NotEmpty();

                allocation.RuleFor(entry => entry.TargetLineId)
                    .NotEqual(Guid.Empty)
                    .When(entry => entry.TargetLineId.HasValue);

                allocation.RuleFor(entry => entry.AllocatedAmount)
                    .GreaterThan(0m);

                allocation.RuleFor(entry => entry.AllocationOrder)
                    .GreaterThan(0);
            });
    }

    private static bool HaveUniqueAllocationOrder(IReadOnlyList<UpsertPaymentAllocationRequest> allocations)
    {
        return allocations.Select(allocation => allocation.AllocationOrder).Distinct().Count() == allocations.Count;
    }
}
