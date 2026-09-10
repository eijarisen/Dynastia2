using Dynastia.Contracts;

namespace Dynastia.Mechanics.Aging;

public sealed class AgingPlugin : IGamePlugin
{
    public void Initialize(IGamePluginContext context)
    {
        var registry =
            context.GetService<IYearSystemRegistry>();

        if (registry is null)
        {
            throw new InvalidOperationException(
                "Year system registry is unavailable.");
        }

        registry.Register(new AgingSystem());

        context.Log("Aging mechanics registered.");
    }
}