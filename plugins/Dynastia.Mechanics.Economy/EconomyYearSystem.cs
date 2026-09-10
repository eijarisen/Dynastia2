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
        return person.Tags.Has(
                "state.alive")
            && _family.GetSex(person)
                == Sex.Male
            && _family.IsMaleLineage(
                person)
            && person.Age >= 18;
    }

    private void ProcessLivingHousehold(
        IPerson head)
    {
        var household =
            _economy.GetRequiredHousehold(
                head);

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

        foreach (var child in children)
        {
            if (!child.Tags.Has(
                "state.alive"))
            {
                continue;
            }

            if (child.Age < 18)
            {
                members.Add(
                    child);

                continue;
            }

            if (_family.GetSex(child)
                    == Sex.Female
                && _family.GetSpouse(
                    child) is null)
            {
                members.Add(
                    child);

                adultUnmarriedDaughters.Add(
                    child);
            }
        }

        var income =
            _income.GetAnnualIncome(
                head);

        if (spouse is not null
            && spouse.Tags.Has(
                "state.alive"))
        {
            income +=
                _income.GetAnnualIncome(
                    spouse);
        }

        foreach (var daughter in
            adultUnmarriedDaughters)
        {
            income +=
                _income.GetAnnualIncome(
                    daughter);
        }

        income +=
            household.RentedHouses
            * RentalIncomePerHouse;

        var expenses =
            members.Count
            * LivingExpense;

        if (household.HousesOwned == 0)
        {
            expenses +=
                RentExpense;
        }

        if (household.NannyId.HasValue)
        {
            expenses +=
                NannyExpense;
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

            var spouse =
                _family.GetSpouse(
                    head);

            decimal income =
                0;

            decimal expenses;

            if (spouse is not null
                && spouse.Tags.Has(
                    "state.alive"))
            {
                income +=
                    _income.GetAnnualIncome(
                        spouse);

                expenses =
                    (1 + children.Count)
                    * LivingExpense;
            }
            else
            {
                // Source behavior for orphaned estates:
                // no normal living expenses are deducted.
                expenses =
                    0;
            }

            if (household.NannyId.HasValue)
            {
                expenses +=
                    NannyExpense;
            }

            household.LastIncome =
                income;

            household.LastExpenses =
                expenses;

            _economy.ChangePendingInheritance(
                head,
                income - expenses);
        }
    }
}
