using Dynastia.Contracts;

namespace Dynastia.Mechanics.ReligiousVocation;

public sealed class ReligiousVocationPlugin : IGamePlugin
{
    public void Initialize(IGamePluginContext context)
    {
        var family = Required<IFamilyService>(context, "Family service");
        var career = Required<ICareerService>(context, "Career service");
        var locations = Required<ILocationService>(context, "Location service");
        var institutions = Required<ITownInstitutionService>(context, "Town institution service");
        var random = Required<IGameRandom>(context, "Random service");
        var events = Required<IGameEventBus>(context, "Event bus");
        var systems = Required<IYearSystemRegistry>(context, "Year-system registry");
        var data = Required<IGameDataService>(context, "Game data service");

        var rules = ReligiousCallingRules.Load(data);
        systems.Register(new ReligiousVocationYearSystem(
            family,
            career,
            locations,
            institutions,
            random,
            events,
            rules));

        context.Log("Religious vocation mechanics registered.");
    }

    private static T Required<T>(IGamePluginContext context, string label)
        where T : class =>
        context.GetService<T>()
        ?? throw new InvalidOperationException($"{label} is unavailable.");
}
