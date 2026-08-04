using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Seneschal.Persistence.PostgreSql;

public sealed record PersistenceReadinessResult(
    string Provider,
    bool Reachable,
    bool MigrationsCurrent)
{
    public bool IsReady => Reachable && MigrationsCurrent;
}

public interface IPersistenceReadiness
{
    Task<PersistenceReadinessResult> CheckAsync(
        CancellationToken cancellationToken = default);
}

internal sealed class InMemoryPersistenceReadiness : IPersistenceReadiness
{
    public Task<PersistenceReadinessResult> CheckAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new PersistenceReadinessResult(
            "InMemory", Reachable: true, MigrationsCurrent: true));
    }
}

internal sealed class PostgreSqlPersistenceReadiness(
    IDbContextFactory<PostgreSqlPersistenceDbContext> contextFactory,
    ILogger<PostgreSqlPersistenceReadiness> logger) : IPersistenceReadiness
{
    public async Task<PersistenceReadinessResult> CheckAsync(
        CancellationToken cancellationToken = default)
    {
        PostgreSqlPersistenceDbContext? context = null;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(2));
        try
        {
            context = await contextFactory.CreateDbContextAsync(timeout.Token);
            context.Database.SetCommandTimeout(TimeSpan.FromSeconds(2));
            var pending = await context.Database.GetPendingMigrationsAsync(
                timeout.Token);
            if (pending.Any())
            {
                return new PersistenceReadinessResult(
                    "PostgreSql", Reachable: true, MigrationsCurrent: false);
            }

            await context.Database
                .SqlQueryRaw<int>("SELECT 1 AS \"Value\"")
                .SingleAsync(timeout.Token);
            return new PersistenceReadinessResult(
                "PostgreSql", Reachable: true, MigrationsCurrent: true);
        }
        catch (OperationCanceledException) when (
            cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            PostgreSqlReadResilience.ClearStalePool(context, exception);
            logger.LogWarning(
                "PostgreSQL readiness check failed ({ExceptionType}).",
                exception.GetType().Name);
            return new PersistenceReadinessResult(
                "PostgreSql", Reachable: false, MigrationsCurrent: false);
        }
        finally
        {
            if (context is not null)
            {
                await context.DisposeAsync();
            }
        }
    }
}
