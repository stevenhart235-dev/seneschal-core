using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Seneschal.Client;
using Seneschal.Client.Models;

namespace Seneschal.AspNetCore;

/// <summary>
/// Evaluates endpoints decorated with <see cref="RequiresCapabilityAttribute"/>.
/// </summary>
public sealed class SeneschalCapabilityAttributeMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ISeneschalClient _client;
    private readonly SeneschalOptions _options;

    /// <summary>
    /// Initializes a new instance of the
    /// <see cref="SeneschalCapabilityAttributeMiddleware"/> class.
    /// </summary>
    /// <param name="next">The next delegate in the pipeline.</param>
    /// <param name="client">The Seneschal decision client.</param>
    /// <param name="enforcementBehavior">How returned decisions are applied.</param>
    public SeneschalCapabilityAttributeMiddleware(
        RequestDelegate next,
        ISeneschalClient client,
        SeneschalEnforcementBehavior enforcementBehavior)
        : this(
            next,
            client,
            Options.Create(new SeneschalOptions
            {
                EnforcementBehavior = enforcementBehavior
            }))
    {
    }

    /// <summary>
    /// Initializes the middleware with the recommended integration options.
    /// </summary>
    /// <param name="next">The next delegate in the pipeline.</param>
    /// <param name="client">The Seneschal decision client.</param>
    /// <param name="options">The configured Seneschal options.</param>
    public SeneschalCapabilityAttributeMiddleware(
        RequestDelegate next,
        ISeneschalClient client,
        IOptions<SeneschalOptions> options)
    {
        ArgumentNullException.ThrowIfNull(next);
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(options);

        _next = next;
        _client = client;
        _options = options.Value;
    }

    /// <summary>
    /// Evaluates the endpoint capability metadata when present.
    /// </summary>
    /// <param name="context">The current HTTP context.</param>
    /// <returns>A task representing middleware execution.</returns>
    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var attribute = context
            .GetEndpoint()
            ?.Metadata
            .GetMetadata<RequiresCapabilityAttribute>();

        if (attribute is null)
        {
            await _next(context);
            return;
        }

        var decision = await _client.EvaluateAsync(
            BuildDecisionRequest(context, attribute),
            context.RequestAborted);

        if (SeneschalDecisionHandler.ShouldContinue(
                decision,
                _options.EnforcementBehavior))
        {
            await _next(context);
            return;
        }

        await SeneschalDecisionHandler.WriteResponseAsync(context, decision);
    }

    private DecisionRequest BuildDecisionRequest(
        HttpContext context,
        RequiresCapabilityAttribute attribute)
    {
        var resource = string.IsNullOrWhiteSpace(attribute.ResourceId)
            ? context.Request.Path.Value ?? "/"
            : attribute.ResourceId;
        var identity = _options.IdentityResolver(context);

        if (string.IsNullOrWhiteSpace(identity))
        {
            identity = "anonymous";
        }

        var request = new DecisionRequest
        {
            Identity = identity,
            Capability = attribute.CapabilityId,
            Context = new Dictionary<string, string>
            {
                ["resource"] = resource
            }
        };

        var environment = string.IsNullOrWhiteSpace(attribute.Environment)
            ? _options.DefaultEnvironment
            : attribute.Environment;

        if (!string.IsNullOrWhiteSpace(environment))
        {
            request.Context["environment"] = environment;
        }

        return request;
    }
}
