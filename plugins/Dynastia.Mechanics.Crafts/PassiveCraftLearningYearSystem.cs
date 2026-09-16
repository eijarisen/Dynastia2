using Dynastia.Contracts;

namespace Dynastia.Mechanics.Crafts;

internal sealed class PassiveCraftLearningYearSystem : IYearSystem
{
    private readonly StandardCraftService _crafts;
    private readonly IFamilyService _family;
    private readonly IEconomyService _economy;
    private readonly IGameRandom _random;
    private readonly IGameEventBus _events;

    public PassiveCraftLearningYearSystem(
        StandardCraftService crafts,
        IFamilyService family,
        IEconomyService economy,
        IGameRandom random,
        IGameEventBus events)
    {
        _crafts = crafts;
        _family = family;
        _economy = economy;
        _random = random;
        _events = events;
    }

    public string Id => "craft.passive_learning";
    public YearPhase Phase => YearPhase.Status;
    public IReadOnlyCollection<string> Before => Array.Empty<string>();
    public IReadOnlyCollection<string> After => Array.Empty<string>();

    public void Execute(IGameState gameState)
    {
        foreach (var child in gameState.People.Where(person =>
                     person.Tags.Has("state.alive")
                     && person.Age is >= 10 and <= 17
                     && !SimulationState.IsInactive(person)).ToList())
        {
            if (_crafts.GetKnownCrafts(child).Count >= CraftRules.MaximumCrafts)
                continue;

            var householdId = _economy.GetHouseholdId(child);
            if (householdId is null)
                continue;

            var teachers = new[] { _family.GetFather(child), _family.GetMother(child) }
                .Where(parent => parent is not null
                    && parent.Tags.Has("state.alive")
                    && _economy.GetHouseholdId(parent) == householdId)
                .Cast<IPerson>()
                .ToList();

            var choices = teachers
                .SelectMany(parent => _crafts.GetKnownCrafts(parent)
                    .Where(craft => craft.StartYear <= gameState.Year
                        && !_crafts.KnowsCraft(child, craft.Id))
                    .Select(craft => (Teacher: parent, Craft: craft)))
                .GroupBy(choice => choice.Craft.Id, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .ToList();

            if (choices.Count == 0 || _random.NextDouble() >= CraftRules.PassiveLearningChance)
                continue;

            var selected = choices[_random.NextInt(0, choices.Count - 1)];
            if (!_crafts.LearnCraft(child, selected.Craft.Id))
                continue;

            _events.Publish(new GameEvent
            {
                Type = "craft.learned",
                Year = gameState.Year,
                SubjectId = child.Id,
                RelatedPersonIds = [selected.Teacher.Id],
                Data = new Dictionary<string, string>
                {
                    ["craftId"] = selected.Craft.Id,
                    ["craftName"] = selected.Craft.Name,
                    ["teacherId"] = selected.Teacher.Id.ToString(),
                    ["learningMode"] = "passive",
                    ["text"] =
                        $"At age {child.Age}, {_family.GetDisplayName(child)} learned {selected.Craft.Name} from {_family.GetDisplayName(selected.Teacher)}."
                }
            });
        }
    }
}
