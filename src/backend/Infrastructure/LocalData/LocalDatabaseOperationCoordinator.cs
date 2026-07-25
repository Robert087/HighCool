using ERP.Application.LocalData;

namespace ERP.Infrastructure.LocalData;

public sealed class LocalDatabaseOperationCoordinator : ILocalDatabaseOperationCoordinator
{
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private readonly object _sync = new();
    private readonly HashSet<string> _activeBackupIds = new(StringComparer.OrdinalIgnoreCase);
    private readonly AsyncLocal<LocalDatabaseOperationLease?> _currentLease = new();

    public IReadOnlyCollection<string> ActiveBackupIds
    {
        get
        {
            lock (_sync)
            {
                return _activeBackupIds.ToArray();
            }
        }
    }

    public Task<ILocalDatabaseOperationLease?> TryAcquireExclusiveAsync(
        LocalDatabaseOperationKind kind,
        string? backupId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (_currentLease.Value is { IsDisposed: false } current)
        {
            current.AddNestedBackupId(backupId);
            return Task.FromResult<ILocalDatabaseOperationLease?>(new NestedLocalDatabaseOperationLease(this, current, backupId));
        }

        if (!_semaphore.Wait(0))
        {
            return Task.FromResult<ILocalDatabaseOperationLease?>(null);
        }

        var lease = new LocalDatabaseOperationLease(this, kind, backupId);
        _currentLease.Value = lease;
        if (!string.IsNullOrWhiteSpace(backupId))
        {
            lock (_sync)
            {
                _activeBackupIds.Add(backupId);
            }
        }

        return Task.FromResult<ILocalDatabaseOperationLease?>(lease);
    }

    private void Release(LocalDatabaseOperationLease lease)
    {
        if (!string.IsNullOrWhiteSpace(lease.BackupId))
        {
            lock (_sync)
            {
                _activeBackupIds.Remove(lease.BackupId);
            }
        }

        if (ReferenceEquals(_currentLease.Value, lease))
        {
            _currentLease.Value = null;
        }

        _semaphore.Release();
    }

    private void AddActiveBackupId(string? backupId)
    {
        if (string.IsNullOrWhiteSpace(backupId))
        {
            return;
        }

        lock (_sync)
        {
            _activeBackupIds.Add(backupId);
        }
    }

    private void RemoveActiveBackupId(string? backupId)
    {
        if (string.IsNullOrWhiteSpace(backupId))
        {
            return;
        }

        lock (_sync)
        {
            _activeBackupIds.Remove(backupId);
        }
    }

    private sealed class LocalDatabaseOperationLease(
        LocalDatabaseOperationCoordinator coordinator,
        LocalDatabaseOperationKind kind,
        string? backupId) : ILocalDatabaseOperationLease
    {
        private readonly LocalDatabaseOperationCoordinator _coordinator = coordinator;

        public bool IsDisposed { get; private set; }

        public LocalDatabaseOperationKind Kind { get; } = kind;

        public string? BackupId { get; } = backupId;

        public void AddNestedBackupId(string? nestedBackupId)
            => _coordinator.AddActiveBackupId(nestedBackupId);

        public ValueTask DisposeAsync()
        {
            if (!IsDisposed)
            {
                IsDisposed = true;
                _coordinator.Release(this);
            }

            return ValueTask.CompletedTask;
        }
    }

    private sealed class NestedLocalDatabaseOperationLease(
        LocalDatabaseOperationCoordinator coordinator,
        LocalDatabaseOperationCoordinator.LocalDatabaseOperationLease parent,
        string? backupId) : ILocalDatabaseOperationLease
    {
        public LocalDatabaseOperationKind Kind => parent.Kind;

        public string? BackupId { get; } = backupId;

        public ValueTask DisposeAsync()
        {
            coordinator.RemoveActiveBackupId(BackupId);
            return ValueTask.CompletedTask;
        }
    }
}
