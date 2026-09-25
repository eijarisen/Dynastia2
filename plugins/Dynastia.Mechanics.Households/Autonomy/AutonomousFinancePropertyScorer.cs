using System.Globalization;
using Dynastia.Contracts;
using static Dynastia.Mechanics.Households.AutonomousScoringHelpers;

namespace Dynastia.Mechanics.Households;

internal sealed class AutonomousFinancePropertyScorer : IAutonomousActionScorer
{
    private readonly IGamePluginContext _context;
    private readonly IGameState _gameState;
    private readonly IEconomyService _economy;

    public AutonomousFinancePropertyScorer(
        IGamePluginContext context,
        IGameState gameState,
        IEconomyService economy)
    {
        _context = context;
        _gameState = gameState;
        _economy = economy;
    }

    public bool Handles(string actionId)
    {
        var id = actionId.ToLowerInvariant();
        return id switch
        {
            "household.sell_house" => true,
            "farming.sell_farmland" => true,
            "loan.take" => true,
            "household.buy_house" => true,
            "household.extend_house" => true,
            "farming.buy_farmland" => true,
            "loan.give" => true,
            "household.ask_move_out" => true,
            _ => false
        };
    }

    public AutonomousActionCandidate? Score(
        AutonomousActionCandidate option,
        AutonomousHouseholdSnapshot snapshot)
    {
        var id = option.Action.Id.ToLowerInvariant();

        return id switch
        {
            "household.sell_house" =>
                ScoreSellHouse(option, snapshot),

            "farming.sell_farmland" =>
                ScoreSellFarmland(option, snapshot),

            "loan.take" =>
                ScoreTakeLoan(option, snapshot),

            "household.buy_house" =>
                ScoreBuyHouse(option, snapshot),

            "household.extend_house" =>
                ScoreExtendHouse(option, snapshot),

            "farming.buy_farmland" =>
                ScoreBuyFarmland(option, snapshot),

            "loan.give" =>
                ScoreGiveLoan(option, snapshot),

            "household.ask_move_out" =>
                ScoreMoveOut(option, snapshot),

            _ => null
        };
    }

    private AutonomousActionCandidate? ScoreSellHouse(
        AutonomousActionCandidate option,
        AutonomousHouseholdSnapshot snapshot)
    {
        if (!snapshot.HasInvestmentHouse)
            return null;

        if ((snapshot.HasImmediateMedicalDanger || snapshot.HasMaterialUnmetDependentNeed)
            && (snapshot.Finance?.Wealth ?? 0) < 3000m)
        {
            return WithScore(option, AutonomyCategory.Survival,
                AutonomousPriorityBands.EmergencySurvival, 97);
        }

        return snapshot.FinancialState switch
        {
            AutonomousFinancialState.Critical => WithScore(option,
                AutonomyCategory.Solvency,
                AutonomousPriorityBands.HouseholdSolvency, 96),
            AutonomousFinancialState.Poor => WithScore(option,
                AutonomyCategory.Solvency,
                AutonomousPriorityBands.HouseholdSolvency, 78),
            _ => null
        };
    }

    private AutonomousActionCandidate? ScoreSellFarmland(
        AutonomousActionCandidate option,
        AutonomousHouseholdSnapshot snapshot)
    {
        var farming = _context.GetService<IFarmingService>();
        var farm = farming?.GetSnapshot(snapshot.Head);
        if (farm is null || farm.TotalParcelCount == 0)
            return null;

        var wealth = snapshot.Finance?.Wealth ?? 0m;
        var remoteCount = farm.TotalParcelCount - farm.LocalParcelCount;

        if ((snapshot.HasImmediateMedicalDanger || snapshot.HasMaterialUnmetDependentNeed)
            && wealth < 3000m)
        {
            return WithScore(option, AutonomyCategory.Survival,
                AutonomousPriorityBands.EmergencySurvival, 101);
        }

        return snapshot.FinancialState switch
        {
            AutonomousFinancialState.Critical => WithScore(option,
                AutonomyCategory.Solvency,
                AutonomousPriorityBands.HouseholdSolvency,
                remoteCount > 0 ? 99 : 92),
            AutonomousFinancialState.Poor when remoteCount > 0 => WithScore(option,
                AutonomyCategory.Solvency,
                AutonomousPriorityBands.HouseholdSolvency, 82),
            AutonomousFinancialState.Poor => WithScore(option,
                AutonomyCategory.Solvency,
                AutonomousPriorityBands.HouseholdSolvency, 70),
            _ => null
        };
    }

