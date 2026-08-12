public static class PolicyInitCommand
{
    public const string GeneratedDocument = """
        policies:
          - name: example-policy
            identity: Developer
            capability: DeployApplication
            environment: dev
            decision: allow
            reason: Example policy created by Seneschal.
        """;

    public static async Task<int> RunAsync(
        string[] args,
        TextWriter? output = null)
    {
        output ??= Console.Out;
        if (!TryParse(args, out var path, out var force))
        {
            await output.WriteLineAsync(
                "Usage: seneschal policy init <path> [--force]");
            return 1;
        }

        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(path!);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or
                ArgumentException or NotSupportedException)
        {
            await WriteCreationFailureAsync(output, path!);
            return 2;
        }

        if (File.Exists(fullPath) && !force)
        {
            await output.WriteLineAsync($"Policy file already exists: {path}");
            await output.WriteLineAsync("No changes were made.");
            return 2;
        }

        try
        {
            var directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            File.WriteAllText(fullPath, GeneratedDocument + Environment.NewLine);

            var findings = PolicySchemaValidator.Validate(
                File.ReadAllText(fullPath));
            if (findings.Count > 0)
            {
                await output.WriteLineAsync(
                    "Generated policy document failed Policy Schema v1 validation.");
                return 2;
            }
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or
                ArgumentException or NotSupportedException)
        {
            await WriteCreationFailureAsync(output, path!);
            return 2;
        }

        await output.WriteLineAsync("Created Policy Schema v1 document:");
        await output.WriteLineAsync($"  {path}");
        await output.WriteLineAsync();
        await output.WriteLineAsync($"Schema:   {PolicySchemaValidator.ContractVersion}");
        await output.WriteLineAsync($"Revision: {PolicySchemaValidator.ContractRevision}");
        await output.WriteLineAsync("Status:   Valid");
        await output.WriteLineAsync();
        await output.WriteLineAsync("Next:");
        await output.WriteLineAsync($"  seneschal policy validate {path}");
        await output.WriteLineAsync("  seneschal policy simulate ...");
        return 0;
    }

    private static async Task WriteCreationFailureAsync(
        TextWriter output,
        string path)
    {
        await output.WriteLineAsync($"Policy file could not be created: {path}");
        await output.WriteLineAsync("No runtime state was changed.");
    }

    private static bool TryParse(
        string[] args,
        out string? path,
        out bool force)
    {
        path = null;
        force = false;
        foreach (var arg in args)
        {
            if (arg.Equals("--force", StringComparison.OrdinalIgnoreCase))
            {
                if (force) return false;
                force = true;
            }
            else if (arg.StartsWith("--", StringComparison.Ordinal) ||
                     path is not null ||
                     string.IsNullOrWhiteSpace(arg))
            {
                return false;
            }
            else
            {
                path = arg;
            }
        }

        return path is not null;
    }
}
