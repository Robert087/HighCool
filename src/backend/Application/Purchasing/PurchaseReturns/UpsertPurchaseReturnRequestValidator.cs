using FluentValidation;

namespace ERP.Application.Purchasing.PurchaseReturns;

public sealed class UpsertPurchaseReturnRequestValidator : AbstractValidator<UpsertPurchaseReturnRequest>
{
    public UpsertPurchaseReturnRequestValidator()
    {
        RuleFor(entity => entity.SupplierId)
            .NotEmpty()
            .WithMessage("Supplier is required.");

        RuleFor(entity => entity.ReturnDate)
            .NotNull()
            .WithMessage("Return date is required.");

        RuleFor(entity => entity.Notes)
            .MaximumLength(1000);

        RuleFor(entity => entity.Lines)
            .NotEmpty()
            .WithMessage("At least one purchase return line is required.");

        RuleFor(entity => entity.Lines)
            .Must(HaveUniqueLineNumbers)
            .WithMessage("Line numbers must be unique inside the purchase return.");

        RuleFor(entity => entity.Lines)
            .Must(HaveUniqueLogicalRows)
            .WithMessage("Duplicate purchase return rows are not allowed inside the same document.");

        RuleForEach(entity => entity.Lines)
            .ChildRules(line =>
            {
                line.RuleFor(item => item.LineNo)
                    .GreaterThan(0)
                    .WithMessage("Line number must be greater than zero.");

                line.RuleFor(item => item.ItemId)
                    .NotEmpty()
                    .WithMessage("Item is required.");

                line.RuleFor(item => item.WarehouseId)
                    .NotEmpty()
                    .WithMessage("Warehouse is required.");

                line.RuleFor(item => item.ReturnQty)
                    .GreaterThan(0m)
                    .WithMessage("Return quantity must be greater than zero.");

                line.RuleFor(item => item.UomId)
                    .NotEmpty()
                    .WithMessage("UOM is required.");
            });
    }

    private static bool HaveUniqueLineNumbers(IReadOnlyList<UpsertPurchaseReturnLineRequest> lines)
    {
        return lines.Select(line => line.LineNo).Distinct().Count() == lines.Count;
    }

    private static bool HaveUniqueLogicalRows(IReadOnlyList<UpsertPurchaseReturnLineRequest> lines)
    {
        return lines
            .Where(line => line.ReferenceReceiptLineId.HasValue)
            .Select(line => line.ReferenceReceiptLineId!.Value)
            .Distinct()
            .Count() == lines.Count(line => line.ReferenceReceiptLineId.HasValue)
            && lines
                .Where(line => !line.ReferenceReceiptLineId.HasValue)
                .Select(line => $"{line.ItemId}:{line.ComponentId}:{line.WarehouseId}:{line.UomId}")
                .Distinct()
                .Count() == lines.Count(line => !line.ReferenceReceiptLineId.HasValue);
    }
}
