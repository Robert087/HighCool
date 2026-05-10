using FluentValidation;

namespace ERP.Application.Purchasing.PurchaseOrders;

public sealed class UpsertPurchaseOrderRequestValidator : AbstractValidator<UpsertPurchaseOrderRequest>
{
    public UpsertPurchaseOrderRequestValidator()
    {
        RuleFor(request => request.PoNo)
            .MaximumLength(32);

        RuleFor(request => request.SupplierId)
            .NotEmpty();

        RuleFor(request => request.OrderDate)
            .NotNull();

        RuleFor(request => request.Notes)
            .MaximumLength(1000);

        RuleFor(request => request.Lines)
            .NotNull()
            .Must(lines => lines.Count > 0)
            .WithMessage("At least one line is required.");

        RuleFor(request => request.Lines)
            .Must(HaveUniqueLineNumbers)
            .WithMessage("Line numbers must be unique inside the document.");

        RuleForEach(request => request.Lines)
            .ChildRules(line =>
            {
                line.RuleFor(entry => entry.LineNo)
                    .GreaterThan(0);

                line.RuleFor(entry => entry.ItemId)
                    .NotEmpty();

                line.RuleFor(entry => entry.OrderedQty)
                    .GreaterThan(0m);

                line.RuleFor(entry => entry.UnitPrice)
                    .GreaterThanOrEqualTo(0m);

                line.RuleFor(entry => entry.UomId)
                    .NotEmpty();

                line.RuleFor(entry => entry.Notes)
                    .MaximumLength(500);
            });
    }

    private static bool HaveUniqueLineNumbers(IReadOnlyList<UpsertPurchaseOrderLineRequest> lines)
    {
        return lines.Select(line => line.LineNo).Distinct().Count() == lines.Count;
    }
}
