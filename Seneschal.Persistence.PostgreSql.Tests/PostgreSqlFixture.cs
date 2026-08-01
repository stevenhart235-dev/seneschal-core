using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Testcontainers.PostgreSql;

namespace Seneschal.Persistence.PostgreSql.Tests;

public sealed class PostgreSqlFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container =
        new PostgreSqlBuilder("postgres:17-alpine")
            .WithDatabase("seneschal_tests")
            .WithUsername("seneschal")
            .WithPassword("test-only-password")
            .Build();

    public string ConnectionString => _container.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        await using var context = CreateFactory().CreateDbContext();
        await context.Database.MigrateAsync();
    }

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    public IDbContextFactory<PostgreSqlPersistenceDbContext> CreateFactory(
        string? connectionString = null)
    {
        var options = new DbContextOptionsBuilder<PostgreSqlPersistenceDbContext>()
            .UseNpgsql(connectionString ?? ConnectionString)
            .Options;
        return new PooledDbContextFactory<PostgreSqlPersistenceDbContext>(options);
    }

    public async Task ResetAsync()
    {
        await using var context = await CreateFactory().CreateDbContextAsync();
        await context.Database.ExecuteSqlRawAsync(
            "TRUNCATE TABLE evaluation_evidence, approvals RESTART IDENTITY;");
    }
}
