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

    public StandardFarmingService(
        IGameState gameState,
        IEconomyService economy,
        ICareerService career,
        ITownProsperityService prosperity,
        ILocalEconomicStrengthService economicStrength,
        IWorkCapacityService workCapacity,
        IGameRandom random,
        IGameEventBus events,
        FarmingEraSchedule eraSchedule)
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
    }

    public string Id =>
        "farming";

    public string Label =>
        "Farming";

    public decimal PurchasePrice =>
        FarmingRules.PurchasePrice;

    public decimal SalePrice =>
        FarmingRules.SalePrice;

    public FarmingHouseholdSnapshot GetSnapshot(
        IPerson householdRepresentative)
    {
        var farmland =
            _economy.GetFarmland(
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
            || person.Age < 10
            || person.Tags.Has("state.imprisoned")
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
        return GetExpectedAnnualIncomeForWorkers(
            householdRepresentative,
            GetWorkingFarmWorkers(householdRepresentative));
    }

    public decimal GetExpectedAnnualIncomeAfterAddingLocalParcel(
        IPerson householdRepresentative)
    {
        var localParcels =
            GetLocalParcelCount(
                householdRepresentative);

        var workers =
            GetAvailableWorkers(
                householdRepresentative);

        var activeWorkers = FarmingRules.GetActiveWorkerCount(
            localParcels + 1,
            workers.Count);

        return GetExpectedAnnualIncomeForWorkers(
            householdRepresentative,
            workers.Take(activeWorkers));
    }

    private decimal GetExpectedAnnualIncomeForWorkers(
        IPerson householdRepresentative,
        IEnumerable<IPerson> workers)
    {
        var workerBaseIncome = GetWorkerBaseIncome();
        decimal baseIncome = 0m;

        foreach (var worker in workers)
        {
            var output = ApplyRecoverReduction(
                worker,
                workerBaseIncome);

            baseIncome += _workCapacity
                .GetWorkCapacity(worker)
                .Apply(output);
        }

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

        decimal total = 0m;

        foreach (var worker in workers)
        {
            var output = workerBaseIncome
                * (decimal)(_random.NextDouble() * 2.0);

            output = ApplyRecoverReduction(
                worker,
                output);

            total += _workCapacity
                .GetWorkCapacity(worker)
                .Apply(output);
        }

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

    private static decimal ApplyRecoverReduction(
        IPerson worker,
        decimal income)
    {
        var reduction = ReadPercent(
            worker,
            "modifier.salary.recover.");

        return reduction <= 0m
            ? income
            : income * (1m - reduction / 100m);
    }

    private static decimal ReadPercent(
        IPerson person,
        string prefix)
    {
        foreach (var tag in person.Tags.All)
        {
            if (!tag.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                continue;

            if (decimal.TryParse(tag[prefix.Length..], out var percent))
                return Math.Clamp(percent, 0m, 50m);
        }

        return 0m;
    }

    private decimal ApplyTownIncomeMultiplier(
        IPerson householdRepresentative,
        decimal income)
    {
        if (income <= 0m)
            return 0m;

        var town = _economy.GetResidenceTown(householdRepresentative);
        var strength = _economicStrength.ResolveFarming(town);
        var multiplier = _prosperity.GetIncomeMultiplier(town, strength);
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

        return GetAvailableWorkers(
                householdRepresentative)
            .Take(localParcels * 2)
            .ToList();
    }

    private int GetActiveWorkerCount(
        IPerson householdRepresentative)
    {
        var localParcels =
            GetLocalParcelCount(
                householdRepresentative);

        var workers =
            GetAvailableWorkers(
                householdRepresentative)
            .Count;

        return FarmingRules.GetActiveWorkerCount(
            localParcels,
            workers);
    }

    private decimal GetWorkerBaseIncome() =>
        _career.GetLevelOneSalary(
            AgricultureCareerId)
        * _eraSchedule.GetMultiplier(
            _gameState.Year)
        * FarmingRules.WorkerBaseIncomeScale;

    private int GetLocalParcelCount(
        IPerson householdRepresentative)
    {
        var residence =
            _economy.GetResidenceTown(
                householdRepresentative);

        return _economy.GetFarmland(
                householdRepresentative)
            .Count(asset =>
                asset.Town.Id.Equals(
                    residence.Id,
                    StringComparison.OrdinalIgnoreCase));
    }
}
