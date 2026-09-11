using Dynastia.Contracts;

namespace Dynastia.Mechanics.Economy;

public sealed class EconomyYearSystem : IYearSystem
{
    private const decimal LivingExpense =
        250m;

    private const decimal RentalIncomePerHouse =
        250m;

    private const decimal RentExpense =
        250m;

    private const decimal NannyExpense =
        250m;

    private readonly StandardEconomyService _economy;
    private readonly IFamilyService _family;
    private readonly IIncomeProviderRegistry _income;

    public EconomyYearSystem(
        StandardEconomyService economy,
        IFamilyService family,
        IIncomeProviderRegistry income)
    {
        _economy = economy;
        _family = family;
        _income = income;
    }

    public string Id =>
        "economy.household_finances";

    public YearPhase Phase =>
        YearPhase.Finances;

    public IReadOnlyCollection<string> Before =>
        Array.Empty<string>();

    public IReadOnlyCollection<string> After =>
        Array.Empty<string>();

    public void Execute(
        IGameState gameState)
    {
        EnsureMaleLineHouseholds(
            gameState);

        foreach (var head in
            gameState.People)
        {
            if (!IsLivingAdultHead(
                head))
            {
                continue;
            }

            ProcessLivingHousehold(
                gameState,
                head);
        }

        ProcessDeceasedEstates(
            gameState);
    }

    private void EnsureMaleLineHouseholds(
        IGameState gameState)
    {
        foreach (var person in
            gameState.People)
        {
            if (_family.GetSex(person)
                    == Sex.Male
                && _family.IsMaleLineage(
                    person))
            {
                _economy.EnsureHousehold(
                    person);
            }
        }
    }

    private bool IsLivingAdultHead(
        IPerson person)
    {
        var dynastyHead =
            _family.GetSex(person)
                == Sex.Male
            && _family.IsMaleLineage(
                person);

        var independentOrphanHead =
            person.Tags.Has(
                "household.independent_orphan")
            && _economy.HasHousehold(
                person);

        return person.Tags.Has(
                "state.alive")
            && person.Age >= 18
            && !person.Tags.Has(
                "residence.orphanage")
            && (
                dynastyHead
                || independentOrphanHead);
    }

    private void ProcessLivingHousehold(
        IGameState gameState,
        IPerson head)
    {
        var household =
            _economy.GetRequiredHousehold(
                head);

        _economy.SynchronizeForFinance(
            head,
            household);

        var spouse =
            _family.GetSpouse(
                head);

        var children =
            _family.GetChildren(
                head);

        var members =
            new List<IPerson>
            {
                head
            };

        if (spouse is not null
            && spouse.Tags.Has(
                "state.alive"))
        {
            members.Add(
                spouse);
        }

        var adultUnmarriedDaughters =
            new List<IPerson>();

        var memberIds =
            members
                .Select(
                    member =>
                        member.Id)
                .ToHashSet();

        foreach (var child in
            children)
        {
            if (!child.Tags.Has(
                "state.alive"))
            {
                continue;
            }

            if (child.Age < 18)
            {
                if (memberIds.Add(
                    child.Id))
                {
                    members.Add(
                        child);
                }

                continue;
            }

            if (_family.GetSex(child)
                    == Sex.Female
                && _family.GetSpouse(
                    child) is null)
            {
                if (memberIds.Add(
                    child.Id))
                {
                    members.Add(
                        child);
                }

                adultUnmarriedDaughters.Add(
                    child);
            }
        }

        foreach (var dependentId in
            _economy.GetHostedDependentIds(
                head))
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
                || dependent.Tags.Has(
                    "role.nanny")
                || dependent.Age >= 18
                || !memberIds.Add(
                    dependent.Id))
            {
                continue;
            }

