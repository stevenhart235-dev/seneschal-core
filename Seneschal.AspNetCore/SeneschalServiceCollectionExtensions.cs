using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Seneschal.Client;

namespace Seneschal.AspNetCore;

/// <summary>
/// Extension methods for registering the recommended Seneschal services.
/// </summary>
public static class SeneschalServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Seneschal client and ASP.NET Core integration.
    /// </summary>
    /// <param name="services">The application service collection.</param>
    /// <param name="configure">The Seneschal configuration.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddSeneschal(
        this IServiceCollection services,
        Action<SeneschalOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        var options = new SeneschalOptions();
        configure(options);
        options.Validate();

        services.AddSingleton<IOptions<SeneschalOptions>>(
            Options.Create(options));
        services.AddSingleton<IOptions<SeneschalClientOptions>>(
            Options.Create(new SeneschalClientOptions
            {
                BaseUrl = options.BaseUrl,
                ApiKey = options.ApiKey,
                EvaluatePath = options.EvaluatePath,
                ApiKeyHeaderName = options.ApiKeyHeaderName
            }));
        services.AddHttpClient<ISeneschalClient, SeneschalClient>();

        return services;
    }
}
