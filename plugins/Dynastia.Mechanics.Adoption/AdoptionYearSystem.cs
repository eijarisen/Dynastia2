using Dynastia.Contracts;

namespace Dynastia.Mechanics.Adoption;

public sealed class AdoptionYearSystem :
    IYearSystem
{
    private const double PreferHighestIncomeChance =
        0.70;

    private readonly StandardAdoptionService _adoption;
    private readonly IFamilyService _family;
    private readonly IEconomyService _economy;
    private readonly IGameRandom _random;
    private readonly IGameEventBus _events;

    public AdoptionYearSystem(
        StandardAdoptionService adoption,
        IFamilyService family,
        IEconomyService economy,
        IGameRandom random,
        IGameEventBus events)
    {
        _adoption = adoption;
        _family = family;
        _economy = economy;
        _random = random;
        _events = events;
    }

    public string Id =>
        "adoption.child_placement";

    public YearPhase Phase =>
        YearPhase.Inheritance;

    public IReadOnlyCollection<string> Before =>
        ["inheritance.estate_settlement"];

    public IReadOnlyCollection<string> After =>
        Array.Empty<string>();

    public void Execute(
        IGameState gameState)
    {
        CleanupHostedDependents(
            gameState);

        // Process adulthood placement before younger orphans. Family-resident
        // adults stay put; orphanage residents age out first so they can
        // become host candidates in the same year.
        foreach (var person in
            gameState.People
                .Where(
                    person =>
                        person.Tags.Has(
                            "state.alive")
                        && person.Age >= 18)
                .ToList())
        {
            ProcessAdult(
                gameState,
                person);
        }

        foreach (var child in
            gameState.People
                .Where(
                    person =>
                        person.Tags.Has(
                            "state.alive")
                        && person.Age < 18
                        && _family.IsBloodline(
                            person))
                .ToList())
        {
            ProcessMinor(
                gameState,
                child);
        }
    }

    private void ProcessAdult(
        IGameState gameState,
        IPerson person)
    {
        var component =
            person.Components.Get<
                AdoptionPlacementComponent>();

        if (component is null)
            return;

        if (component.Kind
            == AdoptionPlacementKind.Independent)
        {
            return;
        }

        var previousKind =
            component.Kind;

        // Reaching adulthood no longer creates a household for children who
        // already live with family. Sons and daughters remain resident until
        // an explicit move-out/relationship mechanic or household succession
        // changes their status. Orphanage residents are the exception because
        // they have no family household to remain in.
        if (previousKind != AdoptionPlacementKind.Orphanage)
        {
            return;
        }

        RemoveFromPreviousHost(
            person,
            component);

        SetPlacement(
            person,
            component,
            AdoptionPlacementKind.Independent,
            guardianId: null,
            householdHeadId:
                person.Id);

        if (previousKind
            == AdoptionPlacementKind.Orphanage)
        {
            _economy.EnsureIndependentHousehold(
                person);

            person.Tags.Add(
                "household.independent_orphan");

            _events.Publish(
                new GameEvent
                {
                    Type =
                        "adoption.left_orphanage",

                    Year =
                        gameState.Year,

                    SubjectId =
                        person.Id,

                    Data =
                        new Dictionary<string, string>
                        {
                            ["text"] =
                                $"{_family.GetDisplayName(person)} " +
                                "left the orphanage upon reaching adulthood " +
                                "and established an independent household."
                        }
                });
        }
    }

    private void ProcessMinor(
        IGameState gameState,
        IPerson child)
    {
        var father =
            _family.GetFather(
                child);

        var mother =
            _family.GetMother(
                child);

        var fatherAlive =
            IsAlive(
                father);

        var motherAlive =
            IsAlive(
                mother);

        var component =
            _adoption.GetOrCreateComponent(
                child);

        if (fatherAlive)
        {
            RemoveFromPreviousHost(
                child,
                component);

            SetPlacement(
                child,
                component,
                AdoptionPlacementKind.BiologicalHousehold,
                father!.Id,
                father.Id);

            return;
        }

        if (motherAlive)
        {
            RemoveFromPreviousHost(
                child,
                component);

            var changed =
                component.Kind
                    != AdoptionPlacementKind.Mother
                || component.GuardianId
                    != mother!.Id;

            SetPlacement(
                child,
                component,
                AdoptionPlacementKind.Mother,
                mother!.Id,
                father?.Id);

            if (changed)
            {
                _events.Publish(
                    new GameEvent
                    {
                        Type =
                            "adoption.with_mother",

                        Year =
                            gameState.Year,

                        SubjectId =
                            child.Id,

                        RelatedPersonIds =
                            [mother.Id],

                        Data =
                            new Dictionary<string, string>
                            {
                                ["text"] =
                                    $"{_family.GetDisplayName(child)} " +
                                    $"will remain with their mother, " +
                                    $"{_family.GetDisplayName(mother)}."
                            }
                    });
            }

            return;
        }

        EnsureOrphanTrait(
            gameState,
            child,
            component);

        if (component.Kind
            == AdoptionPlacementKind.Orphanage)
        {
            // Once placed in an orphanage, the child remains there
            // until adulthood even if a new family household later
            // becomes available.
            return;
        }

        var currentHost =
            _adoption.FindPerson(
                component.HouseholdHeadId);

        if (component.Kind
                == AdoptionPlacementKind.AdoptiveHousehold
            && IsEligibleHost(
                child,
                currentHost))
        {
            return;
        }

        RemoveFromPreviousHost(
            child,
            component);

        var host =
            ChooseHost(
                gameState,
                child);

        if (host is null)
        {
            SetPlacement(
                child,
                component,
                AdoptionPlacementKind.Orphanage,
                guardianId: null,
                householdHeadId: null);

            _events.Publish(
                new GameEvent
                {
                    Type =
                        "adoption.orphanage",

                    Year =
                        gameState.Year,

                    SubjectId =
                        child.Id,

                    Data =
                        new Dictionary<string, string>
                        {
                            ["text"] =
                                $"No family household was available for " +
                                $"{_family.GetDisplayName(child)}, " +
                                "so the child was placed in an orphanage."
                        }
                });

            return;
        }

        _economy.AddHostedDependent(
            host,
            child);

        SetPlacement(
            child,
            component,
            AdoptionPlacementKind.AdoptiveHousehold,
            guardianId:
                host.Id,
            householdHeadId:
                host.Id);

        _events.Publish(
            new GameEvent
            {
                Type =
                    "adoption.placed",

                Year =
                    gameState.Year,

                SubjectId =
                    child.Id,

                RelatedPersonIds =
                    [host.Id],

                Data =
                    new Dictionary<string, string>
                    {
                        ["text"] =
                            $"{_family.GetDisplayName(child)} " +
                            $"was taken in by the household of " +
                            $"{_family.GetDisplayName(host)}."
                    }
            });
    }

    private void EnsureOrphanTrait(
        IGameState gameState,
        IPerson child,
        AdoptionPlacementComponent component)
    {
        if (child.Tags.Has(
            "trait.orphan"))
        {
            if (component.OrphanedYear
                is null)
            {
                component.OrphanedYear =
                    gameState.Year;
            }

            return;
        }

        child.Tags.Add(
            "trait.orphan");

        component.OrphanedYear =
            gameState.Year;

        _events.Publish(
            new GameEvent
            {
                Type =
                    "adoption.orphaned",

                Year =
                    gameState.Year,

                SubjectId =
                    child.Id,

                Data =
                    new Dictionary<string, string>
                    {
                        ["text"] =
                            $"{_family.GetDisplayName(child)} " +
                            "lost both parents and became an orphan."
                    }
            });
    }

    private IPerson? ChooseHost(
        IGameState gameState,
        IPerson child)
    {
        var candidates =
            gameState.People
                .Where(
                    person =>
                        IsEligibleHost(
                            child,
                            person))
                .Select(
                    person =>
                    {
                        var finance =
                            _economy.GetHousehold(
                                person)!;

                        return new
                        {
                            Person =
                                person,

                            Income =
                                finance.LastIncome
                        };
                    })
                .ToList();

        if (candidates.Count == 0)
            return null;

        var maxIncome =
            candidates.Max(
                candidate =>
                    candidate.Income);

        var preferred =
            candidates
                .Where(
                    candidate =>
                        candidate.Income
                        == maxIncome)
                .ToList();

        var pool =
            _random.NextDouble()
                < PreferHighestIncomeChance
                ? preferred
                : candidates;

        var totalWeight =
            pool.Sum(candidate =>
                HostPersonalityWeight(
                    candidate.Person));

        var roll =
            _random.NextDouble()
            * totalWeight;

        foreach (var candidate in pool)
        {
            roll -=
                HostPersonalityWeight(
                    candidate.Person);

            if (roll <= 0)
                return candidate.Person;
        }

        return pool[^1].Person;
    }


    private static double HostPersonalityWeight(
        IPerson person)
    {
        return PersonalityInfluence.Multiplier(
            person,
            good: 0.10,
            evil: -0.10);
    }

    private bool IsEligibleHost(
        IPerson child,
        IPerson? person)
    {
        if (person is null
            || person.Id == child.Id
            || !person.Tags.Has(
                "state.alive")
            || person.Tags.Has(
                "state.imprisoned")
            || person.Tags.Has("vocation.religious.active")
            || person.Tags.Has(
                "residence.orphanage")
            || person.Age < 18
            || !_family.IsBloodline(
                person)
            || !_economy.HasHousehold(
                person))
        {
            return false;
        }

        return _economy.GetHousehold(
            person) is not null;
    }

    private void CleanupHostedDependents(
        IGameState gameState)
    {
        foreach (var head in
            gameState.People)
        {
            if (!_economy.HasHousehold(
                head))
            {
                continue;
            }

            foreach (var dependentId in
                _economy
                    .GetHostedDependentIds(
                        head)
                    .ToList())
            {
                var dependent =
                    gameState.People
                        .FirstOrDefault(
                            person =>
                                person.Id
                                == dependentId);

                if (dependent is null
                    || !dependent.Tags.Has(
                        "state.alive")
                    || dependent.Age >= 18
                    || dependent.Tags.Has(
                        "role.nanny"))
                {
                    if (dependent is not null)
                    {
                        _economy.RemoveHostedDependent(
                            head,
                            dependent);
                    }
                }
            }
        }
    }

    private void RemoveFromPreviousHost(
        IPerson child,
        AdoptionPlacementComponent component)
    {
        if (component.HouseholdHeadId
            is not Guid hostId)
        {
            return;
        }

        var host =
            _adoption.FindPerson(
                hostId);

        if (host is null
            || !_economy.HasHousehold(
                host))
        {
            return;
        }

        _economy.RemoveHostedDependent(
            host,
            child);
    }

    private static void SetPlacement(
        IPerson person,
        AdoptionPlacementComponent component,
        AdoptionPlacementKind kind,
        Guid? guardianId,
        Guid? householdHeadId)
    {
        component.Kind =
            kind;

        component.GuardianId =
            guardianId;

        component.HouseholdHeadId =
            householdHeadId;

        person.Tags.Remove(
            "residence.with_mother");

        person.Tags.Remove(
            "residence.adopted");

        person.Tags.Remove(
            "residence.orphanage");

        person.Tags.Remove(
            "residence.independent");

        switch (kind)
        {
            case AdoptionPlacementKind.Mother:
                person.Tags.Add(
                    "residence.with_mother");
                break;

            case AdoptionPlacementKind.AdoptiveHousehold:
                person.Tags.Add(
                    "residence.adopted");
                break;

            case AdoptionPlacementKind.Orphanage:
                person.Tags.Add(
                    "residence.orphanage");
                break;

            case AdoptionPlacementKind.Independent:
                person.Tags.Add(
                    "residence.independent");
                break;
        }
    }

    private static bool IsAlive(
        IPerson? person)
    {
        return person is not null
            && person.Tags.Has(
                "state.alive");
    }
}