    private AutonomousActionCandidate? ScoreTakeLoan(
        AutonomousActionCandidate option,
        AutonomousHouseholdSnapshot snapshot)
    {
        if (snapshot.ProjectedIncome <= 0)
            return null;

        if ((snapshot.HasImmediateMedicalDanger || snapshot.HasMaterialUnmetDependentNeed)
            && (snapshot.Finance?.Wealth ?? 0) < 3000m)
        {
            return WithScore(option, AutonomyCategory.Survival,
                AutonomousPriorityBands.EmergencySurvival, 62);
        }

        if (snapshot.FinancialState == AutonomousFinancialState.Critical)
        {
            return WithScore(option, AutonomyCategory.Solvency,
                AutonomousPriorityBands.HouseholdSolvency, 54);
        }

        return null;
    }

    private AutonomousActionCandidate? ScoreBuyHouse(
        AutonomousActionCandidate option,
        AutonomousHouseholdSnapshot snapshot)
    {
        var needsResidence = !snapshot.HasResidence;
        var urgentCrowding = snapshot.Status?.IsOvercrowded == true;
        if (!urgentCrowding
            && !IsAtLeast(snapshot.FinancialState, needsResidence
                ? AutonomousFinancialState.Stable
                : AutonomousFinancialState.Secure))
        {
            return null;
        }

        if (!needsResidence && (snapshot.Status?.IsLargeFamilyStrained == true
                || snapshot.Status?.IsOvercrowded == true
                || snapshot.NeedsFamilyContinuity))
        {
            // Extra investment houses are optional. Current-home strain and
            // expansion are solved through the residence or ordinary rented
            // branch route instead of buying a second house speculatively.
            return null;
        }

        var finance = snapshot.Finance;
        if (finance is null
            || !option.Parameters.TryGetValue("houseAskingPrice", out var priceText)
            || !decimal.TryParse(priceText, NumberStyles.Number,
                CultureInfo.InvariantCulture, out var price)
            || price <= 0m)
        {
            return null;
        }

        var capacityHelpsContinuity = needsResidence
            && snapshot.NeedsFamilyExpansion
            && snapshot.HasRealisticReproductivePath
            && NeedsMoreResidentCapacity(snapshot);
        var reserve = snapshot.ExpectedExpenses * (needsResidence ? 1m : 2m);
        var protectedPlanReserve = urgentCrowding || capacityHelpsContinuity
            ? 0m
            : ProtectedCash(snapshot);
        if (finance.Wealth - price < reserve + protectedPlanReserve)
            return null;

        if (capacityHelpsContinuity)
        {
            if (snapshot.Status is not { } status
                || !option.Parameters.TryGetValue("houseCapacity", out var capacityText)
                || !int.TryParse(capacityText, out var capacity)
                || capacity < Math.Min(status.ResidentCount, status.OvercrowdingThreshold))
            {
                return null;
            }

            // A rented home may already be as large as every market offer.
            // Buying it unlocks extensions; reserve the complete path to one
            // additional resident before committing to that first step.
            var requiredExtensions = Math.Max(0, (status.ResidentCount + 2 - capacity) / 2);
            var extensionCost = Math.Round(price * 0.25m, 0, MidpointRounding.AwayFromZero);
            if (finance.Wealth - price - requiredExtensions * extensionCost < reserve)
                return null;
        }

        return WithScore(option, AutonomyCategory.Property,
            urgentCrowding
                ? AutonomousPriorityBands.EmergencySurvival
                : capacityHelpsContinuity
                    ? AutonomousPriorityBands.SustainableFamilyContinuity
                    : AutonomousPriorityBands.LongTermImprovement,
            urgentCrowding ? 96 : needsResidence ? 78 : 44);
    }

    private AutonomousActionCandidate? ScoreExtendHouse(
        AutonomousActionCandidate option,
        AutonomousHouseholdSnapshot snapshot)
    {
        var urgentCrowding = snapshot.Status?.IsOvercrowded == true;
        if (!urgentCrowding && !IsAtLeast(snapshot.FinancialState, AutonomousFinancialState.Stable)
            || !NeedsMoreResidentCapacity(snapshot)
            || !option.Parameters.TryGetValue("propertyId", out var idText)
            || !Guid.TryParse(idText, out var propertyId))
        {
            return null;
        }

        var residence = snapshot.Finance?.Houses.FirstOrDefault(house =>
            house.Id == propertyId && house.IsResidence);
        var continuityExtension = snapshot.NeedsFamilyExpansion
            && snapshot.HasRealisticReproductivePath;
        var postExtensionReserve = urgentCrowding
            ? snapshot.ExpectedExpenses * AutonomousStrategyRules.ForecastReserveFraction
            : snapshot.ExpectedExpenses + (continuityExtension ? 0m : ProtectedCash(snapshot));
        if (residence is null || residence.ExtensionCost <= 0m
            || snapshot.Finance!.Wealth - residence.ExtensionCost < postExtensionReserve)
        {
            return null;
        }

        return WithScore(option, AutonomyCategory.Property,
            urgentCrowding
                ? AutonomousPriorityBands.EmergencySurvival
                : continuityExtension
                    ? AutonomousPriorityBands.SustainableFamilyContinuity
                    : AutonomousPriorityBands.FamilyStability,
            urgentCrowding ? 98 : 88);
    }

