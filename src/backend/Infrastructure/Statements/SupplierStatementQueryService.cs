using ERP.Application.Common.Pagination;
using ERP.Application.Statements;
using ERP.Domain.Payments;
using ERP.Domain.Statements;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Statements;

public sealed class SupplierStatementQueryService(AppDbContext dbContext) : ISupplierStatementQueryService
{
    public async Task<PagedResult<SupplierStatementEntryDto>> ListAsync(SupplierStatementQuery query, CancellationToken cancellationToken)
    {
        var pagination = new PaginationRequest(query.Page, query.PageSize);
        var entriesQuery = dbContext.SupplierStatementEntries
            .AsNoTracking()
            .Where(entity => entity.Debit > 0m || entity.Credit > 0m)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            entriesQuery = entriesQuery.Where(entity =>
                entity.Supplier!.Code.Contains(search) ||
                entity.Supplier.Name.Contains(search) ||
                (entity.Notes != null && entity.Notes.Contains(search)));
        }

        if (query.SupplierId.HasValue)
        {
            entriesQuery = entriesQuery.Where(entity => entity.SupplierId == query.SupplierId.Value);
        }

        if (query.EffectType.HasValue)
        {
            entriesQuery = entriesQuery.Where(entity => entity.EffectType == query.EffectType.Value);
        }

        if (query.SourceDocType.HasValue)
        {
            entriesQuery = entriesQuery.Where(entity => entity.SourceDocType == query.SourceDocType.Value);
        }

        if (query.FromDate.HasValue)
        {
            entriesQuery = entriesQuery.Where(entity => entity.EntryDate >= query.FromDate.Value);
        }

        if (query.ToDate.HasValue)
        {
            entriesQuery = entriesQuery.Where(entity => entity.EntryDate <= query.ToDate.Value);
        }

        entriesQuery = ApplySorting(entriesQuery, query);

        var totalCount = await entriesQuery.CountAsync(cancellationToken);
        var entries = await entriesQuery
            .Skip(pagination.Skip)
            .Take(pagination.NormalizedPageSize)
            .Select(entity => new SupplierStatementListProjection(
                entity.Id,
                entity.SupplierId,
                entity.Supplier!.Code,
                entity.Supplier.Name,
                entity.EntryDate,
                entity.SourceDocType,
                entity.SourceDocId,
                entity.SourceLineId,
                entity.EffectType,
                entity.Debit,
                entity.Credit,
                entity.RunningBalance,
                entity.Currency,
                entity.Notes,
                entity.CreatedAt,
                entity.CreatedBy))
            .ToListAsync(cancellationToken);

        var purchaseReceiptIds = entries
            .Where(entity => entity.SourceDocType == SupplierStatementSourceDocumentType.PurchaseReceipt)
            .Select(entity => entity.SourceDocId)
            .Distinct()
            .ToArray();

        var shortageResolutionIds = entries
            .Where(entity =>
                entity.SourceDocType == SupplierStatementSourceDocumentType.ShortageFinancialResolution ||
                entity.SourceDocType == SupplierStatementSourceDocumentType.ShortageResolution)
            .Select(entity => entity.SourceDocId)
            .Distinct()
            .ToArray();

        var paymentIds = entries
            .Where(entity => entity.SourceDocType == SupplierStatementSourceDocumentType.Payment)
            .Select(entity => entity.SourceDocId)
            .Distinct()
            .ToArray();

        var purchaseReturnIds = entries
            .Where(entity => entity.SourceDocType == SupplierStatementSourceDocumentType.PurchaseReturn)
            .Select(entity => entity.SourceDocId)
            .Distinct()
            .ToArray();

        var reversalIds = entries
            .Where(entity =>
                entity.SourceDocType == SupplierStatementSourceDocumentType.PurchaseReceiptReversal ||
                entity.SourceDocType == SupplierStatementSourceDocumentType.PaymentReversal ||
                entity.SourceDocType == SupplierStatementSourceDocumentType.ShortageResolutionReversal ||
                entity.SourceDocType == SupplierStatementSourceDocumentType.DocumentReversal)
            .Select(entity => entity.SourceDocId)
            .Distinct()
            .ToArray();

        var receiptNumbers = purchaseReceiptIds.Length == 0
            ? new Dictionary<Guid, string>()
            : await dbContext.PurchaseReceipts
                .AsNoTracking()
                .Where(entity => purchaseReceiptIds.Contains(entity.Id))
                .ToDictionaryAsync(entity => entity.Id, entity => entity.ReceiptNo, cancellationToken);

        var resolutionNumbers = shortageResolutionIds.Length == 0
            ? new Dictionary<Guid, string>()
            : await dbContext.ShortageResolutions
                .AsNoTracking()
                .Where(entity => shortageResolutionIds.Contains(entity.Id))
                .ToDictionaryAsync(entity => entity.Id, entity => entity.ResolutionNo, cancellationToken);

