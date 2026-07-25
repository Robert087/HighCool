namespace ERP.Application.LocalData;

public enum DatabaseHealthStatus
{
    Healthy,
    Missing,
    Unavailable,
    Corrupt,
    UnsupportedSchema,
    ReadOnly
}

public sealed record DatabaseHealthResult(
    DatabaseHealthStatus Status,
    string Message,
    int? SchemaVersion = null,
    string? ApplicationVersion = null);

public interface IDatabaseHealthService
{
    Task<DatabaseHealthResult> CheckAsync(
        bool requireWritable,
        CancellationToken cancellationToken);
}
