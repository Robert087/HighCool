using ERP.Application.LocalData;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace ERP.Infrastructure.LocalData;

public sealed class InMemoryRestorePreflightOperationStore : IRestorePreflightOperationStore
{
    private readonly ConcurrentDictionary<string, RestorePreflightOperation> _operations = new(StringComparer.Ordinal);

    public InMemoryRestorePreflightOperationStore(IOptions<RestorePreflightOperationOptions> options)
    {
        Lifetime = TimeSpan.FromSeconds(Math.Clamp(options.Value.LifetimeSeconds, 1, 3600));
    }

    public TimeSpan Lifetime { get; }

    public RestorePreflightOperation Create(
        string backupId,
        Guid userId,
        string? installationId,
        string bindingHash)
    {
        RemoveExpired();
        var operation = new RestorePreflightOperation(
            Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant(),
            backupId,
            userId,
            installationId,
            bindingHash,
            DateTime.UtcNow.Add(Lifetime),
            DateTime.UtcNow);

        _operations[operation.OperationId] = operation;
        return operation;
    }

    public RestorePreflightOperationConsumeResult ValidateAvailable(string? operationId)
    {
        if (string.IsNullOrWhiteSpace(operationId))
        {
            return Rejected(RestorePreflightOperationConsumeStatus.Missing, "Restore preflight operation is required.");
        }

        if (!_operations.TryGetValue(operationId, out var operation))
        {
            return Rejected(RestorePreflightOperationConsumeStatus.NotFound, "Restore preflight operation was not found.");
        }

        if (operation.ExpiresAtUtc <= DateTime.UtcNow)
        {
            _operations.TryRemove(operation.OperationId, out _);
            return Rejected(RestorePreflightOperationConsumeStatus.Expired, "Restore preflight operation has expired.");
        }

        return new RestorePreflightOperationConsumeResult(RestorePreflightOperationConsumeStatus.Consumed, "Restore preflight operation is available.");
    }

    public RestorePreflightOperationConsumeResult Consume(
        string? operationId,
        string backupId,
        Guid userId,
        string? installationId,
        string bindingHash)
    {
        if (string.IsNullOrWhiteSpace(operationId))
        {
            return Rejected(RestorePreflightOperationConsumeStatus.Missing, "Restore preflight operation is required.");
        }

        if (!_operations.TryGetValue(operationId, out var operation))
        {
            return Rejected(RestorePreflightOperationConsumeStatus.NotFound, "Restore preflight operation was not found.");
        }

        if (operation.ExpiresAtUtc <= DateTime.UtcNow)
        {
            _operations.TryRemove(operation.OperationId, out _);
            return Rejected(RestorePreflightOperationConsumeStatus.Expired, "Restore preflight operation has expired.");
        }

        if (!string.Equals(operation.BackupId, backupId, StringComparison.Ordinal))
        {
            return Rejected(RestorePreflightOperationConsumeStatus.BackupMismatch, "Restore preflight operation does not match the selected backup.");
        }

        if (operation.UserId != userId)
        {
            return Rejected(RestorePreflightOperationConsumeStatus.UserMismatch, "Restore preflight operation does not belong to the current user.");
        }

        if (!string.Equals(operation.InstallationId, installationId, StringComparison.Ordinal) ||
            !string.Equals(operation.BindingHash, bindingHash, StringComparison.Ordinal))
        {
            return Rejected(RestorePreflightOperationConsumeStatus.BindingMismatch, "Restore preflight operation is no longer valid.");
        }

        return _operations.TryRemove(operation.OperationId, out _)
            ? new RestorePreflightOperationConsumeResult(RestorePreflightOperationConsumeStatus.Consumed, "Restore preflight operation consumed.")
            : Rejected(RestorePreflightOperationConsumeStatus.NotFound, "Restore preflight operation was already used.");
    }

    private static RestorePreflightOperationConsumeResult Rejected(
        RestorePreflightOperationConsumeStatus status,
        string message)
        => new(status, message);

    private void RemoveExpired()
    {
        var utcNow = DateTime.UtcNow;
        foreach (var operation in _operations.Values)
        {
            if (operation.ExpiresAtUtc <= utcNow)
            {
                _operations.TryRemove(operation.OperationId, out _);
            }
        }
    }
}
