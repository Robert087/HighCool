using ERP.Application.Statements;
using ERP.Domain.Inventory;
using ERP.Domain.Payments;
using ERP.Domain.Purchasing;
using ERP.Domain.Reversals;
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
        if (amount <= 0m)
        {
            return [];
        }

        var runningBalance = await GetLatestRunningBalanceAsync(receipt.SupplierId, cancellationToken);
        runningBalance = ApplyRunningBalance(runningBalance, 0m, amount);

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
            Notes = $"Purchase receipt {receipt.ReceiptNo}",
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
                entity => (entity.SourceDocType == SupplierStatementSourceDocumentType.ShortageFinancialResolution ||
                           entity.SourceDocType == SupplierStatementSourceDocumentType.ShortageResolution) &&
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
            if (amount <= 0m)
            {
                throw new InvalidOperationException("Financial shortage resolutions require a positive supplier statement amount.");
            }

            runningBalance = ApplyRunningBalance(runningBalance, amount, 0m);

            var notes = shortage.PurchaseReceipt is null
                ? $"Financial shortage resolution {resolution.ResolutionNo}"
                : $"Financial shortage resolution {resolution.ResolutionNo} for receipt {shortage.PurchaseReceipt.ReceiptNo} component {shortage.ComponentItem?.Code ?? shortage.ComponentItemId.ToString()}";

            var entry = new SupplierStatementEntry
            {
                SupplierId = resolution.SupplierId,
                EntryDate = resolution.ResolutionDate,
                SourceDocType = SupplierStatementSourceDocumentType.ShortageFinancialResolution,
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
            if (amount <= 0m)
            {
                throw new InvalidOperationException("Supplier payments require positive allocated amounts before statement rows can be created.");
            }

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
            }
            else
            {
                debit = 0m;
                credit = amount;
            }

            runningBalance = ApplyRunningBalance(runningBalance, debit, credit);

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

    public async Task<IReadOnlyList<SupplierStatementEntry>> CreatePurchaseReturnEntriesAsync(
        PurchaseReturn purchaseReturn,
        decimal returnAmount,
        string actor,
        CancellationToken cancellationToken)
    {
        var duplicateExists = await dbContext.SupplierStatementEntries
            .AnyAsync(
                entity => entity.SourceDocType == SupplierStatementSourceDocumentType.PurchaseReturn &&
                          entity.SourceDocId == purchaseReturn.Id &&
                          entity.EffectType == SupplierStatementEffectType.PurchaseReturn,
                cancellationToken);

        if (duplicateExists)
        {
            throw new InvalidOperationException("Supplier statement effects already exist for this purchase return.");
        }

        var amount = Round(returnAmount);
        if (amount <= 0m)
        {
            return [];
        }

        var runningBalance = await GetLatestRunningBalanceAsync(purchaseReturn.SupplierId, cancellationToken);
        runningBalance = ApplyRunningBalance(runningBalance, amount, 0m);

        var entry = new SupplierStatementEntry
        {
            SupplierId = purchaseReturn.SupplierId,
            EntryDate = purchaseReturn.ReturnDate,
            SourceDocType = SupplierStatementSourceDocumentType.PurchaseReturn,
            SourceDocId = purchaseReturn.Id,
            SourceLineId = purchaseReturn.ReferenceReceiptId,
            EffectType = SupplierStatementEffectType.PurchaseReturn,
            Debit = amount,
            Credit = 0m,
            RunningBalance = runningBalance,
            Currency = null,
            Notes = $"Purchase return {purchaseReturn.ReturnNo}",
            CreatedBy = actor
        };

        dbContext.SupplierStatementEntries.Add(entry);
        return [entry];
    }

    public async Task<IReadOnlyList<SupplierStatementEntry>> CreatePurchaseReceiptReversalEntriesAsync(
        PurchaseReceipt receipt,
        DocumentReversal reversal,
        string actor,
        CancellationToken cancellationToken)
    {
        var originalEntries = await LoadOriginalEntriesAsync(
            receipt.Id,
            [SupplierStatementSourceDocumentType.PurchaseReceipt],
            SupplierStatementEffectType.PurchaseReceipt,
            cancellationToken);

        if (originalEntries.Count == 0)
        {
            return [];
        }

        var duplicateExists = await dbContext.SupplierStatementEntries
            .AnyAsync(
                entity => entity.SourceDocType == SupplierStatementSourceDocumentType.PurchaseReceiptReversal &&
                          entity.SourceDocId == reversal.Id &&
                          entity.EffectType == SupplierStatementEffectType.PurchaseReceiptReversal,
                cancellationToken);

        if (duplicateExists)
        {
            throw new InvalidOperationException("Supplier statement reversal effects already exist for this purchase receipt.");
        }

        var runningBalance = await GetLatestRunningBalanceAsync(receipt.SupplierId, cancellationToken);
        var entries = new List<SupplierStatementEntry>(originalEntries.Count);

        foreach (var originalEntry in originalEntries)
        {
            var debit = Round(originalEntry.Credit);
            var credit = Round(originalEntry.Debit);
            runningBalance = ApplyRunningBalance(runningBalance, debit, credit);

            entries.Add(new SupplierStatementEntry
            {
                SupplierId = receipt.SupplierId,
                EntryDate = reversal.ReversalDate,
                SourceDocType = SupplierStatementSourceDocumentType.PurchaseReceiptReversal,
                SourceDocId = reversal.Id,
                SourceLineId = originalEntry.SourceLineId ?? receipt.Id,
                EffectType = SupplierStatementEffectType.PurchaseReceiptReversal,
                Debit = debit,
                Credit = credit,
                RunningBalance = runningBalance,
                Currency = originalEntry.Currency,
                Notes = $"Reversal {reversal.ReversalNo} for purchase receipt {receipt.ReceiptNo}",
                CreatedBy = actor
            });
        }

        dbContext.SupplierStatementEntries.AddRange(entries);
        return entries;
    }

    public async Task<IReadOnlyList<SupplierStatementEntry>> CreatePaymentReversalEntriesAsync(
        Payment payment,
        DocumentReversal reversal,
        string actor,
        CancellationToken cancellationToken)
    {
        if (payment.PartyType != PaymentPartyType.Supplier)
        {
            throw new InvalidOperationException("Only supplier payments can create supplier statement reversal entries.");
        }

        var originalEntries = await LoadOriginalEntriesAsync(
            payment.Id,
            [SupplierStatementSourceDocumentType.Payment],
            SupplierStatementEffectType.Payment,
            cancellationToken);

        if (originalEntries.Count == 0)
        {
            return [];
        }

        var allocationIds = originalEntries
            .Where(entity => entity.SourceLineId.HasValue)
            .Select(entity => entity.SourceLineId!.Value)
            .ToArray();
        var duplicateExists = await dbContext.SupplierStatementEntries
            .AnyAsync(
                entity => entity.SourceDocType == SupplierStatementSourceDocumentType.PaymentReversal &&
                          entity.SourceDocId == reversal.Id &&
                          entity.SourceLineId.HasValue &&
                          allocationIds.Contains(entity.SourceLineId.Value) &&
                          entity.EffectType == SupplierStatementEffectType.PaymentReversal,
                cancellationToken);

        if (duplicateExists)
        {
            throw new InvalidOperationException("Supplier statement reversal effects already exist for this payment.");
        }

        var runningBalance = await GetLatestRunningBalanceAsync(payment.PartyId, cancellationToken);
        var entries = new List<SupplierStatementEntry>(originalEntries.Count);

        foreach (var originalEntry in originalEntries)
        {
            var debit = Round(originalEntry.Credit);
            var credit = Round(originalEntry.Debit);
            runningBalance = ApplyRunningBalance(runningBalance, debit, credit);

            entries.Add(new SupplierStatementEntry
            {
                SupplierId = payment.PartyId,
                EntryDate = reversal.ReversalDate,
                SourceDocType = SupplierStatementSourceDocumentType.PaymentReversal,
                SourceDocId = reversal.Id,
                SourceLineId = originalEntry.SourceLineId,
                EffectType = SupplierStatementEffectType.PaymentReversal,
                Debit = debit,
                Credit = credit,
                RunningBalance = runningBalance,
                Currency = originalEntry.Currency ?? payment.Currency,
                Notes = $"Reversal {reversal.ReversalNo} for payment {payment.PaymentNo}",
                CreatedBy = actor
            });
        }

        dbContext.SupplierStatementEntries.AddRange(entries);
        return entries;
    }

    public async Task<IReadOnlyList<SupplierStatementEntry>> CreateShortageResolutionReversalEntriesAsync(
        ShortageResolution resolution,
        DocumentReversal reversal,
        string actor,
        CancellationToken cancellationToken)
    {
        var originalEntries = await LoadOriginalEntriesAsync(
            resolution.Id,
            [SupplierStatementSourceDocumentType.ShortageFinancialResolution, SupplierStatementSourceDocumentType.ShortageResolution],
            SupplierStatementEffectType.ShortageFinancialResolution,
            cancellationToken);

        if (originalEntries.Count == 0)
        {
            return [];
        }

        var allocationIds = originalEntries
            .Where(entity => entity.SourceLineId.HasValue)
            .Select(entity => entity.SourceLineId!.Value)
            .ToArray();
        var duplicateExists = await dbContext.SupplierStatementEntries
            .AnyAsync(
                entity => entity.SourceDocType == SupplierStatementSourceDocumentType.ShortageResolutionReversal &&
                          entity.SourceDocId == reversal.Id &&
                          entity.SourceLineId.HasValue &&
                          allocationIds.Contains(entity.SourceLineId.Value) &&
                          entity.EffectType == SupplierStatementEffectType.ShortageResolutionReversal,
                cancellationToken);

        if (duplicateExists)
        {
            throw new InvalidOperationException("Supplier statement reversal effects already exist for this shortage resolution.");
        }

        var runningBalance = await GetLatestRunningBalanceAsync(resolution.SupplierId, cancellationToken);
        var entries = new List<SupplierStatementEntry>(originalEntries.Count);

        foreach (var originalEntry in originalEntries)
        {
            var debit = Round(originalEntry.Credit);
            var credit = Round(originalEntry.Debit);
            runningBalance = ApplyRunningBalance(runningBalance, debit, credit);

            entries.Add(new SupplierStatementEntry
            {
                SupplierId = resolution.SupplierId,
                EntryDate = reversal.ReversalDate,
                SourceDocType = SupplierStatementSourceDocumentType.ShortageResolutionReversal,
                SourceDocId = reversal.Id,
                SourceLineId = originalEntry.SourceLineId,
                EffectType = SupplierStatementEffectType.ShortageResolutionReversal,
                Debit = debit,
                Credit = credit,
                RunningBalance = runningBalance,
                Currency = originalEntry.Currency ?? resolution.Currency,
                Notes = $"Reversal {reversal.ReversalNo} for shortage resolution {resolution.ResolutionNo}",
                CreatedBy = actor
            });
        }

        dbContext.SupplierStatementEntries.AddRange(entries);
        return entries;
    }

    private async Task<List<SupplierStatementEntry>> LoadOriginalEntriesAsync(
        Guid sourceDocId,
        IReadOnlyCollection<SupplierStatementSourceDocumentType> sourceDocTypes,
        SupplierStatementEffectType effectType,
        CancellationToken cancellationToken)
    {
        return await dbContext.SupplierStatementEntries
            .AsNoTracking()
            .Where(entity =>
                entity.SourceDocId == sourceDocId &&
                sourceDocTypes.Contains(entity.SourceDocType) &&
                entity.EffectType == effectType)
            .OrderBy(entity => entity.EntryDate)
            .ThenBy(entity => entity.CreatedAt)
            .ThenBy(entity => entity.Id)
            .ToListAsync(cancellationToken);
    }

    private static decimal ApplyRunningBalance(decimal currentBalance, decimal debit, decimal credit)
    {
        return Round(currentBalance + credit - debit);
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
