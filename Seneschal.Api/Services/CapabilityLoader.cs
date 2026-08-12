using System.Text.RegularExpressions;
using Seneschal.Api.Models;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Seneschal.Api.Services;

public class CapabilityLoader
{
    private static readonly Regex PackIdPattern = new(
        "^[a-z0-9][a-z0-9.-]*$", RegexOptions.CultureInvariant);
    private static readonly Regex PackVersionPattern = new(
        "^(0|[1-9][0-9]*)\\.(0|[1-9][0-9]*)\\.(0|[1-9][0-9]*)$",
        RegexOptions.CultureInvariant);
    private readonly List<LoadedCapability> _catalog;

    public CapabilityLoader()
        : this(YamlConfigurationPathResolver.Resolve(
            AppContext.BaseDirectory, null, "capabilities.yaml"), null)
    {
    }

    public CapabilityLoader(IHostEnvironment environment, IConfiguration configuration)
        : this(
            YamlConfigurationPathResolver.Resolve(
                environment.ContentRootPath,
                configuration[YamlConfigurationPathResolver.CapabilitiesPathKey],
                "capabilities.yaml"),
            YamlConfigurationPathResolver.ResolveOptional(
                environment.ContentRootPath,
                configuration[YamlConfigurationPathResolver.CapabilityPacksPathKey]))
    {
    }

    public CapabilityLoader(string path) : this(path, null)
    {
    }

    public CapabilityLoader(string path, string? packsPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var localPath = Path.GetFullPath(path);
        var local = Deserialize<CapabilityFile>(localPath);
        _catalog = local.Capabilities.Select(capability => new LoadedCapability
        {
            Capability = capability,
            Sources = [LocalSource(localPath)]
        }).ToList();

        MergePacks(ResolvePackFiles(packsPath));
    }

    public IReadOnlyList<Capability> GetCapabilities() =>
        _catalog.Select(item => item.Capability).ToList();

    public IReadOnlyList<LoadedCapability> GetCatalogDefinitions() => _catalog;

    public static CapabilityPackFile LoadPack(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var pack = Deserialize<CapabilityPackFile>(Path.GetFullPath(path));
        ValidatePackMetadata(pack, path);
        return pack;
    }

    private void MergePacks(IEnumerable<string> packFiles)
    {
        var sourceIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in packFiles)
        {
            var pack = LoadPack(path);
            var sourceId = $"{pack.Pack.Id}@{pack.Pack.Version}";
            if (!sourceIds.Add(sourceId))
                throw new InvalidDataException($"Duplicate capability pack '{sourceId}'.");

            var namesInPack = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var capability in pack.Capabilities)
            {
                if (!namesInPack.Add(capability.Name))
                    throw new InvalidDataException(
                        $"Capability pack '{sourceId}' contains duplicate capability id '{capability.Name}'.");

                var existing = _catalog.FirstOrDefault(item => string.Equals(
                    item.Capability.Name, capability.Name,
                    StringComparison.OrdinalIgnoreCase));
                var source = PackSource(path, pack.Pack);
                if (existing is null)
                {
                    _catalog.Add(new LoadedCapability
                    {
                        Capability = capability,
                        Sources = [source]
                    });
                    continue;
                }

                if (!DefinitionsEqual(existing.Capability, capability))
                    throw new InvalidDataException(
                        $"Conflicting capability id '{capability.Name}' in pack '{sourceId}'.");

                var index = _catalog.IndexOf(existing);
                _catalog[index] = existing with
                {
                    Sources = existing.Sources.Concat([source]).ToList()
                };
            }
        }
    }

    private static IEnumerable<string> ResolvePackFiles(string? packsPath)
    {
        if (string.IsNullOrWhiteSpace(packsPath)) return [];
        var path = Path.GetFullPath(packsPath);
        if (File.Exists(path)) return [path];
        if (!Directory.Exists(path))
            throw new DirectoryNotFoundException(
                $"Capability packs path was not found: {path}");
        return Directory.EnumerateFiles(path, "*.yaml")
            .Concat(Directory.EnumerateFiles(path, "*.yml"))
            .OrderBy(file => file, StringComparer.Ordinal);
    }

    private static void ValidatePackMetadata(CapabilityPackFile pack, string path)
    {
        if (pack.Pack is null)
            throw new InvalidDataException(
                $"Capability pack '{path}' is missing required pack metadata.");
        var packId = pack.Pack.Id ?? string.Empty;
        var packVersion = pack.Pack.Version ?? string.Empty;
        if (!PackIdPattern.IsMatch(packId))
            throw new InvalidDataException(
                $"Capability pack '{path}' has missing or invalid pack id '{packId}'.");
        if (!PackVersionPattern.IsMatch(packVersion))
            throw new InvalidDataException(
                $"Capability pack '{packId}' has invalid version '{packVersion}'. Expected MAJOR.MINOR.PATCH.");
        if (pack.Capabilities is null || pack.Capabilities.Count == 0)
            throw new InvalidDataException(
                $"Capability pack '{packId}' contains no capabilities.");
    }
    private static T Deserialize<T>(string path)
    {
        var yaml = File.ReadAllText(path);
        return new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build()
            .Deserialize<T>(yaml) ?? throw new InvalidDataException(
                $"Configuration file '{path}' is empty.");
    }

    private static CapabilitySource LocalSource(string path) => new()
    {
        Kind = "LocalCatalog",
        Path = path
    };

    private static CapabilitySource PackSource(
        string path, CapabilityPackMetadata pack) => new()
    {
        Kind = "CapabilityPack",
        PackId = pack.Id,
        PackVersion = pack.Version,
        Path = Path.GetFullPath(path)
    };

    private static bool DefinitionsEqual(Capability left, Capability right) =>
        Equal(left.Name, right.Name) &&
        Equal(left.DisplayName, right.DisplayName) &&
        Equal(left.Description, right.Description) &&
        Equal(left.Risk, right.Risk) &&
        Equal(left.Category, right.Category) &&
        Equal(left.Owner, right.Owner) &&
        Equal(left.Lifecycle, right.Lifecycle) &&
        Equal(left.DocumentationUrl, right.DocumentationUrl) &&
        Equal(left.Technology, right.Technology) &&
        (left.Tags ?? []).SequenceEqual(right.Tags ?? [],
            StringComparer.OrdinalIgnoreCase);

    private static bool Equal(string? left, string? right) =>
        string.Equals(left ?? string.Empty, right ?? string.Empty,
            StringComparison.OrdinalIgnoreCase);
}
