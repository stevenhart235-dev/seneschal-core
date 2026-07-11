using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Seneschal.Api.Services;
using Seneschal.Core.Interfaces;
using Seneschal.Core.Models;
using Seneschal.Core.Repositories;
using ApiCapability = Seneschal.Api.Models.Capability;
using ApiIdentityDefinition = Seneschal.Api.Models.IdentityDefinition;
using ApiPolicy = Seneschal.Api.Models.Policy;
using Xunit;

namespace Seneschal.Api.Tests;

public sealed class MonitorPageTests :
    IClassFixture<ApiApplicationFactory>
{
    private readonly ApiApplicationFactory _factory;
    private readonly HttpClient _client;

    public MonitorPageTests(ApiApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Monitor_RendersDeterministicReadinessWithEmptyRuntimeActivity()
    {
        using var client = _factory
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    services.RemoveAll<IActivityStore>();
                    services.RemoveAll<IAuditEventStore>();
                    services.RemoveAll<IAuditSink>();
                    services.AddSingleton<IActivityStore, InMemoryActivityStore>();
                    services.AddSingleton<IAuditEventStore, InMemoryAuditEventStore>();
                    services.AddSingleton<IAuditSink>(
                        services => services.GetRequiredService<IAuditEventStore>());
                });
            })
            .CreateClient();

        using var response = await client.GetAsync("/monitor");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("Monitor Dashboard", html);
        Assert.Contains("Current Mode: Monitor", html);
        Assert.Contains("Policies are evaluated and audited", html);
        Assert.Contains("runtime requests", html);
        Assert.Contains("not blocked", html);
        Assert.Contains("Governance Readiness", html);
        Assert.Contains("25%", html);
        Assert.Contains("Policies exist", html);
        Assert.Contains("Runtime decisions observed", html);
        Assert.Contains("Audit history available", html);
        Assert.Contains("Activity metrics available", html);
        Assert.Contains("No denied or pending approval decisions", html);
        Assert.Contains("Policy activity appears after runtime evaluations", html);
        Assert.Contains("Capability readiness appears after runtime activity", html);
        Assert.Contains("Enforcement Readiness Advisor", html);
        Assert.Contains("No capability activity has been observed yet", html);
        Assert.Contains("Run monitor-mode evaluations before selecting", html);
        Assert.Contains("Governance Drift", html);
        Assert.Contains("Unused Capabilities", html);
        Assert.Contains("DeployApplication", html);
        Assert.Contains("Unused Identities", html);
        Assert.Contains("FinanceAgent", html);
        Assert.Contains("Unused Policies", html);
        Assert.Contains("Developers can deploy to dev", html);
        Assert.Contains("Governance Recommendations", html);
        Assert.Contains("Review Unused Governance Objects", html);
        Assert.Contains("Review or archive unused governance objects", html);
        Assert.Contains("Unused capabilities: 9", html);
        Assert.Contains("Unused identities: 9", html);
        Assert.Contains("Unused policies: 10", html);
    }

    [Fact]
    public async Task Monitor_SummarizesRuntimeActivity()
    {
        var deniedIdentity = $"MonitorDenied-{Guid.NewGuid():N}";
        var deniedCapability = $"MonitorCapability-{Guid.NewGuid():N}";

        using (var deniedResponse = await _client.PostAsJsonAsync(
            "/evaluate",
            new
            {
                identity = deniedIdentity,
                capability = deniedCapability,
                context = new
                {
                    environment = "production",
                    resource = "monitor-test-resource"
                }
            }))
        {
            Assert.Equal(HttpStatusCode.OK, deniedResponse.StatusCode);
        }

        using (var pendingResponse = await _client.PostAsJsonAsync(
            "/evaluate",
            new
            {
                identity = "SupportAgent",
                capability = "azure.keyvault.secret.read",
                context = new
                {
                    environment = "prod",
                    resource = "monitor-test-resource"
                }
            }))
        {
            Assert.Equal(HttpStatusCode.OK, pendingResponse.StatusCode);
        }

        using var response = await _client.GetAsync("/monitor");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("100%", html);
        Assert.Contains("Would Have Been Blocked", html);
        Assert.Contains("Total Denied Decisions", html);
        Assert.Contains("Pending Approvals Observed", html);
        Assert.Contains("Top Denial Reasons", html);
        Assert.Contains("No matching allow policy found", html);
        Assert.Contains("Most Active Policies", html);
        Assert.Contains("default-deny", html);
        Assert.Contains("Capabilities Ready For Enforcement", html);
        Assert.Contains(deniedCapability, html);
        Assert.Contains("Needs More Observation", html);
    }

    [Fact]
    public async Task Monitor_DriftRendersCleanEmptyStateWhenInventoryHasActivity()
    {
        using var client = CreateClientWithActivity(new ActivitySnapshot
        {
            Capabilities =
            [
                new CapabilityActivity
                {
                    CapabilityId = "DeployApplication",
                    TotalRequests = 30,
                    AllowedCount = 30,
                    AverageEvaluationDurationMs = 5
                },
                new CapabilityActivity
                {
                    CapabilityId = "DeleteProductionDatabase",
                    TotalRequests = 30,
                    AllowedCount = 29,
                    DeniedCount = 1,
                    AverageEvaluationDurationMs = 5
                },
                new CapabilityActivity
                {
                    CapabilityId = "azure.keyvault.secret.read",
                    TotalRequests = 30,
                    AllowedCount = 29,
                    PendingApprovalCount = 1,
                    AverageEvaluationDurationMs = 5
                },
                new CapabilityActivity
                {
                    CapabilityId = "infrastructure.production.apply",
                    TotalRequests = 30,
                    AllowedCount = 30,
                    AverageEvaluationDurationMs = 5
                },
                new CapabilityActivity
                {
                    CapabilityId = "infrastructure.production.destroy",
                    TotalRequests = 30,
                    DeniedCount = 30,
                    AverageEvaluationDurationMs = 5
                },
                new CapabilityActivity
                {
                    CapabilityId = "production.deployment.execute",
                    TotalRequests = 30,
                    AllowedCount = 30,
                    AverageEvaluationDurationMs = 5
                },
                new CapabilityActivity
                {
                    CapabilityId = "database.migration.execute",
                    TotalRequests = 30,
                    DeniedCount = 30,
                    AverageEvaluationDurationMs = 5
                },
                new CapabilityActivity
                {
                    CapabilityId = "payments.refund.create",
                    TotalRequests = 30,
                    AllowedCount = 30,
                    AverageEvaluationDurationMs = 5
                },
                new CapabilityActivity
                {
                    CapabilityId = "production.release.approve",
                    TotalRequests = 30,
                    PendingApprovalCount = 30,
                    AverageEvaluationDurationMs = 5
                }
            ],
            Identities =
            [
                new IdentityActivity
                {
                    IdentityId = "Developer",
                    TotalRequests = 1
                },
                new IdentityActivity
                {
                    IdentityId = "FinanceAgent",
                    TotalRequests = 1
                },
                new IdentityActivity
                {
                    IdentityId = "PlatformEngineer",
                    TotalRequests = 1
                },
                new IdentityActivity
                {
                    IdentityId = "SupportAgent",
                    TotalRequests = 1
                },
                new IdentityActivity
                {
                    IdentityId = "platform-agent",
                    TotalRequests = 1
                },
                new IdentityActivity
                {
                    IdentityId = "deployment-worker",
                    TotalRequests = 1
                },
                new IdentityActivity
                {
                    IdentityId = "migration-worker",
                    TotalRequests = 1
                },
                new IdentityActivity
                {
                    IdentityId = "refund-worker",
                    TotalRequests = 1
                },
                new IdentityActivity
                {
                    IdentityId = "release-approval-worker",
                    TotalRequests = 1
                }
            ],
            Policies =
            [
                new PolicyActivity
                {
                    PolicyId = "Developers can deploy to dev",
                    MatchCount = 1
                },
                new PolicyActivity
                {
                    PolicyId = "Developers cannot delete production databases",
                    MatchCount = 1
                },
                new PolicyActivity
                {
                    PolicyId = "Platform engineers can deploy to dev",
                    MatchCount = 1
                },
                new PolicyActivity
                {
                    PolicyId = "Support secret reads require approval",
                    MatchCount = 1
                },
                new PolicyActivity
                {
                    PolicyId = "Platform agent can apply production infrastructure",
                    MatchCount = 1
                },
                new PolicyActivity
                {
                    PolicyId = "Platform agent cannot destroy production infrastructure",
                    MatchCount = 1
                },
                new PolicyActivity
                {
                    PolicyId = "Deployment worker can deploy to production",
                    MatchCount = 1
                },
                new PolicyActivity
                {
                    PolicyId = "Migration worker cannot migrate production database",
                    MatchCount = 1
                },
                new PolicyActivity
                {
                    PolicyId = "Refund worker can create production refunds",
                    MatchCount = 1
                },
                new PolicyActivity
                {
                    PolicyId = "Release approval worker requires production approval",
                    MatchCount = 1
                }
            ]
        });

        using var response = await client.GetAsync("/monitor");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("Governance Drift", html);
        Assert.Contains(
            "No governance drift detected from current",
            html);
        Assert.Contains("in-memory activity", html);
        Assert.DoesNotContain("Every registered capability has observed", html);
        Assert.Contains("Enforcement Readiness Advisor", html);
        Assert.Contains("Governance Readiness", html);
    }

    [Fact]
    public async Task Monitor_DriftDetectsRuntimeActivityWithoutPolicyMatch()
    {
        using var client = CreateClientWithActivityAndAudit(
            new ActivitySnapshot
            {
                Capabilities =
                [
                    new CapabilityActivity
                    {
                        CapabilityId = "DeployApplication",
                        TotalRequests = 1,
                        AllowedCount = 1,
                        AverageEvaluationDurationMs = 1
                    }
                ]
            },
            [
                new AuditEvent
                {
                    Id = "audit-without-policy",
                    TimestampUtc = DateTimeOffset.UtcNow,
                    IdentityId = "Developer",
                    CapabilityId = "DeployApplication",
                    Decision = Seneschal.Core.Enums.DecisionType.Allow,
                    EnforcementMode = Seneschal.Core.Enums.EnforcementMode.LogOnly,
                    Reason = "Manual test event",
                    EvaluationDurationMs = 1
                }
            ]);

        using var response = await client.GetAsync("/monitor");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("Runtime Activity Without Policy Match", html);
        Assert.Contains("DeployApplication", html);
        Assert.Contains("1 events", html);
        Assert.Contains("Governance Recommendations", html);
        Assert.Contains("Policy Coverage Gap", html);
        Assert.Contains(
            "Create or update policy coverage for observed runtime activity",
            html);
        Assert.Contains("Identity: Developer", html);
    }

    [Fact]
    public async Task Monitor_RecommendationsRenderEmptyStateWhenNoSignalsExist()
    {
        using var client = _factory
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    services.RemoveAll<IActivityStore>();
                    services.RemoveAll<IAuditEventStore>();
                    services.RemoveAll<IAuditSink>();
                    services.RemoveAll<CapabilityLoader>();
                    services.RemoveAll<IdentityLoader>();
                    services.RemoveAll<PolicyLoader>();

                    services.AddSingleton<IActivityStore, InMemoryActivityStore>();
                    services.AddSingleton<IAuditEventStore, InMemoryAuditEventStore>();
                    services.AddSingleton<IAuditSink>(
                        services => services.GetRequiredService<IAuditEventStore>());
                    services.AddSingleton(CreateCapabilityLoader([]));
                    services.AddSingleton(CreateIdentityLoader([]));
                    services.AddSingleton(CreatePolicyLoader([]));
                });
            })
            .CreateClient();

        using var response = await client.GetAsync("/monitor");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("Governance Recommendations", html);
        Assert.Contains(
            "No governance recommendations available from",
            html);
        Assert.Contains("current in-memory activity", html);
    }

    [Fact]
    public async Task Monitor_AdvisorCalculatesDeterministicScores()
    {
        using var client = CreateClientWithActivity(new ActivitySnapshot
        {
            Capabilities =
            [
                new CapabilityActivity
                {
                    CapabilityId = "capability.ready",
                    TotalRequests = 30,
                    AllowedCount = 30,
                    AverageEvaluationDurationMs = 5
                },
                new CapabilityActivity
                {
                    CapabilityId = "capability.monitor",
                    TotalRequests = 10,
                    AllowedCount = 9,
                    DeniedCount = 1,
                    AverageEvaluationDurationMs = 5
                },
                new CapabilityActivity
                {
                    CapabilityId = "capability.not-ready",
                    TotalRequests = 1,
                    DeniedCount = 1,
                    PendingApprovalCount = 1,
                    AverageEvaluationDurationMs = 12
                }
            ]
        });

        using var response = await client.GetAsync("/monitor");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("Enforcement Readiness Advisor", html);
        Assert.Contains("capability.ready", html);
        Assert.Contains("100%", html);
        Assert.Contains("Ready for Enforcement", html);
        Assert.Contains("Candidate for targeted enforcement", html);
        Assert.Contains("No denied evaluations observed", html);
        Assert.Contains("No pending approvals observed", html);
        Assert.Contains("Stable runtime behavior observed", html);
        Assert.Contains("Ready to Enforce", html);
        Assert.Contains("Consider moving this capability to Enforce mode", html);
        Assert.Contains("View capability activity", html);

        Assert.Contains("capability.monitor", html);
        Assert.Contains("60%", html);
        Assert.Contains("Remain in Monitor", html);
        Assert.Contains("Runtime denials still occurring", html);
        Assert.Contains("Limited runtime history", html);
        Assert.Contains("Continue Monitoring", html);
        Assert.Contains("Continue monitoring before enforcing", html);
        Assert.Contains("Review High-Denial Capability", html);
        Assert.Contains("Review denial patterns before enforcement", html);
        Assert.Contains("Denied evaluations: 1", html);

        Assert.Contains("capability.not-ready", html);
        Assert.Contains("10%", html);
        Assert.Contains("Not Ready", html);
        Assert.Contains("Pending approvals still being generated", html);
        Assert.Contains("No successful allowed evaluations observed", html);
        Assert.Contains("Evaluation latency may need review", html);
    }

    [Fact]
    public async Task Monitor_AdvisorOrdersCapabilitiesDeterministically()
    {
        using var client = CreateClientWithActivity(new ActivitySnapshot
        {
            Capabilities =
            [
                new CapabilityActivity
                {
                    CapabilityId = "capability.b",
                    TotalRequests = 30,
                    AllowedCount = 30,
                    AverageEvaluationDurationMs = 5
                },
                new CapabilityActivity
                {
                    CapabilityId = "capability.a",
                    TotalRequests = 30,
                    AllowedCount = 30,
                    AverageEvaluationDurationMs = 5
                },
                new CapabilityActivity
                {
                    CapabilityId = "capability.z",
                    TotalRequests = 40,
                    AllowedCount = 40,
                    AverageEvaluationDurationMs = 5
                },
                new CapabilityActivity
                {
                    CapabilityId = "capability.low-score",
                    TotalRequests = 40,
                    AllowedCount = 39,
                    DeniedCount = 1,
                    AverageEvaluationDurationMs = 5
                }
            ]
        });

        using var response = await client.GetAsync("/monitor");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var html = await response.Content.ReadAsStringAsync();
        var advisorIndex = html.IndexOf(
            "Enforcement Readiness Advisor",
            StringComparison.Ordinal);
        Assert.True(advisorIndex >= 0);

        var advisorHtml = html[advisorIndex..];
        var zIndex = advisorHtml.IndexOf("capability.z", StringComparison.Ordinal);
        var aIndex = advisorHtml.IndexOf("capability.a", StringComparison.Ordinal);
        var bIndex = advisorHtml.IndexOf("capability.b", StringComparison.Ordinal);
        var lowScoreIndex = advisorHtml.IndexOf(
            "capability.low-score",
            StringComparison.Ordinal);

        Assert.True(zIndex >= 0);
        Assert.True(aIndex >= 0);
        Assert.True(bIndex >= 0);
        Assert.True(lowScoreIndex >= 0);
        Assert.True(zIndex < aIndex);
        Assert.True(aIndex < bIndex);
        Assert.True(bIndex < lowScoreIndex);
    }

    [Fact]
    public async Task Dashboard_LinksToMonitor()
    {
        using var response = await _client.GetAsync("/dashboard");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("Monitor", html);
        Assert.Contains("/monitor", html);
    }

    private HttpClient CreateClientWithActivity(ActivitySnapshot snapshot)
    {
        return CreateClientWithActivityAndAudit(snapshot, []);
    }

    private HttpClient CreateClientWithActivityAndAudit(
        ActivitySnapshot snapshot,
        IReadOnlyCollection<AuditEvent> auditEvents)
    {
        return _factory
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    services.RemoveAll<IActivityStore>();
                    services.RemoveAll<IAuditEventStore>();
                    services.RemoveAll<IAuditSink>();
                    services.AddSingleton<IActivityStore>(
                        new SnapshotActivityStore(snapshot));
                    services.AddSingleton<IAuditEventStore>(
                        new SnapshotAuditEventStore(auditEvents));
                    services.AddSingleton<IAuditSink>(
                        services => services.GetRequiredService<IAuditEventStore>());
                });
            })
            .CreateClient();
    }

    private sealed class SnapshotActivityStore : IActivityStore
    {
        private readonly ActivitySnapshot _snapshot;

        public SnapshotActivityStore(ActivitySnapshot snapshot)
        {
            _snapshot = snapshot;
        }

        public Task RecordAsync(
            AuditEvent decisionEvent,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task<ActivitySnapshot> GetSnapshotAsync(
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_snapshot);
        }
    }

    private sealed class SnapshotAuditEventStore : IAuditEventStore
    {
        private readonly IReadOnlyCollection<AuditEvent> _events;

        public SnapshotAuditEventStore(IReadOnlyCollection<AuditEvent> events)
        {
            _events = events;
        }

        public Task WriteAsync(
            AuditEvent auditEvent,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task<AuditEvent?> GetByIdAsync(
            string id,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_events.FirstOrDefault(auditEvent =>
                string.Equals(
                    auditEvent.Id,
                    id,
                    StringComparison.OrdinalIgnoreCase)));
        }

        public Task<IReadOnlyCollection<AuditEvent>> GetRecentAsync(
            int count = 100,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyCollection<AuditEvent>>(
                _events.Take(count).ToList());
        }
    }

    private static CapabilityLoader CreateCapabilityLoader(
        List<ApiCapability> capabilities)
    {
        var loader = new CapabilityLoader();
        SetField(loader, "_capabilities", capabilities);
        return loader;
    }

    private static IdentityLoader CreateIdentityLoader(
        List<ApiIdentityDefinition> identities)
    {
        var loader = new IdentityLoader();
        SetField(loader, "_identities", identities);
        return loader;
    }

    private static PolicyLoader CreatePolicyLoader(List<ApiPolicy> policies)
    {
        var loader = new PolicyLoader();
        SetField(loader, "_policies", policies);
        return loader;
    }

    private static void SetField<T>(
        object target,
        string fieldName,
        T value)
    {
        var field = target
            .GetType()
            .GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(field);
        field.SetValue(target, value);
    }
}
