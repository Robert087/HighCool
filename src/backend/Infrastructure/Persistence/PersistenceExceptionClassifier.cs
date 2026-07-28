using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Persistence;

public static class PersistenceExceptionClassifier
{
    public static bool IsUniqueConstraintViolation(DbUpdateException exception)
    {
        var inner = exception.InnerException;
        var typeName = inner?.GetType().FullName ?? string.Empty;
        var message = inner?.Message ?? exception.Message;

        return typeName.Contains("SqliteException", StringComparison.OrdinalIgnoreCase) &&
               message.Contains("UNIQUE constraint failed", StringComparison.OrdinalIgnoreCase)
            || typeName.Contains("SqlException", StringComparison.OrdinalIgnoreCase) &&
               (message.Contains("Cannot insert duplicate key", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("duplicate key row", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("unique index", StringComparison.OrdinalIgnoreCase));
    }
}
