using FluentValidation;

namespace ERP.Application.Pricing;

public sealed class UpsertPriceListRequestValidator : AbstractValidator<UpsertPriceListRequest>
{
    public UpsertPriceListRequestValidator()
    {
        RuleFor(request => request.Code)
            .NotEmpty()
            .MaximumLength(32);

        RuleFor(request => request.Name)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(request => request.Type)
            .IsInEnum();

        RuleFor(request => request.Currency)
            .NotEmpty()
            .Length(3)
            .Matches("^[A-Za-z]{3}$");

        RuleFor(request => request.Description)
            .MaximumLength(1000);
    }
}

public sealed class UpdatePriceListRequestValidator : AbstractValidator<UpdatePriceListRequest>
{
    public UpdatePriceListRequestValidator()
    {
        RuleFor(request => request.Code)
            .NotEmpty()
            .MaximumLength(32);

        RuleFor(request => request.Name)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(request => request.Type)
            .IsInEnum();

        RuleFor(request => request.Currency)
            .NotEmpty()
            .Length(3)
            .Matches("^[A-Za-z]{3}$");

        RuleFor(request => request.Description)
            .MaximumLength(1000);

        RuleFor(request => request.Version)
            .GreaterThanOrEqualTo(0);
    }
}

public sealed class UpsertItemPriceRequestValidator : AbstractValidator<UpsertItemPriceRequest>
{
    public UpsertItemPriceRequestValidator()
    {
        RuleFor(request => request.PriceListId)
            .NotEmpty();

        RuleFor(request => request.ItemId)
            .NotEmpty();

        RuleFor(request => request.UomId)
            .NotEmpty();

        RuleFor(request => request.Currency)
            .MaximumLength(3)
            .Matches("^[A-Za-z]{3}$")
            .When(request => !string.IsNullOrWhiteSpace(request.Currency));

        RuleFor(request => request.Rate)
            .GreaterThan(0m);

        RuleFor(request => request.MinimumQuantity)
            .GreaterThan(0m);

        RuleFor(request => request.ValidFrom)
            .NotNull();

        RuleFor(request => request.ValidTo)
            .GreaterThanOrEqualTo(request => request.ValidFrom)
            .When(request => request.ValidTo.HasValue && request.ValidFrom.HasValue);

        RuleFor(request => request.Notes)
            .MaximumLength(1000);
    }
}

public sealed class UpdateItemPriceRequestValidator : AbstractValidator<UpdateItemPriceRequest>
{
    public UpdateItemPriceRequestValidator()
    {
        RuleFor(request => request.PriceListId)
            .NotEmpty();

        RuleFor(request => request.ItemId)
            .NotEmpty();

        RuleFor(request => request.UomId)
            .NotEmpty();

        RuleFor(request => request.Currency)
            .MaximumLength(3)
            .Matches("^[A-Za-z]{3}$")
            .When(request => !string.IsNullOrWhiteSpace(request.Currency));

        RuleFor(request => request.Rate)
            .GreaterThan(0m);

        RuleFor(request => request.MinimumQuantity)
            .GreaterThan(0m);

        RuleFor(request => request.ValidFrom)
            .NotNull();

        RuleFor(request => request.ValidTo)
            .GreaterThanOrEqualTo(request => request.ValidFrom)
            .When(request => request.ValidTo.HasValue && request.ValidFrom.HasValue);

        RuleFor(request => request.Notes)
            .MaximumLength(1000);

        RuleFor(request => request.Version)
            .GreaterThanOrEqualTo(0);
    }
}

public sealed class VersionRequestValidator : AbstractValidator<VersionRequest>
{
    public VersionRequestValidator()
    {
        RuleFor(request => request.Version)
            .GreaterThanOrEqualTo(0);
    }
}
