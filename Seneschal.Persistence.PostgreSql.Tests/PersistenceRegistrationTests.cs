using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Seneschal.Core.Interfaces;
using Seneschal.Core.Repositories;

namespace Seneschal.Persistence.PostgreSql.Tests;

public sealed class PersistenceRegistrationTests
{
    [Fact]
    public async Task DefaultProvider_IsInMemory()
    {
        using var provider = new ServiceCollection()
            .AddSingleton<IActivityStore, InMemoryActivityStore>()
            .AddSeneschalPersistence(new ConfigurationBuilder().Build())
            .BuildServiceProvider();

        Assert.IsType<InMemoryAuditEventStore>(
            provider.GetRequiredService<IAuditEventStore>());
        Assert.IsType<InMemoryEvaluationCommitCoordinator>(
            provider.GetRequiredService<IEvaluationCommitCoordinator>());
        Assert.IsType<ActivityStoreInvestigationActivityReader>(
            provider.GetRequiredService<IInvestigationActivityReader>());
        Assert.Equal("InMemory",
            (await provider.GetRequiredService<IPersistenceReadiness>()
                .CheckAsync()).Provider);
    }

    [Fact]
    public void PostgreSqlProvider_RegistersOnlyWhenSelected()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Seneschal:Persistence:Provider"] = "PostgreSql",
                ["ConnectionStrings:SeneschalPostgreSql"] =
                    "Host=localhost;Database=seneschal;Username=user;Password=placeholder"
            })
            .Build();
        using var provider = new ServiceCollection()
            .AddLogging()
            .AddSingleton<IActivityStore, InMemoryActivityStore>()
            .AddSeneschalPersistence(configuration)
            .BuildServiceProvider();

        Assert.IsType<PostgreSqlAuditEventStore>(
            provider.GetRequiredService<IAuditEventStore>());
        Assert.IsType<PostgreSqlEvaluationCommitCoordinator>(
            provider.GetRequiredService<IEvaluationCommitCoordinator>());
        Assert.IsType<PostgreSqlInvestigationActivityReader>(
            provider.GetRequiredService<IInvestigationActivityReader>());
        Assert.IsType<PostgreSqlPersistenceReadiness>(
            provider.GetRequiredService<IPersistenceReadiness>());
    }

    [Fact]
    public void PostgreSqlProvider_RequiresConnectionString()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Seneschal:Persistence:Provider"] = "PostgreSql"
            })
            .Build();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            new ServiceCollection().AddSeneschalPersistence(configuration));

        Assert.Contains("SeneschalPostgreSql", exception.Message);
    }
}
