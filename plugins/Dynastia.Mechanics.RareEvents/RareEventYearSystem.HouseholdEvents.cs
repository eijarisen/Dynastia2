using Dynastia.Contracts;

namespace Dynastia.Mechanics.RareEvents;

internal sealed partial class RareEventYearSystem
{
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
            var permanentExposures =
                new List<(IPerson Person, double Damage)>();

            foreach (var person in
                injured)
            {
                var amount =
                    ApplyNonFatalDamage(
                        person,
                        10,
                        30);
                permanentExposures.Add((person, amount));

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

            foreach (var exposure in permanentExposures)
            {
                PublishPermanentInjuryExposure(
                    gameState,
                    exposure.Person,
                    "rare.house_fire",
                    exposure.Damage);
            }

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

        var catastrophicExposures =
            new List<(IPerson Person, double Damage)>();
        foreach (var occupant in
            household.Occupants)
        {
            var damage = ApplyNonFatalDamage(
                occupant,
                25,
                50);
            catastrophicExposures.Add((occupant, damage));
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

        foreach (var exposure in catastrophicExposures)
        {
            PublishPermanentInjuryExposure(
                gameState,
                exposure.Person,
                "rare.house_fire",
                exposure.Damage);
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
            "rare.storm_flood",
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

        if (injured is not null)
        {
            PublishPermanentInjuryExposure(
                gameState,
                injured,
                "rare.storm_flood",
                damage);
        }
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

        if (injured is not null)
        {
            PublishPermanentInjuryExposure(
                gameState,
                injured,
                "rare.structural_accident",
                damage);
        }
    }

}
