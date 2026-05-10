using ERP.Application.Security;
using ERP.Domain.Identity;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Application.Tests;

internal sealed class TestRequestExecutionContext : IRequestExecutionContext
{
    public Guid? UserId { get; set; }

    public Guid? OrganizationId { get; set; }

    public Guid? MembershipId { get; set; }

    public Guid? SessionId { get; set; }

    public string Actor => "tester";

    public string? Email => "tester@highcool.test";

    public string? IpAddress => null;

    public string? UserAgent => null;

    public bool IsAuthenticated => true;

    public bool IsSystem => false;
}

internal static class TestOrganizationContext
{
    public static TestRequestExecutionContext CreateExecutionContext(Guid? organizationId = null)
        => new() { OrganizationId = organizationId };

    public static async Task<Guid> EnsureOrganizationAsync(AppDbContext dbContext, TestRequestExecutionContext executionContext)
    {
        var existingOrganization = await dbContext.Organizations
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync();

        if (existingOrganization is not null)
        {
            executionContext.OrganizationId = existingOrganization.Id;
            return existingOrganization.Id;
        }

        var organization = new Organization
        {
            Name = "Test Organization",
            DefaultCurrency = "EGP",
            Timezone = "Africa/Cairo",
            DefaultLanguage = "en",
            PurchaseOrderPrefix = "PO",
            PurchaseReceiptPrefix = "PR",
            PurchaseReturnPrefix = "RTN",
            PaymentPrefix = "PAY",
            SetupCompleted = true,
            SetupVersion = "v1",
            EnableProcurement = true,
            EnablePurchaseOrders = true,
            EnablePurchaseReceipts = true,
            EnableInventory = true,
            EnableWarehouses = true,
            EnableMultipleWarehouses = true,
            EnableSupplierManagement = true,
            EnableSupplierFinancials = true,
            EnableShortageManagement = true,
            EnableComponentsBom = true,
            EnableUom = true,
            EnableUomConversion = true,
            RequirePoBeforeReceipt = false,
            AllowDirectPurchaseReceipt = true,
            AllowPartialReceipt = true,
            AllowOverReceipt = false,
            OverReceiptTolerancePercent = 0,
            EnablePostingWorkflow = true,
            LockPostedDocuments = true,
            RequireApprovalBeforePosting = false,
            EnableReversals = true,
            RequireReasonForCancelOrReversal = true,
            AllowNegativeStock = false,
            EnableBatchTracking = false,
            EnableSerialTracking = false,
            EnableExpiryTracking = false,
            EnableStockTransfers = true,
            EnableStockAdjustments = true,
            CreatedBy = "seed"
        };

        dbContext.Organizations.Add(organization);

        await dbContext.SaveChangesAsync();
        executionContext.OrganizationId = organization.Id;
        return organization.Id;
    }
}
