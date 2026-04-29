using System.Text.Json;
using ERP.Application.Security;
using ERP.Domain.Identity;
using ERP.Infrastructure.Persistence;

namespace ERP.Infrastructure.Security;

public sealed class AuditLogService(
    AppDbContext dbContext,
    IRequestExecutionContext executionContext) : IAuditLogService
{
    private readonly AppDbContext _dbContext = dbContext;
    private readonly IRequestExecutionContext _executionContext = executionContext;

    public async Task WriteAsync(
        string action,
        string module,
        string resourceType,
        string? resourceId,
        object? beforeData,
        object? afterData,
        Guid? organizationId,
        Guid? userId,
        CancellationToken cancellationToken)
    {
        var resolvedOrganizationId = organizationId ?? _executionContext.OrganizationId;
        var resolvedUserId = userId ?? _executionContext.UserId;

        if (!resolvedOrganizationId.HasValue && module != "auth")
        {
            return;
        }

        _dbContext.AuditLogEntries.Add(new AuditLogEntry
        {
            OrganizationId = resolvedOrganizationId ?? Guid.Empty,
            UserId = resolvedUserId,
            Action = action,
            Module = module,
            ResourceType = resourceType,
            ResourceId = resourceId,
            BeforeData = Serialize(beforeData),
            AfterData = Serialize(afterData),
            IpAddress = _executionContext.IpAddress,
            UserAgent = _executionContext.UserAgent,
            CreatedBy = _executionContext.Actor
        });

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private static string? Serialize(object? value)
    {
        return value is null ? null : JsonSerializer.Serialize(value);
    }
}
