using ERP.Application.Common.Exceptions;
using ERP.Application.Payments;
using ERP.Domain.Common;
using ERP.Domain.Payments;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Payments;

public sealed class PaymentService(
    AppDbContext dbContext,
    IPaymentAllocationService allocationService,
    IPaymentQueryService queryService) : IPaymentService
{
    public async Task<PaymentDto> CreateDraftAsync(UpsertPaymentRequest request, string actor, CancellationToken cancellationToken)
    {
        await ValidateRequestAsync(request, cancellationToken);

        var payment = new Payment
        {
            PaymentNo = await ResolvePaymentNoAsync(request.PaymentNo, null, cancellationToken),
            PartyType = request.PartyType,
            PartyId = request.PartyId,
            Direction = request.Direction,
            Amount = Round(request.Amount),
            PaymentDate = request.PaymentDate!.Value,
            Currency = NormalizeOptionalText(request.Currency),
            ExchangeRate = request.ExchangeRate,
            PaymentMethod = request.PaymentMethod,
            ReferenceNote = NormalizeOptionalText(request.ReferenceNote),
            Notes = NormalizeOptionalText(request.Notes),
            Status = DocumentStatus.Draft,
            CreatedBy = actor
        };

        dbContext.Payments.Add(payment);
        AddAllocations(payment, request.Allocations, actor);

        await allocationService.ValidateDraftAsync(payment, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return await GetRequiredAsync(payment.Id, cancellationToken);
    }

    public async Task<PaymentDto?> UpdateDraftAsync(Guid id, UpsertPaymentRequest request, string actor, CancellationToken cancellationToken)
    {
        var payment = await dbContext.Payments
            .Include(entity => entity.Allocations)
            .SingleOrDefaultAsync(entity => entity.Id == id, cancellationToken);

        if (payment is null)
        {
            return null;
        }

        EnsureDraftIsEditable(payment);
        await ValidateRequestAsync(request, cancellationToken);

        payment.PaymentNo = await ResolvePaymentNoAsync(request.PaymentNo, id, cancellationToken);
        payment.PartyType = request.PartyType;
        payment.PartyId = request.PartyId;
        payment.Direction = request.Direction;
        payment.Amount = Round(request.Amount);
        payment.PaymentDate = request.PaymentDate!.Value;
        payment.Currency = NormalizeOptionalText(request.Currency);
        payment.ExchangeRate = request.ExchangeRate;
        payment.PaymentMethod = request.PaymentMethod;
        payment.ReferenceNote = NormalizeOptionalText(request.ReferenceNote);
        payment.Notes = NormalizeOptionalText(request.Notes);
        payment.UpdatedBy = actor;

        dbContext.PaymentAllocations.RemoveRange(payment.Allocations);
        payment.Allocations.Clear();
        AddAllocations(payment, request.Allocations, actor);

        await allocationService.ValidateDraftAsync(payment, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return await queryService.GetAsync(payment.Id, cancellationToken);
    }

    private async Task ValidateRequestAsync(UpsertPaymentRequest request, CancellationToken cancellationToken)
    {
        if (request.PartyType != PaymentPartyType.Supplier)
        {
            throw new InvalidOperationException("Only supplier payments are supported in the current procurement flow.");
        }

        var supplierExists = await dbContext.Suppliers.AnyAsync(entity => entity.Id == request.PartyId && entity.IsActive, cancellationToken);
        if (!supplierExists)
        {
            throw new InvalidOperationException("Supplier was not found.");
        }
    }

    private async Task<string> ResolvePaymentNoAsync(string? requestedPaymentNo, Guid? currentId, CancellationToken cancellationToken)
    {
        var value = string.IsNullOrWhiteSpace(requestedPaymentNo)
            ? $"PAY-{DateTime.UtcNow:yyyyMMddHHmmssfff}"
            : requestedPaymentNo.Trim();

        var exists = await dbContext.Payments.AnyAsync(entity => entity.PaymentNo == value && entity.Id != currentId, cancellationToken);
        if (exists)
        {
            throw new DuplicateEntityException($"Payment number '{value}' already exists.");
        }

        return value;
    }

    private void AddAllocations(Payment payment, IReadOnlyList<UpsertPaymentAllocationRequest> allocations, string actor)
    {
        foreach (var allocation in allocations.OrderBy(entity => entity.AllocationOrder))
        {
            payment.Allocations.Add(new PaymentAllocation
            {
                Payment = payment,
                TargetDocType = allocation.TargetDocType,
                TargetDocId = allocation.TargetDocId,
                TargetLineId = allocation.TargetLineId,
                AllocatedAmount = Round(allocation.AllocatedAmount),
                AllocationOrder = allocation.AllocationOrder,
                CreatedBy = actor
            });
        }
    }

    private async Task<PaymentDto> GetRequiredAsync(Guid id, CancellationToken cancellationToken)
    {
        var dto = await queryService.GetAsync(id, cancellationToken);
        return dto ?? throw new InvalidOperationException("Payment was not found after save.");
    }

    private static void EnsureDraftIsEditable(Payment payment)
    {
        if (payment.Status != DocumentStatus.Draft)
        {
            throw new InvalidOperationException("Only Draft payments can be edited.");
        }
    }

    private static decimal Round(decimal value)
    {
        return decimal.Round(value, 6, MidpointRounding.AwayFromZero);
    }

    private static string? NormalizeOptionalText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
