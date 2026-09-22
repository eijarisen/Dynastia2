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

        var formerHouseholdMemberIds =
            CaptureFormerHouseholdMemberIds(actor, spouse);

        var household =
            _economy.GetHousehold(actor);

        var settlement =
            household is null
                || household.Wealth <= 0
                    ? 0
                    : Math.Floor(
                        household.Wealth / 2m);

        // Positive cash is divided by the existing settlement rule. A
        // negative balance represents debt already paid on the household's
        // behalf and must not disappear merely because the couple divorces.
        if (household?.Wealth > 0)
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

        ResolvePostDivorceHouseholds(
            gameState,
            actor,
            spouse,
            settlement);

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

                        ["formerHouseholdMemberIds"] =
                            SerializePersonIds(formerHouseholdMemberIds),

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

        var formerHouseholdMemberIds =
            CaptureFormerHouseholdMemberIds(husband, wife);

        var household =
            _economy.GetHousehold(
                husband);

        var settlement =
            household is null
                || household.Wealth <= 0
                    ? 0
                    : Math.Floor(
                        household.Wealth / 2m);

        if (household?.Wealth > 0)
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

        ResolvePostDivorceHouseholds(
            gameState,
            husband,
            wife,
            settlement);

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

                        ["formerHouseholdMemberIds"] =
                            SerializePersonIds(formerHouseholdMemberIds),

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

        var formerHouseholdMemberIds =
            CaptureFormerHouseholdMemberIds(actor, spouse);

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

        var settlementTransferred =
            ResolvePostDivorceHouseholds(
                gameState,
                actor,
                spouse,
                settlement);

        if (settlement > 0
            && !settlementTransferred
            && _family.GetSex(wife) == Sex.Female)
        {
            _economy.ChangePendingInheritance(
                wife,
                settlement);
        }

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

                        ["formerHouseholdMemberIds"] =
                            SerializePersonIds(formerHouseholdMemberIds),

                        ["text"] =
                            $"{actorEventName} " +
                            $"was caught having an affair! " +
                            $"{spouseEventName} " +
                            $"divorced {pronoun} immediately."
                    }
            });
    }


    private IReadOnlyCollection<Guid> CaptureFormerHouseholdMemberIds(
        IPerson first,
        IPerson second)
    {
        var ids = new HashSet<Guid>();

        foreach (var person in new[] { first, second })
        {
            foreach (var id in _economy.GetHouseholdMemberIds(person))
                ids.Add(id);
        }

        ids.Add(first.Id);
        ids.Add(second.Id);
        return ids;
    }

    private static string SerializePersonIds(
        IEnumerable<Guid> ids) =>
        string.Join(
            ";",
            ids.Distinct().Select(id => id.ToString("N")));

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

        // Divorce consequences apply only to the couple's shared biological
        // minor children. Children from earlier relationships are not part of
        // this breakup and must not receive the child health shock.
        foreach (var child in GetSharedBiologicalChildren(first, second)
            .Where(child =>
                child.Tags.Has("state.alive")
                && child.Age < 18))
        {
            ApplyEmotionalHealthLoss(
                child,
                first,
                childPenalty,
                extendedRelative: true);
        }

        // Preserve the smaller legacy shock for the divorcing adults' parents
        // and siblings, but deliberately exclude every non-shared child.
        var relatives =
            new Dictionary<Guid, (IPerson Person, IPerson Reference, double Penalty)>();

        AddAdultExtendedRelatives(
            first,
            childPenalty,
            relatives);

        AddAdultExtendedRelatives(
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

    private void AddAdultExtendedRelatives(
        IPerson subject,
        double childPenalty,
        IDictionary<Guid, (IPerson Person, IPerson Reference, double Penalty)> relatives)
    {
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

    private bool ResolvePostDivorceHouseholds(
        IGameState gameState,
        IPerson first,
        IPerson second,
        decimal settlement)
    {
        var firstHeadBefore = FindHouseholdHead(gameState, first);
        var secondHeadBefore = FindHouseholdHead(gameState, second);

        var sourceHead = firstHeadBefore ?? secondHeadBefore;
        var sourceHouseholdId = sourceHead is null
            ? null
            : _economy.GetHouseholdId(sourceHead);
        var residenceTown = sourceHead is null
            ? null
            : _economy.GetResidenceTown(sourceHead);

        var sharedMinors = GetSharedBiologicalChildren(first, second)
            .Where(child =>
                child.Tags.Has("state.alive")
                && child.Age < 18)
            .ToList();

        var custody = new Dictionary<Guid, IPerson>();
        foreach (var child in sharedMinors)
        {
            var father = _family.GetFather(child);
            var mother = _family.GetMother(child);
            if (father is null || mother is null)
                continue;

            custody[child.Id] =
                DivorceCustodyRules.AssignToFather(
                    _random.NextDouble())
                    ? father
                    : mother;
        }

        var createdHeads = new List<IPerson>();
        EnsureDivorcedParentResidence(
            gameState,
            first,
            second,
            custody,
            residenceTown,
            createdHeads);
        EnsureDivorcedParentResidence(
            gameState,
            second,
            first,
            custody,
            residenceTown,
            createdHeads);

        foreach (var child in sharedMinors)
        {
            if (!custody.TryGetValue(child.Id, out var custodian))
                continue;

            var custodianHead =
                FindHouseholdHead(gameState, custodian);

            if (custodianHead is not null)
            {
                _economy.AddHouseholdMember(
                    custodianHead,
                    child);
            }
        }

        if (residenceTown is not null)
        {
            foreach (var createdHead in createdHeads
                .DistinctBy(person => person.Id))
            {
                _economy.SetResidenceTown(
                    createdHead,
                    residenceTown);
            }
        }

        var wife = _family.GetSex(first) == Sex.Female
            ? first
            : _family.GetSex(second) == Sex.Female
                ? second
                : null;

        if (wife is null || settlement <= 0)
            return false;

        var wifeHead = FindHouseholdHead(gameState, wife);
        if (wifeHead is null
            || !HouseholdContainsLivingBloodline(gameState, wifeHead))
        {
            return false;
        }

        var wifeHouseholdId = _economy.GetHouseholdId(wifeHead);
        if (sourceHouseholdId is not null
            && wifeHouseholdId == sourceHouseholdId)
        {
            return false;
        }

        _economy.ChangeWealth(
            wifeHead,
            settlement);

        return true;
    }

    private void EnsureDivorcedParentResidence(
        IGameState gameState,
        IPerson parent,
        IPerson formerPartner,
        IReadOnlyDictionary<Guid, IPerson> custody,
        TownInfo? residenceTown,
        ICollection<IPerson> createdHeads)
    {
        var sharedChildren =
            GetSharedBiologicalChildren(parent, formerPartner)
                .Where(child => child.Tags.Has("state.alive"))
                .ToList();

        var assignedBloodlineChildren =
            custody
                .Where(pair => pair.Value.Id == parent.Id)
                .Select(pair =>
                    gameState.People.FirstOrDefault(person => person.Id == pair.Key))
                .Where(child =>
                    child is not null
                    && _family.IsBloodline(child))
                .Cast<IPerson>()
                .ToList();

        var currentHead = FindHouseholdHead(gameState, parent);
        if (currentHead?.Id == parent.Id)
            return;

        // Divorce always separates the former spouses into real residences.
        // A non-bloodline ex-spouse still needs an independent peripheral
        // household even when custody goes to the other parent; otherwise later
        // finances and chronicle events can be incorrectly attributed to a child.
        var dynastyAnchor = _family.IsBloodline(parent)
            ? parent
            : assignedBloodlineChildren.FirstOrDefault()
                ?? sharedChildren.FirstOrDefault(_family.IsBloodline)
                ?? (_family.IsBloodline(formerPartner) ? formerPartner : parent);

        _economy.EnsureIndependentHousehold(
            parent,
            dynastyAnchor);

        createdHeads.Add(parent);

        if (residenceTown is not null)
        {
            _economy.SetResidenceTown(
                parent,
                residenceTown);
        }
    }

    private IReadOnlyList<IPerson> GetSharedBiologicalChildren(
        IPerson first,
        IPerson second)
    {
        return _family.GetChildren(first)
            .Where(child =>
            {
                var fatherId = _family.GetFather(child)?.Id;
                var motherId = _family.GetMother(child)?.Id;

                return DivorceCustodyRules.IsSharedBiologicalChild(
                    fatherId,
                    motherId,
                    first.Id,
                    second.Id);
            })
            .DistinctBy(child => child.Id)
            .ToList();
    }

    private IPerson? FindHouseholdHead(
        IGameState gameState,
        IPerson person)
    {
        var householdId = _economy.GetHouseholdId(person);
        if (householdId is null)
            return null;

        return gameState.People.FirstOrDefault(candidate =>
            _economy.HasHousehold(candidate)
            && _economy.GetHouseholdId(candidate) == householdId);
    }

    private bool HouseholdContainsLivingBloodline(
        IGameState gameState,
        IPerson householdHead)
    {
        return _economy.GetHouseholdMemberIds(householdHead)
            .Select(id => gameState.People.FirstOrDefault(person => person.Id == id))
            .Where(person => person is not null)
            .Cast<IPerson>()
            .Any(person =>
                person.Tags.Has("state.alive")
                && _family.IsBloodline(person));
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
