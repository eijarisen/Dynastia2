using System.Globalization;
using Dynastia.Contracts;

namespace Dynastia.App.ViewModels.Actions;

/// <summary>Builds UI choices and submits their existing mechanics parameters; it never refreshes views.</summary>
internal sealed class ActionSelectionOptionService
{
    private readonly ISuccessionService _succession;
    private readonly IActionRegistry _actionRegistry;
    private readonly IEconomyService? _economyService;
    private readonly IFarmingService? _farmingService;
    private readonly IHeirloomService? _heirloomService;
    private readonly ILoanService? _loanService;
    private readonly ILocationService? _locationService;
    private readonly ITownProsperityService? _townProsperityService;
    private readonly ILocalCareerOpportunityService? _localCareerOpportunityService;

    internal ActionSelectionOptionService(
        ISuccessionService succession,
        IActionRegistry actionRegistry,
        IEconomyService? economyService,
        IFarmingService? farmingService,
        IHeirloomService? heirloomService,
        ILoanService? loanService,
        ILocationService? locationService,
        ITownProsperityService? townProsperityService,
        ILocalCareerOpportunityService? localCareerOpportunityService)
    {
        _succession = succession;
        _actionRegistry = actionRegistry;
        _economyService = economyService;
        _farmingService = farmingService;
        _heirloomService = heirloomService;
        _loanService = loanService;
        _locationService = locationService;
        _townProsperityService = townProsperityService;
        _localCareerOpportunityService = localCareerOpportunityService;
    }

