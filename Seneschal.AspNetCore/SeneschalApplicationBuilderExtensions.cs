using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Seneschal.AspNetCore;

/// <summary>
/// Extension methods for registering Seneschal middleware.
/// </summary>
public static class SeneschalApplicationBuilderExtensions
{
    /// <summary>
    /// Enables evaluation for endpoints protected with Seneschal capability
    /// metadata.
    /// </summary>
    /// <param name="app">The application builder.</param>
    /// <returns>The application builder.</returns>
    public static IApplicationBuilder UseSeneschal(
        this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var options = app.ApplicationServices
            .GetRequiredService<IOptions<SeneschalOptions>>();

        return app.UseMiddleware<SeneschalCapabilityAttributeMiddleware>(
            options);
    }

    /// <summary>
    /// Adds Seneschal capability evaluation middleware to the ASP.NET Core
    /// pipeline.
    /// </summary>
    /// <param name="app">The application builder.</param>
    /// <param name="configure">Configuration for the required capability.</param>
    /// <returns>The application builder.</returns>
    public static IApplicationBuilder UseSeneschalCapability(
        this IApplicationBuilder app,
        Action<SeneschalCapabilityOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentNullException.ThrowIfNull(configure);

        var options = new SeneschalCapabilityOptions();
        configure(options);
        options.Validate();

        return app.UseMiddleware<SeneschalCapabilityMiddleware>(
            Options.Create(options));
    }

    /// <summary>
    /// Enables Seneschal evaluation for endpoints with
    /// <see cref="RequiresCapabilityAttribute"/> metadata.
    /// </summary>
    /// <param name="app">The application builder.</param>
    /// <param name="enforcementBehavior">
    /// The behavior used when applying decisions.
    /// </param>
    /// <returns>The application builder.</returns>
    public static IApplicationBuilder UseSeneschalCapabilityAttributes(
        this IApplicationBuilder app,
        SeneschalEnforcementBehavior enforcementBehavior =
            SeneschalEnforcementBehavior.HonorDecisionMode)
    {
        ArgumentNullException.ThrowIfNull(app);

        return app.UseMiddleware<SeneschalCapabilityAttributeMiddleware>(
            enforcementBehavior);
    }
}
