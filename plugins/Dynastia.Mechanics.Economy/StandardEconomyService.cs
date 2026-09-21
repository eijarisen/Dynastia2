using Dynastia.Contracts;

namespace Dynastia.Mechanics.Economy;

public sealed partial class StandardEconomyService :
    IEconomyService,
    IEconomyBalanceService,
    IHouseholdCapacityService
{
    private const decimal BaseHousePrice = 40000m;

    public decimal OrdinaryLivingCostUnit => 500m;
    public decimal NannyAnnualCost => EconomyAnnualRules.NannyExpense;

    private readonly IGameState _gameState;
    private readonly IFamilyService _family;
    private readonly ILocationService _locations;
    private readonly IIncomeProviderRegistry _income;
    private readonly IHouseholdIncomeProviderRegistry _householdIncome;
    private readonly IHouseholdFinanceProjectionProviderRegistry _financeProjections;
    private readonly IStatsService _stats;
    private readonly IGameRandom _random;
    private readonly ITownProsperityService _prosperity;
    private readonly HouseMarketRules _houseMarketRules;
    private readonly ILocalServiceTownResolver? _localServiceTowns;

    public StandardEconomyService(
        IGameState gameState,
        IFamilyService family,
        ILocationService locations,
        IIncomeProviderRegistry income,
        IHouseholdIncomeProviderRegistry householdIncome,
        IHouseholdFinanceProjectionProviderRegistry financeProjections,
        IStatsService stats,
        IGameRandom random)
        : this(
            gameState,
            family,
            locations,
            income,
            householdIncome,
            financeProjections,
            stats,
            random,
            NeutralTownProsperityService.Instance,
            HouseMarketRules.CreateDefault(),
            null)
    {
    }

    internal StandardEconomyService(
        IGameState gameState,
        IFamilyService family,
        ILocationService locations,
        IIncomeProviderRegistry income,
        IHouseholdIncomeProviderRegistry householdIncome,
        IHouseholdFinanceProjectionProviderRegistry financeProjections,
        IStatsService stats,
        IGameRandom random,
        ITownProsperityService prosperity,
        HouseMarketRules houseMarketRules,
        ILocalServiceTownResolver? localServiceTowns = null)
    {
        _gameState =
            gameState;

        _family =
            family;

        _locations =
            locations;

        _income =
            income;

        _householdIncome =
            householdIncome;

        _financeProjections =
            financeProjections;

        _stats =
            stats;

        _random =
            random;

        _prosperity =
            prosperity;

        _houseMarketRules =
            houseMarketRules;

        _localServiceTowns =
            localServiceTowns;
    }

    public bool HasHousehold(
        IPerson person)
    {
        var direct =
            person.Components.Get<
                HouseholdEconomyComponent>();

        return direct is not null
            && direct.HeadId == person.Id;
    }

    public void EnsureHousehold(
        IPerson person)
    {
        if (HasHousehold(person))
            return;

        if (!person.Tags.Has(
                "state.alive")
            || person.Age < 18
            || _family.GetSex(person)
                != Sex.Male
            || !_family.IsMaleLineage(
                person))
        {
            return;
        }

        CreateHousehold(
            person,
            person);
    }

    public void EnsureIndependentHousehold(
        IPerson person,
        IPerson? dynastyAnchor = null)
    {
        if (HasHousehold(person))
            return;

        var existing =
            FindHousehold(
                person);

        if (existing is not null)
        {
            RemoveHouseholdMember(
                person);
        }

        CreateHousehold(
            person,
            dynastyAnchor
            ?? person);
    }

    public HouseholdFinanceSnapshot?
        GetHousehold(
            IPerson person)
    {
        var resolved =
            FindHousehold(
                person);

        if (resolved is null)
            return null;

        var (
            owner,
            household) =
                resolved.Value;

        var claim =
            person.Components.Get<
                PersonalEstateComponent>();

        var houses =
            household.Houses
                .Select(
                    (house, index) =>
                        ToInfo(
                            house,
                            index,
                            owner,
                            household))
                .ToList();

        return new HouseholdFinanceSnapshot(
            household.Wealth,
            houses.Count,
            houses.Count(house => house.IsRented),
            claim?.PendingInheritance ?? 0m,
            claim?.PendingHouseProperties.Count ?? 0,
            household.NannyId,
            household.LastIncome,
            household.LastExpenses,
            household.LastIncomeBreakdown
                .Select(
                    line =>
                        new FinanceBreakdownItem(
                            line.Label,
                            line.Amount,
                            line.PersonId))
                .ToList(),
            household.LastExpenseBreakdown
                .Select(
                    line =>
                        new FinanceBreakdownItem(
                            line.Label,
                            line.Amount,
                            line.PersonId))
                .ToList(),
            houses)
        {
            History = household.BudgetHistory
                .OrderBy(point => point.Year)
                .ToList(),
            Lifestyle = household.Lifestyle
        };
    }

    public void RecordRealizedExpense(
        IPerson person,
        string label,
        decimal amount)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(label);

        var rounded = RoundCurrency(amount);
        if (rounded <= 0m)
            return;

        var resolved = FindHousehold(person);
        if (resolved is null)
            return;

        var household = resolved.Value.Household;
        household.LastExpenses = RoundCurrency(
            household.LastExpenses + rounded);

        var existing = household.LastExpenseBreakdown
            .FirstOrDefault(line => line.Label.Equals(
                label,
                StringComparison.OrdinalIgnoreCase));

        if (existing is null)
        {
            household.LastExpenseBreakdown.Add(
                new LedgerLineState
                {
                    Label = label,
                    Amount = rounded
                });
        }
        else
        {
            existing.Amount = RoundCurrency(existing.Amount + rounded);
        }
    }

    public bool CanAfford(
        IPerson person,
        decimal amount)
    {
        if (amount < 0m)
            return false;

        var resolved = FindHousehold(person);
        return resolved is not null
            && resolved.Value.Household.Wealth >= RoundCurrency(amount);
    }

    public Guid? GetHouseholdId(
        IPerson person)
    {
        var resolved =
            FindHousehold(
                person);

        return resolved is null
            ? null
            : resolved.Value
                .Household
                .HouseholdId;
    }

    public Guid? GetHouseholdDynastyAnchorId(
        IPerson person)
    {
        var resolved =
            FindHousehold(
                person);

        return resolved is null
            ? null
            : resolved.Value
                .Household
                .DynastyAnchorId;
    }

    public IReadOnlyList<Guid> GetHouseholdMemberIds(
        IPerson person)
    {
        var resolved =
            FindHousehold(
                person);

        return resolved is null
            ? Array.Empty<Guid>()
            : resolved.Value
                .Household
                .MemberIds
                .Distinct()
                .ToList();
    }

    public bool IsLegacyMembershipSeeded(
        IPerson householdRepresentative)
    {
        var resolved =
            FindHousehold(
                householdRepresentative);

        return resolved is not null
            && resolved.Value
                .Household
                .LegacyMembershipSeeded;
    }

    public void MarkLegacyMembershipSeeded(
        IPerson householdRepresentative)
    {
        GetRequiredHousehold(
            householdRepresentative)
            .LegacyMembershipSeeded =
                true;
    }

    public void AddHouseholdMember(
        IPerson householdRepresentative,
        IPerson member)
    {
        var target =
            GetRequiredHousehold(
                householdRepresentative);

        var old =
            FindHousehold(
                member);

        if (old is not null
            && old.Value.Household.HouseholdId
                != target.HouseholdId)
        {
            if (old.Value.Household.HeadId
                == member.Id)
            {
                throw new InvalidOperationException(
                    $"{_family.GetDisplayName(member)} already heads " +
                    "another active household; household transfer must be " +
                    "resolved before membership can change.");
            }

            old.Value
                .Household
                .MemberIds
                .Remove(
                    member.Id);
        }

        if (!target.MemberIds.Contains(
            member.Id))
        {
            target.MemberIds.Add(
                member.Id);
        }

        SynchronizeLifestyleTags(target);
    }

    public void RemoveHouseholdMember(
        IPerson member)
    {
        var resolved =
            FindHousehold(
                member);

        if (resolved is null)
            return;

        resolved.Value
            .Household
            .MemberIds
            .Remove(
                member.Id);

        member.Tags.Remove(HouseholdLifestyleRules.LavishTag);
        member.Tags.Remove(HouseholdLifestyleRules.ThriftyTag);
    }

    public void TransferHouseholdHead(
        IPerson currentHead,
        IPerson newHead)
    {
        var current =
            currentHead.Components.Get<
                HouseholdEconomyComponent>();

        if (current is null)
        {
            throw new InvalidOperationException(
                $"{_family.GetDisplayName(currentHead)} " +
                "is not the current household head.");
        }

        MigrateHousehold(
            currentHead,
            current);

        if (current.HeadId
            != currentHead.Id)
        {
            throw new InvalidOperationException(
                "Household component is not attached to its current head.");
        }

        var previous =
            newHead.Components.Get<
                HouseholdEconomyComponent>();

        if (previous is not null
            && previous.HouseholdId
                != current.HouseholdId)
        {
            throw new InvalidOperationException(
                $"{_family.GetDisplayName(newHead)} already heads another household.");
        }

        currentHead.Components.Remove<
            HouseholdEconomyComponent>();

        current.HeadId =
            newHead.Id;

        current.EstateReady =
            false;

        current.MemberIds.Remove(
            currentHead.Id);

        if (!current.MemberIds.Contains(
            newHead.Id))
        {
            current.MemberIds.Insert(
                0,
                newHead.Id);
        }

        newHead.Components.Set(
            current);

        var residenceTown =
            !string.IsNullOrWhiteSpace(
                current.ResidenceTownId)
                ? _locations.FindTown(
                    current.ResidenceTownId)
                : null;

        residenceTown ??=
            _locations.GetLocation(
                currentHead)
            .HomeTown;

        current.ResidenceTownId =
            residenceTown.Id;

        SetMemberHomeTowns(
            current,
            residenceTown);

        SynchronizeDerivedHouseCounts(
            current);
    }

    public void MarkEstateReady(
        IPerson householdRepresentative,
        bool ready = true)
    {
        GetRequiredHousehold(
            householdRepresentative)
            .EstateReady =
                ready;
    }

    public bool IsEstateReady(
        IPerson householdRepresentative)
    {
        var resolved =
            FindHousehold(
                householdRepresentative);

        return resolved is not null
            && resolved.Value
                .Household
                .EstateReady;
    }

    public void DissolveHousehold(
        IPerson householdRepresentative)
    {
        var resolved =
            FindHousehold(
                householdRepresentative);

        if (resolved is null)
            return;

        var householdId =
            resolved.Value
                .Household
                .HouseholdId;

        foreach (var person in
            _gameState.People)
        {
            var component =
                person.Components.Get<
                    HouseholdEconomyComponent>();

            if (component is not null)
            {
                MigrateHousehold(
                    person,
                    component);

                if (component.HouseholdId
                    == householdId)
                {
                    person.Components.Remove<
                        HouseholdEconomyComponent>();
                }
            }
        }
    }

}
