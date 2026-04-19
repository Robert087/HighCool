namespace ERP.Application.Common.Exceptions;

public sealed class DuplicateEntityException(string message) : Exception(message)
{
}
