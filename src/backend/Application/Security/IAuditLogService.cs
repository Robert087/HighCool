namespace ERP.Application.Security;

public interface IAuditLogService
{
    Task WriteAsync(
        string action,
        string module,
        string resourceType,
        string? resourceId,
        object? beforeData,
        object? afterData,
        Guid? organizationId,
        Guid? userId,
        CancellationToken cancellationToken);
}