            members.Add(
                dependent);
        }

        household.LastIncomeBreakdown.Clear();

        var income =
            AddPersonIncome(
                household,
                head);

        if (spouse is not null
            && spouse.Tags.Has(
                "state.alive"))
        {
            income +=
                AddPersonIncome(
                    household,
                    spouse);
        }

        foreach (var daughter in
            adultUnmarriedDaughters)
        {
            income +=
                AddPersonIncome(
                    household,
                    daughter);
        }

        var rentalHouses =
            Math.Max(
                0,
                household.HousesOwned
                - 1);

        var rentalIncome =
            rentalHouses
            * RentalIncomePerHouse;

        if (rentalIncome > 0)
        {
            household.LastIncomeBreakdown.Add(
                new LedgerLineState
                {
                    Label =
                        "houses",

                    Amount =
                        rentalIncome
                });
        }

        income +=
            rentalIncome;

        household.LastExpenseBreakdown.Clear();

        var livingCosts =
            members.Count
            * LivingExpense;

        if (livingCosts > 0)
        {
            household.LastExpenseBreakdown.Add(
                new LedgerLineState
                {
                    Label =
                        "living costs",

                    Amount =
                        livingCosts
                });
        }

        var expenses =
            livingCosts;

        if (household.HousesOwned == 0)
        {
            expenses +=
                RentExpense;

            household.LastExpenseBreakdown.Add(
                new LedgerLineState
                {
                    Label =
                        "rented home",

                    Amount =
                        RentExpense
                });
        }

        if (household.NannyId.HasValue)
        {
            expenses +=
                NannyExpense;

            household.LastExpenseBreakdown.Add(
                new LedgerLineState
                {
                    Label =
                        "nanny",

                    Amount =
                        NannyExpense
                });
        }

        household.LastIncome =
            income;

        household.LastExpenses =
            expenses;

        household.Wealth =
            Math.Max(
                0,
                household.Wealth
                + income
                - expenses);
    }

    private decimal AddPersonIncome(
        HouseholdEconomyComponent household,
        IPerson person)
    {
        var amount =
            _income.GetAnnualIncome(
                person);

        if (amount != 0)
        {
            household.LastIncomeBreakdown.Add(
                new LedgerLineState
                {
                    Label =
                        person.Name,

                    Amount =
                        amount
                });
        }

        return amount;
    }

    private void ProcessDeceasedEstates(
        IGameState gameState)
    {
        foreach (var head in
            gameState.People)
        {
            if (!head.Tags.Has(
                    "state.dead")
                || _family.GetSex(head)
                    != Sex.Male
                || !_family.IsMaleLineage(
                    head)
                || !_economy.HasHousehold(
                    head))
            {
                continue;
            }

            var children =
                _family.GetChildren(
                    head)
                .Where(
                    child =>
                        child.Tags.Has(
                            "state.alive")
                        && child.Age < 18)
                .ToList();

            if (children.Count == 0)
                continue;

            var household =
                _economy.GetRequiredHousehold(
                    head);

            _economy.SynchronizeForFinance(
                head,
                household);

            var spouse =
                _family.GetSpouse(
                    head);

            var caregivers =
                new Dictionary<Guid, IPerson>();

            if (spouse is not null
                && spouse.Tags.Has(
                    "state.alive"))
            {
                caregivers[spouse.Id] =
                    spouse;
            }

            foreach (var child in
                children)
            {
                var mother =
                    _family.GetMother(
                        child);

                if (mother is null
                    || !mother.Tags.Has(
                        "state.alive"))
                {
                    continue;
                }

                var currentSpouse =
                    _family.GetSpouse(
                        mother);

                // A divorced/unmarried mother returns to the deceased
                // father's household to care for the child. If she has
                // remarried, she belongs to that current household instead.
                if (currentSpouse is null
                    || currentSpouse.Id
                        == head.Id)
                {
                    caregivers[mother.Id] =
                        mother;
                }
            }

            household.LastIncomeBreakdown.Clear();
            household.LastExpenseBreakdown.Clear();

            decimal income =
                0;

            foreach (var caregiver in
                caregivers.Values)
            {
                income +=
                    AddPersonIncome(
                        household,
                        caregiver);
            }

            decimal expenses =
                0;

            if (caregivers.Count > 0)
            {
                expenses =
                    (caregivers.Count
                        + children.Count)
                    * LivingExpense;

                household.LastExpenseBreakdown.Add(
                    new LedgerLineState
                    {
                        Label =
                            "living costs",

                        Amount =
                            expenses
                    });
            }

            if (household.NannyId.HasValue)
            {
                expenses +=
                    NannyExpense;

                household.LastExpenseBreakdown.Add(
                    new LedgerLineState
                    {
                        Label =
                            "nanny",

                        Amount =
                            NannyExpense
                    });
            }

            household.LastIncome =
                income;

            household.LastExpenses =
                expenses;

            _economy.ChangePendingInheritance(
                head,
                income
                - expenses);
        }
    }
}