    private static bool NeedsMoreResidentCapacity(AutonomousHouseholdSnapshot snapshot) =>
        snapshot.Status?.IsOvercrowded == true
        || snapshot.NeedsFamilyExpansion && snapshot.HasRealisticReproductivePath
            && snapshot.Status is { } status
            && status.ResidentCount >= status.OvercrowdingThreshold;

    private static int ContinuityBand(AutonomousHouseholdSnapshot snapshot) =>
        AutonomousPriorityBands.SustainableFamilyContinuity;

    private AutonomousActionCandidate? ScoreBuyFarmland(
        AutonomousActionCandidate option,
        AutonomousHouseholdSnapshot snapshot)
    {
        if (snapshot.FinancialState != AutonomousFinancialState.Secure
            || snapshot.HasImmediateMedicalDanger
            || snapshot.HasMaterialUnmetDependentNeed
            || snapshot.Status?.IsLargeFamilyStrained == true
            || snapshot.Status?.IsOvercrowded == true
            || snapshot.NeedsFamilyContinuity)
        {
            return null;
        }

        var farming = _context.GetService<IFarmingService>();
        if (farming is null || snapshot.Finance is null)
            return null;

        var farm = farming.GetSnapshot(snapshot.Head);

        // Farmland is an investment in household labour, not a generic place
        // to park spare cash. Do not buy another parcel while the current
        // local land already consumes all available farm labour.
        if (farm.AvailableWorkers <= farm.LocalWorkerCapacity)
            return null;

        var town = _economy.GetResidenceTown(snapshot.Head);
        var purchasePrice = farming.GetPurchasePrice(town, _gameState.Year);
        var reserve = Math.Max(
            snapshot.ExpectedExpenses * 2m,
            purchasePrice * 0.5m) + ProtectedCash(snapshot);

        if (snapshot.Finance.Wealth - purchasePrice < reserve)
            return null;

        var projectedIncome =
            farming.GetExpectedAnnualIncomeAfterAddingLocalParcel(
                snapshot.Head);

        var marginalIncome =
            projectedIncome - farm.ExpectedAnnualIncome;

        if (marginalIncome <= 0m)
            return null;

        // Require the additional parcel to recover its purchase price within
        // roughly a decade at expected output. This naturally makes farmland
        // rarer in later eras as its income multiplier declines, while still
        // allowing productive agrarian households to expand.
        var paybackYears =
            purchasePrice / marginalIncome;

        if (paybackYears > 10m)
            return null;

        var spareWorkers =
            farm.AvailableWorkers - farm.LocalWorkerCapacity;

        var earlyEraBonus =
            Math.Clamp((1950 - _gameState.Year) / 50.0, 0, 5);

        var returnBonus =
            Math.Clamp((10.0 - (double)paybackYears) * 2.5, 0, 15);

        var labourBonus =
            Math.Min(spareWorkers, 2) * 4;

        return WithScore(
            option,
            AutonomyCategory.Property,
            AutonomousPriorityBands.LongTermImprovement,
            38 + earlyEraBonus + returnBonus + labourBonus);
    }

    private AutonomousActionCandidate? ScoreGiveLoan(
        AutonomousActionCandidate option,
        AutonomousHouseholdSnapshot snapshot)
    {
        if (snapshot.FinancialState != AutonomousFinancialState.Secure
            || snapshot.HasImmediateMedicalDanger
            || snapshot.HasMaterialUnmetDependentNeed
            || snapshot.Status?.IsLargeFamilyStrained == true
            || snapshot.NeedsFamilyContinuity)
        {
            return null;
        }

        var wealth = snapshot.Finance?.Wealth ?? 0m;
        var principal = option.Parameters.TryGetValue("principal", out var principalText)
            && decimal.TryParse(principalText, NumberStyles.Number,
                CultureInfo.InvariantCulture, out var parsedPrincipal)
            ? Math.Max(0m, parsedPrincipal)
            : 1000m;
        if (wealth - principal < snapshot.ExpectedExpenses * 3m + ProtectedCash(snapshot))
            return null;

        return WithScore(option, AutonomyCategory.Property,
            AutonomousPriorityBands.LongTermImprovement, 36);
    }

