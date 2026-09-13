using Dynastia.Contracts;

namespace Dynastia.Mechanics.Personality;

internal sealed class MoralsDeteriorationYearSystem :
    IYearSystem
{
    private readonly StandardPersonalityService _personality;
    private readonly IGameRandom _random;
    private readonly IGameEventBus _events;

    public MoralsDeteriorationYearSystem(
        StandardPersonalityService personality,
        IGameRandom random,
        IGameEventBus events)
    {
        _personality = personality;
        _random = random;
        _events = events;
    }

    public string Id =>
        "personality.morals_deterioration";

    public YearPhase Phase =>
        YearPhase.PostYear;

    public IReadOnlyCollection<string> Before =>
        Array.Empty<string>();

    public IReadOnlyCollection<string> After =>
        ["health.life_stress"];

    public void Execute(
        IGameState gameState)
    {
        var events =
            _events.GetEventsForYear(
                gameState.Year);

        foreach (var person in
            gameState.People.Where(
                person =>
                    person.Tags.Has("state.alive")
                    && person.Age >= 5
                    && !SimulationState.IsInactive(person)))
        {
            var chance = 0.0;

            if (events.Any(
                gameEvent =>
                    gameEvent.SubjectId == person.Id
                    && gameEvent.Type.Equals(
                        "wellbeing.drink",
                        StringComparison.OrdinalIgnoreCase)))
            {
                chance += 0.015;
            }

            if (person.Tags.Has(
                    "state.imprisoned")
                || events.Any(
                    gameEvent =>
                        gameEvent.SubjectId == person.Id
                        && gameEvent.Type.Equals(
                            "justice.released",
                            StringComparison.OrdinalIgnoreCase)))
            {
                chance += 0.010;
            }

            var stress =
                events
                    .Where(
                        gameEvent =>
                            gameEvent.SubjectId == person.Id
                            && gameEvent.Type.Equals(
                                "health.life_stress_evaluated",
                                StringComparison.OrdinalIgnoreCase))
                    .Select(
                        gameEvent =>
                            gameEvent.Data.TryGetValue(
                                "lifeStress",
                                out var raw)
                            && int.TryParse(raw, out var value)
                                ? value
                                : 0)
                    .DefaultIfEmpty(0)
                    .Max();

            chance += Math.Min(
                0.030,
                stress * 0.0015);

            chance = Math.Min(
                0.05,
                chance);

            if (chance <= 0
                || _random.NextDouble() >= chance)
            {
                continue;
            }

            var before =
                _personality.GetPersonality(
                    person)
                ?.Morals;

            var changed =
                _personality.ShiftMorals(
                    person,
                    -1);

            var after =
                _personality.GetPersonality(
                    person)
                ?.Morals;

            _events.Publish(
                new GameEvent
                {
                    Type = changed
                        ? "personality.morals_declined"
                        : "personality.morals_protected",
                    Year = gameState.Year,
                    SubjectId = person.Id,
                    Data = new Dictionary<string, string>
                    {
                        ["suppressChronicle"] = "true",
                        ["before"] = before ?? string.Empty,
                        ["after"] = after ?? string.Empty
                    }
                });
        }
    }
}
