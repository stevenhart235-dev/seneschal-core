using Microsoft.AspNetCore.Builder;

namespace Seneschal.AspNetCore;

/// <summary>
/// Extension methods for declaring capability requirements on endpoints.
/// </summary>
public static class SeneschalEndpointConventionBuilderExtensions
{
    /// <summary>
    /// Requires a Seneschal capability decision before the endpoint executes.
    /// </summary>
    /// <typeparam name="TBuilder">The endpoint convention builder type.</typeparam>
    /// <param name="builder">The endpoint convention builder.</param>
    /// <param name="capabilityId">The required capability identifier.</param>
    /// <returns>The endpoint convention builder.</returns>
    public static TBuilder RequireCapability<TBuilder>(
        this TBuilder builder,
        string capabilityId)
        where TBuilder : IEndpointConventionBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.WithMetadata(new RequiresCapabilityAttribute(capabilityId));

        return builder;
    }
}
