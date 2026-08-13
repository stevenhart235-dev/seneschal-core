using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using System.Reflection;
using Npgsql;
using Seneschal.Core.Enums;
using Seneschal.Core.Models;

namespace Seneschal.Persistence.PostgreSql.Tests;

#pragma warning disable EF1002 // Test-only schema name is generated as lowercase hexadecimal.

[Collection("PostgreSQL")]
public sealed class PostgreSqlMigrationStrategyTests(PostgreSqlFixture fixture)
{
    [Fact]
    public async Task IncrementalUpgrade_PreservesDataThroughCurrentHead()
    {
        var schema = "upgrade_" + Guid.NewGuid().ToString("N");
        await using var administration = await fixture.CreateFactory()
            .CreateDbContextAsync();
        await administration.Database.ExecuteSqlRawAsync(
            $"CREATE SCHEMA \"{schema}\"");

        try
        {
            var connection = new NpgsqlConnectionStringBuilder(
                fixture.ConnectionString)
            {
                SearchPath = schema
            }.ConnectionString;
            var factory = fixture.CreateFactory(connection);
            await using var context = await factory.CreateDbContextAsync();
            var migrations = context.GetService<IMigrationsAssembly>()
                .Migrations.ToList();
            Assert.Equal(5, migrations.Count);
            await context.Database.ExecuteSqlRawAsync(context
                .GetService<IHistoryRepository>().GetCreateScript());

            await ApplyMigrationAsync(context, migrations[0]);
            var evidence = CreateEvidence("upgrade-evidence");
            await new PostgreSqlAuditEventStore(factory).WriteAsync(evidence);

            await ApplyMigrationAsync(context, migrations[1]);
            var approvals = new PostgreSqlApprovalStore(factory);
            var approval = approvals.GetOrCreate(
                "upgrade-worker", "upgrade.deploy", "production", "service-a",
                "Upgrade approval.", evidence.TimestampUtc, "upgrade-operation")
                .Record;

            await ApplyMigrationAsync(context, migrations[2]);
            await context.Database.ExecuteSqlRawAsync(
                "INSERT INTO runtime_governance_state (id, mode, version) VALUES (1, 1, 3)");
            await context.Database.ExecuteSqlRawAsync(
                "INSERT INTO governance_window_state (id, enabled, mode, version) VALUES (1, TRUE, 1, 4)");

            var pending = await Assert.ThrowsAsync<InvalidOperationException>(
                () => new PostgreSqlStartupValidator(factory).ValidateAsync());
            Assert.Contains("pending migrations", pending.Message,
                StringComparison.OrdinalIgnoreCase);

            await ApplyMigrationAsync(context, migrations[3]);
            await context.Database.ExecuteSqlRawAsync(
                "INSERT INTO incident_operator_state (incident_id, status, version, updated_at) VALUES ('incident-upgrade', 1, 1, TIMESTAMPTZ '2026-08-02 12:00:00Z')");

            await ApplyMigrationAsync(context, migrations[4]);

            await new PostgreSqlStartupValidator(factory).ValidateAsync();

            Assert.Equivalent(evidence,
                await new PostgreSqlAuditEventStore(factory)
                    .GetByIdAsync(evidence.Id), strict: true);
            Assert.Equal(approval, approvals.GetById(approval.Id));
            Assert.Equal(EnforcementMode.Enforce,
                new PostgreSqlGovernanceModeStore(factory).GetState().Mode);
            var window = new PostgreSqlGovernanceWindowStore(factory).GetWindow();
            Assert.True(window.Enabled);
            Assert.Equal(GovernanceWindowMode.Enforce, window.Mode);
            await using var verification = await factory.CreateDbContextAsync();
            var incidentStatus = await verification.Database
                .SqlQueryRaw<int>(
                    "SELECT status AS \"Value\" FROM incident_operator_state")
                .SingleAsync();
            Assert.Equal((int)GovernanceIncidentStatus.Acknowledged,
                incidentStatus);
            Assert.Empty(await verification.Database.GetPendingMigrationsAsync());
            Assert.Equal(migrations.Select(migration => migration.Key),
                await verification.Database.GetAppliedMigrationsAsync());
        }
        finally
        {
            await administration.Database.ExecuteSqlRawAsync(
                $"DROP SCHEMA \"{schema}\" CASCADE");
        }
    }

    private static async Task ApplyMigrationAsync(
        PostgreSqlPersistenceDbContext context,
        KeyValuePair<string, TypeInfo> descriptor)
    {
        var assembly = context.GetService<IMigrationsAssembly>();
        var migration = assembly.CreateMigration(descriptor.Value,
            context.Database.ProviderName!);
        var generator = context.GetService<IMigrationsSqlGenerator>();

        await using var transaction = await context.Database.BeginTransactionAsync();
        foreach (var command in generator.Generate(migration.UpOperations,
                     migration.TargetModel))
        {
            await context.Database.ExecuteSqlRawAsync(command.CommandText);
        }

        await context.Database.ExecuteSqlRawAsync(
            "INSERT INTO \"__EFMigrationsHistory\" (\"MigrationId\", \"ProductVersion\") VALUES ({0}, {1})",
            descriptor.Key, "9.0.1");
        await transaction.CommitAsync();
    }

    private static AuditEvent CreateEvidence(string id) => new()
    {
        Id = id,
        TimestampUtc = new DateTimeOffset(2026, 8, 2, 12, 0, 0,
            TimeSpan.Zero),
        IdentityId = "upgrade-worker",
        CapabilityId = "upgrade.deploy",
        ResourceId = "service-a",
        Environment = "production",
        Decision = DecisionType.Allow,
        PolicyDecision = DecisionType.Allow,
        EnforcementMode = EnforcementMode.LogOnly,
        EffectiveAction = "allow",
        Reason = "Allowed before upgrade."
    };
}

#pragma warning restore EF1002
