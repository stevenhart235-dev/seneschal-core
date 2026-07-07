using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Seneschal.Client;
using Seneschal.Client.Models;

namespace Seneschal.AspNetCore;

/// <summary>
/// Evaluates a required capability with Seneschal before allowing a request to
/// continue through the ASP.NET Core pipeline.
/// </summary>
public sealed class SeneschalCapabilityMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ISeneschalClient _client;
    private readonly SeneschalCapabilityOptions _options;

    /// <summary>
    /// Initializes a new instance of the
    /// <see cref="SeneschalCapabilityMiddleware"/> class.
    /// </summary>
    /// <param name="next">The next delegate in the pipeline.</param>
    /// <param name="client">The Seneschal decision client.</param>
    /// <param name="options">Middleware options.</param>
    public SeneschalCapabilityMiddleware(
        RequestDelegate next,
        ISeneschalClient client,
        IOptions<SeneschalCapabilityOptions> options)
    {
        ArgumentNullException.ThrowIfNull(next);
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(options);

        _next = next;
        _client = client;
        _options = options.Value;
        _options.Validate();
    }

    /// <summary>
    /// Evaluates the configured capability and either continues, blocks, or
    /// returns an approval-required response.
    /// </summary>
    /// <param name="context">The current HTTP context.</param>
    /// <returns>A task representing middleware execution.</returns>
    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        DecisionResult decision;
        try
        {
            decision = await _client.EvaluateAsync(
                BuildDecisionRequest(context),
                context.RequestAborted);
        }
        catch (SeneschalClientException)
        {
            if (_options.EnforcementBehavior ==
                SeneschalEnforcementBehavior.Monitor)
            {
                await _next(context);
                return;
            }

            context.Response.StatusCode =
                StatusCodes.Status503ServiceUnavailable;
            await context.Response.WriteAsJsonAsync(new
            {
                decision = "unavailable",
                reason = "Seneschal decision runtime is unavailable."
            });
            return;
        }

        if (SeneschalDecisionHandler.ShouldContinue(
                decision,
                _options.EnforcementBehavior))
        {
            await _next(context);
            return;
        }

        await SeneschalDecisionHandler.WriteResponseAsync(context, decision);
    }

    private DecisionRequest BuildDecisionRequest(HttpContext context)
    {
        var resource = string.IsNullOrWhiteSpace(_options.ResourceId)
            ? context.Request.Path.Value ?? "/"
            : _options.ResourceId;

        var request = new DecisionRequest
        {
            Identity = _options.ResolveIdentity(context),
            Capability = _options.CapabilityId,
            Context = new Dictionary<string, string>
            {
                ["resource"] = resource
            }
        };

        if (!string.IsNullOrWhiteSpace(_options.Environment))
        {
            request.Context["environment"] = _options.Environment;
        }

        return request;
    }

}
