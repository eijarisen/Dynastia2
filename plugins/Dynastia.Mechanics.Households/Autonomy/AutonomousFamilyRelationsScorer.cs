using System.Globalization;
using Dynastia.Contracts;
using static Dynastia.Mechanics.Households.AutonomousScoringHelpers;

namespace Dynastia.Mechanics.Households;

internal sealed class AutonomousFamilyRelationsScorer : IAutonomousActionScorer
{
    private readonly IGamePluginContext _context;

    public AutonomousFamilyRelationsScorer(
        IGamePluginContext context)
    {
        _context = context;
    }

    public bool Handles(string actionId)
    {
        var id = actionId.ToLowerInvariant();
        return id switch
        {
            "family_relations.ask_money" => true,
            "family_relations.ask_house" => true,
            "family_relations.ask_farmland" => true,
            "family_relations.ask_job_help" => true,
            "family_relations.improve" => true,
            "family_relations.give_money" or
            "family_relations.give_house" or
            "family_relations.give_farmland" or
            "family_relations.give_job_help" => true,
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
            "family_relations.ask_money" =>
                ScoreAskMoney(option, snapshot),

            "family_relations.ask_house" =>
                ScoreAskHouse(option, snapshot),

            "family_relations.ask_farmland" =>
                ScoreAskFarmland(option, snapshot),

            "family_relations.ask_job_help" =>
                ScoreAskJobHelp(option, snapshot),

            "family_relations.improve" =>
                ScoreImproveRelations(option, snapshot),

            "family_relations.give_money" or
            "family_relations.give_house" or
            "family_relations.give_farmland" or
            "family_relations.give_job_help" =>
                ScoreFamilyGenerosity(option, snapshot),

            _ => null
        };
    }

    private AutonomousActionCandidate? ScoreAskMoney(
        AutonomousActionCandidate option,
        AutonomousHouseholdSnapshot snapshot)
    {
        if (!RequestIsReasonable(option))
            return null;

        var willingnessBonus = (option.RequestWillingness ?? 0) * 20;

        if ((snapshot.HasImmediateMedicalDanger || snapshot.HasMaterialUnmetDependentNeed)
            && (snapshot.Finance?.Wealth ?? 0) < 3000m)
        {
            return WithScore(option, AutonomyCategory.Survival,
                AutonomousPriorityBands.EmergencySurvival,
                86 + willingnessBonus);
        }

        return snapshot.FinancialState switch
        {
            AutonomousFinancialState.Critical => WithScore(option,
                AutonomyCategory.FamilyRelations,
                AutonomousPriorityBands.HouseholdSolvency,
                88 + willingnessBonus),
            AutonomousFinancialState.Poor => WithScore(option,
                AutonomyCategory.FamilyRelations,
                AutonomousPriorityBands.HouseholdSolvency,
                72 + willingnessBonus),
            _ => null
        };
    }

    private AutonomousActionCandidate? ScoreAskHouse(
        AutonomousActionCandidate option,
        AutonomousHouseholdSnapshot snapshot)
    {
        if (!RequestIsReasonable(option) || snapshot.HasResidence)
            return null;

        var willingnessBonus = (option.RequestWillingness ?? 0) * 15;
        return snapshot.FinancialState switch
        {
            AutonomousFinancialState.Critical or AutonomousFinancialState.Poor =>
                WithScore(option, AutonomyCategory.FamilyRelations,
                    AutonomousPriorityBands.HouseholdSolvency,
                    70 + willingnessBonus),
            AutonomousFinancialState.Stable =>
                WithScore(option, AutonomyCategory.Property,
                    AutonomousPriorityBands.LongTermImprovement,
                    55 + willingnessBonus),
            _ => null
        };
    }

    private AutonomousActionCandidate? ScoreAskFarmland(
        AutonomousActionCandidate option,
        AutonomousHouseholdSnapshot snapshot)
    {
        if (!RequestIsReasonable(option))
            return null;

        var farming = _context.GetService<IFarmingService>();
        var own = farming?.GetSnapshot(snapshot.Head);
        if (own is null || own.AvailableWorkers <= own.LocalWorkerCapacity)
            return null;

        var willingnessBonus = (option.RequestWillingness ?? 0) * 15;
        return snapshot.FinancialState switch
        {
            AutonomousFinancialState.Critical or AutonomousFinancialState.Poor =>
                WithScore(option, AutonomyCategory.FamilyRelations,
                    AutonomousPriorityBands.HouseholdSolvency,
                    66 + willingnessBonus),
            AutonomousFinancialState.Stable =>
                WithScore(option, AutonomyCategory.Property,
                    AutonomousPriorityBands.LongTermImprovement,
                    48 + willingnessBonus),
            _ => null
        };
    }

    private AutonomousActionCandidate? ScoreAskJobHelp(
        AutonomousActionCandidate option,
        AutonomousHouseholdSnapshot snapshot)
    {
        if (!RequestIsReasonable(option))
            return null;

        var workingAge = snapshot.Members
            .Where(member => member.Person.Age >= 18
                && member.Career is { IsRetired: false })
            .ToList();

        var unemployed = workingAge.Count(member =>
            member.Career?.IsEmployed != true);
        var lowLevelWorkers = workingAge.Count(member =>
            member.Career is
            {
                IsEmployed: true,
                IsSelfEmployed: false,
                JobLevel: <= 1
            });

        if (unemployed == 0
            && snapshot.FinancialState is AutonomousFinancialState.Stable
                or AutonomousFinancialState.Secure)
        {
            return null;
        }

        if (unemployed == 0 && lowLevelWorkers == 0)
            return null;

        var willingnessBonus = (option.RequestWillingness ?? 0) * 18;
        var needBonus = Math.Min(18, unemployed * 8 + lowLevelWorkers * 3);

        return snapshot.FinancialState switch
        {
            AutonomousFinancialState.Critical => WithScore(option,
                AutonomyCategory.FamilyRelations,
                AutonomousPriorityBands.HouseholdSolvency,
                88 + willingnessBonus + needBonus),
            AutonomousFinancialState.Poor => WithScore(option,
                AutonomyCategory.FamilyRelations,
                AutonomousPriorityBands.HouseholdSolvency,
                78 + willingnessBonus + needBonus),
            AutonomousFinancialState.Stable when unemployed > 0 => WithScore(option,
                AutonomyCategory.CareerDevelopment,
                AutonomousPriorityBands.LongTermImprovement,
                42 + willingnessBonus * 0.5 + needBonus),
            _ => null
        };
    }

