using ERP.Application.MasterData.Customers;
using ERP.Application.MasterData.Suppliers;
using ERP.Application.MasterData.Uoms;
using ERP.Application.MasterData.Warehouses;
using FluentValidation.TestHelper;
using Xunit;

namespace ERP.Application.Tests;

public sealed class MasterDataValidatorsTests
{
    [Fact]
    public void CustomerValidators_ShouldRequireCoreFieldsAndRejectNegativeCreditLimit()
    {
        var createValidator = new CreateCustomerRequestValidator();
        var createModel = new CreateCustomerRequest("", "", null, "invalid-email", null, null, null, null, -1m, null, null, true);

        var createResult = createValidator.TestValidate(createModel);

        createResult.ShouldHaveValidationErrorFor(request => request.Code);
        createResult.ShouldHaveValidationErrorFor(request => request.Name);
        createResult.ShouldHaveValidationErrorFor(request => request.Email);
        createResult.ShouldHaveValidationErrorFor(request => request.CreditLimit);

        var updateValidator = new UpdateCustomerRequestValidator();
        var updateModel = new UpdateCustomerRequest("", "", null, "invalid-email", null, null, null, null, -5m, null, null, true);

        var updateResult = updateValidator.TestValidate(updateModel);

        updateResult.ShouldHaveValidationErrorFor(request => request.Code);
        updateResult.ShouldHaveValidationErrorFor(request => request.Name);
        updateResult.ShouldHaveValidationErrorFor(request => request.Email);
        updateResult.ShouldHaveValidationErrorFor(request => request.CreditLimit);
    }

    [Fact]
    public void SupplierValidator_ShouldRequireCoreIdentityFields()
    {
        var validator = new UpsertSupplierRequestValidator();
        var model = new UpsertSupplierRequest("", "", "", null, "invalid-email", true);

        var result = validator.TestValidate(model);

        result.ShouldHaveValidationErrorFor(request => request.Code);
        result.ShouldHaveValidationErrorFor(request => request.Name);
        result.ShouldHaveValidationErrorFor(request => request.StatementName);
        result.ShouldHaveValidationErrorFor(request => request.Email);
    }

    [Fact]
    public void WarehouseValidator_ShouldRequireCodeAndName()
    {
        var validator = new UpsertWarehouseRequestValidator();
        var model = new UpsertWarehouseRequest("", "", null, true);

        var result = validator.TestValidate(model);

        result.ShouldHaveValidationErrorFor(request => request.Code);
        result.ShouldHaveValidationErrorFor(request => request.Name);
    }

    [Fact]
    public void UomValidator_ShouldRejectPrecisionOutsideSupportedRange()
    {
        var validator = new UpsertUomRequestValidator();
        var model = new UpsertUomRequest("PCS", "Pieces", 9, false, true);

        var result = validator.TestValidate(model);

        result.ShouldHaveValidationErrorFor(request => request.Precision);
    }
}
