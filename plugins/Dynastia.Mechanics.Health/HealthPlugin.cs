using Dynastia.Contracts;

namespace Dynastia.Mechanics.Health;

public sealed class HealthPlugin : IGamePlugin
{
    public void Initialize(IGamePluginContext context)
    {
        var healthService =
            new StandardHealthService();

        context.AddService<IHealthService>(
            healthService);

        context.Log("Health mechanics registered.");
    }
}
