using Seneschal.Core.Interfaces;
using Seneschal.Core.Models;
using Seneschal.Core.Tests.Repositories;

namespace Seneschal.Persistence.PostgreSql.Tests;

[Collection("PostgreSQL")]
public sealed class PostgreSqlAuditEventStoreContractTests :
    AuditEventStoreContractTests, IAsyncLifetime
{
    private readonly PostgreSqlFixture _fixture;

    public PostgreSqlAuditEventStoreContractTests(PostgreSqlFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync() => _fixture.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    protected override IAuditEventStore CreateStore() =>
        new PostgreSqlAuditEventStore(_fixture.CreateFactory());

    protected override IAuditEventStore CreateFailingStore(Exception failure) =>
        new FailingAuditEventStore(failure);

    private sealed class FailingAuditEventStore(Exception failure) :
        IAuditEventStore
    {
        public Task WriteAsync(AuditEvent auditEvent,
            CancellationToken cancellationToken = default) =>
            Task.FromException(failure);

        public Task<AuditEvent?> GetByIdAsync(string id,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<AuditEvent?>(null);

        public Task<IReadOnlyCollection<AuditEvent>> GetRecentAsync(
            int count = 100, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyCollection<AuditEvent>>([]);
    }
}

[CollectionDefinition("PostgreSQL")]
public sealed class PostgreSqlCollection : ICollectionFixture<PostgreSqlFixture>;

[Collection("PostgreSQL")]
public sealed class PostgreSqlApprovalStoreContractTests :
    Seneschal.Core.Tests.Repositories.ApprovalStoreContractTests,
    IAsyncLifetime
{
    private readonly PostgreSqlFixture _fixture;

    public PostgreSqlApprovalStoreContractTests(PostgreSqlFixture fixture) =>
        _fixture = fixture;

    public Task InitializeAsync() => _fixture.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    protected override IApprovalStore CreateStore() =>
        new PostgreSqlApprovalStore(_fixture.CreateFactory());
}
