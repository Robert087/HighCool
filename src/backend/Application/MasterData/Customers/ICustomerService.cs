namespace ERP.Application.MasterData.Customers;

public interface ICustomerService
{
    Task<IReadOnlyList<CustomerListItemDto>> ListAsync(CustomerListQuery query, CancellationToken cancellationToken);

    Task<CustomerDto?> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<CustomerDto> CreateAsync(CreateCustomerRequest request, string actor, CancellationToken cancellationToken);

    Task<CustomerDto?> UpdateAsync(Guid id, UpdateCustomerRequest request, string actor, CancellationToken cancellationToken);

    Task<bool> ActivateAsync(Guid id, string actor, CancellationToken cancellationToken);

    Task<bool> DeactivateAsync(Guid id, string actor, CancellationToken cancellationToken);
}
