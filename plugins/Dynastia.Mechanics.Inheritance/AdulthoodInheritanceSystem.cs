using Dynastia.Contracts;

namespace Dynastia.Mechanics.Inheritance;

public sealed class AdulthoodInheritanceSystem :
    IYearSystem
{
    private readonly IFamilyService _family;
    private readonly IEconomyService _economy;
    private readonly IHeirloomService _heirlooms;
    private readonly IGameEventBus _events;

    public AdulthoodInheritanceSystem(
        IFamilyService family,
        IEconomyService economy,
        IHeirloomService heirlooms,
        IGameEventBus events)
    {
        _family =
            family;

        _economy =
            economy;

        _heirlooms =
            heirlooms;

        _events =
            events;
    }

    public string Id =>
        "inheritance.household_claims";

    public YearPhase Phase =>
        YearPhase.DerivedState;

    public IReadOnlyCollection<string> Before =>
        Array.Empty<string>();

    public IReadOnlyCollection<string> After =>
        ["households.post_inheritance_reconcile"];

    public void Execute(
        IGameState gameState)
    {
        foreach (var person in
            gameState.People
                .Where(
                    person =>
                    {
                        if (!person.Tags.Has(
                                "state.alive")
                            || person.Age < 18)
                        {
                            return false;
                        }

                        var householdId =
                            _economy.GetHouseholdId(
                                person);

                        if (householdId is null)
                            return false;

                        var spouse =
                            _family.GetSpouse(
                                person);

                        return _economy.HasHousehold(
                                person)
                            || _economy
                                .GetHouseholdDynastyAnchorId(
                                    person)
                                == person.Id
                            || (
                                spouse is not null
                                && _economy.GetHouseholdId(
                                    spouse)
                                    == householdId
                            );
                    })
                .ToList())
        {
            TransferPendingCash(
                gameState,
                person);

            TransferPendingHouses(
                gameState,
                person);

            TransferPendingFarmland(
                gameState,
                person);

            TransferPendingHeirlooms(
                gameState,
                person);
        }
    }

    private void TransferPendingCash(
        IGameState gameState,
        IPerson person)
    {
        var amount =
            _economy.GetPendingInheritance(
                person);

        if (amount <= 0)
            return;

        _economy.ChangeWealth(
            person,
            amount);

        _economy.SetPendingInheritance(
            person,
            0);

        _events.Publish(
            new GameEvent
            {
                Type =
                    "inheritance.received_at_household",

                Year =
                    gameState.Year,

                SubjectId =
                    person.Id,

                Data =
                    new Dictionary<string, string>
                    {
                        ["amount"] =
                            amount.ToString(),

                        ["text"] =
                            $"{_family.GetDisplayName(person)} " +
                            $"received {amount:N0} zł of inheritance " +
                            "after establishing a household."
                    }
            });
    }

    private void TransferPendingHouses(
        IGameState gameState,
        IPerson person)
    {
        var houses =
            _economy.TakePendingHouses(
                person);

        if (houses.Count == 0)
            return;

        foreach (var house in
            houses)
        {
            _economy.AddExistingHouse(
                person,
                house with { AssignedHeirId = null });
        }

        _events.Publish(
            new GameEvent
            {
                Type =
                    "inheritance.pending_houses_received",

                Year =
                    gameState.Year,

                SubjectId =
                    person.Id,

                Data =
                    new Dictionary<string, string>
                    {
                        ["count"] =
                            houses.Count.ToString(),

                        ["towns"] =
                            string.Join(
                                ", ",
                                houses.Select(
                                    house =>
                                        house.Town.Town)),

                        ["text"] =
                            $"{_family.GetDisplayName(person)} " +
                            $"received {houses.Count} inherited " +
                            $"house{(houses.Count == 1 ? "" : "s")} " +
                            "after establishing a household."
                    }
            });
    }

    private void TransferPendingFarmland(
        IGameState gameState,
        IPerson person)
    {
        var farmland =
            _economy.TakePendingFarmland(
                person);

        if (farmland.Count == 0)
            return;

        foreach (var parcel in farmland)
        {
            _economy.AddExistingFarmland(
                person,
                parcel);
        }

        _events.Publish(
            new GameEvent
            {
                Type = "farmland.received",
                Year = gameState.Year,
                SubjectId = person.Id,
                Data = new Dictionary<string, string>
                {
                    ["count"] = farmland.Count.ToString(),
                    ["towns"] = string.Join(
                        ", ",
                        farmland.Select(asset => asset.Town.Town)),
                    ["text"] =
                        $"{_family.GetDisplayName(person)} received " +
                        $"{farmland.Count} inherited farmland parcel" +
                        $"{(farmland.Count == 1 ? "" : "s")} after establishing a household."
                }
            });
    }

    private void TransferPendingHeirlooms(
        IGameState gameState,
        IPerson person)
    {
        var heirlooms = _heirlooms.TakePending(person);
        if (heirlooms.Count == 0)
            return;

        foreach (var item in heirlooms)
        {
            _heirlooms.AddExisting(
                person,
                item with { AssignedHeirId = null },
                gameState.Year,
                person.Id,
                "inherited");

            _events.Publish(
                new GameEvent
                {
                    Type = "heirloom.inherited",
                    Year = gameState.Year,
                    SubjectId = person.Id,
                    Data = new Dictionary<string, string>
                    {
                        ["heirloomId"] = item.Id.ToString(),
                        ["item"] = item.DisplayName,
                        ["familyNews"] = "true",
                        ["text"] = $"{_family.GetDisplayName(person)} received inherited family heirloom {item.DisplayName} after establishing a household."
                    }
                });
        }
    }

}
