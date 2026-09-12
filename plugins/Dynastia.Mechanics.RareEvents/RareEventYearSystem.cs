using Dynastia.Contracts;

namespace Dynastia.Mechanics.RareEvents;

internal sealed class RareEventYearSystem :
    IYearSystem
{
    // Household probabilities are annual per eligible household.
    private const double HouseFireChance =
        0.00035;

    private const double BurglaryChance =
        0.00060;

    private const double StormFloodChance =
        0.00025;

    private const double StructuralAccidentChance =
        0.00010;

    // Personal probabilities are annual per eligible person.
    private const double AssaultChance =
        0.00020;

    private const double MuggingChance =
        0.00015;

    private const double WorkplaceAccidentChance =
        0.00015;

    private const double TrafficAccidentChance =
        0.00012;

    private const double LightningStrikeChance =
        0.000003;

    private const double SeriousFallChance =
        0.00010;

    private const double LotteryChance =
        0.00002;

    private const double DistantInheritanceChance =
        0.00004;

    private const double FraudChance =
        0.00012;

    private const double FoundPropertyChance =
        0.00004;

    private const double WrongfulArrestChance =
        0.00001;

    private const double BaseSuicideChance =
        0.000005;

    private const double MaximumSuicideChance =
        0.0004;

    // The design calls these "small" / "substantial" chances
    // without fixing exact values.
    private const double CatastrophicFireDeathChance =
        0.05;

    private const double WorkplaceDeathChance =
        0.05;

    private const double TrafficDeathChance =
        0.05;

    private const double LightningDeathChance =
        0.35;

    private readonly IFamilyService _family;
    private readonly IHealthService _health;
    private readonly IEconomyService _economy;
    private readonly ICareerService _career;
    private readonly IJusticeService _justice;
    private readonly IHouseholdService _households;
    private readonly IGameRandom _random;
    private readonly IGameEventBus _events;
    private readonly RecentLifeEventTracker _recent;
    private readonly RareEventDeathService _death;

    public RareEventYearSystem(
        IFamilyService family,
        IHealthService health,
        IEconomyService economy,
        ICareerService career,
        IJusticeService justice,
        IHouseholdService households,
        IGameRandom random,
        IGameEventBus events,
        RecentLifeEventTracker recent,
        RareEventDeathService death)
    {
        _family = family;
        _health = health;
        _economy = economy;
        _career = career;
        _justice = justice;
        _households = households;
        _random = random;
        _events = events;
        _recent = recent;
        _death = death;
    }

    public string Id =>
        "rare_events.annual";

    public YearPhase Phase =>
        YearPhase.Death;

    public IReadOnlyCollection<string> Before =>
        ["mortality.natural_death"];

    public IReadOnlyCollection<string> After =>
        Array.Empty<string>();

    public void Execute(
        IGameState gameState)
    {
        ProcessHouseholdEvents(
            gameState);

        // Rebuild the living list after household events because
        // catastrophic fires can kill an occupant.
        var living =
            gameState.People
                .Where(
                    person =>
                        person.Tags.Has(
                            "state.alive")
                        && !SimulationState.IsInactive(
                            person))
                .ToList();

        foreach (var person in
            living)
        {
            if (person.Tags.Has(
                    "state.dead")
                || SimulationState.IsInactive(
                    person))
            {
                continue;
            }

            ProcessPersonalEvent(
                gameState,
                person);
        }
    }

    private void ProcessHouseholdEvents(
        IGameState gameState)
    {
        foreach (var household in
            GetLivingHouseholds(
                gameState))
        {
            var finance =
                _economy.GetHousehold(
                    household.Head);

            if (finance is null)
                continue;

            var candidates =
                new List<HouseholdEventCandidate>
                {
                    new(
                        HouseholdRareEvent.HouseFire,
                        HouseFireChance),

                    new(
                        HouseholdRareEvent.StormOrFlood,
                        StormFloodChance),

                    new(
                        HouseholdRareEvent.StructuralAccident,
                        StructuralAccidentChance)
                };

            if (finance.Wealth > 0)
            {
                candidates.Add(
                    new HouseholdEventCandidate(
                        HouseholdRareEvent.Burglary,
                        BurglaryChance));
            }

            var selected =
                SelectEvent(
                    candidates);

            if (selected is null)
                continue;

            switch (selected.Value)
            {
                case HouseholdRareEvent.HouseFire:
                    ExecuteHouseFire(
                        gameState,
                        household);
                    break;

                case HouseholdRareEvent.Burglary:
                    ExecuteBurglary(
                        gameState,
                        household);
                    break;

                case HouseholdRareEvent.StormOrFlood:
                    ExecuteStormOrFlood(
                        gameState,
                        household);
                    break;

                case HouseholdRareEvent.StructuralAccident:
                    ExecuteStructuralAccident(
                        gameState,
                        household);
                    break;
            }
        }
    }

    private void ProcessPersonalEvent(
        IGameState gameState,
        IPerson person)
    {
        var candidates =
            new List<PersonalEventCandidate>();

        var financeHead =
            ResolveFinanceHead(
                person);

        var finance =
            financeHead is null
                ? null
                : _economy.GetHousehold(
                    financeHead);

        if (person.Age >= 15)
        {
            candidates.Add(
                new PersonalEventCandidate(
                    PersonalRareEvent.Assault,
                    AssaultChance));

            if (finance is not null)
            {
                candidates.Add(
                    new PersonalEventCandidate(
                        PersonalRareEvent.Mugging,
                        MuggingChance));
            }

            candidates.Add(
                new PersonalEventCandidate(
                    PersonalRareEvent.Suicide,
                    GetSuicideChance(
                        person)));
        }

        var career =
            _career.GetCareer(
                person);

        if (person.Age >= 18
            && career.JobLevel > 0
            && !career.IsRetired)
        {
            candidates.Add(
                new PersonalEventCandidate(
                    PersonalRareEvent.WorkplaceAccident,
                    WorkplaceAccidentChance));
        }

        if (gameState.Year >= 1920
            && person.Age >= 10)
        {
            candidates.Add(
                new PersonalEventCandidate(
                    PersonalRareEvent.TrafficAccident,
                    TrafficAccidentChance));
        }

        candidates.Add(
            new PersonalEventCandidate(
                PersonalRareEvent.LightningStrike,
                LightningStrikeChance));

        if (person.Age >= 10)
        {
            candidates.Add(
                new PersonalEventCandidate(
                    PersonalRareEvent.SeriousFall,
                    SeriousFallChance));
        }

        if (person.Age >= 18
            && finance is not null)
        {
            if (gameState.Year >= 1957)
            {
                candidates.Add(
                    new PersonalEventCandidate(
                        PersonalRareEvent.LotteryWin,
                        LotteryChance));
            }

            candidates.Add(
                new PersonalEventCandidate(
                    PersonalRareEvent.DistantInheritance,
                    DistantInheritanceChance));

            candidates.Add(
                new PersonalEventCandidate(
                    PersonalRareEvent.FoundProperty,
                    FoundPropertyChance));

            if (finance.Wealth >= 1000)
            {
                candidates.Add(
                    new PersonalEventCandidate(
                        PersonalRareEvent.Fraud,
                        FraudChance));
            }
        }

        if (person.Age >= 18
            && !_justice.IsImprisoned(
                person))
        {
            candidates.Add(
                new PersonalEventCandidate(
                    PersonalRareEvent.WrongfulArrest,
                    WrongfulArrestChance));
        }

        var selected =
            SelectEvent(
                candidates);

        if (selected is null)
            return;

        switch (selected.Value)
        {
            case PersonalRareEvent.Assault:
                ExecuteAssault(
                    gameState,
                    person);
                break;

            case PersonalRareEvent.Mugging:
                ExecuteMugging(
                    gameState,
                    person,
                    financeHead!);
                break;

            case PersonalRareEvent.WorkplaceAccident:
                ExecuteWorkplaceAccident(
                    gameState,
                    person);
                break;

            case PersonalRareEvent.TrafficAccident:
                ExecuteTrafficAccident(
                    gameState,
                    person);
                break;

            case PersonalRareEvent.LightningStrike:
                ExecuteLightningStrike(
                    gameState,
                    person);
                break;

            case PersonalRareEvent.SeriousFall:
                ExecuteSeriousFall(
                    gameState,
                    person);
                break;

            case PersonalRareEvent.LotteryWin:
                ExecuteLotteryWin(
                    gameState,
                    person,
                    financeHead!);
                break;

            case PersonalRareEvent.DistantInheritance:
                ExecuteDistantInheritance(
                    gameState,
                    person,
                    financeHead!);
                break;

            case PersonalRareEvent.Fraud:
                ExecuteFraud(
                    gameState,
                    person,
                    financeHead!);
                break;

            case PersonalRareEvent.FoundProperty:
                ExecuteFoundProperty(
                    gameState,
                    person,
                    financeHead!);
                break;

            case PersonalRareEvent.WrongfulArrest:
                ExecuteWrongfulArrest(
                    gameState,
                    person);
                break;

            case PersonalRareEvent.Suicide:
                ExecuteSuicide(
                    gameState,
                    person);
                break;
        }
    }

    private void ExecuteHouseFire(
        IGameState gameState,
        HouseholdContext household)
    {
        var severityRoll =
            _random.NextDouble();

        if (severityRoll < 0.70)
        {
            var requestedLoss =
                RandomMoney(
                    500,
                    1500);

            var actualLoss =
                RemoveHouseholdWealth(
                    household.Head,
                    requestedLoss);

            PublishHouseholdEvent(
                gameState,
                household,
                "rare.house_fire",
                $"A fire broke out in the household of " +
                $"{HouseholdDisplayName(household)} but was contained. " +
                $"The household lost {actualLoss:N0} zł.",
                new Dictionary<string, string>
                {
                    ["severity"] =
                        "contained",

                    ["wealthLoss"] =
                        actualLoss.ToString()
                });

            return;
        }

        if (severityRoll < 0.95)
        {
            var requestedLoss =
                RandomMoney(
                    2000,
                    5000);

            var actualLoss =
                RemoveHouseholdWealth(
                    household.Head,
                    requestedLoss);

            var injuryCount =
                _random.NextInt(
                    1,
                    Math.Min(
                        3,
                        household.Occupants.Count));

            var injured =
                SamplePeople(
                    household.Occupants,
                    injuryCount);

            var damage =
                new List<string>();

            foreach (var person in
                injured)
            {
                var amount =
                    ApplyNonFatalDamage(
                        person,
                        10,
                        30);

                damage.Add(
                    $"{_family.GetDisplayName(person)} (-{amount:0} Health)");
            }

            PublishHouseholdEvent(
                gameState,
                household,
                "rare.house_fire",
                $"A serious house fire struck the household of " +
                $"{HouseholdDisplayName(household)}. " +
                $"The household lost {actualLoss:N0} zł and " +
                $"{injured.Count} occupant" +
                $"{(injured.Count == 1 ? " was" : "s were")} injured.",
                new Dictionary<string, string>
                {
                    ["severity"] =
                        "serious",

                    ["wealthLoss"] =
                        actualLoss.ToString(),

                    ["injuries"] =
                        string.Join(
                            "; ",
                            damage)
                },
                preferredSubject:
                    injured[0]);

            return;
        }

        var catastrophicLoss =
            RemoveHouseholdWealth(
                household.Head,
                RandomMoney(
                    2000,
                    5000));

        var finance =
            _economy.GetHousehold(
                household.Head);

        HousePropertyInfo? destroyedHouse =
            null;

        if (finance is not null
            && finance.Houses.Count > 0)
        {
            destroyedHouse =
                finance.Houses[^1];

            _economy.SetHousesOwned(
                household.Head,
                finance.Houses.Count - 1);
        }

        foreach (var occupant in
            household.Occupants)
        {
            ApplyNonFatalDamage(
                occupant,
                25,
                50);
        }

        IPerson? fatalVictim =
            null;

        if (household.Occupants.Count > 0
            && _random.NextDouble()
                < CatastrophicFireDeathChance)
        {
            fatalVictim =
                household.Occupants[
                    _random.NextInt(
                        0,
                        household.Occupants.Count - 1)];
        }

        var propertyText =
            destroyedHouse is null
                ? "No owned property was lost."
                : $"The house in " +
                  $"{destroyedHouse.Town.Town} was destroyed.";

        PublishHouseholdEvent(
            gameState,
            household,
            "rare.house_fire",
            $"A catastrophic fire devastated the household of " +
            $"{HouseholdDisplayName(household)}. " +
            $"The household lost {catastrophicLoss:N0} zł. " +
            $"{propertyText} " +
            "The occupants suffered serious injuries.",
            new Dictionary<string, string>
            {
                ["severity"] =
                    "catastrophic",

                ["wealthLoss"] =
                    catastrophicLoss.ToString(),

                ["destroyedHouseTown"] =
                    destroyedHouse?.Town.Town
                    ?? string.Empty,

                ["fatal"] =
                    (fatalVictim is not null)
                        .ToString()
            },
            preferredSubject:
                fatalVictim
                ?? household.PrimaryOccupant);

        if (fatalVictim is not null)
        {
            _death.Kill(
                gameState,
                fatalVictim,
                "rare.house_fire");
        }
    }

    private void ExecuteBurglary(
        IGameState gameState,
        HouseholdContext household)
    {
        var finance =
            _economy.GetHousehold(
                household.Head);

        if (finance is null)
            return;

        var percentage =
            0.05
            + _random.NextDouble()
                * 0.15;

        var requestedLoss =
            Math.Min(
                5000m,
                Math.Max(
                    1m,
                    Math.Floor(
                        finance.Wealth
                        * (decimal)percentage)));

        var actualLoss =
            RemoveHouseholdWealth(
                household.Head,
                requestedLoss);

        IPerson? injured =
            null;

        double injuryDamage =
            0;

        var adults =
            household.Occupants
                .Where(
                    person =>
                        person.Age >= 18)
                .ToList();

        if (adults.Count > 0
            && _random.NextDouble()
                < 0.10)
        {
            injured =
                adults[
                    _random.NextInt(
                        0,
                        adults.Count - 1)];

            injuryDamage =
                ApplyNonFatalDamage(
                    injured,
                    5,
                    25);
        }

        var injuryText =
            injured is null
                ? "Nobody confronted the intruder."
                : $"{_family.GetDisplayName(injured)} was injured while confronting the intruder.";

        PublishHouseholdEvent(
            gameState,
            household,
            "rare.burglary",
            $"The household of {HouseholdDisplayName(household)} " +
            $"was burgled and lost {actualLoss:N0} zł. " +
            injuryText,
            new Dictionary<string, string>
            {
                ["wealthLoss"] =
                    actualLoss.ToString(),

                ["interrupted"] =
                    (injured is not null)
                        .ToString(),

                ["healthDamage"] =
                    injuryDamage.ToString(
                        "0")
            },
            preferredSubject:
                injured
                ?? household.PrimaryOccupant);
    }

    private void ExecuteStormOrFlood(
        IGameState gameState,
        HouseholdContext household)
    {
        var actualLoss =
            RemoveHouseholdWealth(
                household.Head,
                RandomMoney(
                    500,
                    2500));

        IPerson? injured =
            null;

        double damage =
            0;

        if (household.Occupants.Count > 0
            && _random.NextDouble()
                < 0.15)
        {
            injured =
                household.Occupants[
                    _random.NextInt(
                        0,
                        household.Occupants.Count - 1)];

            damage =
                ApplyNonFatalDamage(
                    injured,
                    5,
                    15);
        }

        PublishHouseholdEvent(
            gameState,
            household,
            "rare.storm_flood_damage",
            $"A severe storm or flood damaged the household of " +
            $"{HouseholdDisplayName(household)}, causing " +
            $"{actualLoss:N0} zł in losses" +
            $"{(injured is null ? "." : $" and injuring {_family.GetDisplayName(injured)}.")}",
            new Dictionary<string, string>
            {
                ["wealthLoss"] =
                    actualLoss.ToString(),

                ["healthDamage"] =
                    damage.ToString(
                        "0")
            },
            preferredSubject:
                injured
                ?? household.PrimaryOccupant);
    }

    private void ExecuteStructuralAccident(
        IGameState gameState,
        HouseholdContext household)
    {
        var actualLoss =
            RemoveHouseholdWealth(
                household.Head,
                RandomMoney(
                    500,
                    2000));

        IPerson? injured =
            null;

        double damage =
            0;

        if (household.Occupants.Count > 0
            && _random.NextDouble()
                < 0.50)
        {
            injured =
                household.Occupants[
                    _random.NextInt(
                        0,
                        household.Occupants.Count - 1)];

            damage =
                ApplyNonFatalDamage(
                    injured,
                    10,
                    25);
        }

        PublishHouseholdEvent(
            gameState,
            household,
            "rare.structural_accident",
            $"A serious structural accident at home caused " +
            $"{actualLoss:N0} zł in damage to the household of " +
            $"{HouseholdDisplayName(household)}" +
            $"{(injured is null ? "." : $" and injured {_family.GetDisplayName(injured)}.")}",
            new Dictionary<string, string>
            {
                ["wealthLoss"] =
                    actualLoss.ToString(),

                ["healthDamage"] =
                    damage.ToString(
                        "0")
            },
            preferredSubject:
                injured
                ?? household.PrimaryOccupant);
    }

    private void ExecuteAssault(
        IGameState gameState,
        IPerson person)
    {
        var damage =
            ApplyNonFatalDamage(
                person,
                10,
                35);

        _recent.SetFlag(
            person,
            "recent.assault",
            durationYears:
                1,
            currentYear:
                gameState.Year);

        PublishPersonalEvent(
            gameState,
            person,
            "rare.assault",
            $"{_family.GetDisplayName(person)} was assaulted and suffered serious injuries.",
            new Dictionary<string, string>
            {
                ["healthDamage"] =
                    damage.ToString(
                        "0")
            });
    }

    private void ExecuteMugging(
        IGameState gameState,
        IPerson person,
        IPerson householdHead)
    {
        var wealthLoss =
            RemoveHouseholdWealth(
                householdHead,
                RandomMoney(
                    100,
                    1000));

        var damage =
            ApplyNonFatalDamage(
                person,
                5,
                20);

        _recent.SetFlag(
            person,
            "recent.assault",
            durationYears:
                1,
            currentYear:
                gameState.Year);

        PublishPersonalEvent(
            gameState,
            person,
            "rare.mugging",
            $"{_family.GetDisplayName(person)} was mugged, losing " +
            $"{wealthLoss:N0} zł and suffering injuries.",
            new Dictionary<string, string>
            {
                ["wealthLoss"] =
                    wealthLoss.ToString(),

                ["healthDamage"] =
                    damage.ToString(
                        "0")
            });
    }

    private void ExecuteWorkplaceAccident(
        IGameState gameState,
        IPerson person)
    {
        var damage =
            ApplyNonFatalDamage(
                person,
                20,
                50);

        var fatal =
            _random.NextDouble()
            < WorkplaceDeathChance;

        PublishPersonalEvent(
            gameState,
            person,
            "rare.workplace_accident",
            $"{_family.GetDisplayName(person)} suffered a serious workplace accident.",
            new Dictionary<string, string>
            {
                ["healthDamage"] =
                    damage.ToString(
                        "0"),

                ["fatal"] =
                    fatal.ToString()
            });

        if (fatal)
        {
            _death.Kill(
                gameState,
                person,
                "rare.workplace_accident");
        }
    }

    private void ExecuteTrafficAccident(
        IGameState gameState,
        IPerson person)
    {
        var damage =
            ApplyNonFatalDamage(
                person,
                20,
                60);

        var fatal =
            _random.NextDouble()
            < TrafficDeathChance;

        PublishPersonalEvent(
            gameState,
            person,
            "rare.traffic_accident",
            $"{_family.GetDisplayName(person)} was badly injured in a road traffic accident.",
            new Dictionary<string, string>
            {
                ["healthDamage"] =
                    damage.ToString(
                        "0"),

                ["fatal"] =
                    fatal.ToString()
            });

        if (fatal)
        {
            _death.Kill(
                gameState,
                person,
                "rare.traffic_accident");
        }
    }

    private void ExecuteLightningStrike(
        IGameState gameState,
        IPerson person)
    {
        var damage =
            ApplyNonFatalDamage(
                person,
                50,
                90);

        var fatal =
            _random.NextDouble()
            < LightningDeathChance;

        PublishPersonalEvent(
            gameState,
            person,
            "rare.lightning_strike",
            $"{_family.GetDisplayName(person)} was struck by lightning.",
            new Dictionary<string, string>
            {
                ["healthDamage"] =
                    damage.ToString(
                        "0"),

                ["fatal"] =
                    fatal.ToString()
            });

        if (fatal)
        {
            _death.Kill(
                gameState,
                person,
                "rare.lightning_strike");
        }
    }

    private void ExecuteSeriousFall(
        IGameState gameState,
        IPerson person)
    {
        var damage =
            ApplyNonFatalDamage(
                person,
                15,
                45);

        PublishPersonalEvent(
            gameState,
            person,
            "rare.serious_fall",
            $"{_family.GetDisplayName(person)} suffered a serious accidental fall.",
            new Dictionary<string, string>
            {
                ["healthDamage"] =
                    damage.ToString(
                        "0")
            });
    }

    private void ExecuteLotteryWin(
        IGameState gameState,
        IPerson person,
        IPerson householdHead)
    {
        var roll =
            _random.NextDouble();

        var prize =
            roll < 0.90
                ? RandomMoney(
                    20000,
                    30000)
                : roll < 0.99
                    ? RandomMoney(
                        30001,
                        60000)
                    : RandomMoney(
                        60001,
                        100000);

        _economy.ChangeWealth(
            householdHead,
            prize);

        PublishPersonalEvent(
            gameState,
            person,
            "rare.lottery_win",
            $"{_family.GetDisplayName(person)} won a major lottery prize of {prize:N0} zł.",
            new Dictionary<string, string>
            {
                ["amount"] =
                    prize.ToString()
            });
    }

    private void ExecuteDistantInheritance(
        IGameState gameState,
        IPerson person,
        IPerson householdHead)
    {
        var amount =
            RandomMoney(
                2000,
                15000);

        _economy.ChangeWealth(
            householdHead,
            amount);

        PublishPersonalEvent(
            gameState,
            person,
            "rare.distant_inheritance",
            $"{_family.GetDisplayName(person)} unexpectedly inherited " +
            $"{amount:N0} zł from a distant relative outside the known family tree.",
            new Dictionary<string, string>
            {
                ["amount"] =
                    amount.ToString()
            });
    }

    private void ExecuteFraud(
        IGameState gameState,
        IPerson person,
        IPerson householdHead)
    {
        var finance =
            _economy.GetHousehold(
                householdHead);

        if (finance is null)
            return;

        var percentage =
            0.05
            + _random.NextDouble()
                * 0.20;

        var requestedLoss =
            Math.Min(
                5000m,
                Math.Max(
                    1m,
                    Math.Floor(
                        finance.Wealth
                        * (decimal)percentage)));

        var actualLoss =
            RemoveHouseholdWealth(
                householdHead,
                requestedLoss);

        PublishPersonalEvent(
            gameState,
            person,
            "rare.fraud",
            $"{_family.GetDisplayName(person)} fell victim to a fraud or confidence trick and lost {actualLoss:N0} zł.",
            new Dictionary<string, string>
            {
                ["amount"] =
                    actualLoss.ToString(),

                ["percentage"] =
                    percentage.ToString(
                        "0.000")
            });
    }

    private void ExecuteFoundProperty(
        IGameState gameState,
        IPerson person,
        IPerson householdHead)
    {
        var amount =
            RandomMoney(
                500,
                3000);

        _economy.ChangeWealth(
            householdHead,
            amount);

        PublishPersonalEvent(
            gameState,
            person,
            "rare.found_property",
            $"{_family.GetDisplayName(person)} found valuable property worth {amount:N0} zł.",
            new Dictionary<string, string>
            {
                ["amount"] =
                    amount.ToString()
            });
    }

    private void ExecuteWrongfulArrest(
        IGameState gameState,
        IPerson person)
    {
        var career =
            _career.GetCareer(
                person);

        var lostJob =
            career.JobLevel > 0
            && !career.IsRetired;

        if (lostJob)
        {
            _career.SetJobLevel(
                person,
                0);

            _recent.SetFlag(
                person,
                "recent.job_loss",
                durationYears:
                    1,
                currentYear:
                    gameState.Year);
        }

        _justice.Imprison(
            person,
            sentence:
                1,
            reasonId:
                "wrongful_arrest",
            reasonName:
                "Wrongful arrest");

        PublishPersonalEvent(
            gameState,
            person,
            "rare.wrongful_arrest",
            $"{_family.GetDisplayName(person)} was wrongfully arrested and detained for a year despite being innocent.",
            new Dictionary<string, string>
            {
                ["lostJob"] =
                    lostJob.ToString()
            });
    }

    private void ExecuteSuicide(
        IGameState gameState,
        IPerson person)
    {
        PublishPersonalEvent(
            gameState,
            person,
            "rare.suicide",
            $"{_family.GetDisplayName(person)} died by suicide after a period of severe distress.",
            new Dictionary<string, string>());

        _death.Kill(
            gameState,
            person,
            "suicide");
    }

    private double GetSuicideChance(
        IPerson person)
    {
        var health =
            _health.GetHealth(
                person);

        var chance =
            BaseSuicideChance;

        if (HasCondition(
            health,
            "depression"))
        {
            chance *=
                20;
        }

        if (HasCondition(
            health,
            "alcoholism"))
        {
            chance *=
                3;
        }

        if (HasCondition(
            health,
            "anxiety"))
        {
            chance *=
                2;
        }

        if (_recent.HasFlag(
            person,
            "recent.bereavement"))
        {
            chance *=
                3;
        }

        if (_recent.HasFlag(
            person,
            "recent.divorce"))
        {
            chance *=
                2;
        }

        if (_recent.HasFlag(
                person,
                "recent.job_loss")
            && IsHouseholdBroke(
                person))
        {
            chance *=
                2;
        }

        if (health.Percentage < 25)
        {
            chance *=
                2;
        }

        return Math.Min(
            chance,
            MaximumSuicideChance);
    }

    private static bool HasCondition(
        HealthSnapshot health,
        string name)
    {
        return health.Conditions.Any(
            condition =>
                condition.Id.Equals(
                    name,
                    StringComparison.OrdinalIgnoreCase)
                || condition.Name.Equals(
                    name,
                    StringComparison.OrdinalIgnoreCase));
    }

    private bool IsHouseholdBroke(
        IPerson person)
    {
        var head =
            _households.ResolveHouseholdHead(
                person);

        return head is not null
            && _households
                .GetStatus(
                    head)?
                .IsBroke
                == true;
    }

    private IReadOnlyList<HouseholdContext>
        GetLivingHouseholds(
            IGameState gameState)
    {
        var result =
            new Dictionary<
                Guid,
                HouseholdContextBuilder>();

        foreach (var person in
            gameState.People)
        {
            if (!person.Tags.Has(
                "state.alive"))
            {
                continue;
            }

            var head =
                _households.ResolveHouseholdHead(
                    person);

            if (head is null
                || !_economy.HasHousehold(
                    head))
            {
                continue;
            }

            if (!result.TryGetValue(
                head.Id,
                out var builder))
            {
                builder =
                    new HouseholdContextBuilder(
                        head);

                result[head.Id] =
                    builder;
            }

            if (!builder.Occupants.Any(
                occupant =>
                    occupant.Id
                    == person.Id))
            {
                builder.Occupants.Add(
                    person);
            }
        }

        return result.Values
            .Where(
                builder =>
                    builder.Occupants.Count > 0)
            .Select(
                builder =>
                new HouseholdContext(
                    builder.Head,
                    builder.Occupants,
                    builder.Occupants
                        .FirstOrDefault(
                            person =>
                                person.Age >= 18)
                    ?? builder.Occupants[0]))
            .ToList();
    }

    private IPerson? ResolveFinanceHead(
        IPerson person)
    {
        var head =
            _households.ResolveHouseholdHead(
                person);

        if (head is null
            || !_economy.HasHousehold(
                head))
        {
            return null;
        }

        return head;
    }

    private double ApplyNonFatalDamage(
        IPerson person,
        int minimum,
        int maximum)
    {
        var health =
            _health.GetHealth(
                person);

        var rolled =
            _random.NextInt(
                minimum,
                maximum);

        var target =
            Math.Max(
                1,
                health.Current
                - rolled);

        var actual =
            Math.Max(
                0,
                health.Current
                - target);

        _health.SetHealth(
            person,
            target);

        return actual;
    }

    private decimal RemoveHouseholdWealth(
        IPerson head,
        decimal requestedLoss)
    {
        var finance =
            _economy.GetHousehold(
                head);

        if (finance is null
            || finance.Wealth <= 0)
        {
            return 0;
        }

        var actual =
            Math.Min(
                finance.Wealth,
                Math.Max(
                    0,
                    requestedLoss));

        _economy.ChangeWealth(
            head,
            -actual);

        return actual;
    }

    private decimal RandomMoney(
        int minimum,
        int maximum)
    {
        return _random.NextInt(
            minimum,
            maximum);
    }

    private IReadOnlyList<IPerson> SamplePeople(
        IReadOnlyList<IPerson> people,
        int count)
    {
        var pool =
            people.ToList();

        var selected =
            new List<IPerson>();

        while (pool.Count > 0
            && selected.Count < count)
        {
            var index =
                _random.NextInt(
                    0,
                    pool.Count - 1);

            selected.Add(
                pool[index]);

            pool.RemoveAt(
                index);
        }

        return selected;
    }

    private HouseholdRareEvent? SelectEvent(
        IReadOnlyList<HouseholdEventCandidate> candidates)
    {
        var roll =
            _random.NextDouble();

        var cumulative =
            0.0;

        foreach (var candidate in
            candidates)
        {
            cumulative +=
                candidate.Chance;

            if (roll < cumulative)
            {
                return candidate.Event;
            }
        }

        return null;
    }

    private PersonalRareEvent? SelectEvent(
        IReadOnlyList<PersonalEventCandidate> candidates)
    {
        var roll =
            _random.NextDouble();

        var cumulative =
            0.0;

        foreach (var candidate in
            candidates)
        {
            cumulative +=
                candidate.Chance;

            if (roll < cumulative)
            {
                return candidate.Event;
            }
        }

        return null;
    }

    private void PublishPersonalEvent(
        IGameState gameState,
        IPerson person,
        string eventType,
        string text,
        IReadOnlyDictionary<string, string> data)
    {
        var eventData =
            data.ToDictionary(
                pair =>
                    pair.Key,
                pair =>
                    pair.Value);

        eventData["text"] =
            text;

        _events.Publish(
            new GameEvent
            {
                Type =
                    eventType,

                Year =
                    gameState.Year,

                SubjectId =
                    person.Id,

                Data =
                    eventData
            });
    }

    private void PublishHouseholdEvent(
        IGameState gameState,
        HouseholdContext household,
        string eventType,
        string text,
        IReadOnlyDictionary<string, string> data,
        IPerson? preferredSubject = null)
    {
        var subject =
            preferredSubject
            ?? household.PrimaryOccupant;

        var related =
            household.Occupants
                .Where(
                    person =>
                        person.Id
                        != subject.Id)
                .Select(
                    person =>
                        person.Id)
                .ToList();

        var eventData =
            data.ToDictionary(
                pair =>
                    pair.Key,
                pair =>
                    pair.Value);

        eventData["text"] =
            text;

        _events.Publish(
            new GameEvent
            {
                Type =
                    eventType,

                Year =
                    gameState.Year,

                SubjectId =
                    subject.Id,

                RelatedPersonIds =
                    related,

                Data =
                    eventData
            });
    }

    private string HouseholdDisplayName(
        HouseholdContext household)
    {
        var livingHead =
            household.Head.Tags.Has(
                "state.alive")
                    ? household.Head
                    : household.PrimaryOccupant;

        return _family.GetDisplayName(
            livingHead);
    }

    private enum HouseholdRareEvent
    {
        HouseFire,
        Burglary,
        StormOrFlood,
        StructuralAccident
    }

    private enum PersonalRareEvent
    {
        Assault,
        Mugging,
        WorkplaceAccident,
        TrafficAccident,
        LightningStrike,
        SeriousFall,
        LotteryWin,
        DistantInheritance,
        Fraud,
        FoundProperty,
        WrongfulArrest,
        Suicide
    }

    private sealed record HouseholdEventCandidate(
        HouseholdRareEvent Event,
        double Chance);

    private sealed record PersonalEventCandidate(
        PersonalRareEvent Event,
        double Chance);

    private sealed record HouseholdContext(
        IPerson Head,
        IReadOnlyList<IPerson> Occupants,
        IPerson PrimaryOccupant);

    private sealed class HouseholdContextBuilder
    {
        public HouseholdContextBuilder(
            IPerson head)
        {
            Head =
                head;
        }

        public IPerson Head { get; }

        public List<IPerson> Occupants { get; } =
            [];
    }
}