    private AutonomousActionCandidate? ScoreImproveRelations(
        AutonomousActionCandidate option,
        AutonomousHouseholdSnapshot snapshot)
    {
        if (snapshot.FinancialState == AutonomousFinancialState.Critical
            || snapshot.HasImmediateMedicalDanger
            || snapshot.HasMaterialUnmetDependentNeed)
        {
            return null;
        }

        var relation = _context.GetService<IFamilyRelationService>()?
            .GetRelation(snapshot.Head, option.Target);

        if (relation is null || (relation.Familiarity >= 100 && relation.Sympathy >= 100))
            return null;

        var band = relation.Sympathy < 40
            ? AutonomousPriorityBands.LongTermImprovement
            : AutonomousPriorityBands.OptionalDevelopment;

        return WithScore(option, AutonomyCategory.FamilyRelations,
            band, relation.Sympathy < 40 ? 58 : 30);
    }

    private AutonomousActionCandidate? ScoreFamilyGenerosity(
        AutonomousActionCandidate option,
        AutonomousHouseholdSnapshot snapshot)
    {
        if (snapshot.FinancialState != AutonomousFinancialState.Secure
            || snapshot.HasImmediateMedicalDanger
            || snapshot.HasMaterialUnmetDependentNeed)
        {
            return null;
        }

        var economy = _context.GetService<IEconomyService>();
        var recipient = economy?.GetHousehold(option.Target);
        if (economy is null || recipient is null || snapshot.Finance is null
            || option.Target.Id == snapshot.Head.Id
            || economy.GetHouseholdId(option.Target) == economy.GetHouseholdId(snapshot.Head))
        {
            return null;
        }

        var forecast = economy.GetAnnualForecast(option.Target);
        var recipientExpenses = forecast?.ProjectedExpenses ?? recipient.LastExpenses;
        var recipientReserve = Math.Max(1000m, recipientExpenses);
        var needsHelp = recipient.Wealth < recipientReserve;
        if (!needsHelp)
            return null;

        var id = option.Action.Id.ToLowerInvariant();
        if (id == "family_relations.give_money")
        {
            if (!option.Parameters.TryGetValue("amount", out var amountText)
                || !decimal.TryParse(amountText, NumberStyles.Number,
                    CultureInfo.InvariantCulture, out var amount)
                || amount < 1000m
                || snapshot.Finance.Wealth - amount < snapshot.ExpectedExpenses * 2m
                || amount > Math.Ceiling((recipientReserve - recipient.Wealth) / 1000m) * 1000m)
            {
                return null;
            }
        }
        else if (id == "family_relations.give_house")
        {
            if (recipient.Houses.Any(house => house.IsResidence)
                || !option.Parameters.TryGetValue("propertyId", out var propertyText)
                || !Guid.TryParse(propertyText, out var propertyId))
            {
                return null;
            }

            var house = snapshot.Finance.Houses.FirstOrDefault(candidate =>
                candidate.Id == propertyId && !candidate.IsResidence);
            if (house is null
                || !house.Town.Id.Equals(economy.GetResidenceTown(option.Target).Id,
                    StringComparison.OrdinalIgnoreCase)
                || snapshot.ProjectedIncome - economy.GetRentalIncome(house) < snapshot.ExpectedExpenses)
            {
                return null;
            }
        }
        else if (id == "family_relations.give_farmland")
        {
            var farming = _context.GetService<IFarmingService>();
            var targetFarm = farming?.GetSnapshot(option.Target);
            if (targetFarm is null || targetFarm.AvailableWorkers <= targetFarm.LocalWorkerCapacity
                || !option.Parameters.TryGetValue("farmlandId", out var parcelText)
                || !Guid.TryParse(parcelText, out var parcelId))
            {
                return null;
            }

            var parcel = economy.GetFarmland(snapshot.Head).FirstOrDefault(asset => asset.Id == parcelId);
            if (parcel is null
                || parcel.Town.Id.Equals(economy.GetResidenceTown(snapshot.Head).Id,
                    StringComparison.OrdinalIgnoreCase)
                || !parcel.Town.Id.Equals(economy.GetResidenceTown(option.Target).Id,
                    StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }
        }

        var recipientIsExistingChild = snapshot.ExistingChildren.Any(person => person.Id == option.Target.Id);
        if (snapshot.NeedsFamilyContinuity && !recipientIsExistingChild)
            return null;

        return WithScore(option, AutonomyCategory.FamilyRelations,
            recipientIsExistingChild
                ? AutonomousPriorityBands.SustainableFamilyContinuity
                : AutonomousPriorityBands.OptionalDevelopment,
            recipientIsExistingChild ? 76 : 24);
    }

    private static bool RequestIsReasonable(AutonomousActionCandidate option) =>
        option.RequestWillingness is >= AutonomousStrategyRules.MinimumUsefulRequestWillingness;
}
