namespace Seneschal.Api.Services;

internal static class YamlConfigurationPathResolver
{
    public const string CapabilitiesPathKey =
        "Seneschal:Configuration:CapabilitiesPath";
    public const string IdentitiesPathKey =
        "Seneschal:Configuration:IdentitiesPath";
    public const string PoliciesPathKey =
        "Seneschal:Configuration:PoliciesPath";
    public const string IntegrationKeysPathKey =
        "Seneschal:Configuration:IntegrationKeysPath";

    public static string Resolve(
        string contentRootPath,
        string? configuredPath,
        string defaultFileName,
        string? applicationBasePath = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contentRootPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(defaultFileName);

        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            return Path.IsPathRooted(configuredPath)
                ? Path.GetFullPath(configuredPath)
                : Path.GetFullPath(configuredPath, contentRootPath);
        }

        var relativePath = Path.Combine("Policies", defaultFileName);
        var contentRootCandidate = Path.GetFullPath(
            relativePath,
            contentRootPath);

        if (File.Exists(contentRootCandidate))
        {
            return contentRootCandidate;
        }

        return Path.GetFullPath(
            relativePath,
            applicationBasePath ?? AppContext.BaseDirectory);
    }
}
