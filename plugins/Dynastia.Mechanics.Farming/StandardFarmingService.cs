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
    private readonly IGameRandom _random;
    private readonly IGameEventBus _events;
    private readonly FarmingEraSchedule _eraSchedule;

    public StandardFarmingService(
        IGameState gameState,
        IEconomyService economy,
        ICareerService career,
        IGameRandom random,
        IGameEventBus events,
        FarmingEraSchedule eraSchedule)
    {
        _gameState = gameState;
        _economy = economy;
        _career = career;
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
        var staffing =
            GetStaffingFactors(
                householdRepresentative);

        return GetExpectedAnnualIncome(staffing);
    }

    public decimal GetExpectedAnnualIncomeAfterAddingLocalParcel(
        IPerson householdRepresentative)
    {
        var localParcels =
            GetLocalParcelCount(
                householdRepresentative);

        var workers =
            GetAvailableWorkers(
                householdRepresentative)
            .Count;

        return GetExpectedAnnualIncome(
            FarmingRules.GetStaffingFactors(
                localParcels + 1,
                workers));
    }

    private decimal GetExpectedAnnualIncome(
        IReadOnlyList<decimal> staffing)
    {
        if (staffing.Count == 0)
            return 0m;

        var referenceIncome =
            _career.GetLevelOneSalary(
                AgricultureCareerId);

        var multiplier =
            _eraSchedule.GetMultiplier(
                _gameState.Year);

        return Math.Round(
            referenceIncome
            * multiplier
            * FarmingRules.IncomeScale
            * staffing.Sum(),
            0,
            MidpointRounding.AwayFromZero);
    }

    decimal IHouseholdIncomeProvider.GetAnnualIncome(
        IPerson householdRepresentative)
    {
        var staffing =
            GetStaffingFactors(
                householdRepresentative);

        if (staffing.Count == 0)
            return 0m;

        var annualReference =
            _career.GetLevelOneSalary(
                AgricultureCareerId)
            * _eraSchedule.GetMultiplier(
                _gameState.Year)
            * FarmingRules.IncomeScale;

        decimal total = 0m;

        foreach (var staffingFactor in staffing)
        {
            var monthlyExpected =
                annualReference
                / 12m
                * staffingFactor;

            for (var month = 0; month < 12; month++)
            {
                total += monthlyExpected
                    * (decimal)(_random.NextDouble() * 2.0);
            }
        }

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

        var workers = GetWorkingFarmWorkers(
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

    private IReadOnlyList<decimal> GetStaffingFactors(
        IPerson householdRepresentative)
    {
        var localParcels =
            GetLocalParcelCount(
                householdRepresentative);

        var workers =
            GetAvailableWorkers(
                householdRepresentative)
            .Count;

        return FarmingRules.GetStaffingFactors(
            localParcels,
            workers);
    }

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
