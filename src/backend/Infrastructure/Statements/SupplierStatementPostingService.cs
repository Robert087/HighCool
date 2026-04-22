using ERP.Application.Statements;
using ERP.Domain.Inventory;
using ERP.Domain.Payments;
using ERP.Domain.Purchasing;
using ERP.Domain.Shortages;
using ERP.Domain.Statements;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Statements;

public sealed class SupplierStatementPostingService(AppDbContext dbContext) : ISupplierStatementPostingService
{
    public async Task<IReadOnlyList<SupplierStatementEntry>> CreatePurchaseReceiptEntriesAsync(
        PurchaseReceipt receipt,
        IReadOnlyList<StockLedgerEntry> stockEntries,
        string actor,
        CancellationToken cancellationToken)
    {
        var sourceLineId = receipt.Id;
        var duplicateExists = await dbContext.SupplierStatementEntries
            .AnyAsync(
                entity => entity.SourceDocType == SupplierStatementSourceDocumentType.PurchaseReceipt &&
                          entity.SourceDocId == receipt.Id &&
                          entity.SourceLineId == sourceLineId &&
                          entity.EffectType == SupplierStatementEffectType.PurchaseReceipt,
                cancellationToken);

        if (duplicateExists)
        {
            throw new InvalidOperationException("Supplier statement effects already exist for this purchase receipt.");
        }

        var amount = Round(receipt.SupplierPayableAmount);
        var runningBalance = await GetLatestRunningBalanceAsync(receipt.SupplierId, cancellationToken);
        runningBalance = Round(runningBalance + amount);

        var entry = new SupplierStatementEntry
        {
            SupplierId = receipt.SupplierId,
            EntryDate = receipt.ReceiptDate,
            SourceDocType = SupplierStatementSourceDocumentType.PurchaseReceipt,
            SourceDocId = receipt.Id,
            SourceLineId = sourceLineId,
            EffectType = SupplierStatementEffectType.PurchaseReceipt,
            Debit = 0m,
            Credit = amount,
            RunningBalance = runningBalance,
            Currency = null,
            Notes = amount == 0m
                ? $"Purchase receipt {receipt.ReceiptNo} posted with zero supplier payable amount. Until receipt pricing is modeled per line, payable value is tracked from the explicit receipt header amount."
                : $"Purchase receipt {receipt.ReceiptNo}",
            CreatedBy = actor
        };

        dbContext.SupplierStatementEntries.Add(entry);
        return [entry];
    }

    public async Task<IReadOnlyList<SupplierStatementEntry>> CreateFinancialShortageResolutionEntriesAsync(
        ShortageResolution resolution,
        string actor,
        CancellationToken cancellationToken)
    {
        var financialAllocations = resolution.Allocations
            .Where(entity => entity.AllocationType == ShortageAllocationType.Financial)
            .OrderBy(entity => entity.SequenceNo)
            .ToArray();

        if (financialAllocations.Length == 0)
        {
            return [];
        }

        var allocationIds = financialAllocations.Select(entity => entity.Id).ToArray();
        var duplicateExists = await dbContext.SupplierStatementEntries
            .AnyAsync(
                entity => entity.SourceDocType == SupplierStatementSourceDocumentType.ShortageResolution &&
                          entity.SourceDocId == resolution.Id &&
                          entity.SourceLineId.HasValue &&
                          allocationIds.Contains(entity.SourceLineId.Value) &&
                          entity.EffectType == SupplierStatementEffectType.ShortageFinancialResolution,
                cancellationToken);

        if (duplicateExists)
        {
            throw new InvalidOperationException("Supplier statement effects already exist for this shortage resolution.");
        }

        var runningBalance = await GetLatestRunningBalanceAsync(resolution.SupplierId, cancellationToken);
        var entries = new List<SupplierStatementEntry>(financialAllocations.Length);

        foreach (var allocation in financialAllocations)
        {
            var shortage = allocation.ShortageLedgerEntry
                ?? throw new InvalidOperationException("Supplier statement posting requires shortage allocation traceability.");
            var amount = Round(allocation.AllocatedAmount ?? 0m);

            runningBalance = Round(runningBalance - amount);

            var notes = shortage.PurchaseReceipt is null
                ? $"Financial shortage resolution {resolution.ResolutionNo}"
                : $"Financial shortage resolution {resolution.ResolutionNo} for receipt {shortage.PurchaseReceipt.ReceiptNo} component {shortage.ComponentItem?.Code ?? shortage.ComponentItemId.ToString()}";

            var entry = new SupplierStatementEntry
            {
                SupplierId = resolution.SupplierId,
                EntryDate = resolution.ResolutionDate,
                SourceDocType = SupplierStatementSourceDocumentType.ShortageResolution,
                SourceDocId = resolution.Id,
                SourceLineId = allocation.Id,
                EffectType = SupplierStatementEffectType.ShortageFinancialResolution,
                Debit = amount,
                Credit = 0m,
                RunningBalance = runningBalance,
                Currency = resolution.Currency,
                Notes = notes,
                CreatedBy = actor
            };

            entries.Add(entry);
        }

        dbContext.SupplierStatementEntries.AddRange(entries);
        return entries;
    }

