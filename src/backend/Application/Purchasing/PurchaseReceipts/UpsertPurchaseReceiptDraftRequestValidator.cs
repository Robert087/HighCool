using FluentValidation;

namespace ERP.Application.Purchasing.PurchaseReceipts;

public sealed class UpsertPurchaseReceiptDraftRequestValidator : AbstractValidator<UpsertPurchaseReceiptDraftRequest>
{
    public UpsertPurchaseReceiptDraftRequestValidator()
    {
        RuleFor(request => request.ReceiptNo)
            .MaximumLength(32);

        RuleFor(request => request.SupplierId)
            .NotEmpty();

        RuleFor(request => request.WarehouseId)
            .NotEmpty();

        RuleFor(request => request.ReceiptDate)
            .NotNull();

        RuleFor(request => request.SupplierPayableAmount)
            .GreaterThanOrEqualTo(0m);

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

                line.RuleFor(entry => entry.PurchaseOrderLineId)
                    .NotEqual(Guid.Empty)
                    .When(entry => entry.PurchaseOrderLineId.HasValue);

                line.RuleFor(entry => entry.ItemId)
                    .NotEmpty();

                line.RuleFor(entry => entry.OrderedQtySnapshot)
                    .GreaterThanOrEqualTo(0m)
                    .When(entry => entry.OrderedQtySnapshot.HasValue);

                line.RuleFor(entry => entry.ReceivedQty)
                    .GreaterThan(0m);

                line.RuleFor(entry => entry.UomId)
                    .NotEmpty();

                line.RuleFor(entry => entry.Notes)
                    .MaximumLength(500);

                line.RuleFor(entry => entry.Components)
                    .NotNull();

                line.RuleFor(entry => entry.Components)
                    .Must(HaveUniqueComponentRows)
                    .WithMessage("No duplicate component item rows are allowed inside the same purchase receipt line.");

                line.RuleForEach(entry => entry.Components)
                    .ChildRules(component =>
                    {
                        component.RuleFor(row => row.ComponentItemId)
                            .NotEmpty();

                        component.RuleFor(row => row.ActualReceivedQty)
                            .GreaterThanOrEqualTo(0m);

                        component.RuleFor(row => row.UomId)
                            .NotEmpty();

                        component.RuleFor(row => row.Notes)
                            .MaximumLength(500);
                    });
            });
    }

    private static bool HaveUniqueLineNumbers(IReadOnlyList<UpsertPurchaseReceiptLineRequest> lines)
    {
        return lines.Select(line => line.LineNo).Distinct().Count() == lines.Count;
    }

    private static bool HaveUniqueComponentRows(IReadOnlyList<UpsertPurchaseReceiptLineComponentRequest> components)
    {
        return components
            .Where(component => component.ComponentItemId != Guid.Empty)
            .Select(component => component.ComponentItemId)
            .Distinct()
            .Count() == components.Count(component => component.ComponentItemId != Guid.Empty);
    }
}
