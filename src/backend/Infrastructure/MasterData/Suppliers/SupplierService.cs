using ERP.Application.Common.Exceptions;
using ERP.Application.MasterData.Suppliers;
using ERP.Domain.MasterData;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.MasterData.Suppliers;

public sealed class SupplierService(AppDbContext dbContext) : ISupplierService
{
    public async Task<IReadOnlyList<SupplierDto>> ListAsync(
        SupplierListQuery query,
        CancellationToken cancellationToken)
    {
        var suppliers = dbContext.Suppliers.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            suppliers = suppliers.Where(entity =>
                entity.Code.Contains(search) ||
                entity.Name.Contains(search) ||
                entity.StatementName.Contains(search) ||
                (entity.Phone != null && entity.Phone.Contains(search)) ||
                (entity.Email != null && entity.Email.Contains(search)));
        }

        if (query.IsActive.HasValue)
        {
            suppliers = suppliers.Where(entity => entity.IsActive == query.IsActive.Value);
        }

        return await suppliers
            .OrderBy(entity => entity.Name)
            .ThenBy(entity => entity.Code)
            .Select(entity => ToDto(entity))
            .ToListAsync(cancellationToken);
    }

    public Task<SupplierDto?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        return dbContext.Suppliers
            .AsNoTracking()
            .Where(entity => entity.Id == id)
            .Select(entity => ToDto(entity))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<SupplierDto> CreateAsync(
        UpsertSupplierRequest request,
        string actor,
        CancellationToken cancellationToken)
    {
        await EnsureCodeIsUniqueAsync(request.Code, null, cancellationToken);

        var supplier = new Supplier
        {
            Code = request.Code.Trim(),
            Name = request.Name.Trim(),
            StatementName = request.StatementName.Trim(),
            Phone = NormalizeOptional(request.Phone),
            Email = NormalizeOptional(request.Email),
            TaxNumber = NormalizeOptional(request.TaxNumber),
            Address = NormalizeOptional(request.Address),
            City = NormalizeOptional(request.City),
            Area = NormalizeOptional(request.Area),
            CreditLimit = request.CreditLimit,
            PaymentTerms = NormalizeOptional(request.PaymentTerms),
            Notes = NormalizeOptional(request.Notes),
            IsActive = request.IsActive,
            CreatedBy = actor
        };

        dbContext.Suppliers.Add(supplier);
        await dbContext.SaveChangesAsync(cancellationToken);

        return ToDto(supplier);
    }

    public async Task<SupplierDto?> UpdateAsync(
        Guid id,
        UpsertSupplierRequest request,
        string actor,
        CancellationToken cancellationToken)
    {
        var supplier = await dbContext.Suppliers.SingleOrDefaultAsync(entity => entity.Id == id, cancellationToken);

        if (supplier is null)
        {
            return null;
        }

        await EnsureCodeIsUniqueAsync(request.Code, id, cancellationToken);

        supplier.Code = request.Code.Trim();
        supplier.Name = request.Name.Trim();
        supplier.StatementName = request.StatementName.Trim();
        supplier.Phone = NormalizeOptional(request.Phone);
        supplier.Email = NormalizeOptional(request.Email);
        supplier.TaxNumber = NormalizeOptional(request.TaxNumber);
        supplier.Address = NormalizeOptional(request.Address);
        supplier.City = NormalizeOptional(request.City);
        supplier.Area = NormalizeOptional(request.Area);
        supplier.CreditLimit = request.CreditLimit;
        supplier.PaymentTerms = NormalizeOptional(request.PaymentTerms);
        supplier.Notes = NormalizeOptional(request.Notes);
        supplier.IsActive = request.IsActive;
        supplier.UpdatedBy = actor;

        await dbContext.SaveChangesAsync(cancellationToken);

        return ToDto(supplier);
    }

    public async Task<bool> DeactivateAsync(Guid id, string actor, CancellationToken cancellationToken)
    {
        var supplier = await dbContext.Suppliers.SingleOrDefaultAsync(entity => entity.Id == id, cancellationToken);

        if (supplier is null)
        {
            return false;
        }

        if (!supplier.IsActive)
        {
            return true;
        }

        supplier.IsActive = false;
        supplier.UpdatedBy = actor;
        await dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }

    private async Task EnsureCodeIsUniqueAsync(string code, Guid? currentId, CancellationToken cancellationToken)
    {
        var normalizedCode = code.Trim();

        var exists = await dbContext.Suppliers.AnyAsync(
            entity => entity.Code == normalizedCode && entity.Id != currentId,
            cancellationToken);

        if (exists)
        {
            throw new DuplicateEntityException($"Supplier code '{normalizedCode}' already exists.");
        }
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static SupplierDto ToDto(Supplier entity)
    {
        return new SupplierDto(
            entity.Id,
            entity.Code,
            entity.Name,
            entity.StatementName,
            entity.Phone,
            entity.Email,
            entity.TaxNumber,
            entity.Address,
            entity.City,
            entity.Area,
            entity.CreditLimit,
            entity.PaymentTerms,
            entity.Notes,
            entity.IsActive,
            entity.CreatedAt,
            entity.UpdatedAt);
    }
}