    public async Task<IReadOnlyList<SupplierStatementEntry>> CreatePaymentEntriesAsync(
        Payment payment,
        string actor,
        CancellationToken cancellationToken)
    {
        if (payment.PartyType != PaymentPartyType.Supplier)
        {
            throw new InvalidOperationException("Only supplier payments can create supplier statement entries.");
        }

        var allocations = payment.Allocations
            .OrderBy(entity => entity.AllocationOrder)
            .ToArray();

        if (allocations.Length == 0)
        {
            return [];
        }

        var allocationIds = allocations.Select(entity => entity.Id).ToArray();
        var duplicateExists = await dbContext.SupplierStatementEntries
            .AnyAsync(
                entity => entity.SourceDocType == SupplierStatementSourceDocumentType.Payment &&
                          entity.SourceDocId == payment.Id &&
                          entity.SourceLineId.HasValue &&
                          allocationIds.Contains(entity.SourceLineId.Value) &&
                          entity.EffectType == SupplierStatementEffectType.Payment,
                cancellationToken);

        if (duplicateExists)
        {
            throw new InvalidOperationException("Supplier statement effects already exist for this payment.");
        }

        var receiptIds = allocations
            .Where(entity => entity.TargetDocType == PaymentTargetDocumentType.PurchaseReceipt)
            .Select(entity => entity.TargetDocId)
            .Distinct()
            .ToArray();

        var resolutionIds = allocations
            .Where(entity => entity.TargetDocType == PaymentTargetDocumentType.ShortageResolution)
            .Select(entity => entity.TargetDocId)
            .Distinct()
            .ToArray();

        var receiptNumbers = receiptIds.Length == 0
            ? new Dictionary<Guid, string>()
            : await dbContext.PurchaseReceipts
                .AsNoTracking()
                .Where(entity => receiptIds.Contains(entity.Id))
                .ToDictionaryAsync(entity => entity.Id, entity => entity.ReceiptNo, cancellationToken);

        var resolutionNumbers = resolutionIds.Length == 0
            ? new Dictionary<Guid, string>()
            : await dbContext.ShortageResolutions
                .AsNoTracking()
                .Where(entity => resolutionIds.Contains(entity.Id))
                .ToDictionaryAsync(entity => entity.Id, entity => entity.ResolutionNo, cancellationToken);

        var runningBalance = await GetLatestRunningBalanceAsync(payment.PartyId, cancellationToken);
        var entries = new List<SupplierStatementEntry>(allocations.Length);

        foreach (var allocation in allocations)
        {
            var amount = Round(allocation.AllocatedAmount);
            var targetDocumentNo = allocation.TargetDocType switch
            {
                PaymentTargetDocumentType.PurchaseReceipt when receiptNumbers.TryGetValue(allocation.TargetDocId, out var receiptNo) => receiptNo,
                PaymentTargetDocumentType.ShortageResolution when resolutionNumbers.TryGetValue(allocation.TargetDocId, out var resolutionNo) => resolutionNo,
                _ => allocation.TargetDocId.ToString()
            };

            decimal debit;
            decimal credit;

            if (payment.Direction == PaymentDirection.OutboundToParty)
            {
                debit = amount;
                credit = 0m;
                runningBalance = Round(runningBalance - amount);
            }
            else
            {
                debit = 0m;
                credit = amount;
                runningBalance = Round(runningBalance + amount);
            }

            entries.Add(new SupplierStatementEntry
            {
                SupplierId = payment.PartyId,
                EntryDate = payment.PaymentDate,
                SourceDocType = SupplierStatementSourceDocumentType.Payment,
                SourceDocId = payment.Id,
                SourceLineId = allocation.Id,
                EffectType = SupplierStatementEffectType.Payment,
                Debit = debit,
                Credit = credit,
                RunningBalance = runningBalance,
                Currency = payment.Currency,
                Notes = $"Payment {payment.PaymentNo} allocated to {targetDocumentNo}",
                CreatedBy = actor
            });
        }

        dbContext.SupplierStatementEntries.AddRange(entries);
        return entries;
    }

    private async Task<decimal> GetLatestRunningBalanceAsync(Guid supplierId, CancellationToken cancellationToken)
    {
        return await dbContext.SupplierStatementEntries
            .Where(entity => entity.SupplierId == supplierId)
            .OrderByDescending(entity => entity.EntryDate)
            .ThenByDescending(entity => entity.CreatedAt)
            .ThenByDescending(entity => entity.Id)
            .Select(entity => entity.RunningBalance)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private static decimal Round(decimal value)
    {
        return decimal.Round(value, 6, MidpointRounding.AwayFromZero);
    }
}
