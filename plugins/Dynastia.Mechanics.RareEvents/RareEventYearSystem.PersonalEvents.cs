using Dynastia.Contracts;

namespace Dynastia.Mechanics.RareEvents;

internal sealed partial class RareEventYearSystem
{
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
        IPerson person,
        RareEventDefinition definition)
    {
        var damage =
            ApplyNonFatalDamage(
                person,
                20,
                50);

        var fatal =
            _random.NextDouble()
            < WorkplaceDeathChance;

        var definitionName =
            _variants.ResolveName(
                definition,
                gameState.Year);

        PublishPersonalEvent(
            gameState,
            person,
            "rare.workplace_accident",
            $"{_family.GetDisplayName(person)} suffered a serious {definitionName.ToLowerInvariant()}.",
            new Dictionary<string, string>
            {
                ["healthDamage"] =
                    damage.ToString(
                        "0"),

                ["fatal"] =
                    fatal.ToString()
            },
            definitionName);

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
        IPerson person,
        RareEventDefinition definition)
    {
        var damage =
            ApplyNonFatalDamage(
                person,
                20,
                60);

        var fatal =
            _random.NextDouble()
            < TrafficDeathChance;

        var definitionName =
            _variants.ResolveName(
                definition,
                gameState.Year);

        PublishPersonalEvent(
            gameState,
            person,
            "rare.traffic_accident",
            $"{_family.GetDisplayName(person)} was badly injured in a {definitionName.ToLowerInvariant()}.",
            new Dictionary<string, string>
            {
                ["healthDamage"] =
                    damage.ToString(
                        "0"),

                ["fatal"] =
                    fatal.ToString()
            },
            definitionName);

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
            career.IsEmployed
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

}
