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
        if (snapshot.FinancialState != AutonomousFinancialState.Secure
            || snapshot.HasSeriousMedicalDanger
            || snapshot.Status?.IsLargeFamilyStrained == true
            || snapshot.LivingChildCount < 2 && snapshot.HasRealisticReproductivePath)
        {
            return null;
        }

        var finance = snapshot.Finance;
        if (finance is null)
            return null;

        var price = _economy.GetHousePrice(_economy.GetResidenceTown(snapshot.Head));
        var reserve = snapshot.HasResidence
            ? snapshot.ExpectedExpenses * 2m
            : snapshot.ExpectedExpenses;
        if (finance.Wealth - price < reserve)
            return null;

        return WithScore(option, AutonomyCategory.Property,
            AutonomousPriorityBands.LongTermImprovement,
            snapshot.HasResidence ? 44 : 78);
    }

    private AutonomousActionCandidate? ScoreBuyFarmland(
        AutonomousActionCandidate option,
        AutonomousHouseholdSnapshot snapshot)
    {
        if (snapshot.FinancialState != AutonomousFinancialState.Secure
            || snapshot.HasSeriousMedicalDanger
            || snapshot.Status?.IsLargeFamilyStrained == true
            || snapshot.Status?.IsOvercrowded == true
            || snapshot.LivingChildCount < 2 && snapshot.HasRealisticReproductivePath)
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

        var reserve = Math.Max(
            snapshot.ExpectedExpenses * 2m,
            farming.PurchasePrice * 0.5m);

        if (snapshot.Finance.Wealth - farming.PurchasePrice < reserve)
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
            farming.PurchasePrice / marginalIncome;

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
            || snapshot.LivingChildCount < 2 && snapshot.HasRealisticReproductivePath)
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
        var hasSpareHouse = snapshot.Finance?.Houses
            .Any(house => !house.IsResidence) == true;

        if (hasSpareHouse)
        {
            return WithScore(
                option,
                AutonomyCategory.FamilyRelations,
                AutonomousPriorityBands.FamilyStability,
                86);
        }

        if (snapshot.Status?.IsOvercrowded != true)
            return null;

        return WithScore(
            option,
            AutonomyCategory.FamilyRelations,
            AutonomousPriorityBands.FamilyStability,
            68);
    }
}
