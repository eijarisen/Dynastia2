using Dynastia.Contracts;

namespace Dynastia.Mechanics.Relationships;

public sealed class RelationshipBreakupService
{
    private const double AffairHealthPenalty = 25;
    private const double AffairChildHealthPenalty = 15;
    private const double DivorceHealthPenalty = 10;

    private const string SurnamesPath =
        "Names/polish_surnames.csv";

    private readonly IFamilyService _family;
    private readonly IHealthService _health;
    private readonly IEconomyService _economy;
    private readonly IGameDataService _data;
    private readonly IGameRandom _random;
    private readonly IGameEventBus _events;

    public RelationshipBreakupService(
        IFamilyService family,
        IHealthService health,
        IEconomyService economy,
        IGameDataService data,
        IGameRandom random,
        IGameEventBus events)
    {
        _family = family;
        _health = health;
        _economy = economy;
        _data = data;
        _random = random;
        _events = events;
    }

    public GameActionResult PlayerDivorce(
        IGameState gameState,
        IPerson actor)
    {
        var spouse =
            _family.GetSpouse(actor);

        if (spouse is null)
        {
            return new GameActionResult(
                false,
                "There is no current spouse to divorce.");
        }

        var actorEventName =
            _family.GetDisplayName(actor);

        var spouseEventName =
            _family.GetDisplayName(spouse);

        var household =
            _economy.GetHousehold(actor);

        var settlement =
            household is null
                ? 0
                : Math.Floor(
                    household.Wealth / 2m);

        // Dynasty 4's event reports floor(wealth / 2),
        // but the actual balance is divided by two exactly.
        if (household is not null)
        {
            _economy.SetWealth(
                actor,
                household.Wealth / 2m);
        }

        _health.ChangeHealth(
            actor,
            -DivorceHealthPenalty);

        _health.ChangeHealth(
            spouse,
            -DivorceHealthPenalty);

        // Source behavior: ALL children stored on the actor
        // receive the penalty, not only children of this union.
        foreach (var child in
            _family.GetChildren(actor))
        {
            _health.ChangeHealth(
                child,
                -DivorceHealthPenalty);
        }

        // Source action changes the spouse's surname directly,
        // even in unusual same-sex cases.
        spouse.Surname =
            !string.IsNullOrWhiteSpace(
                spouse.MaidenName)
                ? spouse.MaidenName
                : RandomSurname();

        _family.EndRelationship(
            actor,
            spouse,
            gameState.Year,
            "divorce");

        _events.Publish(
            new GameEvent
            {
                Type =
                    "relationship.divorce",

                Year =
                    gameState.Year,

                SubjectId =
                    actor.Id,

                RelatedPersonIds =
                    [spouse.Id],

                Data =
                    new Dictionary<string, string>
                    {
                        ["settlement"] =
                            settlement.ToString(),

                        ["text"] =
                            $"{actorEventName} and " +
                            $"{spouseEventName} divorced. " +
                            $"The settlement cost ${settlement:N0}."
                    }
            });

        return new GameActionResult(
            true);
    }

