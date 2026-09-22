using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Dynastia.Contracts;

namespace Dynastia.Mechanics.Farming;

internal sealed class StandardFarmingService :
    IFarmingService,
    IHouseholdIncomeProvider
{
    private const string AgricultureCareerId =
        "agriculture_and_farm_estates";

    private readonly IGameState _gameState;
    private readonly IEconomyService _economy;
    private readonly ICareerService _career;
    private readonly ITownProsperityService _prosperity;
    private readonly ILocalEconomicStrengthService _economicStrength;
    private readonly IWorkCapacityService _workCapacity;
    private readonly IGameRandom _random;
    private readonly IGameEventBus _events;
    private readonly FarmingEraSchedule _eraSchedule;
    private readonly FarmingFlavorCatalog _flavors;
    private readonly FarmingWorkerContributionCatalog _workerContribution;
    private readonly Func<ICommunityPolicyService?> _communityResolver;

    public StandardFarmingService(
        IGameState gameState,
        IEconomyService economy,
        ICareerService career,
        ITownProsperityService prosperity,
        ILocalEconomicStrengthService economicStrength,
        IWorkCapacityService workCapacity,
        IGameRandom random,
        IGameEventBus events,
        FarmingEraSchedule eraSchedule,
        FarmingFlavorCatalog flavors,
        FarmingWorkerContributionCatalog workerContribution,
        Func<ICommunityPolicyService?>? communityResolver = null)
    {
        _gameState = gameState;
        _economy = economy;
        _career = career;
        _prosperity = prosperity;
        _economicStrength = economicStrength;
        _workCapacity = workCapacity;
        _random = random;
        _events = events;
        _eraSchedule = eraSchedule;
        _flavors = flavors;
        _workerContribution = workerContribution;
        _communityResolver = communityResolver ?? (() => null);
    }

    public string Id =>
        "farming";

    public string Label =>
        "Farming";

    public decimal PurchasePrice =>
        FarmingRules.PurchasePrice;

    public decimal SalePrice =>
        FarmingRules.SalePrice;

    public decimal LivestockPurchasePrice =>
        FarmingRules.LivestockPurchasePrice;

    public decimal LivestockSalePrice =>
        FarmingRules.LivestockSalePrice;

    public FarmingHouseholdSnapshot GetSnapshot(
        IPerson householdRepresentative)
    {
        var farmland =
            GetResolvedFarmland(
                householdRepresentative);

        var residence =
            _economy.GetResidenceTown(
                householdRepresentative);

        var localCount =
            farmland.Count(asset =>
                asset.Town.Id.Equals(
                    residence.Id,
                    StringComparison.OrdinalIgnoreCase));

        var workers =
            GetAvailableWorkers(
                householdRepresentative)
            .Count;

        var finance =
            _economy.GetHousehold(
                householdRepresentative);

        var lastIncome =
            finance?.LastIncomeBreakdown
                .Where(line =>
                    line.Label.Equals(
                        Label,
                        StringComparison.OrdinalIgnoreCase))
                .Sum(line => line.Amount)
            ?? 0m;

        return new FarmingHouseholdSnapshot(
            farmland,
            residence.Id,
            localCount,
            workers,
            localCount * 2,
            lastIncome,
            GetExpectedAnnualIncome(
                householdRepresentative));
    }

    public bool IsAvailableFarmWorker(
        IPerson person,
        IPerson householdRepresentative)
    {
        if (!person.Tags.Has("state.alive")
            || _workerContribution.GetAgeContribution(person.Age) <= 0m
            || person.Tags.Has("state.imprisoned")
            || person.Tags.Has("occupation.criminal")
            || person.Tags.Has("vocation.religious.active")
            || !_workCapacity.GetWorkCapacity(person).CanWork
            || person.Tags.Has("role.nanny")
            || person.Tags.Has("role.family_nanny"))
        {
            return false;
        }

        var householdId =
            _economy.GetHouseholdId(
                householdRepresentative);

        if (householdId is null
            || _economy.GetHouseholdId(person) != householdId)
        {
            return false;
        }

        return !_career.IsEmployed(person);
    }

    public bool IsWorkingFarmWorker(
        IPerson person,
        IPerson householdRepresentative)
    {
        if (!IsAvailableFarmWorker(
                person,
                householdRepresentative))
        {
            return false;
        }

        var localParcelCount =
            GetLocalParcelCount(
                householdRepresentative);

        if (localParcelCount <= 0)
            return false;

        return GetWorkingFarmWorkers(
                householdRepresentative)
            .Any(worker => worker.Id == person.Id);
    }

    public decimal GetExpectedAnnualIncome(
        IPerson householdRepresentative)
    {
        var (localParcels, localLivestock) =
            GetLocalFarmlandCounts(
                householdRepresentative);

        return GetExpectedAnnualIncomeForWorkers(
            householdRepresentative,
            GetWorkingFarmWorkers(householdRepresentative),
            localParcels,
            localLivestock);
    }

    public decimal GetExpectedAnnualIncomeAfterAddingLocalParcel(
        IPerson householdRepresentative)
    {
        var (localParcels, localLivestock) =
            GetLocalFarmlandCounts(
                householdRepresentative);

        var workers =
            GetRankedAvailableWorkers(
                householdRepresentative);

        var activeWorkers = FarmingRules.GetActiveWorkerCount(
            localParcels + 1,
            workers.Count);

        return GetExpectedAnnualIncomeForWorkers(
            householdRepresentative,
            workers.Take(activeWorkers),
            localParcels + 1,
            localLivestock);
    }

    public IReadOnlyList<FarmingFlavorInfo> GetAvailableLivestockOptions(
        TownInfo town,
        int year)
    {
        ArgumentNullException.ThrowIfNull(town);
        return _flavors.GetAvailableLivestock(town.RegionId, year);
    }

    public decimal GetFarmlandSaleValue(
        FarmlandAssetInfo farmland)
    {
        ArgumentNullException.ThrowIfNull(farmland);
        return SalePrice
            + (string.IsNullOrWhiteSpace(farmland.LivestockTypeId)
                ? 0m
                : LivestockSalePrice);
    }

    public FarmlandAssetInfo? AssignNewFarmlandType(
        IPerson householdRepresentative,
        Guid farmlandId)
    {
        var parcel = _economy.GetFarmland(householdRepresentative)
            .FirstOrDefault(asset => asset.Id == farmlandId);
        if (parcel is null)
            return null;

        if (!string.IsNullOrWhiteSpace(parcel.FarmTypeId)
            && _flavors.FindFarmType(parcel.FarmTypeId) is not null)
        {
            return ResolveDisplay(parcel);
        }

        var type = _flavors.SelectFarmType(
            parcel.Town.RegionId,
            parcel.AcquiredYear,
            _random.NextDouble());

        if (!_economy.SetFarmlandFlavor(
                householdRepresentative,
                parcel.Id,
                type.Id,
                parcel.LivestockTypeId))
        {
            return null;
        }

        return ResolveDisplay(parcel with { FarmTypeId = type.Id });
    }

    public FarmlandAssetInfo? EnsureFarmlandFlavor(
        IPerson householdRepresentative,
        Guid farmlandId)
    {
        var parcel = _economy.GetFarmland(householdRepresentative)
            .FirstOrDefault(asset => asset.Id == farmlandId);
        if (parcel is null)
            return null;

        var farmType = _flavors.FindFarmType(parcel.FarmTypeId);
        var livestock = _flavors.FindLivestock(parcel.LivestockTypeId);
        var changed = false;

        if (farmType is null)
        {
            farmType = _flavors.SelectFarmType(
                parcel.Town.RegionId,
                parcel.AcquiredYear,
                DeterministicRoll(
                    parcel.Id,
                    parcel.Town.RegionId,
                    parcel.AcquiredYear,
                    "farm-type"));
            changed = true;
        }

        if (!string.IsNullOrWhiteSpace(parcel.LivestockTypeId)
            && livestock is null)
        {
            changed = true;
        }

        var livestockId = livestock?.Id;
        if (changed
            && !_economy.SetFarmlandFlavor(
                householdRepresentative,
                parcel.Id,
                farmType.Id,
                livestockId))
        {
            return null;
        }

        return ResolveDisplay(parcel with
        {
            FarmTypeId = farmType.Id,
            LivestockTypeId = livestockId
        });
    }

    public FarmlandAssetInfo? AddLivestock(
        IPerson householdRepresentative,
        Guid farmlandId,
        int year)
    {
        var parcel = EnsureFarmlandFlavor(
            householdRepresentative,
            farmlandId);
        if (parcel is null
            || !string.IsNullOrWhiteSpace(parcel.LivestockTypeId))
        {
            return null;
        }

        var livestock = _flavors.SelectLivestock(
            parcel.Town.RegionId,
            year,
            _random.NextDouble());

        if (!_economy.SetFarmlandFlavor(
                householdRepresentative,
                parcel.Id,
                parcel.FarmTypeId,
                livestock.Id))
        {
            return null;
        }

        return ResolveDisplay(parcel with
        {
            LivestockTypeId = livestock.Id
        });
    }

    public FarmlandRelocationSaleResult SellOriginFarmlandForVoluntaryRelocation(
        IPerson householdRepresentative,
        TownInfo origin,
        TownInfo destination)
    {
        ArgumentNullException.ThrowIfNull(origin);
        ArgumentNullException.ThrowIfNull(destination);

        var originParcels = GetResolvedFarmland(householdRepresentative)
            .Where(asset => asset.Town.Id.Equals(
                origin.Id,
                StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (originParcels.Count == 0)
            return FarmlandRelocationSaleResult.None;

        var livestockCount = originParcels.Count(asset =>
            !string.IsNullOrWhiteSpace(asset.LivestockTypeId));
        var proceeds = originParcels.Sum(GetFarmlandSaleValue);

        foreach (var parcel in originParcels)
            _economy.TakeFarmland(householdRepresentative, parcel.Id);

        _economy.ChangeWealth(householdRepresentative, proceeds);

        var farmLabel = originParcels.Count == 1 ? "farm" : "farms";
        var livestockText = livestockCount == 0
            ? string.Empty
            : $" and {livestockCount} livestock {(livestockCount == 1 ? "holding" : "holdings")}";

        _events.Publish(new GameEvent
        {
            Type = "farming.relocation_sale",
            Year = _gameState.Year,
            SubjectId = householdRepresentative.Id,
            Data = new Dictionary<string, string>
            {
                ["fromTown"] = origin.Town,
                ["toTown"] = destination.Town,
                ["parcelCount"] = originParcels.Count.ToString(),
                ["livestockCount"] = livestockCount.ToString(),
                ["amount"] = proceeds.ToString(),
                ["text"] =
                    $"Before moving to {destination.Town}, the household sold {originParcels.Count} {farmLabel}{livestockText} near {origin.Town} for {proceeds:N0} zł."
            }
        });

        return new FarmlandRelocationSaleResult(
            originParcels.Count,
            livestockCount,
            proceeds);
    }

    private decimal GetExpectedAnnualIncomeForWorkers(
        IPerson householdRepresentative,
        IEnumerable<IPerson> workers,
        int localParcelCount,
        int localLivestockCount)
    {
        var workerBaseIncome = GetWorkerBaseIncome();
        decimal baseIncome = 0m;

        foreach (var worker in workers)
        {
            var productiveEffort = AnnualProductiveEffortRules.Get(
                worker,
                _workCapacity);

            var ageContribution = _workerContribution.GetAgeContribution(worker.Age);
            baseIncome += productiveEffort.Apply(
                workerBaseIncome * ageContribution);
        }

        var coverage = FarmingRules.GetLivestockCoverage(
            localParcelCount,
            localLivestockCount);
        baseIncome *= FarmingRules.GetLivestockIncomeMultiplier(coverage);

        return ApplyTownIncomeMultiplier(
            householdRepresentative,
            baseIncome);
    }

    decimal IHouseholdIncomeProvider.GetAnnualIncome(
        IPerson householdRepresentative)
    {
        var workers =
            GetWorkingFarmWorkers(
                householdRepresentative);

        if (workers.Count <= 0)
            return 0m;

        var workerBaseIncome =
            GetWorkerBaseIncome();
        var (localParcels, localLivestock) =
            GetLocalFarmlandCounts(householdRepresentative);
        var coverage = FarmingRules.GetLivestockCoverage(
            localParcels,
            localLivestock);

        decimal total = 0m;

        foreach (var worker in workers)
        {
            var ageContribution = _workerContribution.GetAgeContribution(worker.Age);
            var output = workerBaseIncome * ageContribution;
            var productiveEffort = AnnualProductiveEffortRules.Get(
                worker,
                _workCapacity);

            total += productiveEffort.Apply(output);
        }

        var weather = ResolveWeatherState();
        var adjustedWeatherMultiplier =
            FarmingRules.AdjustVolatilityMultiplier(
                weather.RawYieldMultiplier,
                coverage);
        total *= adjustedWeatherMultiplier;
        total *= FarmingRules.GetLivestockIncomeMultiplier(coverage);
        total = ApplyTownIncomeMultiplier(
            householdRepresentative,
            total);

        var rounded =
            Math.Round(
                total,
                0,
                MidpointRounding.AwayFromZero);

        var expected =
            GetExpectedAnnualIncome(
                householdRepresentative);

        var performance =
            expected <= 0m
                ? "none"
                : rounded >= expected * 1.45m
                    ? "strong"
                    : rounded <= expected * 0.55m
                        ? "poor"
                        : "ordinary";

        PublishWeatherNewsOnce(
            weather,
            householdRepresentative);

        _events.Publish(
            new GameEvent
            {
                Type = "farming.income",
                Year = _gameState.Year,
                SubjectId = householdRepresentative.Id,
                RelatedPersonIds = workers.Select(worker => worker.Id).ToList(),
                Data = new Dictionary<string, string>
                {
                    ["amount"] = rounded.ToString(),
                    ["expected"] = expected.ToString(),
                    ["performance"] = performance,
                    ["suppressChronicle"] = "true"
                }
            });

        return rounded;
    }

    decimal IHouseholdIncomeProvider.GetExpectedAnnualIncome(
        IPerson householdRepresentative) =>
        GetExpectedAnnualIncome(
            householdRepresentative);

    private FarmingWeatherStateComponent ResolveWeatherState()
    {
        var anchor = _gameState.People.FirstOrDefault()
            ?? throw new InvalidOperationException(
                "Farming weather requires at least one game person.");

        var state = anchor.Components.Get<FarmingWeatherStateComponent>();
        if (state is null)
        {
            state = new FarmingWeatherStateComponent();
            anchor.Components.Set(state);
        }

        if (state.ResolvedGameYear == _gameState.Year)
            return state;

        state.ResolvedGameYear = _gameState.Year;
        state.WeatherYear = _gameState.Year - 1;
        state.WinterTemperature = RollNormalizedSeason();
        state.SpringTemperature = RollNormalizedSeason();
        state.SummerPrecipitation = RollNormalizedSeason();
        state.AutumnPrecipitation = RollNormalizedSeason();
        state.RawYieldMultiplier = FarmingRules.GetRawWeatherYieldMultiplier(
            state.WinterTemperature,
            state.SpringTemperature,
            state.SummerPrecipitation,
            state.AutumnPrecipitation);
        state.NewsPublished = false;
        return state;
    }

    private double RollNormalizedSeason() =>
        _random.NextDouble() * 2.0 - 1.0;

    private void PublishWeatherNewsOnce(
        FarmingWeatherStateComponent weather,
        IPerson householdRepresentative)
    {
        if (weather.NewsPublished)
            return;

        weather.NewsPublished = true;
        var harvestBand = GetHarvestBand(weather.RawYieldMultiplier);
        var text =
            $"Last year's weather brought {DescribeWinter(weather.WinterTemperature)}, " +
            $"{DescribeSpring(weather.SpringTemperature)}, " +
            $"{DescribeSummer(weather.SummerPrecipitation)} and " +
            $"{DescribeAutumn(weather.AutumnPrecipitation)}. " +
            $"Harvests were {harvestBand} across farming households.";

        _events.Publish(new GameEvent
        {
            Type = "farming.weather",
            Year = _gameState.Year,
            SubjectId = householdRepresentative.Id,
            Data = new Dictionary<string, string>
            {
                ["weatherYear"] = weather.WeatherYear.ToString(CultureInfo.InvariantCulture),
                ["winterTemperature"] = weather.WinterTemperature.ToString("0.0000", CultureInfo.InvariantCulture),
                ["springTemperature"] = weather.SpringTemperature.ToString("0.0000", CultureInfo.InvariantCulture),
                ["summerPrecipitation"] = weather.SummerPrecipitation.ToString("0.0000", CultureInfo.InvariantCulture),
                ["autumnPrecipitation"] = weather.AutumnPrecipitation.ToString("0.0000", CultureInfo.InvariantCulture),
                ["rawYieldMultiplier"] = weather.RawYieldMultiplier.ToString("0.0000", CultureInfo.InvariantCulture),
                ["harvestBand"] = harvestBand,
                ["globalNews"] = "true",
                ["suppressChronicle"] = "false",
                ["text"] = text
            }
        });
    }

    private static string DescribeWinter(double value) => value switch
    {
        <= -0.65 => "a harsh winter that kept pests down",
        <= -0.20 => "a cold winter with fewer pests",
        < 0.20 => "an average winter",
        < 0.65 => "a mild winter that favored pests",
        _ => "a very mild winter in which pests flourished"
    };

    private static string DescribeSpring(double value) => value switch
    {
        <= -0.65 => "severe spring frosts",
        <= -0.20 => "a cold spring with some frost damage",
        < 0.20 => "an average spring",
        < 0.65 => "a warm spring",
        _ => "a very warm spring"
    };

    private static string DescribeSummer(double value) => value switch
    {
        <= -0.65 => "a severe summer drought",
        <= -0.20 => "a dry summer",
        < 0.20 => "average summer rainfall",
        < 0.65 => "a well-watered summer",
        _ => "a very wet summer"
    };

    private static string DescribeAutumn(double value) => value switch
    {
        <= -0.65 => "a very dry autumn with an easy harvest",
        <= -0.20 => "a dry autumn",
        < 0.20 => "average autumn rainfall",
        < 0.65 => "a rainy autumn that complicated harvest",
        _ => "a very rainy autumn that seriously complicated harvest"
    };

    private static string GetHarvestBand(decimal multiplier) => multiplier switch
    {
        < 0.40m => "disastrous",
        < 0.75m => "poor",
        < 1.25m => "broadly average",
        < 1.60m => "good",
        _ => "exceptional"
    };

    private decimal ApplyTownIncomeMultiplier(
        IPerson householdRepresentative,
        decimal income)
    {
        if (income <= 0m)
            return 0m;

        var town = _economy.GetResidenceTown(householdRepresentative);
        var strength = _economicStrength.ResolveFarming(town);
        var multiplier = _prosperity.GetIncomeMultiplier(town, strength);
        multiplier *= _communityResolver()?.GetModifiers(town, _gameState.Year).FarmingIncomeMultiplier ?? 1m;
        return Math.Round(
            income * multiplier,
            0,
            MidpointRounding.AwayFromZero);
    }

    private IReadOnlyList<IPerson> GetAvailableWorkers(
        IPerson householdRepresentative)
    {
        var memberIds =
            _economy.GetHouseholdMemberIds(
                householdRepresentative);

        var peopleById =
            _gameState.People.ToDictionary(person => person.Id);

        return memberIds
            .Distinct()
            .Select(id => peopleById.GetValueOrDefault(id))
            .Where(person => person is not null)
            .Cast<IPerson>()
            .Where(person =>
                IsAvailableFarmWorker(
                    person,
                    householdRepresentative))
            .ToList();
    }

    private IReadOnlyList<IPerson> GetWorkingFarmWorkers(
        IPerson householdRepresentative)
    {
        var localParcels =
            GetLocalParcelCount(
                householdRepresentative);

        if (localParcels <= 0)
            return [];

        return GetRankedAvailableWorkers(
                householdRepresentative)
            .Take(localParcels * 2)
            .ToList();
    }

    private IReadOnlyList<IPerson> GetRankedAvailableWorkers(
        IPerson householdRepresentative) =>
        GetAvailableWorkers(householdRepresentative)
            .OrderByDescending(GetExpectedProductiveContribution)
            .ThenByDescending(worker => worker.Age)
            .ThenBy(worker => worker.Id)
            .ToList();

    private decimal GetExpectedProductiveContribution(IPerson worker)
    {
        var ageContribution = _workerContribution.GetAgeContribution(worker.Age);
        var productiveEffort = AnnualProductiveEffortRules.Get(worker, _workCapacity);
        return ageContribution * (decimal)productiveEffort.OutputMultiplier;
    }

    private decimal GetWorkerBaseIncome() =>
        _career.GetLevelOneSalary(
            AgricultureCareerId)
        * _eraSchedule.GetMultiplier(
            _gameState.Year)
        * FarmingRules.WorkerBaseIncomeScale;

    private int GetLocalParcelCount(
        IPerson householdRepresentative) =>
        GetLocalFarmlandCounts(householdRepresentative).LocalParcels;

    private (int LocalParcels, int LocalLivestock) GetLocalFarmlandCounts(
        IPerson householdRepresentative)
    {
        var residence =
            _economy.GetResidenceTown(
                householdRepresentative);
        var local = GetResolvedFarmland(householdRepresentative)
            .Where(asset => asset.Town.Id.Equals(
                residence.Id,
                StringComparison.OrdinalIgnoreCase))
            .ToList();

        return (
            local.Count,
            local.Count(asset => !string.IsNullOrWhiteSpace(asset.LivestockTypeId)));
    }

    private IReadOnlyList<FarmlandAssetInfo> GetResolvedFarmland(
        IPerson householdRepresentative)
    {
        var raw = _economy.GetFarmland(householdRepresentative);
        var result = new List<FarmlandAssetInfo>(raw.Count);
        foreach (var parcel in raw)
        {
            var resolved = EnsureFarmlandFlavor(
                householdRepresentative,
                parcel.Id);
            if (resolved is not null)
                result.Add(resolved);
        }

        return result;
    }

    private FarmlandAssetInfo ResolveDisplay(FarmlandAssetInfo parcel)
    {
        var farm = _flavors.FindFarmType(parcel.FarmTypeId);
        var livestock = _flavors.FindLivestock(parcel.LivestockTypeId);

        return parcel with
        {
            FarmTypeDisplayName = farm?.DisplayName ?? "Farm",
            FarmTypeEmoji = farm?.Emoji ?? "🌾",
            LivestockDisplayName = livestock?.DisplayName,
            LivestockEmoji = livestock?.Emoji
        };
    }

    private static double DeterministicRoll(
        Guid farmlandId,
        string regionId,
        int acquiredYear,
        string purpose)
    {
        var key = $"{farmlandId:N}|{regionId}|{acquiredYear}|{purpose}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(key));
        var value = BinaryPrimitives.ReadUInt64LittleEndian(hash.AsSpan(0, 8));
        return value / ((double)ulong.MaxValue + 1d);
    }
}
