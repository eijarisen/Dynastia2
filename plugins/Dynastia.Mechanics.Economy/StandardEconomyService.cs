using Dynastia.Contracts;

namespace Dynastia.Mechanics.Economy;

public sealed partial class StandardEconomyService :
    IEconomyService,
    IEconomyBalanceService
{
    private const decimal BaseHousePrice = 20000m;

    public decimal OrdinaryLivingCostUnit => 250m;

    private readonly IGameState _gameState;
    private readonly IFamilyService _family;
    private readonly ILocationService _locations;
    private readonly IIncomeProviderRegistry _income;
    private readonly IHouseholdIncomeProviderRegistry _householdIncome;

    public StandardEconomyService(
        IGameState gameState,
        IFamilyService family,
        ILocationService locations,
        IIncomeProviderRegistry income,
        IHouseholdIncomeProviderRegistry householdIncome)
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
    }

    public bool HasHousehold(
        IPerson person)
    {
        var direct =
            person.Components.Get<
                HouseholdEconomyComponent>();

        if (direct is null)
            return false;

        MigrateHousehold(
            person,
            direct);

        return direct.HeadId
            == person.Id;
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

        SynchronizeHouses(
            owner,
            household);

        var claim =
            GetClaim(
                person);

        SynchronizePendingHouses(
            person,
            claim);

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
            claim.PendingInheritance,
            claim.PendingHouseProperties.Count,
            household.NannyId,
            household.LastIncome,
            household.LastExpenses,
            household.LastIncomeBreakdown
                .Select(
                    line =>
                        new FinanceBreakdownItem(
                            line.Label,
                            line.Amount))
                .ToList(),
            household.LastExpenseBreakdown
                .Select(
                    line =>
                        new FinanceBreakdownItem(
                            line.Label,
                            line.Amount))
                .ToList(),
            houses);
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
