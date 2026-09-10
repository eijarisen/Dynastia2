using Dynastia.Contracts;

namespace Dynastia.PluginHost;

public sealed record LoadedPlugin(
    PluginManifest Manifest,
    IGamePlugin Instance);