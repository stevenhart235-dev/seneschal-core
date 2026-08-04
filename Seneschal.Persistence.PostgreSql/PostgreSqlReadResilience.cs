using Microsoft.EntityFrameworkCore;
using Npgsql;
using System.Net.Sockets;

namespace Seneschal.Persistence.PostgreSql;

public sealed class PostgreSqlReadUnavailableException : Exception
{
    public PostgreSqlReadUnavailableException(Exception innerException)
        : base("PostgreSQL is temporarily unavailable.", innerException)
    {
    }
}

internal static class PostgreSqlReadResilience
{
    internal const int MaxRetryCount = 2;
    internal static readonly TimeSpan[] RetryDelays =
    [
        TimeSpan.FromMilliseconds(50),
        TimeSpan.FromMilliseconds(150)
    ];

    public static async Task<T> ExecuteAsync<T>(
        this IDbContextFactory<PostgreSqlPersistenceDbContext> contextFactory,
        Func<PostgreSqlPersistenceDbContext, CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(contextFactory);
        ArgumentNullException.ThrowIfNull(operation);

        for (var attempt = 0; ; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            PostgreSqlPersistenceDbContext? context = null;
            try
            {
                context = await contextFactory.CreateDbContextAsync(
                    cancellationToken);
                return await operation(context, cancellationToken);
            }
            catch (Exception exception) when (
                IsTransient(exception, cancellationToken))
            {
                ClearStalePool(context, exception);
                if (attempt >= MaxRetryCount)
                {
                    throw new PostgreSqlReadUnavailableException(exception);
                }

                await Task.Delay(RetryDelays[attempt], cancellationToken);
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

    public static T Execute<T>(
        this IDbContextFactory<PostgreSqlPersistenceDbContext> contextFactory,
        Func<PostgreSqlPersistenceDbContext, T> operation)
    {
        ArgumentNullException.ThrowIfNull(contextFactory);
        ArgumentNullException.ThrowIfNull(operation);

        for (var attempt = 0; ; attempt++)
        {
            PostgreSqlPersistenceDbContext? context = null;
            try
            {
                context = contextFactory.CreateDbContext();
                return operation(context);
            }
            catch (Exception exception) when (IsTransient(exception))
            {
                ClearStalePool(context, exception);
                if (attempt >= MaxRetryCount)
                {
                    throw new PostgreSqlReadUnavailableException(exception);
                }

                Thread.Sleep(RetryDelays[attempt]);
            }
            finally
            {
                context?.Dispose();
            }
        }
    }

    internal static bool IsTransient(
        Exception exception,
        CancellationToken cancellationToken = default)
    {
        if (exception is OperationCanceledException &&
            cancellationToken.IsCancellationRequested)
        {
            return false;
        }

        if (exception is TimeoutException)
        {
            return true;
        }

        if (exception is NpgsqlException npgsqlException)
        {
            return npgsqlException.IsTransient;
        }

        return exception.InnerException is not null &&
            IsTransient(exception.InnerException, cancellationToken);
    }

    internal static void ClearStalePool(
        PostgreSqlPersistenceDbContext? context,
        Exception exception)
    {
        if (context?.Database.GetDbConnection() is NpgsqlConnection connection &&
            IsStaleConnectionFailure(exception))
        {
            NpgsqlConnection.ClearPool(connection);
        }
    }

    private static bool IsStaleConnectionFailure(Exception exception)
    {
        if (exception is PostgresException
            { SqlState: PostgresErrorCodes.AdminShutdown })
        {
            return true;
        }

        if (exception is IOException or SocketException)
        {
            return true;
        }

        return exception.InnerException is not null &&
            IsStaleConnectionFailure(exception.InnerException);
    }
}
