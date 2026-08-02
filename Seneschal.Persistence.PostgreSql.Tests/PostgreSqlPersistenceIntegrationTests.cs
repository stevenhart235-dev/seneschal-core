using Microsoft.EntityFrameworkCore;
using Seneschal.Core.Enums;
using Seneschal.Core.Exceptions;
using Seneschal.Core.Models;
using Seneschal.Core.Repositories;

namespace Seneschal.Persistence.PostgreSql.Tests;

[Collection("PostgreSQL")]
public sealed class PostgreSqlPersistenceIntegrationTests(
    PostgreSqlFixture fixture) : IAsyncLifetime
{
    public Task InitializeAsync() => fixture.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Migration_IsAppliedAndHasNoPendingMigrations()
    {
        await using var context = await fixture.CreateFactory().CreateDbContextAsync();
        Assert.Empty(await context.Database.GetPendingMigrationsAsync());
        Assert.Contains("202608010001_InitialEvaluationEvidence",
            await context.Database.GetAppliedMigrationsAsync());
        Assert.Contains("202608020001_CompleteApprovalPersistence",
            await context.Database.GetAppliedMigrationsAsync());
    }

    [Fact]
    public async Task Evidence_SurvivesStoreRecreation()
    {
        var evidence = CreateEvidence("restart-evidence");
        await new PostgreSqlAuditEventStore(fixture.CreateFactory())
            .WriteAsync(evidence);

        var restarted = new PostgreSqlAuditEventStore(fixture.CreateFactory());

        Assert.Equivalent(evidence,
            await restarted.GetByIdAsync(evidence.Id), strict: true);
    }

    [Fact]
    public async Task InvestigationActivity_AggregatesAllDurableEvaluations()
    {
        var factory = fixture.CreateFactory();
        var audit = new PostgreSqlAuditEventStore(factory);
        var now = new DateTimeOffset(2026, 8, 2, 12, 0, 0, TimeSpan.Zero);
        await audit.WriteAsync(CreateEvidence("investigation-allow") with
        {
            TimestampUtc = now,
            IdentityId = "identity-a",
            CapabilityId = "capability-a",
            Decision = DecisionType.Allow
        });
        await audit.WriteAsync(CreateEvidence("investigation-deny") with
        {
            TimestampUtc = now.AddMinutes(1),
            IdentityId = "identity-a",
            CapabilityId = "capability-a",
            Decision = DecisionType.Deny
        });
        await audit.WriteAsync(CreateEvidence("investigation-pending") with
        {
            TimestampUtc = now.AddMinutes(2),
            IdentityId = "identity-b",
            CapabilityId = "capability-b",
            Decision = DecisionType.RequireApproval
        });
        await audit.WriteAsync(CreateEvidence("administrative-transition") with
        {
            TimestampUtc = now.AddMinutes(3),
            IdentityId = "identity-a",
            CapabilityId = "capability-a",
            Decision = DecisionType.Allow,
            EffectiveAction = "approval_approved"
        });

        var snapshot = await new PostgreSqlInvestigationActivityReader(
                factory, new InMemoryActivityStore())
            .GetSnapshotAsync();

        var capabilityA = Assert.Single(snapshot.Capabilities,
            item => item.CapabilityId == "capability-a");
        Assert.Equal(2, capabilityA.TotalRequests);
        Assert.Equal(1, capabilityA.AllowedCount);
        Assert.Equal(1, capabilityA.DeniedCount);
        Assert.Equal(now.AddMinutes(1), capabilityA.LastUsedUtc);
        var capabilityB = Assert.Single(snapshot.Capabilities,
            item => item.CapabilityId == "capability-b");
        Assert.Equal(1, capabilityB.PendingApprovalCount);
        var identityA = Assert.Single(snapshot.Identities,
            item => item.IdentityId == "identity-a");
        Assert.Equal(2, identityA.TotalRequests);
        Assert.Equal(["capability-a"], identityA.DistinctCapabilitiesUsed);
    }

    [Fact]
    public async Task ConcurrentIdenticalWrites_AppendOnce()
    {
        var evidence = CreateEvidence("concurrent-evidence");
        var stores = Enumerable.Range(0, 8)
            .Select(_ => new PostgreSqlAuditEventStore(fixture.CreateFactory()))
            .ToList();

        await Task.WhenAll(stores.Select(store => store.WriteAsync(evidence)));

        Assert.Single(await stores[0].GetRecentAsync());
    }

    [Fact]
    public async Task ReorderedDictionaryContent_IsIdentical()
    {
        var store = new PostgreSqlAuditEventStore(fixture.CreateFactory());
        var first = CreateEvidence("canonical-evidence") with
        {
            RequestContext = new() { ["z"] = "last", ["a"] = "first" },
            PolicyEvaluations = [CreatePolicyEvaluation("z", "a")]
        };
        var reordered = first with
        {
            RequestContext = new() { ["a"] = "first", ["z"] = "last" },
            PolicyEvaluations = [CreatePolicyEvaluation("a", "z")]
        };

        await store.WriteAsync(first);
        await store.WriteAsync(reordered);

        Assert.Single(await store.GetRecentAsync());
    }

    [Fact]
    public async Task ConcurrentConflictingWrites_OneFailsExplicitly()
    {
        var factory = fixture.CreateFactory();
        var writes = new[]
        {
            CreateEvidence("conflicting-evidence"),
            CreateEvidence("conflicting-evidence") with { Reason = "Different." }
        };

        var outcomes = await Task.WhenAll(writes.Select(async evidence =>
        {
            try
            {
                await new PostgreSqlAuditEventStore(factory).WriteAsync(evidence);
                return null;
            }
            catch (Exception exception)
            {
                return exception;
            }
        }));

        Assert.Single(outcomes, exception => exception is null);
        Assert.IsType<EvaluationEvidenceConflictException>(
            Assert.Single(outcomes, exception => exception is not null));
        Assert.Single(await new PostgreSqlAuditEventStore(factory).GetRecentAsync());
    }

    [Fact]
    public async Task ConcurrentApprovalCreation_CommitsOneScopeAtomically()
    {
        var factory = fixture.CreateFactory();
        var commits = new[]
        {
            new EvaluationCommit
            {
                Evidence = CreateEvidence("approval-race-1"),
                ApprovalMutation = new ApprovalMutation
                {
                    Kind = ApprovalMutationKind.Create,
                    Record = CreateApproval("approval-race-1")
                }
            },
            new EvaluationCommit
            {
                Evidence = CreateEvidence("approval-race-2"),
                ApprovalMutation = new ApprovalMutation
                {
                    Kind = ApprovalMutationKind.Create,
                    Record = CreateApproval("approval-race-2")
                }
            }
        };

        var outcomes = await Task.WhenAll(commits.Select(async commit =>
        {
            try
            {
                await new PostgreSqlEvaluationCommitCoordinator(factory)
                    .CommitAsync(commit);
                return true;
            }
            catch (EvaluationCommitException)
            {
                return false;
            }
        }));

        Assert.Single(outcomes, success => success);
        Assert.Single(new PostgreSqlApprovalStore(factory).GetAll());
        Assert.Single(await new PostgreSqlAuditEventStore(factory).GetRecentAsync());
    }

    [Fact]
    public async Task ApprovalConflict_RollsBackEvidence()
    {
        var factory = fixture.CreateFactory();
        var coordinator = new PostgreSqlEvaluationCommitCoordinator(factory);
        var approval = CreateApproval("approval-1");
        await coordinator.CommitAsync(new EvaluationCommit
        {
            Evidence = CreateEvidence("first"),
            ApprovalMutation = new ApprovalMutation
            {
                Kind = ApprovalMutationKind.Create,
                Record = approval
            }
        });

        var conflicting = approval with { RequestReason = "different" };
        await Assert.ThrowsAsync<EvaluationCommitException>(() =>
            coordinator.CommitAsync(new EvaluationCommit
            {
                Evidence = CreateEvidence("rollback-evidence"),
                ApprovalMutation = new ApprovalMutation
                {
                    Kind = ApprovalMutationKind.Create,
                    Record = conflicting
                }
            }));

        Assert.Null(await new PostgreSqlAuditEventStore(factory)
            .GetByIdAsync("rollback-evidence"));
    }

    [Fact]
    public async Task ApprovalConsumption_CommitsWithEvidence()
    {
        var factory = fixture.CreateFactory();
        var coordinator = new PostgreSqlEvaluationCommitCoordinator(factory);
        var approvals = new PostgreSqlApprovalStore(factory);
        var approval = CreateApproval("approval-consume");
        await coordinator.CommitAsync(new EvaluationCommit
        {
            Evidence = CreateEvidence("approval-created"),
            ApprovalMutation = new ApprovalMutation
            {
                Kind = ApprovalMutationKind.Create,
                Record = approval
            }
        });
        var approved = approvals.Resolve(approval.Id, ApprovalStatus.Approved,
            "reviewer", DateTimeOffset.UtcNow)!;
        var consumed = approved with
        {
            Status = ApprovalStatus.Consumed,
            ConsumedAt = DateTimeOffset.UtcNow,
            ConsumedByDecisionId = "approval-consumed"
        };

        await coordinator.CommitAsync(new EvaluationCommit
        {
            Evidence = CreateEvidence("approval-consumed"),
            ApprovalMutation = new ApprovalMutation
            {
                Kind = ApprovalMutationKind.Consume,
                Record = consumed,
                ExpectedStatus = ApprovalStatus.Approved
            }
        });

        Assert.Equal(ApprovalStatus.Consumed,
            Assert.Single(approvals.GetAll()).Status);
        Assert.NotNull(await new PostgreSqlAuditEventStore(factory)
            .GetByIdAsync("approval-consumed"));
    }

    [Theory]
    [InlineData(ApprovalStatus.Approved)]
    [InlineData(ApprovalStatus.Rejected)]
    public async Task ApprovalResolution_CommitsStateAndEvidenceAtomically(
        ApprovalStatus status)
    {
        var factory = fixture.CreateFactory();
        var approvals = new PostgreSqlApprovalStore(factory);
        var approval = approvals.GetOrCreate("resolver", "deploy", "prod", "api",
            "Review required.", DateTimeOffset.UtcNow).Record;
        var resolved = approval with
        {
            Status = status,
            ResolvedAt = DateTimeOffset.UtcNow,
            ResolvedBy = "reviewer"
        };
        var evidence = CreateEvidence("resolution-" + status) with
        {
            ApprovalId = approval.Id,
            ApprovalStatus = status.ToString(),
            ApprovalAction = status.ToString()
        };

        await new PostgreSqlEvaluationCommitCoordinator(factory).CommitAsync(
            new EvaluationCommit
            {
                Evidence = evidence,
                ApprovalMutation = new ApprovalMutation
                {
                    Kind = ApprovalMutationKind.Resolve,
                    Record = resolved,
                    ExpectedStatus = ApprovalStatus.Pending
                }
            });

        Assert.Equal(status, approvals.GetById(approval.Id)!.Status);
        Assert.NotNull(await new PostgreSqlAuditEventStore(factory)
            .GetByIdAsync(evidence.Id));
    }

    [Fact]
    public async Task ConnectionFailure_Propagates()
    {
        var store = new PostgreSqlAuditEventStore(fixture.CreateFactory(
            "Host=127.0.0.1;Port=1;Database=missing;Username=missing;Password=missing;Timeout=1;Command Timeout=1"));

        await Assert.ThrowsAnyAsync<Exception>(() =>
            store.WriteAsync(CreateEvidence("connection-failure")));
    }

    private static AuditEvent CreateEvidence(string id) => new()
    {
        Id = id,
        TimestampUtc = new DateTimeOffset(2026, 8, 1, 12, 0, 0, TimeSpan.Zero),
        IdentityId = "Developer",
        CapabilityId = "DeployApplication",
        ResourceId = "payment-api",
        Environment = "dev",
        Decision = DecisionType.Allow,
        EnforcementMode = EnforcementMode.LogOnly,
        EffectiveAction = "allow",
        Reason = "Allowed."
    };

    private static ApprovalRecord CreateApproval(string id) => new()
    {
        Id = id,
        IdentityId = "SupportAgent",
        CapabilityId = "azure.keyvault.secret.read",
        Environment = "prod",
        ResourceId = "vault-a",
        RequestReason = "Review required.",
        RequestedAt = new DateTimeOffset(2026, 8, 1, 12, 0, 0, TimeSpan.Zero)
    };

    private static PolicyEvaluation CreatePolicyEvaluation(
        string firstKey, string secondKey) => new()
    {
        Policy = new Policy
        {
            Id = "policy-1",
            Name = "Policy",
            Effect = DecisionType.Allow,
            Reason = "Allowed.",
            Conditions = new()
            {
                [firstKey] = firstKey == "a" ? "first" : "last",
                [secondKey] = secondKey == "a" ? "first" : "last"
            }
        },
        Matched = true
    };
}
