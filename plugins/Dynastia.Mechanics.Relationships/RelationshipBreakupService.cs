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

        if (actor.Tags.Has("morals.good")
            && _random.NextDouble() < 0.10)
        {
            _events.Publish(
                new GameEvent
                {
                    Type = "relationship.divorce_refused",
                    Year = gameState.Year,
                    SubjectId = actor.Id,
                    RelatedPersonIds = [spouse.Id],
                    Data = new Dictionary<string, string>
                    {
                        ["text"] =
                            $"{_family.GetDisplayName(actor)} could not " +
                            "bring themselves to go through with the divorce."
                    }
                });

            return new GameActionResult(true);
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

        ApplyBreakupHealthShock(
            actor,
            spouse,
            DivorceHealthPenalty,
            DivorceHealthPenalty);

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
                            $"The settlement cost {settlement:N0} zł."
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

        ApplyBreakupHealthShock(
            husband,
            wife,
            DivorceHealthPenalty,
            DivorceHealthPenalty);

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

        ApplyBreakupHealthShock(
            actor,
            spouse,
            AffairHealthPenalty,
            AffairChildHealthPenalty);

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


    private void ApplyBreakupHealthShock(
        IPerson first,
        IPerson second,
        double directPenalty,
        double childPenalty)
    {
        ApplyEmotionalHealthLoss(
            first,
            second,
            directPenalty,
            extendedRelative: false);

        ApplyEmotionalHealthLoss(
            second,
            first,
            directPenalty,
            extendedRelative: false);

        var relatives =
            new Dictionary<Guid, (IPerson Person, IPerson Reference, double Penalty)>();

        AddExtendedRelatives(
            first,
            childPenalty,
            relatives);

        AddExtendedRelatives(
            second,
            childPenalty,
            relatives);

        relatives.Remove(first.Id);
        relatives.Remove(second.Id);

        foreach (var item in relatives.Values)
        {
            ApplyEmotionalHealthLoss(
                item.Person,
                item.Reference,
                item.Penalty,
                extendedRelative: true);
        }
    }

    private void AddExtendedRelatives(
        IPerson subject,
        double childPenalty,
        IDictionary<Guid, (IPerson Person, IPerson Reference, double Penalty)> relatives)
    {
        foreach (var child in _family.GetChildren(subject))
        {
            AddRelative(
                child,
                subject,
                childPenalty,
                relatives);
        }

        var familyPenalty =
            childPenalty * 0.70;

        var father = _family.GetFather(subject);
        var mother = _family.GetMother(subject);

        AddRelative(
            father,
            subject,
            familyPenalty,
            relatives);

        AddRelative(
            mother,
            subject,
            familyPenalty,
            relatives);

        var siblingIds =
            new HashSet<Guid>();

        if (father is not null)
        {
            foreach (var sibling in _family.GetChildren(father))
            {
                if (sibling.Id != subject.Id
                    && siblingIds.Add(sibling.Id))
                {
                    AddRelative(
                        sibling,
                        subject,
                        familyPenalty,
                        relatives);
                }
            }
        }

        if (mother is not null)
        {
            foreach (var sibling in _family.GetChildren(mother))
            {
                if (sibling.Id != subject.Id
                    && siblingIds.Add(sibling.Id))
                {
                    AddRelative(
                        sibling,
                        subject,
                        familyPenalty,
                        relatives);
                }
            }
        }
    }

    private static void AddRelative(
        IPerson? relative,
        IPerson reference,
        double penalty,
        IDictionary<Guid, (IPerson Person, IPerson Reference, double Penalty)> relatives)
    {
        if (relative is null)
            return;

        if (!relatives.TryGetValue(
                relative.Id,
                out var existing)
            || penalty > existing.Penalty)
        {
            relatives[relative.Id] =
                (relative, reference, penalty);
        }
    }

    private void ApplyEmotionalHealthLoss(
        IPerson person,
        IPerson eventRelative,
        double basePenalty,
        bool extendedRelative)
    {
        if (person.Tags.Has("state.dead")
            || SimulationState.IsInactive(person))
        {
            return;
        }

        var personHousehold =
            _economy.GetHouseholdId(person);

        var eventHousehold =
            _economy.GetHouseholdId(eventRelative);

        var sameHousehold =
            personHousehold is not null
            && eventHousehold is not null
            && personHousehold == eventHousehold;

        var penalty =
            FamilyShockRules.ScaleHealthLoss(
                person,
                basePenalty,
                sameHousehold,
                extendedRelative);

        _health.ChangeHealth(
            person,
            -penalty);
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