    public void LowSatisfactionDivorce(
        IGameState gameState,
        IPerson husband,
        IPerson wife,
        double satisfaction)
    {
        if (_family.GetSpouse(
                husband)?.Id
                != wife.Id)
        {
            return;
        }

        var husbandEventName =
            _family.GetDisplayName(
                husband);

        var wifeEventName =
            _family.GetDisplayName(
                wife);

        var household =
            _economy.GetHousehold(
                husband);

        var settlement =
            household is null
                ? 0
                : Math.Floor(
                    household.Wealth / 2m);

        if (household is not null)
        {
            _economy.SetWealth(
                husband,
                household.Wealth / 2m);
        }

        _health.ChangeHealth(
            husband,
            -DivorceHealthPenalty);

        _health.ChangeHealth(
            wife,
            -DivorceHealthPenalty);

        foreach (var child in
            _family.GetChildren(
                husband))
        {
            _health.ChangeHealth(
                child,
                -DivorceHealthPenalty);
        }

        wife.Surname =
            !string.IsNullOrWhiteSpace(
                wife.MaidenName)
                ? wife.MaidenName
                : RandomSurname();

        _family.EndRelationship(
            husband,
            wife,
            gameState.Year,
            "divorce");

        _events.Publish(
            new GameEvent
            {
                Type =
                    "relationship.low_satisfaction_divorce",

                Year =
                    gameState.Year,

                SubjectId =
                    husband.Id,

                RelatedPersonIds =
                    [wife.Id],

                Data =
                    new Dictionary<string, string>
                    {
                        ["settlement"] =
                            settlement.ToString(),

                        ["satisfaction"] =
                            satisfaction.ToString(
                                "0"),

                        ["text"] =
                            $"{husbandEventName} and " +
                            $"{wifeEventName} divorced after " +
                            "their marriage deteriorated. " +
                            $"The settlement cost " +
                            $"{settlement:N0} zł."
                    }
            });
    }

    public void AffairDivorce(
        IGameState gameState,
        IPerson actor,
        IPerson spouse)
    {
        var actorEventName =
            _family.GetDisplayName(actor);

        var spouseEventName =
            _family.GetDisplayName(spouse);

        decimal settlement =
            0;

        if (_family.GetSex(actor)
                == Sex.Male
            && _family.IsMaleLineage(
                actor))
        {
            var household =
                _economy.GetHousehold(
                    actor);

            if (household is not null)
            {
                settlement =
                    Math.Floor(
                        household.Wealth / 2m);

                _economy.ChangeWealth(
                    actor,
                    -settlement);

                if (_family.GetSex(spouse)
                    == Sex.Female)
                {
                    _economy.ChangePendingInheritance(
                        spouse,
                        settlement);
                }
            }
        }

        _health.ChangeHealth(
            actor,
            -AffairHealthPenalty);

        _health.ChangeHealth(
            spouse,
            -AffairHealthPenalty);

        // Only children shared by both spouses receive grief.
        var spouseChildIds =
            _family.GetChildren(spouse)
                .Select(child => child.Id)
                .ToHashSet();

        foreach (var child in
            _family.GetChildren(actor))
        {
            if (spouseChildIds.Contains(
                child.Id))
            {
                _health.ChangeHealth(
                    child,
                    -AffairChildHealthPenalty);
            }
        }

        // Source selects "wife" as:
        // actor if Female, otherwise spouse.
        var wife =
            _family.GetSex(actor)
                == Sex.Female
                    ? actor
                    : spouse;

        wife.Surname =
            !string.IsNullOrWhiteSpace(
                wife.MaidenName)
                ? wife.MaidenName
                : RandomSurname();

        _family.EndRelationship(
            actor,
            spouse,
            gameState.Year,
            "divorce");

        var pronoun =
            _family.GetSex(actor)
                == Sex.Male
                    ? "him"
                    : "her";

        _events.Publish(
            new GameEvent
            {
                Type =
                    "relationship.affair",

                Year =
                    gameState.Year,

                SubjectId =
                    actor.Id,

                RelatedPersonIds =
                    [spouse.Id],

                Data =
                    new Dictionary<string, string>
                    {
                        ["settlement"] =
                            settlement.ToString(),

                        ["text"] =
                            $"{actorEventName} " +
                            $"was caught having an affair! " +
                            $"{spouseEventName} " +
                            $"divorced {pronoun} immediately."
                    }
            });
    }

    private string RandomSurname()
    {
        var entries =
            _data.GetWeightedStringList(
                SurnamesPath);

        var totalWeight =
            entries.Sum(
                entry =>
                    (double)entry.Weight);

        var roll =
            _random.NextDouble()
            * totalWeight;

        foreach (var entry in
            entries)
        {
            if (roll < entry.Weight)
                return entry.Value;

            roll -= entry.Weight;
        }

        return entries[^1].Value;
    }
}
