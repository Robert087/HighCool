using System.Data;
using ERP.Application.Common.Exceptions;
using ERP.Application.Common.Numbering;
using ERP.Application.Security;
using ERP.Domain.System;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Common.Numbering;

public sealed class DocumentNumberService(
    AppDbContext dbContext,
    IRequestExecutionContext executionContext) : IDocumentNumberService
{
    private const int MaxAttempts = 8;

    public async Task<string> GenerateAsync(
        DocumentNumberRequest request,
        string actor,
        CancellationToken cancellationToken)
    {
        if (!executionContext.OrganizationId.HasValue)
        {
            throw new InvalidOperationException("Organization access is required before generating document numbers.");
        }

        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            var ownsTransaction = dbContext.Database.CurrentTransaction is null;
            await using var transaction = ownsTransaction
                ? await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken)
                : null;

            try
            {
                var sequence = await dbContext.DocumentNumberSequences
                    .SingleOrDefaultAsync(entity =>
                        entity.OrganizationId == executionContext.OrganizationId.Value &&
                        entity.DocumentType == request.DocumentType,
                        cancellationToken);

                if (sequence is null)
                {
                    sequence = new DocumentNumberSequence
                    {
                        OrganizationId = executionContext.OrganizationId.Value,
                        DocumentType = request.DocumentType,
                        Prefix = request.Prefix,
                        NextValue = Math.Max(1, request.MinimumNextValue),
                        PaddingLength = request.PaddingLength,
                        CreatedBy = actor
                    };
                    dbContext.DocumentNumberSequences.Add(sequence);
                }
                else
                {
                    sequence.NextValue = Math.Max(sequence.NextValue, request.MinimumNextValue);
                    sequence.Prefix = request.Prefix;
                    sequence.PaddingLength = request.PaddingLength;
                    sequence.UpdatedBy = actor;
                }

                var value = sequence.NextValue;
                sequence.NextValue++;
                sequence.Version++;

                await dbContext.SaveChangesAsync(cancellationToken);

                if (transaction is not null)
                {
                    await transaction.CommitAsync(cancellationToken);
                }

                return $"{request.Prefix}{value.ToString($"D{request.PaddingLength}", System.Globalization.CultureInfo.InvariantCulture)}";
            }
            catch (DbUpdateConcurrencyException) when (attempt < MaxAttempts)
            {
                await RollbackIfOwnedAsync(transaction, cancellationToken);
                dbContext.ChangeTracker.Clear();
            }
            catch (DbUpdateException exception) when (PersistenceExceptionClassifier.IsUniqueConstraintViolation(exception) && attempt < MaxAttempts)
            {
                await RollbackIfOwnedAsync(transaction, cancellationToken);
                dbContext.ChangeTracker.Clear();
            }
        }

        throw new ConcurrencyConflictException("Document number generation conflicted with another request. Please retry.");
    }

    private static async Task RollbackIfOwnedAsync(
        Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction? transaction,
        CancellationToken cancellationToken)
    {
        if (transaction is not null)
        {
            await transaction.RollbackAsync(cancellationToken);
        }
    }
}
