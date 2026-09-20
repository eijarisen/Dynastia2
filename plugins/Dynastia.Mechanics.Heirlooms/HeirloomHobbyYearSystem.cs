using Dynastia.Contracts;

namespace Dynastia.Mechanics.Heirlooms;

internal sealed class HeirloomHobbyYearSystem : IYearSystem
{
    private readonly IEconomyService _economy;
    private readonly IFamilyService _family;
    private readonly IHobbyService _hobbies;
    private readonly IHeirloomService _heirlooms;
    private readonly IGameRandom _random;
    private readonly HeirloomEventCatalog _catalog;

    public HeirloomHobbyYearSystem(
        IEconomyService economy,
        IFamilyService family,
        IHobbyService hobbies,
        IHeirloomService heirlooms,
        IGameRandom random,
        HeirloomEventCatalog catalog)
    {
        _economy = economy;
        _family = family;
        _hobbies = hobbies;
        _heirlooms = heirlooms;
        _random = random;
        _catalog = catalog;
    }

    public string Id => "heirlooms.hobby_keepsakes";
    public YearPhase Phase => YearPhase.LifeEvents;
    public IReadOnlyCollection<string> Before => [];
    public IReadOnlyCollection<string> After => [];

    public void Execute(IGameState gameState)
    {
        foreach (var person in gameState.People)
        {
            if (!person.Tags.Has("state.alive")
                || _economy.GetHouseholdId(person) is null)
            {
                continue;
            }

            var snapshot = _hobbies.GetHobbies(person);
            foreach (var hobby in snapshot.Hobbies)
            {
                if (!_catalog.Hobbies.TryGetValue(hobby.Id, out var rule)
                    || person.Age < rule.MinimumAge)
                {
                    continue;
                }

                var triggerId = $"hobby:{person.Id}:{hobby.Id}";
                if (HasCompleted(person, triggerId)
                    || !_random.Chance(rule.AnnualChance))
                {
                    continue;
                }

                _heirlooms.Create(
                    person,
                    new HeirloomCreationRequest(
                        Choose(rule.TemplateIds),
                        person.Id,
                        "hobby",
                        triggerId,
                        $"Created or collected through {_family.GetDisplayName(person)}'s long-term {hobby.Name} hobby.",
                        NewsTokens: new Dictionary<string, string>
                        {
                            ["Hobby"] = hobby.Name
                        }));
                MarkCompleted(person, triggerId);
            }
        }
    }

    private string Choose(IReadOnlyList<string> values) =>
        values[_random.NextInt(0, values.Count - 1)];

    private static bool HasCompleted(IPerson person, string triggerId) =>
        person.Components.Get<HeirloomPersonTriggerComponent>()?.CompletedTriggerIds.Contains(triggerId)
        ?? false;

    private static void MarkCompleted(IPerson person, string triggerId)
    {
        var component = person.Components.Get<HeirloomPersonTriggerComponent>();
        if (component is null)
        {
            component = new HeirloomPersonTriggerComponent();
            person.Components.Set(component);
        }
        component.CompletedTriggerIds.Add(triggerId);
    }
}
