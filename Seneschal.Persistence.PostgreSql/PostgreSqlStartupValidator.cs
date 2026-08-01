using Microsoft.EntityFrameworkCore;

namespace Seneschal.Persistence.PostgreSql;

public sealed class PostgreSqlStartupValidator(
    IDbContextFactory<PostgreSqlPersistenceDbContext> contextFactory)
{
    public async Task ValidateAsync(CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(
            cancellationToken);
        if (!await context.Database.CanConnectAsync(cancellationToken))
        {
            throw new InvalidOperationException(
                "PostgreSQL persistence is selected, but the database is unavailable.");
        }
        var pending = await context.Database.GetPendingMigrationsAsync(
            cancellationToken);
        if (pending.Any())
        {
            throw new InvalidOperationException(
                "PostgreSQL persistence has pending migrations. Apply migrations before starting Seneschal.");
        }
    }

}
