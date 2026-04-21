using ERP.Application.Common.Exceptions;
using ERP.Application.MasterData.Customers;
using ERP.Domain.MasterData;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.MasterData.Customers;

public sealed class CustomerService(AppDbContext dbContext) : ICustomerService
{
    public async Task<IReadOnlyList<CustomerListItemDto>> ListAsync(
        CustomerListQuery query,
        CancellationToken cancellationToken)
    {
        var customers = dbContext.Customers.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            customers = customers.Where(entity =>
                entity.Code.Contains(search) ||
                entity.Name.Contains(search) ||
                (entity.Phone != null && entity.Phone.Contains(search)));
        }

        if (query.IsActive.HasValue)
        {
            customers = customers.Where(entity => entity.IsActive == query.IsActive.Value);
        }

        return await customers
            .OrderBy(entity => entity.Name)
            .ThenBy(entity => entity.Code)
            .Select(entity => ToListItemDto(entity))
            .ToListAsync(cancellationToken);
    }

    public Task<CustomerDto?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        return dbContext.Customers
            .AsNoTracking()
            .Where(entity => entity.Id == id)
            .Select(entity => ToDto(entity))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<CustomerDto> CreateAsync(
        CreateCustomerRequest request,
        string actor,
        CancellationToken cancellationToken)
    {
        await EnsureCodeIsUniqueAsync(request.Code, null, cancellationToken);

        var customer = new Customer
        {
            Code = request.Code.Trim(),
            Name = request.Name.Trim(),
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

        dbContext.Customers.Add(customer);
        await dbContext.SaveChangesAsync(cancellationToken);

        return ToDto(customer);
    }

    public async Task<CustomerDto?> UpdateAsync(
        Guid id,
        UpdateCustomerRequest request,
        string actor,
        CancellationToken cancellationToken)
    {
        var customer = await dbContext.Customers.SingleOrDefaultAsync(entity => entity.Id == id, cancellationToken);

        if (customer is null)
        {
            return null;
        }

        await EnsureCodeIsUniqueAsync(request.Code, id, cancellationToken);

        customer.Code = request.Code.Trim();
        customer.Name = request.Name.Trim();
        customer.Phone = NormalizeOptional(request.Phone);
        customer.Email = NormalizeOptional(request.Email);
        customer.TaxNumber = NormalizeOptional(request.TaxNumber);
        customer.Address = NormalizeOptional(request.Address);
        customer.City = NormalizeOptional(request.City);
        customer.Area = NormalizeOptional(request.Area);
        customer.CreditLimit = request.CreditLimit;
        customer.PaymentTerms = NormalizeOptional(request.PaymentTerms);
        customer.Notes = NormalizeOptional(request.Notes);
        customer.IsActive = request.IsActive;
        customer.UpdatedBy = actor;

        await dbContext.SaveChangesAsync(cancellationToken);

        return ToDto(customer);
    }

    public async Task<bool> ActivateAsync(Guid id, string actor, CancellationToken cancellationToken)
    {
        var customer = await dbContext.Customers.SingleOrDefaultAsync(entity => entity.Id == id, cancellationToken);

        if (customer is null)
        {
            return false;
        }

        if (customer.IsActive)
        {
            return true;
        }

        customer.IsActive = true;
        customer.UpdatedBy = actor;
        await dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<bool> DeactivateAsync(Guid id, string actor, CancellationToken cancellationToken)
    {
        var customer = await dbContext.Customers.SingleOrDefaultAsync(entity => entity.Id == id, cancellationToken);

        if (customer is null)
        {
            return false;
        }

        if (!customer.IsActive)
        {
            return true;
        }

        customer.IsActive = false;
        customer.UpdatedBy = actor;
        await dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }

    private async Task EnsureCodeIsUniqueAsync(string code, Guid? currentId, CancellationToken cancellationToken)
    {
        var normalizedCode = code.Trim();

        var exists = await dbContext.Customers.AnyAsync(
            entity => entity.Code == normalizedCode && entity.Id != currentId,
            cancellationToken);

        if (exists)
        {
            throw new DuplicateEntityException($"Customer code '{normalizedCode}' already exists.");
        }
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static CustomerDto ToDto(Customer entity)
    {
        return new CustomerDto(
            entity.Id,
            entity.Code,
            entity.Name,
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

    private static CustomerListItemDto ToListItemDto(Customer entity)
    {
        return new CustomerListItemDto(
            entity.Id,
            entity.Code,
            entity.Name,
            entity.Phone,
            entity.Email,
            entity.City,
            entity.Area,
            entity.CreditLimit,
            entity.PaymentTerms,
            entity.IsActive,
            entity.CreatedAt,
            entity.UpdatedAt);
    }
}
