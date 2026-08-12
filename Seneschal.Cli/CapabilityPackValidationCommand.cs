using Seneschal.Api.Services;

public static class CapabilityPackValidationCommand
{
    public static Task<int> RunAsync(string[] args, TextWriter? output = null)
    {
        output ??= Console.Out;
        if (args.Length != 1)
        {
            WriteUsage(output);
            return Task.FromResult(2);
        }

        var path = Path.GetFullPath(args[0]);
        try
        {
            var yaml = File.ReadAllText(path);
            var schemaFindings = CapabilityPackSchemaValidator.Validate(yaml);
            if (schemaFindings.Count > 0)
            {
                output.WriteLine("Capability pack validation: FAILED");
                output.WriteLine();
                foreach (var finding in schemaFindings)
                {
                    output.WriteLine($"ERROR   {path}");
                    output.WriteLine($"        Capability Pack v1 violation at {finding.Path}: {finding.Issue}");
                }
                output.WriteLine($"{schemaFindings.Count} errors, 0 warnings");
                return Task.FromResult(1);
            }

            var pack = CapabilityLoader.LoadPack(path);
            var validation = ConfigurationValidator.ValidateCapabilities(
                pack.Capabilities);

            output.WriteLine($"Capability pack validation: {(validation.IsValid ? "VALID" : "FAILED")}");
            output.WriteLine();
            output.WriteLine($"Pack:    {pack.Pack.Id}");
            output.WriteLine($"Version: {pack.Pack.Version}");
            output.WriteLine($"File:    {path}");
            output.WriteLine();
            foreach (var finding in validation.Findings)
            {
                output.WriteLine($"{finding.Severity.ToUpperInvariant(),-7} {finding.RelatedObjectId ?? pack.Pack.Id}");
                output.WriteLine($"        {finding.Message}");
            }
            output.WriteLine($"{validation.ErrorCount} errors, {validation.WarningCount} warnings");
            return Task.FromResult(validation.IsValid ? 0 : 1);
        }
        catch (Exception exception) when (exception is IOException or
            UnauthorizedAccessException or YamlDotNet.Core.YamlException)
        {
            output.WriteLine("Capability pack validation: FAILED");
            output.WriteLine();
            output.WriteLine($"ERROR   {path}");
            output.WriteLine($"        {exception.Message}");
            return Task.FromResult(1);
        }
    }

    private static void WriteUsage(TextWriter output)
    {
        output.WriteLine("Usage:");
        output.WriteLine("  seneschal capability pack validate <path>");
    }
}
