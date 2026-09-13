using Dynastia.Contracts;

namespace Dynastia.Mechanics.Households;

public sealed class StandardPropertyActionService :
    IPropertyActionService
{
    private readonly IGameState _gameState;
    private readonly IEconomyService _economy;
    private readonly IPropertyEconomyService _propertyEconomy;
    private readonly ITownDirectoryService _towns;
    private readonly ITownEconomyService _townEconomy;
    private readonly ITownCareerOpportunityService _opportunities;
    private readonly ICareerMobilityService _careerMobility;
    private readonly ICareerService _career;
    private readonly IFamilyService _family;
    private readonly IGameEventBus _events;

    public StandardPropertyActionService(
        IGameState gameState,
        IEconomyService economy,
        IPropertyEconomyService propertyEconomy,
        ITownDirectoryService towns,
        ITownEconomyService townEconomy,
        ITownCareerOpportunityService opportunities,
        ICareerMobilityService careerMobility,
        ICareerService career,
        IFamilyService family,
        IGameEventBus events)
    {
        _gameState = gameState;
        _economy = economy;
        _propertyEconomy = propertyEconomy;
        _towns = towns;
        _townEconomy = townEconomy;
        _opportunities = opportunities;
        _careerMobility = careerMobility;
        _career = career;
        _family = family;
        _events = events;
    }

    public IReadOnlyList<PropertyTownOption> GetPurchaseOptions(
        IPerson householdHead)
    {
        ArgumentNullException.ThrowIfNull(householdHead);

        return _towns.GetAllTowns()
            .Select(
                town =>
                {
                    var economy = _townEconomy.GetProfile(town);
                    var opportunities = _opportunities.GetOpportunitySnapshot(town);

                    return new PropertyTownOption(
                        town,
                        opportunities.RegionName,
                        opportunities.Description,
                        economy.HousePrice,
                        economy.LivingCostIndex,
                        economy.LivingCostLevel);
                })
            .OrderBy(option => option.Town.Town, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(option => option.Town.County, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    public IReadOnlyList<PropertySaleOption> GetSaleOptions(
        IPerson householdHead)
    {
        ArgumentNullException.ThrowIfNull(householdHead);

        return _economy.GetHouses(householdHead)
            .Select(
                house =>
                {
                    var economy = _townEconomy.GetProfile(house.Town);
                    var opportunities = _opportunities.GetOpportunitySnapshot(house.Town);

                    return new PropertySaleOption(
                        house.Id,
                        house.Town,
                        opportunities.RegionName,
                        house.IsResidence,
                        economy.HousePrice,
                        RoundMoney(economy.HousePrice * 0.80m));
                })
            .OrderByDescending(option => option.IsResidence)
            .ThenBy(option => option.Town.Town, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    public void PreparePurchase(
        IPerson householdHead,
        string townId)
    {
        var selection = GetSelection(householdHead);
        selection.PurchaseTownId = townId;
        selection.SalePropertyId = null;
        householdHead.Components.Set(selection);
    }

    public void PrepareSale(
        IPerson householdHead,
        Guid propertyId)
    {
        var selection = GetSelection(householdHead);
        selection.SalePropertyId = propertyId;
        selection.PurchaseTownId = null;
        householdHead.Components.Set(selection);
    }

    internal GameActionResult ExecutePreparedPurchase(
        IPerson householdHead,
        int year)
    {
        var selection = GetSelection(householdHead);
        var town = string.IsNullOrWhiteSpace(selection.PurchaseTownId)
            ? null
            : _towns.FindTown(selection.PurchaseTownId);

        selection.PurchaseTownId = null;
        householdHead.Components.Set(selection);

        if (town is null)
        {
            return new GameActionResult(
                false,
                "No town was selected for the purchase.");
        }

        var finance = _economy.GetHousehold(householdHead);
        var profile = _townEconomy.GetProfile(town);

        if (finance is null || finance.Wealth < profile.HousePrice)
        {
            _events.Publish(
                new GameEvent
                {
                    Type = "household.house_purchase_failed",
                    Year = year,
                    SubjectId = householdHead.Id,
                    Data = new Dictionary<string, string>
                    {
                        ["town"] = town.Town,
                        ["text"] =
                            $"{_family.GetDisplayName(householdHead)} could no longer afford the planned house in {town.Town}."
                    }
                });

            return new GameActionResult(true);
        }

        _economy.ChangeWealth(householdHead, -profile.HousePrice);
        _economy.AddHouse(householdHead, town);

        _events.Publish(
            new GameEvent
            {
                Type = "household.house_bought",
                Year = year,
                SubjectId = householdHead.Id,
                Data = new Dictionary<string, string>
                {
                    ["amount"] = profile.HousePrice.ToString(),
                    ["town"] = town.Town,
                    ["text"] =
                        $"{_family.GetDisplayName(householdHead)} bought a house in {town.Town} for {profile.HousePrice:N0} zł."
                }
            });

        return new GameActionResult(true);
    }

    internal GameActionResult ExecutePreparedSale(
        IPerson householdHead,
        int year)
    {
        var selection = GetSelection(householdHead);
        var propertyId = selection.SalePropertyId;
        selection.SalePropertyId = null;
        householdHead.Components.Set(selection);

        if (propertyId is not Guid id)
        {
            return new GameActionResult(
                false,
                "No property was selected for sale.");
        }

        var option = GetSaleOptions(householdHead)
            .FirstOrDefault(candidate => candidate.PropertyId == id);

        if (option is null)
        {
            return new GameActionResult(
                true,
                "The selected property is no longer owned.");
        }

        var sold = _propertyEconomy.TakeHouse(householdHead, id);
        if (sold is null)
            return new GameActionResult(true);

        _economy.ChangeWealth(householdHead, option.SaleValue);

        _events.Publish(
            new GameEvent
            {
                Type = "household.house_sold",
                Year = year,
                SubjectId = householdHead.Id,
                Data = new Dictionary<string, string>
                {
                    ["amount"] = option.SaleValue.ToString(),
                    ["town"] = option.Town.Town,
                    ["text"] =
                        $"{_family.GetDisplayName(householdHead)} sold a house in {option.Town.Town} for {option.SaleValue:N0} zł."
                }
            });

        return new GameActionResult(true);
    }

    public bool CanMoveTo(
        IPerson householdHead,
        string townId)
    {
        var residence = _propertyEconomy.GetResidenceTown(householdHead);

        return residence is not null
            && !residence.Id.Equals(townId, StringComparison.OrdinalIgnoreCase)
            && _propertyEconomy.HasHouseInTown(householdHead, townId);
    }

    public GameActionResult MoveHousehold(
        IPerson householdHead,
        string townId,
        int year)
    {
        var destination = _towns.FindTown(townId);
        var origin = _propertyEconomy.GetResidenceTown(householdHead);

        if (destination is null
            || origin is null
            || !CanMoveTo(householdHead, townId))
        {
            return new GameActionResult(false);
        }

        _propertyEconomy.SetResidenceTown(householdHead, destination);

        foreach (var memberId in _economy.GetHouseholdMemberIds(householdHead))
        {
            var member = _gameState.People.FirstOrDefault(person => person.Id == memberId);
            if (member is null
                || !member.Tags.Has("state.alive")
                || member.Age < 18
                || member.Tags.Has("role.nanny"))
            {
                continue;
            }

            var before = _career.GetCareer(member);
            if (before.IsRetired || before.JobLevel <= 0)
                continue;

            var result = _careerMobility.ReestablishCareerAfterMove(member);

            _events.Publish(
                new GameEvent
                {
                    Type = "career.relocated",
                    Year = year,
                    SubjectId = member.Id,
                    Data = new Dictionary<string, string>
                    {
                        ["suppressChronicle"] = "true",
                        ["text"] = result.NewLevel <= 0
                            ? $"After moving to {destination.Town}, {_family.GetDisplayName(member)} was unable to re-establish a career."
                            : $"After moving to {destination.Town}, {_family.GetDisplayName(member)} began working in {result.NewCareerName} at job level {result.NewLevel}."
                    }
                });
        }

        _events.Publish(
            new GameEvent
            {
                Type = "household.moved",
                Year = year,
                SubjectId = householdHead.Id,
                Data = new Dictionary<string, string>
                {
                    ["fromTown"] = origin.Town,
                    ["toTown"] = destination.Town,
                    ["text"] =
                        $"The {householdHead.Surname} household moved from {origin.Town} to {destination.Town}."
                }
            });

        return new GameActionResult(true);
    }

    public IReadOnlyList<HousePropertyInfo> GetTransferableProperties(
        IPerson householdRepresentative)
    {
        var houses =
            _economy.GetHouses(
                householdRepresentative);

        if (houses.Count <= 1)
            return Array.Empty<HousePropertyInfo>();

        return houses
            .Where(house => house.IsRented)
            .ToList();
    }

    public GameActionResult TransferPropertyFromParent(
        IPerson parentRepresentative,
        IPerson receivingHouseholdHead,
        HousePropertyInfo property,
        int year)
    {
        var taken = _propertyEconomy.TakeHouse(parentRepresentative, property.Id);
        if (taken is null)
            return new GameActionResult(false);

        _economy.AddExistingHouse(receivingHouseholdHead, taken);

        var residence = _propertyEconomy.GetResidenceTown(receivingHouseholdHead);
        if (residence is null)
            return new GameActionResult(false);

        if (!residence.Id.Equals(taken.Town.Id, StringComparison.OrdinalIgnoreCase))
        {
            return MoveHousehold(
                receivingHouseholdHead,
                taken.Town.Id,
                year);
        }

        return new GameActionResult(true);
    }

    private static decimal RoundMoney(decimal value) =>
        Math.Round(value, 0, MidpointRounding.AwayFromZero);

    private static PropertyActionSelectionComponent GetSelection(
        IPerson householdHead)
    {
        return householdHead.Components.Get<PropertyActionSelectionComponent>()
            ?? new PropertyActionSelectionComponent();
    }
}