        var paymentNumbers = paymentIds.Length == 0
            ? new Dictionary<Guid, string>()
            : await dbContext.Payments
                .AsNoTracking()
                .Where(entity => paymentIds.Contains(entity.Id))
                .ToDictionaryAsync(entity => entity.Id, entity => entity.PaymentNo, cancellationToken);

        var purchaseReturnNumbers = purchaseReturnIds.Length == 0
            ? new Dictionary<Guid, string>()
            : await dbContext.PurchaseReturns
                .AsNoTracking()
                .Where(entity => purchaseReturnIds.Contains(entity.Id))
                .ToDictionaryAsync(entity => entity.Id, entity => entity.ReturnNo, cancellationToken);

        var reversalNumbers = reversalIds.Length == 0
            ? new Dictionary<Guid, string>()
            : await dbContext.DocumentReversals
                .AsNoTracking()
                .Where(entity => reversalIds.Contains(entity.Id))
                .ToDictionaryAsync(entity => entity.Id, entity => entity.ReversalNo, cancellationToken);

        var allocationIds = entries
            .Where(entity =>
                entity.SourceLineId.HasValue &&
                (entity.SourceDocType == SupplierStatementSourceDocumentType.ShortageFinancialResolution ||
                 entity.SourceDocType == SupplierStatementSourceDocumentType.ShortageResolution ||
                 entity.SourceDocType == SupplierStatementSourceDocumentType.ShortageResolutionReversal))
            .Select(entity => entity.SourceLineId!.Value)
            .Distinct()
            .ToArray();

        var allocationSequences = allocationIds.Length == 0
            ? new Dictionary<Guid, int>()
            : await dbContext.ShortageResolutionAllocations
                .AsNoTracking()
                .Where(entity => allocationIds.Contains(entity.Id))
                .ToDictionaryAsync(entity => entity.Id, entity => entity.SequenceNo, cancellationToken);

        var paymentAllocationIds = entries
            .Where(entity =>
                entity.SourceLineId.HasValue &&
                (entity.SourceDocType == SupplierStatementSourceDocumentType.Payment ||
                 entity.SourceDocType == SupplierStatementSourceDocumentType.PaymentReversal))
            .Select(entity => entity.SourceLineId!.Value)
            .Distinct()
            .ToArray();

        var paymentAllocationSequences = paymentAllocationIds.Length == 0
            ? new Dictionary<Guid, int>()
            : await dbContext.PaymentAllocations
                .AsNoTracking()
                .Where(entity => paymentAllocationIds.Contains(entity.Id))
                .ToDictionaryAsync(entity => entity.Id, entity => entity.AllocationOrder, cancellationToken);

        var items = entries.Select(entity => new SupplierStatementEntryDto(
                entity.Id,
                entity.SupplierId,
                entity.SupplierCode,
                entity.SupplierName,
                entity.EntryDate,
                entity.SourceDocType,
                entity.SourceDocId,
                entity.SourceLineId,
                ResolveSourceSequenceNo(entity, allocationSequences, paymentAllocationSequences),
                ResolveSourceDocumentNo(entity.SourceDocType, entity.SourceDocId, receiptNumbers, resolutionNumbers, paymentNumbers, purchaseReturnNumbers, reversalNumbers),
                entity.EffectType,
                entity.Debit,
                entity.Credit,
                entity.RunningBalance,
                entity.Currency,
                entity.Notes,
                entity.CreatedAt,
                entity.CreatedBy))
            .ToArray();

