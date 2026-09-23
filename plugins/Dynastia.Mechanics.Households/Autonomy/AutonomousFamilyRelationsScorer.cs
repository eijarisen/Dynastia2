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

        if ((snapshot.HasImmediateMedicalDanger || snapshot.HasSeriousMedicalDanger)
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
        if (own is null || own.AvailableWorkers == 0)
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
            || snapshot.HasSeriousMedicalDanger)
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
            || snapshot.HasSeriousMedicalDanger
            || snapshot.LivingChildCount < 2 && snapshot.HasRealisticReproductivePath)
        {
            return null;
        }

        return WithScore(option, AutonomyCategory.FamilyRelations,
            AutonomousPriorityBands.OptionalDevelopment, 24);
    }

    private static bool RequestIsReasonable(AutonomousActionCandidate option) =>
        option.RequestWillingness is >= AutonomousStrategyRules.MinimumUsefulRequestWillingness;
}
