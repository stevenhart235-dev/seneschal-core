using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
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
                ApiKeyHeaderName = options.ApiKeyHeaderName,
                Timeout = options.Timeout
            }));
        services
            .AddHttpClient<ISeneschalClient, SeneschalClient>()
            .ConfigureHttpClient(client => client.Timeout = options.Timeout);

        return services;
    }

    /// <summary>
    /// Registers Seneschal from an application configuration section.
    /// </summary>
    public static IServiceCollection AddSeneschal(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        return services.AddSeneschal(options =>
        {
            var baseUrl = configuration["BaseUrl"];
            if (!string.IsNullOrWhiteSpace(baseUrl))
            {
                if (!Uri.TryCreate(
                        baseUrl,
                        UriKind.RelativeOrAbsolute,
                        out var parsedBaseUrl))
                {
                    throw new InvalidOperationException(
                        "Seneschal configuration is invalid: BaseUrl must be a valid absolute URI.");
                }

                options.BaseUrl = parsedBaseUrl;
            }

            options.ApiKey = configuration["ApiKey"];
            options.DefaultEnvironment = configuration["DefaultEnvironment"];

            if (Enum.TryParse<SeneschalFailureBehavior>(
                    configuration["FailureBehavior"],
                    ignoreCase: true,
                    out var failureBehavior))
            {
                options.FailureBehavior = failureBehavior;
            }
            else if (!string.IsNullOrWhiteSpace(configuration["FailureBehavior"]))
            {
                throw new InvalidOperationException(
                    "Seneschal configuration is invalid: FailureBehavior must be FailClosed or FailOpen.");
            }

            if (TimeSpan.TryParse(configuration["Timeout"], out var timeout))
            {
                options.Timeout = timeout;
            }
        });
    }
}
