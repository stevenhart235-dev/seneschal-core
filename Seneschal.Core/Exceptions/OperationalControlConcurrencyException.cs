namespace Seneschal.Core.Exceptions;

public sealed class OperationalControlConcurrencyException(
    string control, long expectedVersion, long actualVersion) : Exception(
        $"The {control} state changed after it was read (expected version {expectedVersion}, actual version {actualVersion}). Refresh and retry.")
{
    public string Control { get; } = control;
    public long ExpectedVersion { get; } = expectedVersion;
    public long ActualVersion { get; } = actualVersion;
}
