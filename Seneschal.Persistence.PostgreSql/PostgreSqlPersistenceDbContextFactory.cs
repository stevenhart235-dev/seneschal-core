using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

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
            throw new InvalidOperationException(
                "Set ConnectionStrings__SeneschalPostgreSql before running migrations.");
        }
        var options = new DbContextOptionsBuilder<PostgreSqlPersistenceDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        return new PostgreSqlPersistenceDbContext(options);
    }
}
