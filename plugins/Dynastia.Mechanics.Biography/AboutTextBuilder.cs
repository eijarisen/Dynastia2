using Dynastia.Contracts;

namespace Dynastia.Mechanics.Biography;

public sealed partial class AboutTextBuilder
{
    private const int AdultAge =
        18;

    private const int ElderAge =
        60;

    private const int YoungChildAge =
        10;

    // Used only internally to compare a deceased person's actual lifespan
    // with their Longevity profile. The numeric estimate is never exposed
    // in About text.
    private const int BaseLifespan =
        40;

    private const int LongevityMultiplier =
        10;

    private readonly IGameState _gameState;
    private readonly IFamilyService _family;
    private readonly IStatsService _stats;
    private readonly ILocationService _locations;

    public AboutTextBuilder(
        IGameState gameState,
        IFamilyService family,
        IStatsService stats,
        ILocationService locations)
    {
        _gameState =
            gameState;

        _family =
            family;

        _stats =
            stats;

        _locations =
            locations;
    }

    public string Build(
        IPerson person)
    {
        var stats =
            _stats.GetStats(
                person)
            .ToDictionary(
                stat =>
                    stat.Id,
                stat =>
                    stat.Value,
                StringComparer.OrdinalIgnoreCase);

        var sex =
            _family.GetSex(
                person);

        var pronouns =
            Pronouns.For(
                sex);

        var isAlive =
            person.Tags.Has(
                "state.alive")
            && !person.Tags.Has(
                "state.dead");

        var location =
            _locations.GetLocation(
                person);

        var sentences =
            new List<string>
            {
                OpeningDescription(
                    person,
                    location,
                    pronouns),

                PersonalityDescription(
                    person,
                    isAlive,
                    pronouns),

                ImmunityDescription(
                    stats["immunity"],
                    isAlive,
                    pronouns),

                ImmunitySuperpowerDescription(
                    stats["immunity"],
                    isAlive,
                    pronouns),

                LongevityDescription(
                    person,
                    stats["longevity"],
                    isAlive,
                    pronouns),

                LongevitySuperpowerDescription(
                    stats["longevity"],
                    isAlive,
                    pronouns),

                FertilityDescription(
                    person,
                    stats["fertility"],
                    isAlive,
                    sex,
                    pronouns),

                FertilitySuperpowerDescription(
                    stats["fertility"],
                    isAlive,
                    sex,
                    pronouns),

                AppealDescription(
                    person,
                    stats["appeal"],
                    isAlive,
                    pronouns),

                AppealSuperpowerDescription(
                    stats["appeal"],
                    isAlive,
                    pronouns),

                StrengthDescription(
                    person,
                    stats["strength"],
                    isAlive,
                    pronouns),

                StrengthSuperpowerDescription(
                    stats["strength"],
                    isAlive,
                    pronouns),

                IntellectDescription(
                    stats["intellect"],
                    isAlive,
                    pronouns),

                IntellectSuperpowerDescription(
                    stats["intellect"],
                    isAlive,
                    pronouns)
            };

        return string.Join(
            " ",
            sentences
                .Where(
                    sentence =>
                        !string.IsNullOrWhiteSpace(
                            sentence))
                .Select(
                    sentence =>
                        sentence.Trim()));
    }

}
