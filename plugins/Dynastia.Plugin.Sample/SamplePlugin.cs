using Dynastia.Contracts;

namespace Dynastia.Plugin.Sample;

public sealed class SamplePlugin : IGamePlugin
{
    public void Initialize(IGamePluginContext context)
    {
        context.Log("Sample plugin initialized successfully.");
    }
}