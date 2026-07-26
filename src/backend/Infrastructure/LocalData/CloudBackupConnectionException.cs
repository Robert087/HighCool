using ERP.Application.LocalData;

namespace ERP.Infrastructure.LocalData;

public sealed class CloudBackupConnectionException : Exception
{
    public CloudBackupConnectionException(
        CloudBackupConnectionFailureCategory category,
        CloudBackupConnectionTestStage stage,
        string message,
        int? statusCode = null,
        string? providerErrorCode = null,
        bool cleanupSucceeded = true,
        Exception? innerException = null)
        : base(message, innerException)
    {
        Category = category;
        Stage = stage;
        StatusCode = statusCode;
        ProviderErrorCode = providerErrorCode;
        CleanupSucceeded = cleanupSucceeded;
    }

    public CloudBackupConnectionFailureCategory Category { get; }

    public CloudBackupConnectionTestStage Stage { get; }

    public int? StatusCode { get; }

    public string? ProviderErrorCode { get; }

    public bool CleanupSucceeded { get; }
}