    public IReadOnlyList<PropertySelectionOption> GetPropertySelectionOptions(
        string actionId)
    {
        var actor = _succession.ActiveController;
        if (actor is null || _economyService is null || _locationService is null)
            return Array.Empty<PropertySelectionOption>();

        if (actionId.Equals("household.buy_house", StringComparison.OrdinalIgnoreCase))
        {
            var currentTown = _economyService.GetResidenceTown(actor);
            var ownedTownIds =
                _economyService.GetHouses(actor)
                    .Select(house => house.Town.Id)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

            return _locationService.GetTowns()
                .Select(town =>
                {
                    var opportunities = _localCareerOpportunityService?
                        .GetOpportunitySnapshot(town);
                    var prosperity = _townProsperityService?.Get(town);
                    var region = opportunities?.RegionName ?? town.RegionId;
                    var category = town.Id.Equals(
                            currentTown.Id,
                            StringComparison.OrdinalIgnoreCase)
                        ? 0
                        : ownedTownIds.Contains(town.Id)
                            ? 1
                            : 2;
                    var opportunityText = opportunities is null
                        ? "—"
                        : string.Join(
                            ", ",
                            opportunities.TownOpportunityTags
                                .Concat(opportunities.RegionOpportunityTags)
                                .Distinct(StringComparer.OrdinalIgnoreCase)
                                .Select(FormatOpportunityTag));
                    if (string.IsNullOrWhiteSpace(opportunityText))
                        opportunityText = "—";

                    var details =
                        $"Population: {town.Population:N0} • "
                        + $"Prosperity: {prosperity?.Index ?? 100} ({prosperity?.Label ?? "Stable"}) • "
                        + $"Opportunities: {opportunityText}";

                    var regionalText = opportunities is { RegionOpportunityTags.Count: > 0 }
                        ? string.Join(", ", opportunities.RegionOpportunityTags
                            .Select(FormatOpportunityTag))
                        : string.Empty;

                    var search =
                        $"{town.Town} {town.County} {region} {town.PolityName} "
                        + $"{town.SettlementClassDisplayName} {opportunityText} "
                        + $"{regionalText}";

                    return new
                    {
                        Category = category,
                        Option = new PropertySelectionOption(
                            town.Id,
                            town.Town,
                            $"{town.PolityName} • {region} • {town.County}",
                            details,
                            string.Empty,
                            search,
                            LeadingEmoji: "🏠",
                            LeadingEmojiFontSize: 32)
                    };
                })
                .OrderBy(item => item.Category)
                .ThenBy(item => item.Option.PrimaryText, StringComparer.CurrentCultureIgnoreCase)
                .Select(item => item.Option)
                .ToList();
        }

        if (actionId.Equals("household.sell_house", StringComparison.OrdinalIgnoreCase))
        {
            return _economyService.GetHouses(actor)
                .Select(house =>
                {
                    var opportunities = _localCareerOpportunityService?.GetOpportunitySnapshot(house.Town);
                    var localPrice = _economyService.GetHousePrice(house.Town);
                    var propertyValue = _economyService.GetHouseValue(house);
                    var sale = _economyService.GetHouseSaleValue(house);
                    var rentalIncome = _economyService.GetRentalIncome(house);
                    var status = house.IsResidence ? "Residence" : "Rented property";
                    var region = opportunities?.RegionName ?? house.Town.RegionId;
                    return new PropertySelectionOption(
                        house.Id.ToString(),
                        house.Town.Town,
                        $"{house.Town.County} • {region}",
                        $"{status} • {house.Town.SettlementClassDisplayName}\nProperty value: {propertyValue:N0} zł • Local base price: {localPrice:N0} zł\nRental income: {rentalIncome:N0} zł/year",
                        $"Sale: {sale:N0} zł",
                        $"{house.Town.Town} {house.Town.County} {region} {status}",
                        LeadingEmoji: "🏠",
                        LeadingEmojiFontSize: 32);
                })
                .OrderByDescending(option => option.DetailsText.StartsWith("Residence", StringComparison.OrdinalIgnoreCase))
                .ThenBy(option => option.PrimaryText, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }

        if (actionId.Equals(
                "farming.sell_farmland",
                StringComparison.OrdinalIgnoreCase)
            && _farmingService is not null)
        {
            return _farmingService.GetSnapshot(actor).Farmland
                .Select(farmland =>
                {
                    var sale = _farmingService.GetFarmlandSaleValue(farmland);
                    var livestock = string.IsNullOrWhiteSpace(farmland.LivestockDisplayName)
                        ? "No livestock"
                        : $"Livestock: {farmland.LivestockEmoji} {farmland.LivestockDisplayName}";
                    var title =
                        $"{farmland.FarmTypeEmoji} {farmland.FarmTypeDisplayName}";

                    return new PropertySelectionOption(
                        farmland.Id.ToString(),
                        title,
                        $"{farmland.Town.Town} • {farmland.Town.County}",
                        $"{livestock}\nAcquired: {farmland.AcquiredYear}",
                        $"Sale: {sale:N0} zł",
                        $"{title} {farmland.Town.Town} {farmland.Town.County} {livestock}");
                })
                .OrderBy(option => option.PrimaryText, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(option => option.SecondaryText, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }

        if (actionId.Equals(
                "household.ask_move_out",
                StringComparison.OrdinalIgnoreCase))
        {
            return _economyService.GetHouses(actor)
                .Where(house => !house.IsResidence)
                .Select(house =>
                {
                    var opportunities =
                        _localCareerOpportunityService?.GetOpportunitySnapshot(house.Town);
                    var rentalIncome = _economyService.GetRentalIncome(house);
                    var region = opportunities?.RegionName ?? house.Town.RegionId;
                    return new PropertySelectionOption(
                        house.Id.ToString(),
                        house.Town.Town,
                        $"{house.Town.County} • {region}",
                        $"Spare property • {house.Town.SettlementClassDisplayName}\nCurrently yields {rentalIncome:N0} zł/year as rental income. It will become the selected resident's new home.",
                        "Provide house",
                        $"{house.Town.Town} {house.Town.County} {region} move out resident",
                        LeadingEmoji: "🏠",
                        LeadingEmojiFontSize: 32);
                })
                .OrderBy(option => option.PrimaryText, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }

        return Array.Empty<PropertySelectionOption>();
    }

    public ActionUiExecutionResult QueueActionWithSelection(
        string actionId,
        string selectedId,
        IPerson? selectedPerson)
    {
        var actor = _succession.ActiveController;
        if (actor is null || _succession.IsGameOver)
            return ActionUiExecutionResult.NotAttempted;

        var moveOut = actionId.Equals(
            "household.ask_move_out",
            StringComparison.OrdinalIgnoreCase);

        // Inventory actions belong to the active household. Move Out is the
        // exception: the selected resident remains the queued target.
        var target = moveOut
            ? selectedPerson
            : actor;

        if (target is null)
            return ActionUiExecutionResult.NotAttempted;

        var key = actionId.Equals("household.buy_house", StringComparison.OrdinalIgnoreCase)
            ? "townId"
            : actionId.Equals("heirloom.sell", StringComparison.OrdinalIgnoreCase)
                ? "heirloomId"
                : (actionId.Equals("farming.sell_farmland", StringComparison.OrdinalIgnoreCase)
                   || actionId.Equals("farming.add_livestock", StringComparison.OrdinalIgnoreCase))
                    ? "farmlandId"
                    : "propertyId";

        var parameters =
            new Dictionary<string, string>(
                StringComparer.OrdinalIgnoreCase)
            {
                [key] = selectedId
            };

        if (actionId.Equals(
                "household.buy_house",
                StringComparison.OrdinalIgnoreCase)
            && _locationService?.FindTown(selectedId) is { } buyTown
            && _economyService is not null)
        {
            parameters["summaryTown"] = buyTown.Town;
            parameters["summaryPrice"] =
                _economyService.GetHousePrice(buyTown)
                    .ToString(CultureInfo.InvariantCulture);
        }
        else if ((actionId.Equals(
                      "household.sell_house",
                      StringComparison.OrdinalIgnoreCase)
                  || actionId.Equals(
                      "household.extend_house",
                      StringComparison.OrdinalIgnoreCase))
                 && _economyService is not null
                 && Guid.TryParse(selectedId, out var propertyId))
        {
            var house =
                _economyService.GetHouses(actor)
                    .FirstOrDefault(candidate => candidate.Id == propertyId);

            if (house is not null)
            {
                parameters["summaryTown"] = house.Town.Town;
                parameters["summaryPrice"] =
                    (actionId.Equals(
                        "household.extend_house",
                        StringComparison.OrdinalIgnoreCase)
                        ? house.ExtensionCost
                        : _economyService.GetHouseSaleValue(house))
                    .ToString(CultureInfo.InvariantCulture);
            }
        }
        else if ((actionId.Equals(
                      "farming.sell_farmland",
                      StringComparison.OrdinalIgnoreCase)
                  || actionId.Equals(
                      "farming.add_livestock",
                      StringComparison.OrdinalIgnoreCase))
                 && _farmingService is not null
                 && Guid.TryParse(selectedId, out var farmlandId)
                 && _succession.ActiveController is { } farmingActor)
        {
            var farmland = _farmingService.GetSnapshot(farmingActor).Farmland
                .FirstOrDefault(item => item.Id == farmlandId);
            if (farmland is not null)
            {
                parameters["summaryFarmland"] =
                    $"{farmland.FarmTypeEmoji} {farmland.FarmTypeDisplayName} — {farmland.Town.Town}";
                parameters["summaryPrice"] =
                    (actionId.Equals(
                        "farming.add_livestock",
                        StringComparison.OrdinalIgnoreCase)
                        ? _farmingService.LivestockPurchasePrice
                        : _farmingService.GetFarmlandSaleValue(farmland))
                    .ToString(CultureInfo.InvariantCulture);
            }
        }
        else if (actionId.Equals(
                     "heirloom.sell",
                     StringComparison.OrdinalIgnoreCase)
                 && _heirloomService is not null
                 && Guid.TryParse(selectedId, out var heirloomId))
        {
            var heirloom = _heirloomService.GetHeirlooms(actor)
                .FirstOrDefault(item => item.Id == heirloomId);
            if (heirloom is not null)
            {
                parameters["summaryHeirloom"] = heirloom.DisplayName;
                parameters["summaryPrice"] = _heirloomService.GetSaleValue(heirloom)
                    .ToString(CultureInfo.InvariantCulture);
            }
        }
        else if (moveOut
                 && _economyService is not null
                 && Guid.TryParse(selectedId, out var movePropertyId))
        {
            var house = _economyService.GetHouses(actor)
                .FirstOrDefault(candidate => candidate.Id == movePropertyId);

            if (house is not null)
                parameters["summaryTown"] = house.Town.Town;
        }

        var result = _actionRegistry.Execute(
            actionId,
            actor,
            target,
            parameters);

        return new ActionUiExecutionResult(
            true, result.Success, result.Message, true, FromSelection: true);
    }

    public decimal GetMaximumLoanPrincipal(
        string actionId)
    {
        if (!actionId.Equals(
                "loan.give",
                StringComparison.OrdinalIgnoreCase))
        {
            return 10000m;
        }

        var actor =
            _succession.ActiveController;

        var wealth =
            actor is null
                ? 0m
                : _economyService?.GetHousehold(actor)?.Wealth
                    ?? 0m;

        var wholeThousands =
            Math.Floor(wealth / 1000m) * 1000m;

        return Math.Clamp(
            wholeThousands,
            0m,
            10000m);
    }

    public IReadOnlyList<LoanOfferInfo> GetLoanOffers(
        bool isGivingLoan,
        decimal maximumPrincipal)
    {
        var actor = _succession.ActiveController;
        if (actor is null
            || _loanService is null
            || maximumPrincipal < 1000m)
        {
            return [];
        }

        return _loanService.GetOffers(
            actor,
            isGivingLoan,
            maximumPrincipal);
    }

    public LoanTermsInfo? GetLoanTerms(
        decimal principal,
        int durationYears)
    {
        if (_loanService is null)
            return null;

        try
        {
            return _loanService.CalculateTerms(
                principal,
                durationYears);
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }

    public ActionUiExecutionResult QueueLoanAction(
        string actionId,
        LoanSelectionResult selection)
    {
        var actor =
            _succession.ActiveController;

        if (actor is null
            || _succession.IsGameOver)
        {
            return ActionUiExecutionResult.NotAttempted;
        }

        // Loan inventory actions always belong to the active household,
        // regardless of which family member is currently selected in the UI.
        var target = actor;

        var parameters =
            new Dictionary<string, string>(
                StringComparer.OrdinalIgnoreCase)
            {
                ["principal"] =
                    selection.Principal.ToString(
                        System.Globalization.CultureInfo.InvariantCulture),

                ["durationYears"] =
                    selection.DurationYears.ToString(
                        System.Globalization.CultureInfo.InvariantCulture),

                ["interestMultiplier"] =
                    selection.InterestMultiplier.ToString(
                        System.Globalization.CultureInfo.InvariantCulture),

                ["counterpartyName"] =
                    selection.CounterpartyName,

                ["counterpartyTownId"] =
                    selection.CounterpartyTownId,

                ["counterpartyNationalityId"] =
                    selection.CounterpartyNationalityId
            };

        var result =
            _actionRegistry.Execute(
                actionId,
                actor,
                target,
                parameters);

        return new ActionUiExecutionResult(
            true, result.Success, result.Message, true, FromSelection: true);
    }

    private static string FormatOpportunityTag(string tag)
    {
        var value = tag;
        var separator = value.LastIndexOf('.');
        if (separator >= 0 && separator + 1 < value.Length)
            value = value[(separator + 1)..];

        value = value.Replace('_', ' ').Replace('-', ' ');
        return System.Globalization.CultureInfo.CurrentCulture.TextInfo
            .ToTitleCase(value);
    }

}