    private AutonomousActionCandidate? ScoreMoveOut(
        AutonomousActionCandidate option,
        AutonomousHouseholdSnapshot snapshot)
    {
        // The shared move action already supports establishing a rented home
        // when no spare property is supplied. Autonomy must project that normal
        // route rather than requiring a second-house grind.
        var family = _context.GetService<IFamilyService>();
        if (family is null || option.Target.Id == snapshot.Head.Id
            || option.Target.Id == snapshot.Spouse?.Id)
        {
            return null;
        }

        var residents = snapshot.Members.ToDictionary(member => member.Person.Id);
        var movingIds = new HashSet<Guid>();
        void AddBranch(IPerson person, bool includeSpouse)
        {
            if (person.Id == snapshot.Head.Id || !residents.ContainsKey(person.Id)
                || !movingIds.Add(person.Id))
                return;
            if (includeSpouse && family.GetSpouse(person) is { } spouse)
                AddBranch(spouse, false);
            foreach (var child in family.GetChildren(person).Where(child => child.Age < 18))
                AddBranch(child, false);
        }

        AddBranch(option.Target, true);
        var branch = movingIds.Select(id => residents[id]).ToList();
        if (branch.Count == 0
            || branch.Any(member => member.IsImmediateHealthRisk
                || member.Person.Id == snapshot.Finance?.NannyId))
        {
            return null;
        }

        HousePropertyInfo? house = null;
        if (option.Parameters.TryGetValue("propertyId", out var idText))
        {
            if (!Guid.TryParse(idText, out var propertyId))
                return null;
            house = snapshot.Finance?.Houses.FirstOrDefault(candidate =>
                candidate.Id == propertyId && !candidate.IsResidence);
            if (house is null)
                return null;
        }

        var residenceTown = _economy.GetResidenceTown(snapshot.Head);
        if (house is not null
            && !house.Town.Id.Equals(residenceTown.Id, StringComparison.OrdinalIgnoreCase))
        {
            // The shared transition relocates remote-house recipients and can
            // replace their employment. Without a pure relocation-income preview
            // this planner does not gamble on that branch.
            return null;
        }

        var destination = house?.Town ?? residenceTown;
        // Independent rented households use the economy mechanic's established
        // base residence capacity. Keep this preview local so Households does
        // not require an expanded IEconomyService contract merely to score a
        // legal rented move-out path.
        const int defaultRentalResidenceCapacity = 6;
        var capacity = house?.ResidentCapacity
            ?? defaultRentalResidenceCapacity;
        if (branch.Count > capacity)
            return null;

        var branchIncome = branch.Sum(member => member.Career?.AnnualIncome ?? 0m);
        var livingCost = _economy.GetLivingCostPerPerson(destination);
        var branchExpenses = livingCost * branch.Count
            + (house is null ? _economy.GetResidenceRent(destination) : 0m);
        // Employment income is already a committed annual contribution. Do not
        // apply the variable-income stress haircut to the entire wage bill. A
        // rented branch must be able to cover its ordinary essential costs.
        if (branchIncome < branchExpenses)
            return null;

        var farming = _context.GetService<IFarmingService>();
        var farm = farming?.GetSnapshot(snapshot.Head);
        var farmWorkersLeaving = farming is null ? 0 : branch.Count(member =>
            farming.IsWorkingFarmWorker(member.Person, snapshot.Head));
        var farmIncomeLost = farm is null || farm.AvailableWorkers <= 0
            ? 0m
            : farm.ExpectedAnnualIncome * Math.Min(1m,
                (decimal)farmWorkersLeaving / farm.AvailableWorkers);
        var sourceIncome = snapshot.ProjectedIncome - branchIncome - farmIncomeLost
            - (house is null ? 0m : _economy.GetRentalIncome(house));
        var remainingExpenses = Math.Max(0m,
            snapshot.ExpectedExpenses - livingCost * branch.Count);
        if (sourceIncome < remainingExpenses)
            return null;

        var existingChild = snapshot.ExistingChildren.Any(child => child.Id == option.Target.Id);
        var priority = snapshot.Status?.IsOvercrowded == true
            ? AutonomousPriorityBands.EmergencySurvival
            : existingChild
                ? AutonomousPriorityBands.SustainableFamilyContinuity
                : AutonomousPriorityBands.LongTermImprovement;
        var score = snapshot.Status?.IsOvercrowded == true ? 98 : existingChild ? 92 : 54;

        return WithScore(option,
            existingChild ? AutonomyCategory.Continuity : AutonomyCategory.FamilyRelations,
            priority, score);
    }

}
