namespace Dynastia.PluginHost;

public sealed class PluginManifest
{
    public string Id { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string Version { get; init; } = string.Empty;

    public int ApiVersion { get; init; }

    public string Assembly { get; init; } = string.Empty;

    public string EntryType { get; init; } = string.Empty;
}