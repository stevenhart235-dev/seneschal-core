using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Npgsql;

namespace Seneschal.Persistence.PostgreSql;

public sealed class PostgreSqlPersistenceDbContextFactory :
    IDesignTimeDbContextFactory<PostgreSqlPersistenceDbContext>
{
    public PostgreSqlPersistenceDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable(
            "ConnectionStrings__SeneschalPostgreSql");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            if (string.Equals(
                    Environment.GetEnvironmentVariable(
                        "SENESCHAL_MIGRATION_BUNDLE_BUILD"),
                    "true",
                    StringComparison.OrdinalIgnoreCase))
            {
                var bundleOptions =
                    new DbContextOptionsBuilder<PostgreSqlPersistenceDbContext>()
                        .UseNpgsql(new NpgsqlConnection())
                        .Options;
                return new PostgreSqlPersistenceDbContext(bundleOptions);
            }

            throw new InvalidOperationException(
                "Set ConnectionStrings__SeneschalPostgreSql before running migrations.");
        }
        var options = new DbContextOptionsBuilder<PostgreSqlPersistenceDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        return new PostgreSqlPersistenceDbContext(options);
    }
}
