using FluentValidation;

namespace ERP.Application.Reversals;

public sealed class ReverseDocumentRequestValidator : AbstractValidator<ReverseDocumentRequest>
{
    public ReverseDocumentRequestValidator()
    {
        RuleFor(entity => entity.ReversalDate)
            .NotNull()
            .WithMessage("Reversal date is required.");

        RuleFor(entity => entity.ReversalReason)
            .NotEmpty()
            .WithMessage("Reversal reason is required.")
            .MaximumLength(1000);
    }
}
