using ERP.Application.Payments;
using ERP.Domain.Common;
using ERP.Domain.Payments;

namespace ERP.Infrastructure.Payments;

public sealed class PaymentAllocationService(ISupplierOpenBalanceService openBalanceService) : IPaymentAllocationService
{
    public async Task ValidateDraftAsync(Payment payment, CancellationToken cancellationToken)
    {
        ValidateParty(payment);
        ValidateAllocationStructure(payment, requireAllocations: false);

        if (payment.Allocations.Count == 0)
        {
            return;
        }

        await ValidateAgainstOpenBalancesAsync(payment, requireExactAllocationMatch: false, cancellationToken);
    }

    public async Task ValidateForPostingAsync(Payment payment, CancellationToken cancellationToken)
    {
        ValidateParty(payment);
        ValidateAllocationStructure(payment, requireAllocations: true);
        await ValidateAgainstOpenBalancesAsync(payment, requireExactAllocationMatch: true, cancellationToken);
    }

    private async Task ValidateAgainstOpenBalancesAsync(
        Payment payment,
        bool requireExactAllocationMatch,
        CancellationToken cancellationToken)
    {
        var openBalances = await openBalanceService.ListAsync(
            new SupplierOpenBalanceQuery(payment.PartyId, payment.Direction, null, null, null),
            cancellationToken);

        var targetsByKey = openBalances.ToDictionary(
            target => BuildTargetKey(target.TargetDocType, target.TargetDocId, null),
            target => target);

        var allocationTotal = Round(payment.Allocations.Sum(entity => entity.AllocatedAmount));
        if (allocationTotal > Round(payment.Amount))
        {
            throw new InvalidOperationException("Allocated total cannot exceed the payment amount.");
        }

        if (requireExactAllocationMatch && allocationTotal != Round(payment.Amount))
        {
            throw new InvalidOperationException("Posted supplier payments must be fully allocated. Payment amount must equal the allocated total.");
        }

        var groupedAllocations = payment.Allocations
            .GroupBy(entity => BuildTargetKey(entity.TargetDocType, entity.TargetDocId, entity.TargetLineId))
            .ToArray();

        foreach (var allocationGroup in groupedAllocations)
        {
            var firstAllocation = allocationGroup.First();
            EnsureTargetAllowedForDirection(payment.Direction, firstAllocation.TargetDocType);

            var targetKey = BuildTargetKey(firstAllocation.TargetDocType, firstAllocation.TargetDocId, null);
            if (!targetsByKey.TryGetValue(targetKey, out var target))
            {
                throw new InvalidOperationException("One or more payment allocation targets are no longer open or do not belong to the selected supplier and direction.");
            }

            var groupTotal = Round(allocationGroup.Sum(entity => entity.AllocatedAmount));
            if (groupTotal > target.OpenAmount)
            {
                throw new InvalidOperationException($"Allocated amount cannot exceed the open amount for {target.TargetDocumentNo}.");
            }
        }
    }

    private static void ValidateParty(Payment payment)
    {
        if (payment.PartyType != PaymentPartyType.Supplier)
        {
            throw new InvalidOperationException("Only supplier payments are supported in the current procurement flow.");
        }
    }

    private static void ValidateAllocationStructure(Payment payment, bool requireAllocations)
    {
        if (requireAllocations && payment.Allocations.Count == 0)
        {
            throw new InvalidOperationException("At least one payment allocation is required before posting.");
        }

        foreach (var allocation in payment.Allocations)
        {
            if (allocation.AllocatedAmount <= 0m)
            {
                throw new InvalidOperationException("Allocated amount must be greater than zero.");
            }

            if (allocation.AllocationOrder <= 0)
            {
                throw new InvalidOperationException("Allocation order must be greater than zero.");
            }
        }

        var duplicateTargets = payment.Allocations
            .GroupBy(entity => BuildTargetKey(entity.TargetDocType, entity.TargetDocId, entity.TargetLineId))
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToArray();

        if (duplicateTargets.Length > 0)
        {
            throw new InvalidOperationException("Duplicate payment allocation targets are not allowed inside the same payment.");
        }

        var duplicateOrder = payment.Allocations
            .GroupBy(entity => entity.AllocationOrder)
            .Any(group => group.Count() > 1);

        if (duplicateOrder)
        {
            throw new InvalidOperationException("Allocation order values must be unique inside the payment.");
        }
    }

    private static void EnsureTargetAllowedForDirection(PaymentDirection direction, PaymentTargetDocumentType targetDocType)
    {
        if (direction == PaymentDirection.OutboundToParty && targetDocType != PaymentTargetDocumentType.PurchaseReceipt)
        {
            throw new InvalidOperationException("Outbound supplier payments can only allocate to purchase receipt obligations.");
        }

        if (direction == PaymentDirection.InboundFromParty && targetDocType != PaymentTargetDocumentType.ShortageResolution)
        {
            throw new InvalidOperationException("Inbound supplier payments can only allocate to financial shortage resolution receivables.");
        }
    }

    private static string BuildTargetKey(PaymentTargetDocumentType targetDocType, Guid targetDocId, Guid? targetLineId)
    {
        return $"{targetDocType}:{targetDocId}:{targetLineId?.ToString() ?? "header"}";
    }

    private static decimal Round(decimal value)
    {
        return decimal.Round(value, 6, MidpointRounding.AwayFromZero);
    }
}
