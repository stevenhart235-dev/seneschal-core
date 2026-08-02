using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Seneschal.Core.Interfaces;
using Seneschal.Core.Repositories;

namespace Seneschal.Persistence.PostgreSql;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddSeneschalPersistence(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var provider = configuration[$"{PersistenceOptions.SectionName}:Provider"]
            ?? "InMemory";
        if (string.Equals(provider, "InMemory", StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<InMemoryAuditEventStore>();
            services.AddSingleton<IAuditEventStore>(sp =>
                sp.GetRequiredService<InMemoryAuditEventStore>());
            services.AddSingleton<IAuditSink>(sp =>
                sp.GetRequiredService<IAuditEventStore>());
            services.AddSingleton<InMemoryApprovalStore>();
            services.AddSingleton<IApprovalStore>(sp =>
                sp.GetRequiredService<InMemoryApprovalStore>());
            services.AddSingleton<IEvaluationCommitCoordinator>(sp =>
                new InMemoryEvaluationCommitCoordinator(
                    sp.GetRequiredService<InMemoryAuditEventStore>(),
                    sp.GetRequiredService<InMemoryApprovalStore>()));
            services.AddSingleton<IInvestigationActivityReader,
                ActivityStoreInvestigationActivityReader>();
            return services;
        }

        if (!string.Equals(provider, "PostgreSql", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Unsupported Seneschal persistence provider '{provider}'.");
        }

        var connectionString = configuration.GetConnectionString("SeneschalPostgreSql");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "PostgreSQL persistence requires ConnectionStrings:SeneschalPostgreSql.");
        }

        services.AddPooledDbContextFactory<PostgreSqlPersistenceDbContext>(options =>
            options.UseNpgsql(connectionString, npgsql => npgsql.MigrationsAssembly(
                typeof(PostgreSqlPersistenceDbContext).Assembly.FullName)));
        services.AddSingleton<PostgreSqlAuditEventStore>();
        services.AddSingleton<IAuditEventStore>(sp =>
            sp.GetRequiredService<PostgreSqlAuditEventStore>());
        services.AddSingleton<IAuditSink>(sp =>
            sp.GetRequiredService<IAuditEventStore>());
        services.AddSingleton<PostgreSqlApprovalStore>();
        services.AddSingleton<IApprovalStore>(sp =>
            sp.GetRequiredService<PostgreSqlApprovalStore>());
        services.AddSingleton<IEvaluationCommitCoordinator,
            PostgreSqlEvaluationCommitCoordinator>();
        services.AddSingleton<IInvestigationActivityReader,
            PostgreSqlInvestigationActivityReader>();
        services.AddSingleton<IGovernanceModeStore, PostgreSqlGovernanceModeStore>();
        services.AddSingleton<IGovernanceWindowStore, PostgreSqlGovernanceWindowStore>();
        services.AddSingleton<IGovernanceIncidentStore, PostgreSqlGovernanceIncidentStore>();
        services.AddSingleton<PostgreSqlStartupValidator>();
        return services;
    }

    public static async Task ValidateSeneschalPersistenceAsync(
        this IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        var validator = services.GetService<PostgreSqlStartupValidator>();
        if (validator is not null)
        {
            await validator.ValidateAsync(cancellationToken);
        }
    }
}
