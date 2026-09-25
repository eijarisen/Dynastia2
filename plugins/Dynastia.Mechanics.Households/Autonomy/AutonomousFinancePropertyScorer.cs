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

        if ((snapshot.HasImmediateMedicalDanger || snapshot.HasSeriousMedicalDanger)
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

        if ((snapshot.HasImmediateMedicalDanger || snapshot.HasSeriousMedicalDanger)
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

        if ((snapshot.HasImmediateMedicalDanger || snapshot.HasSeriousMedicalDanger)
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
        if (!IsAtLeast(snapshot.FinancialState, needsResidence
                ? AutonomousFinancialState.Stable
                : AutonomousFinancialState.Secure)
            || snapshot.HasSeriousMedicalDanger
            || !needsResidence && (snapshot.Status?.IsLargeFamilyStrained == true
                || snapshot.Status?.IsOvercrowded == true
                || snapshot.NeedsFamilyContinuity && snapshot.HasRealisticReproductivePath))
        {
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

        var reserve = snapshot.ExpectedExpenses * (needsResidence ? 1m : 2m);
        if (finance.Wealth - price < reserve)
            return null;

        var capacityHelpsContinuity = needsResidence
            && snapshot.NeedsFamilyContinuity
            && snapshot.HasRealisticReproductivePath
            && NeedsMoreResidentCapacity(snapshot);
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
            capacityHelpsContinuity ? ContinuityBand(snapshot)
                : AutonomousPriorityBands.LongTermImprovement,
            needsResidence ? 78 : 44);
    }

    private AutonomousActionCandidate? ScoreExtendHouse(
        AutonomousActionCandidate option,
        AutonomousHouseholdSnapshot snapshot)
    {
        if (!IsAtLeast(snapshot.FinancialState, AutonomousFinancialState.Stable)
            || snapshot.HasSeriousMedicalDanger
            || !NeedsMoreResidentCapacity(snapshot)
            || !option.Parameters.TryGetValue("propertyId", out var idText)
            || !Guid.TryParse(idText, out var propertyId))
        {
            return null;
        }

        var residence = snapshot.Finance?.Houses.FirstOrDefault(house =>
            house.Id == propertyId && house.IsResidence);
        if (residence is null || residence.ExtensionCost <= 0m
            || snapshot.Finance!.Wealth - residence.ExtensionCost < snapshot.ExpectedExpenses)
        {
            return null;
        }

        return WithScore(option, AutonomyCategory.Property,
            snapshot.NeedsFamilyContinuity && snapshot.HasRealisticReproductivePath
                ? ContinuityBand(snapshot)
                : AutonomousPriorityBands.FamilyStability,
            snapshot.Status?.IsOvercrowded == true ? 94 : 88);
    }

    private static bool NeedsMoreResidentCapacity(AutonomousHouseholdSnapshot snapshot) =>
        snapshot.Status?.IsOvercrowded == true
        || snapshot.NeedsFamilyContinuity && snapshot.HasRealisticReproductivePath
            && snapshot.Status is { } status
            && status.ResidentCount >= status.OvercrowdingThreshold;

    private static int ContinuityBand(AutonomousHouseholdSnapshot snapshot) =>
        snapshot.NeedsMaleLineContinuity
            ? AutonomousPriorityBands.MaleLineContinuity
            : AutonomousPriorityBands.BloodlineContinuity;

    private AutonomousActionCandidate? ScoreBuyFarmland(
        AutonomousActionCandidate option,
        AutonomousHouseholdSnapshot snapshot)
    {
        if (snapshot.FinancialState != AutonomousFinancialState.Secure
            || snapshot.HasSeriousMedicalDanger
            || snapshot.Status?.IsLargeFamilyStrained == true
            || snapshot.Status?.IsOvercrowded == true
            || snapshot.NeedsFamilyContinuity && snapshot.HasRealisticReproductivePath)
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
            purchasePrice * 0.5m);

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
            || snapshot.HasSeriousMedicalDanger
            || snapshot.Status?.IsLargeFamilyStrained == true
            || snapshot.NeedsFamilyContinuity && snapshot.HasRealisticReproductivePath)
        {
            return null;
        }

        var wealth = snapshot.Finance?.Wealth ?? 0m;
        if (wealth < snapshot.ExpectedExpenses * 3m + 1000m)
            return null;

        return WithScore(option, AutonomyCategory.Property,
            AutonomousPriorityBands.LongTermImprovement, 36);
    }

    private AutonomousActionCandidate? ScoreMoveOut(
        AutonomousActionCandidate option,
        AutonomousHouseholdSnapshot snapshot)
    {
        // Branch creation starts a separate budget. A free house alone does
        // not make an unemployed heir or a family with dependants solvent.
        var family = _context.GetService<IFamilyService>();
        if (family is null || option.Target.Id == snapshot.Head.Id
            || option.Target.Id == snapshot.Spouse?.Id
            || !option.Parameters.TryGetValue("propertyId", out var idText)
            || !Guid.TryParse(idText, out var propertyId))
        {
            return null;
        }

        var house = snapshot.Finance?.Houses.FirstOrDefault(candidate =>
            candidate.Id == propertyId && !candidate.IsResidence);
        var residenceTown = _economy.GetResidenceTown(snapshot.Head);
        if (house is null || !house.Town.Id.Equals(residenceTown.Id,
                StringComparison.OrdinalIgnoreCase))
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
        if (branch.Count == 0 || branch.Any(member => member.IsSeriousHealthRisk
                || member.Person.Id == snapshot.Finance?.NannyId)
            || house.ResidentCapacity < branch.Count + 1)
        {
            return null;
        }

        var branchIncome = branch.Sum(member => member.Career?.AnnualIncome ?? 0m);
        var livingCost = _economy.GetLivingCostPerPerson(house.Town);
        // Keep room in the new household budget for a child or a bad year.
        if (branchIncome < livingCost * (branch.Count + 1))
            return null;

        var farm = _context.GetService<IFarmingService>();
        var farming = farm?.GetSnapshot(snapshot.Head);
        var farmWorkersLeaving = farm is null ? 0 : branch.Count(member =>
            farm.IsWorkingFarmWorker(member.Person, snapshot.Head));
        var farmIncomeLost = farming is null || farming.AvailableWorkers <= 0
            ? 0m
            : farming.ExpectedAnnualIncome * Math.Min(1m,
                (decimal)farmWorkersLeaving / farming.AvailableWorkers);
        var sourceIncome = snapshot.ProjectedIncome - branchIncome - farmIncomeLost
            - _economy.GetRentalIncome(house);
        var remainingExpenses = Math.Max(0m,
            snapshot.ExpectedExpenses - livingCost * branch.Count);
        if (sourceIncome < remainingExpenses)
            return null;

        return WithScore(option, AutonomyCategory.FamilyRelations,
            AutonomousPriorityBands.FamilyStability, 86);
    }
}