        return new PagedResult<SupplierStatementEntryDto>(
            items,
            pagination.NormalizedPage,
            pagination.NormalizedPageSize,
            totalCount,
            CalculateTotalPages(totalCount, pagination.NormalizedPageSize),
            new
            {
                query.Search,
                query.SupplierId,
                query.EffectType,
                query.SourceDocType,
                query.FromDate,
                query.ToDate
            },
            new PagedSort(ResolveSortBy(query.SortBy), query.SortDirection));
    }

    private static string ResolveSourceDocumentNo(
        SupplierStatementSourceDocumentType sourceDocType,
        Guid sourceDocId,
        IReadOnlyDictionary<Guid, string> receiptNumbers,
        IReadOnlyDictionary<Guid, string> resolutionNumbers,
        IReadOnlyDictionary<Guid, string> paymentNumbers,
        IReadOnlyDictionary<Guid, string> purchaseReturnNumbers,
        IReadOnlyDictionary<Guid, string> reversalNumbers)
    {
        return sourceDocType switch
        {
            SupplierStatementSourceDocumentType.PurchaseReceipt when receiptNumbers.TryGetValue(sourceDocId, out var receiptNo) => receiptNo,
            SupplierStatementSourceDocumentType.ShortageFinancialResolution when resolutionNumbers.TryGetValue(sourceDocId, out var resolutionNo) => resolutionNo,
            SupplierStatementSourceDocumentType.ShortageResolution when resolutionNumbers.TryGetValue(sourceDocId, out var legacyResolutionNo) => legacyResolutionNo,
            SupplierStatementSourceDocumentType.Payment when paymentNumbers.TryGetValue(sourceDocId, out var paymentNo) => paymentNo,
            SupplierStatementSourceDocumentType.PurchaseReturn when purchaseReturnNumbers.TryGetValue(sourceDocId, out var returnNo) => returnNo,
            SupplierStatementSourceDocumentType.PurchaseReceiptReversal when reversalNumbers.TryGetValue(sourceDocId, out var receiptReversalNo) => receiptReversalNo,
            SupplierStatementSourceDocumentType.PaymentReversal when reversalNumbers.TryGetValue(sourceDocId, out var paymentReversalNo) => paymentReversalNo,
            SupplierStatementSourceDocumentType.ShortageResolutionReversal when reversalNumbers.TryGetValue(sourceDocId, out var shortageReversalNo) => shortageReversalNo,
            SupplierStatementSourceDocumentType.DocumentReversal when reversalNumbers.TryGetValue(sourceDocId, out var reversalNo) => reversalNo,
            _ => sourceDocId.ToString()
        };
    }

    private static int? ResolveSourceSequenceNo(
        SupplierStatementListProjection entity,
        IReadOnlyDictionary<Guid, int> shortageAllocationSequences,
        IReadOnlyDictionary<Guid, int> paymentAllocationSequences)
    {
        if (!entity.SourceLineId.HasValue)
        {
            return null;
        }

        return entity.SourceDocType switch
        {
            SupplierStatementSourceDocumentType.ShortageFinancialResolution when shortageAllocationSequences.TryGetValue(entity.SourceLineId.Value, out var shortageSequenceNo) => shortageSequenceNo,
            SupplierStatementSourceDocumentType.ShortageResolution when shortageAllocationSequences.TryGetValue(entity.SourceLineId.Value, out var legacyShortageSequenceNo) => legacyShortageSequenceNo,
            SupplierStatementSourceDocumentType.ShortageResolutionReversal when shortageAllocationSequences.TryGetValue(entity.SourceLineId.Value, out var shortageReversalSequenceNo) => shortageReversalSequenceNo,
            SupplierStatementSourceDocumentType.Payment when paymentAllocationSequences.TryGetValue(entity.SourceLineId.Value, out var paymentSequenceNo) => paymentSequenceNo,
            SupplierStatementSourceDocumentType.PaymentReversal when paymentAllocationSequences.TryGetValue(entity.SourceLineId.Value, out var paymentReversalSequenceNo) => paymentReversalSequenceNo,
            _ => null
        };
    }

    private static IQueryable<SupplierStatementEntry> ApplySorting(IQueryable<SupplierStatementEntry> query, SupplierStatementQuery request)
    {
        var sortBy = ResolveSortBy(request.SortBy);
        var ascending = request.SortDirection == SortDirection.Asc;

        return (sortBy, ascending) switch
        {
            ("supplierName", true) => query.OrderBy(entity => entity.Supplier!.Name).ThenBy(entity => entity.EntryDate),
            ("supplierName", false) => query.OrderByDescending(entity => entity.Supplier!.Name).ThenByDescending(entity => entity.EntryDate),
            ("sourceDocType", true) => query.OrderBy(entity => entity.SourceDocType).ThenBy(entity => entity.EntryDate),
            ("sourceDocType", false) => query.OrderByDescending(entity => entity.SourceDocType).ThenByDescending(entity => entity.EntryDate),
            ("runningBalance", true) => query.OrderBy(entity => entity.RunningBalance).ThenBy(entity => entity.EntryDate),
            ("runningBalance", false) => query.OrderByDescending(entity => entity.RunningBalance).ThenByDescending(entity => entity.EntryDate),
            _ when ascending => query.OrderBy(entity => entity.EntryDate).ThenBy(entity => entity.CreatedAt).ThenBy(entity => entity.Id),
            _ => query.OrderByDescending(entity => entity.EntryDate).ThenByDescending(entity => entity.CreatedAt).ThenByDescending(entity => entity.Id)
        };
    }

    private static string ResolveSortBy(string? sortBy)
    {
        return string.IsNullOrWhiteSpace(sortBy) ? "entryDate" : sortBy.Trim();
    }

    private static int CalculateTotalPages(int totalCount, int pageSize)
    {
        return totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize);
    }

    private sealed record SupplierStatementListProjection(
        Guid Id,
        Guid SupplierId,
        string SupplierCode,
        string SupplierName,
        DateTime EntryDate,
        SupplierStatementSourceDocumentType SourceDocType,
        Guid SourceDocId,
        Guid? SourceLineId,
        SupplierStatementEffectType EffectType,
        decimal Debit,
        decimal Credit,
        decimal RunningBalance,
        string? Currency,
        string? Notes,
        DateTime CreatedAt,
        string CreatedBy);
}
