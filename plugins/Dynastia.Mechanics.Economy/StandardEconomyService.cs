using Dynastia.Contracts;

namespace Dynastia.Mechanics.Economy;

public sealed class StandardEconomyService :
    IEconomyService,
    IEconomyBalanceService
{
    public decimal OrdinaryLivingCostUnit => 250m;

    private readonly IGameState _gameState;
    private readonly IFamilyService _family;
    private readonly ILocationService _locations;

    public StandardEconomyService(
        IGameState gameState,
        IFamilyService family,
        ILocationService locations)
    {
        _gameState =
            gameState;

        _family =
            family;

        _locations =
            locations;
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
                            index))
                .ToList();

        return new HouseholdFinanceSnapshot(
            household.Wealth,
            houses.Count,
            Math.Max(
                0,
                houses.Count - 1),
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

        if (current.Houses.Count > 0)
        {
            var residence =
                current.Houses[0].Town;

            if (residence is not null)
            {
                _locations.SetHouseholdHomeTown(
                    newHead,
                    residence);
            }
        }
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

    public void SetWealth(
        IPerson person,
        decimal wealth)
    {
        var household =
            GetRequiredHousehold(
                person);

        household.Wealth =
            Math.Max(
                0,
                wealth);
    }

    public void ChangeWealth(
        IPerson person,
        decimal amount)
    {
        var household =
            GetRequiredHousehold(
                person);

        household.Wealth =
            Math.Max(
                0,
                household.Wealth
                + amount);
    }

    public void SetHousesOwned(
        IPerson person,
        int housesOwned)
    {
        var household =
            GetRequiredHousehold(
                person);

        var owner =
            GetHead(
                household);

        SynchronizeHouses(
            owner,
            household);

        var target =
            Math.Max(
                0,
                housesOwned);

        while (household.Houses.Count
            < target)
        {
            AddHouse(
                person);
        }

        while (household.Houses.Count
            > target)
        {
            household.Houses.RemoveAt(
                household.Houses.Count
                - 1);
        }

        SynchronizeDerivedHouseCounts(
            household);
    }

    public void SetRentedHouses(
        IPerson person,
        int rentedHouses)
    {
        var household =
            GetRequiredHousehold(
                person);

        SynchronizeDerivedHouseCounts(
            household);
    }

    public IReadOnlyList<HousePropertyInfo> GetHouses(
        IPerson person)
    {
        var household =
            GetRequiredHousehold(
                person);

        SynchronizeHouses(
            GetHead(household),
            household);

        return household.Houses
            .Select(
                (house, index) =>
                    ToInfo(
                        house,
                        index))
            .ToList();
    }

    public HousePropertyInfo AddHouse(
        IPerson person,
        TownInfo? town = null)
    {
        var household =
            GetRequiredHousehold(
                person);

        var head =
            GetHead(
                household);

        SynchronizeHouses(
            head,
            household);

        var firstHouse =
            household.Houses.Count == 0;

        var assignedTown =
            town
            ?? (
                firstHouse
                    ? _locations
                        .GetLocation(
                            head)
                        .HomeTown
                    : _locations
                        .ChoosePropertyTown(
                            head)
            );

        var state =
            new HousePropertyState
            {
                Id =
                    Guid.NewGuid(),

                Town =
                    assignedTown
            };

        household.Houses.Add(
            state);

        if (firstHouse)
        {
            _locations.SetHouseholdHomeTown(
                head,
                assignedTown);
        }

        SynchronizeDerivedHouseCounts(
            household);

        return ToInfo(
            state,
            household.Houses.Count - 1);
    }

    public void AddExistingHouse(
        IPerson person,
        HousePropertyInfo house)
    {
        ArgumentNullException.ThrowIfNull(
            house);

        var household =
            GetRequiredHousehold(
                person);

        var head =
            GetHead(
                household);

        SynchronizeHouses(
            head,
            household);

        if (household.Houses.Any(
            existing =>
                existing.Id
                == house.Id))
        {
            return;
        }

        var firstHouse =
            household.Houses.Count == 0;

        household.Houses.Add(
            new HousePropertyState
            {
                Id =
                    house.Id,

                Town =
                    house.Town
            });

        if (firstHouse)
        {
            _locations.SetHouseholdHomeTown(
                head,
                house.Town);
        }

        SynchronizeDerivedHouseCounts(
            household);
    }

    public HousePropertyInfo? TakeAdditionalHouse(
        IPerson person)
    {
        var household =
            GetRequiredHousehold(
                person);

        SynchronizeHouses(
            GetHead(household),
            household);

        if (household.Houses.Count <= 1)
            return null;

        var index =
            household.Houses.Count - 1;

        var state =
            household.Houses[index];

        var info =
            ToInfo(
                state,
                index);

        household.Houses.RemoveAt(
            index);

        SynchronizeDerivedHouseCounts(
            household);

        return info;
    }

    public IReadOnlyList<HousePropertyInfo> TakeAllHouses(
        IPerson person)
    {
        var household =
            GetRequiredHousehold(
                person);

        SynchronizeHouses(
            GetHead(household),
            household);

        var houses =
            household.Houses
                .Select(
                    (house, index) =>
                        ToInfo(
                            house,
                            index))
                .ToList();

        household.Houses.Clear();

        SynchronizeDerivedHouseCounts(
            household);

        return houses;
    }

    public decimal GetPendingInheritance(
        IPerson person)
    {
        return GetClaim(
            person)
            .PendingInheritance;
    }

    public void SetPendingInheritance(
        IPerson person,
        decimal amount)
    {
        GetClaim(
            person)
            .PendingInheritance =
                Math.Max(
                    0,
                    amount);
    }

    public void ChangePendingInheritance(
        IPerson person,
        decimal amount)
    {
        SetPendingInheritance(
            person,
            GetPendingInheritance(person)
            + amount);
    }

    public int GetPendingHouses(
        IPerson person)
    {
        var claim =
            GetClaim(
                person);

        SynchronizePendingHouses(
            person,
            claim);

        return claim
            .PendingHouseProperties
            .Count;
    }

    public void SetPendingHouses(
        IPerson person,
        int houses)
    {
        var claim =
            GetClaim(
                person);

        SynchronizePendingHouses(
            person,
            claim);

        var target =
            Math.Max(
                0,
                houses);

        while (claim.PendingHouseProperties.Count
            < target)
        {
            claim.PendingHouseProperties.Add(
                CreatePendingHouse(
                    person,
                    claim.PendingHouseProperties.Count));
        }

        while (claim.PendingHouseProperties.Count
            > target)
        {
            claim.PendingHouseProperties.RemoveAt(
                claim.PendingHouseProperties.Count
                - 1);
        }

        claim.PendingHouses =
            claim.PendingHouseProperties.Count;
    }

    public void ChangePendingHouses(
        IPerson person,
        int houses)
    {
        SetPendingHouses(
            person,
            GetPendingHouses(person)
            + houses);
    }

    public void AddPendingHouse(
        IPerson person,
        HousePropertyInfo house)
    {
        ArgumentNullException.ThrowIfNull(
            house);

        var claim =
            GetClaim(
                person);

        SynchronizePendingHouses(
            person,
            claim);

        if (!claim.PendingHouseProperties.Any(
            existing =>
                existing.Id
                == house.Id))
        {
            claim.PendingHouseProperties.Add(
                new HousePropertyState
                {
                    Id =
                        house.Id,

                    Town =
                        house.Town
                });
        }

        claim.PendingHouses =
            claim.PendingHouseProperties.Count;
    }

    public IReadOnlyList<HousePropertyInfo> TakePendingHouses(
        IPerson person)
    {
        var claim =
            GetClaim(
                person);

        SynchronizePendingHouses(
            person,
            claim);

        var result =
            claim.PendingHouseProperties
                .Select(
                    (house, index) =>
                        ToInfo(
                            house,
                            index))
                .ToList();

        claim.PendingHouseProperties.Clear();
        claim.PendingHouses =
            0;

        return result;
    }

    public void SetNanny(
        IPerson person,
        Guid? nannyId)
    {
        GetRequiredHousehold(
            person)
            .NannyId =
                nannyId;
    }

    public IReadOnlyList<Guid> GetHostedDependentIds(
        IPerson householdHead)
    {
        return GetRequiredHousehold(
                householdHead)
            .HostedDependentIds
            .ToList();
    }

    public void AddHostedDependent(
        IPerson householdHead,
        IPerson dependent)
    {
        var household =
            GetRequiredHousehold(
                householdHead);

        if (!household.HostedDependentIds.Contains(
            dependent.Id))
        {
            household.HostedDependentIds.Add(
                dependent.Id);
        }

        AddHouseholdMember(
            householdHead,
            dependent);
    }

    public void RemoveHostedDependent(
        IPerson householdHead,
        IPerson dependent)
    {
        var resolved =
            FindHousehold(
                householdHead);

        if (resolved is null)
            return;

        resolved.Value
            .Household
            .HostedDependentIds
            .Remove(
                dependent.Id);

        resolved.Value
            .Household
            .MemberIds
            .Remove(
                dependent.Id);
    }

    internal HouseholdEconomyComponent
        GetRequiredHousehold(
            IPerson person)
    {
        var resolved =
            FindHousehold(
                person);

        if (resolved is not null)
            return resolved.Value.Household;

        EnsureHousehold(
            person);

        resolved =
            FindHousehold(
                person);

        if (resolved is not null)
        {
            return resolved.Value
                .Household;
        }

        throw new InvalidOperationException(
            $"{_family.GetDisplayName(person)} " +
            "does not belong to an active dynasty household.");
    }

    internal void SynchronizeForFinance(
        IPerson person,
        HouseholdEconomyComponent household)
    {
        SynchronizeHouses(
            GetHead(
                household),
            household);
    }

    private void CreateHousehold(
        IPerson head,
        IPerson dynastyAnchor)
    {
        var component =
            new HouseholdEconomyComponent
            {
                HouseholdId =
                    Guid.NewGuid(),

                HeadId =
                    head.Id,

                DynastyAnchorId =
                    dynastyAnchor.Id,

                DynastyGeneration =
                    _family.GetGeneration(
                        dynastyAnchor),

                LegacyMembershipSeeded =
                    true
            };

        component.MemberIds.Add(
            head.Id);

        if (dynastyAnchor.Id
            != head.Id)
        {
            component.MemberIds.Add(
                dynastyAnchor.Id);
        }

        head.Components.Set(
            component);
    }

    private (
        IPerson Owner,
        HouseholdEconomyComponent Household)?
        FindHousehold(
            IPerson person)
    {
        var direct =
            person.Components.Get<
                HouseholdEconomyComponent>();

        if (direct is not null)
        {
            MigrateHousehold(
                person,
                direct);

            return (
                person,
                direct);
        }

        foreach (var candidate in
            _gameState.People)
        {
            var household =
                candidate.Components.Get<
                    HouseholdEconomyComponent>();

            if (household is null)
                continue;

            MigrateHousehold(
                candidate,
                household);

            if (household.MemberIds.Contains(
                person.Id))
            {
                return (
                    candidate,
                    household);
            }
        }

        return null;
    }

    private void MigrateHousehold(
        IPerson owner,
        HouseholdEconomyComponent household)
    {
        if (household.HouseholdId
            == Guid.Empty)
        {
            household.HouseholdId =
                Guid.NewGuid();
        }

        if (household.HeadId
            == Guid.Empty)
        {
            household.HeadId =
                owner.Id;
        }

        if (household.DynastyAnchorId
            == Guid.Empty)
        {
            var spouse =
                _family.GetSpouse(
                    owner);

            household.DynastyAnchorId =
                _family.IsBloodline(
                    owner)
                    ? owner.Id
                    : spouse is not null
                      && _family.IsBloodline(
                          spouse)
                        ? spouse.Id
                        : owner.Id;
        }

        if (household.DynastyGeneration
            is null)
        {
            var anchor =
                _gameState.People
                    .FirstOrDefault(
                        person =>
                            person.Id
                            == household.DynastyAnchorId);

            household.DynastyGeneration =
                anchor is null
                    ? _family.GetGeneration(
                        owner)
                    : _family.GetGeneration(
                        anchor);
        }

        if (!household.MemberIds.Contains(
            household.HeadId))
        {
            household.MemberIds.Insert(
                0,
                household.HeadId);
        }
    }

    private IPerson GetHead(
        HouseholdEconomyComponent household)
    {
        return _gameState.People
            .FirstOrDefault(
                person =>
                    person.Id
                    == household.HeadId)
            ?? throw new InvalidOperationException(
                "Household head is unavailable.");
    }

    private void SynchronizeHouses(
        IPerson head,
        HouseholdEconomyComponent household)
    {
        if (household.Houses.Count == 0
            && household.HousesOwned > 0)
        {
            for (var index = 0;
                index < household.HousesOwned;
                index++)
            {
                var town =
                    index == 0
                        ? _locations
                            .GetLocation(
                                head)
                            .HomeTown
                        : _locations
                            .ChoosePropertyTown(
                                head);

                household.Houses.Add(
                    new HousePropertyState
                    {
                        Id =
                            Guid.NewGuid(),

                        Town =
                            town
                    });
            }
        }

        foreach (var house in
            household.Houses)
        {
            house.Id =
                house.Id == Guid.Empty
                    ? Guid.NewGuid()
                    : house.Id;

            house.Town ??=
                _locations
                    .GetLocation(
                        head)
                    .HomeTown;
        }

        SynchronizeDerivedHouseCounts(
            household);
    }

    private void SynchronizePendingHouses(
        IPerson person,
        PersonalEstateComponent claim)
    {
        if (claim.PendingHouseProperties.Count == 0
            && claim.PendingHouses > 0)
        {
            for (var index = 0;
                index < claim.PendingHouses;
                index++)
            {
                claim.PendingHouseProperties.Add(
                    CreatePendingHouse(
                        person,
                        index));
            }
        }

        foreach (var house in
            claim.PendingHouseProperties)
        {
            house.Id =
                house.Id == Guid.Empty
                    ? Guid.NewGuid()
                    : house.Id;

            house.Town ??=
                _locations
                    .GetLocation(
                        person)
                    .HomeTown;
        }

        claim.PendingHouses =
            claim.PendingHouseProperties.Count;
    }

    private HousePropertyState CreatePendingHouse(
        IPerson person,
        int index)
    {
        var town =
            index == 0
                ? _locations
                    .GetLocation(
                        person)
                    .HomeTown
                : _locations
                    .ChoosePropertyTown(
                        person);

        return new HousePropertyState
        {
            Id =
                Guid.NewGuid(),

            Town =
                town
        };
    }

    private static void SynchronizeDerivedHouseCounts(
        HouseholdEconomyComponent household)
    {
        household.HousesOwned =
            household.Houses.Count;

        household.RentedHouses =
            Math.Max(
                0,
                household.Houses.Count
                - 1);
    }

    private static HousePropertyInfo ToInfo(
        HousePropertyState house,
        int index)
    {
        var town =
            house.Town
            ?? throw new InvalidOperationException(
                "House town is unavailable.");

        return new HousePropertyInfo(
            house.Id,
            town,
            IsResidence:
                index == 0,
            IsRented:
                index > 0);
    }

    private static PersonalEstateComponent
        GetClaim(
            IPerson person)
    {
        var claim =
            person.Components.Get<
                PersonalEstateComponent>();

        if (claim is not null)
            return claim;

        claim =
            new PersonalEstateComponent();

        person.Components.Set(
            claim);

        return claim;
    }
}
