using Dynastia.Contracts;

namespace Dynastia.Mechanics.RareEvents;

internal sealed partial class RareEventYearSystem
{
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

            candidates.RemoveAll(
                candidate =>
                    !_availability.IsAvailable(
                        GetEventId(candidate.Event),
                        gameState.Year));

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
            && career.IsEmployed
            && !career.IsRetired)
        {
            candidates.Add(
                new PersonalEventCandidate(
                    PersonalRareEvent.WorkplaceAccident,
                    WorkplaceAccidentChance));
        }

        if (person.Age >= 10)
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
            candidates.Add(
                new PersonalEventCandidate(
                    PersonalRareEvent.LotteryWin,
                    LotteryChance));

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

        candidates.RemoveAll(
            candidate =>
                !_availability.IsAvailable(
                    GetEventId(candidate.Event),
                    gameState.Year));

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

}
