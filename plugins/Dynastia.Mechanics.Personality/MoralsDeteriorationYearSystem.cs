using Dynastia.Contracts;

namespace Dynastia.Mechanics.Personality;

internal sealed class MoralsDeteriorationYearSystem : IYearSystem
{
    private readonly IPersonalityService _personality;
    private readonly IEconomyService _economy;
    private readonly IGameRandom _random;
    private readonly IGameEventBus _events;

    public MoralsDeteriorationYearSystem(
        IPersonalityService personality,
        IEconomyService economy,
        IGameRandom random,
        IGameEventBus events)
    {
        _personality = personality;
        _economy = economy;
        _random = random;
        _events = events;
    }

    public string Id => "personality.morals_deterioration";
    public YearPhase Phase => YearPhase.PostYear;
    public IReadOnlyCollection<string> Before => Array.Empty<string>();
    public IReadOnlyCollection<string> After => ["health.life_stress"];

    public void Execute(IGameState gameState)
    {
        var yearEvents = _events.GetEventsForYear(gameState.Year);

        foreach (var person in gameState.People.Where(p =>
            p.Tags.Has("state.alive")
            && !SimulationState.IsInactive(p)
            && p.Age >= 5
            && !p.Tags.Has("morals.evil")))
        {
            var chance = 0.0;

            if (person.Tags.Has("state.imprisoned"))
                chance += 0.012;

            if (yearEvents.Any(e =>
                e.SubjectId == person.Id
                && e.Type.Equals("wellbeing.drink", StringComparison.OrdinalIgnoreCase)))
            {
                chance += 0.015;
            }

            var stress = CalculateStress(person, yearEvents);
            if (_economy.GetHousehold(person)?.HasUnfundedBasicNeeds == true)
                stress += 1;

            // Stress should shape a minority of lives even after repeated hardship.
            chance += Math.Min(0.030, stress * 0.0035);
            chance = Math.Min(chance, 0.045);

            if (chance <= 0 || _random.NextDouble() >= chance)
                continue;

            var before = _personality.GetPersonality(person)?.Morals;
            var protectedBefore = _personality.HasMoralsProtection(person);
            var changed = _personality.ShiftMorals(person, -1);
            var after = _personality.GetPersonality(person)?.Morals;

            if (changed)
            {
                _events.Publish(new GameEvent
                {
                    Type = "personality.morals_declined",
                    Year = gameState.Year,
                    SubjectId = person.Id,
                    Data = new Dictionary<string, string>
                    {
                        ["from"] = before ?? string.Empty,
                        ["to"] = after ?? string.Empty,
                        ["suppressChronicle"] = "true",
                        ["text"] = $"A difficult period changed {person.Name}'s outlook on life."
                    }
                });
            }
            else if (protectedBefore && !_personality.HasMoralsProtection(person))
            {
                _events.Publish(new GameEvent
                {
                    Type = "personality.morals_protected",
                    Year = gameState.Year,
                    SubjectId = person.Id,
                    Data = new Dictionary<string, string>
                    {
                        ["suppressChronicle"] = "true",
                        ["text"] = $"{person.Name}'s earlier religious reflection helped them withstand a difficult period."
                    }
                });
            }
        }
    }

    private static int CalculateStress(IPerson person, IReadOnlyList<GameEvent> events)
    {
        var stress = 0;
        foreach (var e in events)
        {
            var directlyInvolved = e.SubjectId == person.Id || e.RelatedPersonIds.Contains(person.Id);
            if (!directlyInvolved)
                continue;

            if (e.Type.Equals("life.death", StringComparison.OrdinalIgnoreCase)) stress += 3;
            else if (e.Type.Contains("divorce", StringComparison.OrdinalIgnoreCase)) stress += 2;
            else if (e.Type.Equals("justice.crime", StringComparison.OrdinalIgnoreCase)) stress += 2;
            else if (e.Type.Equals("rare.wrongful_arrest", StringComparison.OrdinalIgnoreCase)) stress += 2;
            else if (e.Type is "rare.assault" or "rare.workplace_accident" or "rare.traffic_accident" or "rare.structural_accident") stress += 2;
        }
        return stress;
    }
}
