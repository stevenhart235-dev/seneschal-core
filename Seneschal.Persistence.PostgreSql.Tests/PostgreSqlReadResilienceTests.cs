using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Seneschal.Core.Enums;
using Seneschal.Core.Models;

namespace Seneschal.Persistence.PostgreSql.Tests;

[Collection("PostgreSQL")]
public sealed class PostgreSqlReadResilienceTests(PostgreSqlFixture fixture) :
    IAsyncLifetime
{
    public Task InitializeAsync() => fixture.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task TransientFailure_RetriesCompleteReadWithFreshContext()
    {
        var evidence = Evidence("read-retry");
        await new PostgreSqlAuditEventStore(fixture.CreateFactory())
            .WriteAsync(evidence);
        var factory = new FaultingFactory(
            fixture.CreateFactory(),
            failures: 1,
            () => new NpgsqlException(
                "transient test failure", new IOException("connection reset")));

        var result = await new PostgreSqlAuditEventStore(factory)
            .GetRecentAsync();

        Assert.Equal(evidence.Id, Assert.Single(result).Id);
        Assert.Equal(2, factory.AsyncAttempts);
    }

    [Fact]
    public async Task ExhaustedTransientFailure_ThrowsSafeUnavailableException()
    {
        var factory = new FaultingFactory(
            fixture.CreateFactory(),
            failures: int.MaxValue,
            () => new NpgsqlException(
                "transient test failure", new IOException("connection reset")));

        var exception = await Assert.ThrowsAsync<
            PostgreSqlReadUnavailableException>(() =>
            new PostgreSqlAuditEventStore(factory).GetRecentAsync());

        Assert.Equal("PostgreSQL is temporarily unavailable.",
            exception.Message);
        Assert.Equal(3, factory.AsyncAttempts);
    }

    [Fact]
    public async Task NonTransientFailure_IsNotRetried()
    {
        var factory = new FaultingFactory(
            fixture.CreateFactory(),
            failures: int.MaxValue,
            () => new InvalidOperationException("non-transient test failure"));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new PostgreSqlAuditEventStore(factory).GetRecentAsync());

        Assert.Equal(1, factory.AsyncAttempts);
    }

    [Fact]
    public async Task Cancellation_StopsRetryDelay()
    {
        var factory = new FaultingFactory(
            fixture.CreateFactory(),
            failures: int.MaxValue,
            () => new NpgsqlException(
                "transient test failure", new IOException("connection reset")));
        using var cancellation = new CancellationTokenSource();
        cancellation.CancelAfter(TimeSpan.FromMilliseconds(10));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new PostgreSqlAuditEventStore(factory)
                .GetRecentAsync(cancellationToken: cancellation.Token));

        Assert.Equal(1, factory.AsyncAttempts);
    }

    [Fact]
    public async Task PostgreSqlReplacement_ReadAndReadinessRecover()
    {
        var evidence = Evidence("replacement-recovery");
        var factory = fixture.CreateFactory();
        var store = new PostgreSqlAuditEventStore(factory);
        await store.WriteAsync(evidence);
        Assert.Single(await store.GetRecentAsync());
        var readiness = new PostgreSqlPersistenceReadiness(
            factory,
            NullLogger<PostgreSqlPersistenceReadiness>.Instance);
        Assert.True((await readiness.CheckAsync()).IsReady);

        await fixture.StopAsync();
        try
        {
            var unavailable = await readiness.CheckAsync();
            Assert.False(unavailable.IsReady);
            Assert.False(unavailable.Reachable);
        }
        finally
        {
            await fixture.StartAsync();
        }

        // Testcontainers may assign a new host port when restarting a stopped
        // container. A fresh factory models the stable Service endpoint used
        // by deployed Seneschal while preserving the same database volume.
        var recoveredFactory = fixture.CreateFactory();
        var recoveredReadiness = new PostgreSqlPersistenceReadiness(
            recoveredFactory,
            NullLogger<PostgreSqlPersistenceReadiness>.Instance);
        var recovered = await WaitForReadyAsync(recoveredReadiness);
        Assert.True(recovered.IsReady);
        Assert.Equal(evidence.Id,
            Assert.Single(await new PostgreSqlAuditEventStore(recoveredFactory)
                .GetRecentAsync()).Id);
    }

    [Fact]
    public async Task TerminatedPooledConnection_FirstReadRecovers()
    {
        var evidence = Evidence("terminated-pooled-connection");
        var factory = fixture.CreateFactory();
        var store = new PostgreSqlAuditEventStore(factory);
        await store.WriteAsync(evidence);
        Assert.Single(await store.GetRecentAsync());

        await using (var context = await fixture.CreateFactory()
            .CreateDbContextAsync())
        {
            await context.Database.ExecuteSqlRawAsync(
                "SELECT pg_terminate_backend(pid) FROM pg_stat_activity " +
                "WHERE datname = current_database() AND pid <> pg_backend_pid();");
        }

        Assert.Equal(evidence.Id,
            Assert.Single(await store.GetRecentAsync()).Id);
    }

    [Fact]
    public async Task Readiness_InvalidCredentials_IsNonReady()
    {
        var invalid = new NpgsqlConnectionStringBuilder(fixture.ConnectionString)
        {
            Password = "incorrect-password",
            Timeout = 1
        }.ConnectionString;
        var readiness = new PostgreSqlPersistenceReadiness(
            fixture.CreateFactory(invalid),
            NullLogger<PostgreSqlPersistenceReadiness>.Instance);

        var result = await readiness.CheckAsync();

        Assert.False(result.IsReady);
        Assert.False(result.Reachable);
        Assert.Equal("PostgreSql", result.Provider);
    }

    [Fact]
    public async Task Readiness_PendingMigrations_IsReachableButNonReady()
    {
        var withoutSeneschalSchema =
            new NpgsqlConnectionStringBuilder(fixture.ConnectionString)
            {
                Database = "postgres"
            }.ConnectionString;
        var readiness = new PostgreSqlPersistenceReadiness(
            fixture.CreateFactory(withoutSeneschalSchema),
            NullLogger<PostgreSqlPersistenceReadiness>.Instance);

        var result = await readiness.CheckAsync();

        Assert.False(result.IsReady);
        Assert.True(result.Reachable);
        Assert.False(result.MigrationsCurrent);
    }

    private static async Task<PersistenceReadinessResult> WaitForReadyAsync(
        IPersistenceReadiness readiness)
    {
        for (var attempt = 0; attempt < 20; attempt++)
        {
            var result = await readiness.CheckAsync();
            if (result.IsReady)
            {
                return result;
            }
            await Task.Delay(100);
        }

        return await readiness.CheckAsync();
    }

    private static AuditEvent Evidence(string id) => new()
    {
        Id = id,
        RequestId = id,
        TimestampUtc = DateTimeOffset.UtcNow,
        IdentityId = "resilience-test",
        CapabilityId = "resilience.read",
        Environment = "test",
        ResourceId = "postgresql",
        Decision = DecisionType.Allow,
        PolicyDecision = DecisionType.Allow,
        EnforcementMode = EnforcementMode.LogOnly,
        EffectiveAction = "allow",
        Reason = "Resilience test evidence."
    };

    private sealed class FaultingFactory(
        IDbContextFactory<PostgreSqlPersistenceDbContext> inner,
        int failures,
        Func<Exception> exceptionFactory) :
        IDbContextFactory<PostgreSqlPersistenceDbContext>
    {
        private int _asyncAttempts;

        public int AsyncAttempts => _asyncAttempts;

        public PostgreSqlPersistenceDbContext CreateDbContext() =>
            inner.CreateDbContext();

        public Task<PostgreSqlPersistenceDbContext> CreateDbContextAsync(
            CancellationToken cancellationToken = default)
        {
            var attempt = Interlocked.Increment(ref _asyncAttempts);
            if (attempt <= failures)
            {
                return Task.FromException<PostgreSqlPersistenceDbContext>(
                    exceptionFactory());
            }

            return inner.CreateDbContextAsync(cancellationToken);
        }
    }
}
