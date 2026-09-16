using System.Globalization;
using Dynastia.Contracts;

namespace Dynastia.Mechanics.Households;

internal sealed class AdvancedAutonomousHouseholdStrategy :
    IAutonomousHouseholdStrategy
{
    private const decimal NannyExpenseEstimate = 250m;

    private readonly IGamePluginContext _context;
    private readonly IGameState _gameState;
    private readonly IHouseholdService _households;
    private readonly IActionRegistry _actions;
    private readonly IEconomyService _economy;
    private readonly IHealthService _health;
    private readonly ICareerService _career;
    private readonly IFamilyService _family;
    private readonly IStatsService _stats;
    private readonly IGameRandom _random;

    public AdvancedAutonomousHouseholdStrategy(
        IGamePluginContext context,
        IGameState gameState,
        IHouseholdService households,
        IActionRegistry actions,
        IEconomyService economy,
        IHealthService health,
        ICareerService career,
        IFamilyService family,
        IStatsService stats,
        IGameRandom random)
    {
        _context = context;
        _gameState = gameState;
        _households = households;
        _actions = actions;
        _economy = economy;
        _health = health;
        _career = career;
        _family = family;
        _stats = stats;
        _random = random;
    }

    public AutonomousHouseholdSnapshot BuildSnapshot(
        HouseholdInfo household)
    {
        var head = FindPerson(household.HeadId)
            ?? throw new InvalidOperationException(
                $"Household {household.HouseholdId} has no head.");

        var memberIds = household.MemberIds.ToHashSet();
        memberIds.Add(head.Id);

        var members = _gameState.People
            .Where(person => memberIds.Contains(person.Id)
                && person.Tags.Has("state.alive"))
            .DistinctBy(person => person.Id)
            .Select(BuildMemberSnapshot)
            .ToList();

        if (!members.Any(member => member.Person.Id == head.Id))
            members.Insert(0, BuildMemberSnapshot(head));

        var finance = _economy.GetHousehold(head);
        var status = _households.GetStatus(head);
        var loans = _context.GetService<ILoanService>();

        var projectedIncome = _economy.GetProjectedAnnualIncome(head);
        if (loans is not null)
        {
            projectedIncome += loans.GetLoansGiven(head)
                .Sum(loan => loan.AnnualPayment);
        }

        var debtPayments = loans?.GetDebts(head)
            .Sum(loan => loan.AnnualPayment) ?? 0m;

        var expectedExpenses = EstimateOrdinaryExpenses(
            head,
            members.Count,
            finance,
            debtPayments);

        var wealth = finance?.Wealth ?? 0m;
        var financialState = AutonomousStrategyRules.GetFinancialState(
            wealth,
            projectedIncome,
            expectedExpenses);

        var spouse = _family.GetSpouse(head);
        if (spouse is not null && !spouse.Tags.Has("state.alive"))
            spouse = null;

        var livingChildren = _family.GetChildren(head)
            .Where(child => child.Tags.Has("state.alive"))
            .DistinctBy(child => child.Id)
            .ToList();

        var dependentChildren = livingChildren.Count(child => child.Age < 18);
        var reproductivePath = HasRealisticReproductivePath(head, spouse);
        var canTryForChild = CanActivelyTryForChild(
            head,
            spouse,
            livingChildren.Count,
            dependentChildren,
            financialState,
            status,
            reproductivePath);

        var marriageSatisfaction =
            spouse is null
                ? null
                : _context
                    .GetService<IMarriageSatisfactionService>()?
                    .GetSatisfactionBetween(head, spouse)?
                    .Value;

        var relatedHouseholds =
            _context.GetService<IFamilyRelationService>()?
                .GetRelatedHouseholds(head)
            ?? Array.Empty<RelatedFamilyHouseholdInfo>();

        var hasImmediateMedicalDanger = members.Any(member => member.IsImmediateHealthRisk);
        var hasSeriousMedicalDanger = members.Any(member => member.IsSeriousHealthRisk);

        return new AutonomousHouseholdSnapshot(
            household,
            head,
            finance,
            status,
            members,
            spouse,
            livingChildren,
            financialState,
            projectedIncome,
            expectedExpenses,
            debtPayments,
            hasImmediateMedicalDanger,
            hasSeriousMedicalDanger,
            finance?.Houses.Any(house => house.IsRented) == true,
            finance?.Houses.Any(house => house.IsResidence) == true,
            reproductivePath,
            canTryForChild,
            livingChildren.Count,
            dependentChildren,
            marriageSatisfaction,
            CalculateReproductiveUrgency(head, spouse, livingChildren.Count),
            relatedHouseholds);
    }

    public IReadOnlyList<AutonomousActionCandidate> GetAvailableActions(
        AutonomousHouseholdSnapshot snapshot)
    {
        var candidates = new List<AutonomousActionCandidate>();

        foreach (var member in snapshot.Members)
        {
            var parameters = EmptyParameters();
            foreach (var action in GetMechanicallyAvailableActions(
                snapshot.Head,
                member.Person,
                parameters))
            {
                var actionParameters = BuildParametersForAction(
                    action.Id,
                    snapshot,
                    member.Person,
                    null);

                if (actionParameters is null)
                    continue;

                candidates.Add(NewCandidate(
                    action,
                    member.Person,
                    actionParameters));
            }
        }

        var relations = _context.GetService<IFamilyRelationService>();
        if (relations is not null)
        {
            foreach (var related in snapshot.RelatedHouseholds)
            {
                var relative = FindPerson(related.PrimaryRelation.RelativeId);
                if (relative is null || !relative.Tags.Has("state.alive"))
                    continue;

                var baseParameters = new Dictionary<string, string>(
                    StringComparer.OrdinalIgnoreCase)
                {
                    ["familyRelations"] = "true"
                };

                foreach (var action in GetMechanicallyAvailableActions(
                    snapshot.Head,
                    relative,
                    baseParameters))
                {
                    if (!action.Id.StartsWith(
                        "family_relations.",
                        StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var parameters = BuildParametersForAction(
                        action.Id,
                        snapshot,
                        relative,
                        baseParameters);

                    if (parameters is null)
                        continue;

                    double? willingness = action.Id is
                        "family_relations.ask_money" or
                        "family_relations.ask_house" or
                        "family_relations.ask_job_help"
                            ? relations.EvaluateRequestWillingness(
                                snapshot.Head,
                                relative)
                            : null;

                    candidates.Add(
                        NewCandidate(
                            action,
                            relative,
                            parameters,
                            willingness));
                }
            }
        }

        return candidates
            .GroupBy(candidate =>
                BuildCandidateKey(candidate),
                StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();
    }

    public AutonomousActionCandidate? ScoreAction(
        AutonomousActionCandidate action,
        AutonomousHouseholdSnapshot snapshot)
    {
        var scored = ScoreCore(action, snapshot);
        if (scored is null || scored.Score <= 0 || scored.PriorityBand <= 0)
            return null;

        var adjusted = ApplyPersonality(scored, snapshot.Head);
        return adjusted with
        {
            Score = Math.Clamp(adjusted.Score, 1, 150)
        };
    }

    public AutonomousActionCandidate? ChooseAction(
        IReadOnlyList<AutonomousActionCandidate> scoredActions)
    {
        if (scoredActions.Count == 0)
            return null;

        var highestBand = scoredActions.Max(action => action.PriorityBand);
        var inBand = scoredActions
            .Where(action => action.PriorityBand == highestBand)
            .OrderByDescending(action => action.Score)
            .ThenBy(action => action.Action.Id, StringComparer.OrdinalIgnoreCase)
            .ThenBy(action => action.Target.Id)
            .ToList();

        if (inBand.Count == 0)
            return null;

        var best = inBand[0].Score;
        var competitive = inBand
            .Where(action => AutonomousStrategyRules.IsCloseEnoughToCompete(
                action.Score,
                best))
            .ToList();

        if (competitive.Count == 1)
            return competitive[0];

        var totalWeight = competitive.Sum(action => action.Score * action.Score);
        var roll = _random.NextDouble() * totalWeight;

        foreach (var action in competitive)
        {
            var weight = action.Score * action.Score;
            if (roll < weight)
                return action;

            roll -= weight;
        }

        return competitive[^1];
    }

    public bool QueueAction(
        AutonomousActionCandidate action,
        AutonomousHouseholdSnapshot snapshot)
    {
        return _actions.ExecuteAutonomous(
            action.Action.Id,
            snapshot.Head,
            action.Target,
            action.Parameters).Success;
    }

    private AutonomousActionCandidate? ScoreCore(
        AutonomousActionCandidate option,
        AutonomousHouseholdSnapshot snapshot)
    {
        var id = option.Action.Id.ToLowerInvariant();
        var targetMember = snapshot.Members
            .FirstOrDefault(member => member.Person.Id == option.Target.Id);

        return id switch
        {
            "wellbeing.heal_relative" =>
                ScoreMedicalTreatment(option, snapshot, targetMember),

            "wellbeing.therapy" =>
                ScoreTherapy(option, snapshot, targetMember),

            "wellbeing.recover" =>
                ScoreRecover(option, snapshot),

            "career.ask_to_recover" =>
                ScoreRequestedRecover(option, snapshot, targetMember),

            "household.hire_nanny" or
            "household.ask_daughter_nanny" =>
                ScoreNanny(option, snapshot),

            "household.fire_nanny" =>
                ScoreFireNanny(option, snapshot),

            "household.sell_house" =>
                ScoreSellHouse(option, snapshot),

            "farming.sell_farmland" =>
                ScoreSellFarmland(option, snapshot),

            "loan.take" =>
                ScoreTakeLoan(option, snapshot),

            "career.seek_employment" =>
                ScoreSeekEmployment(option, snapshot),

            "career.help_seek_employment" =>
                ScoreHelpSeekEmployment(option, snapshot, targetMember),

            "career.find_another_job" =>
                ScoreFindAnotherJob(option, snapshot),

            "family_relations.ask_money" =>
                ScoreAskMoney(option, snapshot),

            "family_relations.ask_house" =>
                ScoreAskHouse(option, snapshot),

            "family_relations.ask_farmland" =>
                ScoreAskFarmland(option, snapshot),

            "family_relations.ask_job_help" =>
                ScoreAskJobHelp(option, snapshot),

            "relationship.repair_marriage" =>
                ScoreRepairMarriage(option, snapshot),

            "reproduction.try_for_baby" =>
                ScoreTryForBaby(option, snapshot),

            "relationship.find_spouse" =>
                ScoreFindSpouse(option, snapshot),

            "childhood.raise_child" =>
                ScoreRaiseChild(option, snapshot, targetMember),

            "education.help_learning" =>
                ScoreHelpLearning(option, snapshot, targetMember),

            "education.get_education" =>
                ScoreEducation(option, snapshot),

            "career.work_harder" =>
                ScoreWorkHarder(option, snapshot),

            "career.quit_job" =>
                ScoreQuitJob(option, snapshot, snapshot.Members.FirstOrDefault(
                    member => member.Person.Id == snapshot.Head.Id)),

            "career.ask_to_quit" =>
                ScoreQuitJob(option, snapshot, targetMember),

            "household.buy_house" =>
                ScoreBuyHouse(option, snapshot),

            "farming.buy_farmland" =>
                ScoreBuyFarmland(option, snapshot),

            "family_relations.improve" =>
                ScoreImproveRelations(option, snapshot),

            "family_relations.give_money" or
            "family_relations.give_house" or
            "family_relations.give_farmland" or
            "family_relations.give_job_help" =>
                ScoreFamilyGenerosity(option, snapshot),

            "loan.give" =>
                ScoreGiveLoan(option, snapshot),

            "relationship.marry_off_daughter" =>
                ScoreMarryOffDaughter(option, snapshot),

            "personality.religious_study" =>
                ScoreReligiousStudy(option, snapshot),

            "wellbeing.drink" =>
                ScoreDrink(option, snapshot),

            "turn.pass" =>
                ScorePass(option, snapshot),

            _ when id.StartsWith("stats.", StringComparison.OrdinalIgnoreCase) =>
                ScoreSelfImprovement(option, snapshot),

            _ => null
        };
    }

    private AutonomousActionCandidate? ScoreMedicalTreatment(
        AutonomousActionCandidate option,
        AutonomousHouseholdSnapshot snapshot,
        AutonomousMemberSnapshot? target)
    {
        if (target is null)
            return null;

        var importance = HealthTargetImportance(snapshot, target);

        if (target.IsImmediateHealthRisk)
        {
            return WithScore(
                option,
                AutonomyCategory.Survival,
                AutonomousPriorityBands.EmergencySurvival,
                100 + importance);
        }

        if (target.IsSeriousHealthRisk)
        {
            var workingAdults = snapshot.Members.Count(member =>
                member.Career?.JobLevel > 0);

            var survivalCriticalAdult =
                target.Person.Id == snapshot.Head.Id
                || target.Career?.JobLevel > 0 && workingAdults <= 1
                || snapshot.Spouse?.Id == target.Person.Id
                    && snapshot.LivingChildCount < 2
                    && snapshot.HasRealisticReproductivePath;

            var emergency =
                target.IsChild
                || survivalCriticalAdult;

            return WithScore(
                option,
                target.IsChild
                    ? AutonomyCategory.ChildProtection
                    : AutonomyCategory.Survival,
                emergency
                    ? AutonomousPriorityBands.EmergencySurvival
                    : AutonomousPriorityBands.FamilyStability,
                82 + importance);
        }

        if (target.Health.Percentage < 75)
        {
            return WithScore(
                option,
                target.IsChild
                    ? AutonomyCategory.ChildProtection
                    : AutonomyCategory.PersonalDevelopment,
                target.IsChild
                    ? AutonomousPriorityBands.FamilyStability
                    : AutonomousPriorityBands.LongTermImprovement,
                55 + importance * 0.5);
        }

        return null;
    }

    private AutonomousActionCandidate? ScoreTherapy(
        AutonomousActionCandidate option,
        AutonomousHouseholdSnapshot snapshot,
        AutonomousMemberSnapshot? target)
    {
        if (target is null)
            return null;

        var dangerous = target.Health.Conditions.Any(condition =>
            condition.Id.Equals("alcoholism", StringComparison.OrdinalIgnoreCase)
            && target.Health.Percentage <= 60);

        return WithScore(
            option,
            AutonomyCategory.Survival,
            dangerous
                ? AutonomousPriorityBands.EmergencySurvival
                : AutonomousPriorityBands.FamilyStability,
            dangerous ? 72 : 58);
    }

    private AutonomousActionCandidate? ScoreRecover(
        AutonomousActionCandidate option,
        AutonomousHouseholdSnapshot snapshot)
    {
        var head = snapshot.Members.First(member => member.Person.Id == snapshot.Head.Id);

        if (head.IsImmediateHealthRisk)
        {
            return WithScore(option, AutonomyCategory.Survival,
                AutonomousPriorityBands.EmergencySurvival, 78);
        }

        if (head.IsSeriousHealthRisk)
        {
            return WithScore(option, AutonomyCategory.Survival,
                AutonomousPriorityBands.EmergencySurvival,
                72 + (head.Career?.JobSatisfaction == 1 ? 10 : 0));
        }

        if (head.Health.Percentage < 65)
        {
            return WithScore(option, AutonomyCategory.Survival,
                AutonomousPriorityBands.FamilyStability,
                66 + (head.Career?.JobSatisfaction == 1 ? 10 : 0));
        }

        if (head.Career?.JobSatisfaction == 1
            && IsAtLeast(snapshot.FinancialState, AutonomousFinancialState.Stable))
        {
            return WithScore(option, AutonomyCategory.PersonalDevelopment,
                AutonomousPriorityBands.LongTermImprovement, 52);
        }

        return null;
    }

    private AutonomousActionCandidate? ScoreRequestedRecover(
        AutonomousActionCandidate option,
        AutonomousHouseholdSnapshot snapshot,
        AutonomousMemberSnapshot? target)
    {
        if (target is null)
            return null;

        if (target.IsImmediateHealthRisk)
        {
            return WithScore(option, AutonomyCategory.Survival,
                AutonomousPriorityBands.EmergencySurvival, 65 + HealthTargetImportance(snapshot, target));
        }

        if (target.IsSeriousHealthRisk || target.Health.Percentage < 65)
        {
            return WithScore(option, AutonomyCategory.Survival,
                AutonomousPriorityBands.FamilyStability, 62);
        }

        return null;
    }

    private AutonomousActionCandidate? ScoreNanny(
        AutonomousActionCandidate option,
        AutonomousHouseholdSnapshot snapshot)
    {
        if (snapshot.Status?.IsLargeFamilyStrained != true)
            return null;

        var band = snapshot.HasSeriousMedicalDanger
            ? AutonomousPriorityBands.EmergencySurvival
            : AutonomousPriorityBands.FamilyStability;

        return WithScore(option, AutonomyCategory.ChildProtection, band,
            snapshot.HasSeriousMedicalDanger ? 82 : 90);
    }

    private AutonomousActionCandidate? ScoreFireNanny(
        AutonomousActionCandidate option,
        AutonomousHouseholdSnapshot snapshot)
    {
        if (snapshot.Status is not
            {
                HasNannyReference: true,
                IsLargeFamilyStrained: false
            })
        {
            return null;
        }

        if (snapshot.FinancialState is AutonomousFinancialState.Critical
            or AutonomousFinancialState.Poor)
        {
            return WithScore(option, AutonomyCategory.Solvency,
                AutonomousPriorityBands.HouseholdSolvency, 68);
        }

        return null;
    }

    private AutonomousActionCandidate? ScoreSellHouse(
        AutonomousActionCandidate option,
        AutonomousHouseholdSnapshot snapshot)
    {
        if (!snapshot.HasInvestmentHouse)
            return null;

        if ((snapshot.HasImmediateMedicalDanger || snapshot.HasSeriousMedicalDanger)
            && (snapshot.Finance?.Wealth ?? 0) < 1000m)
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
            && wealth < 1000m)
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
            && (snapshot.Finance?.Wealth ?? 0) < 1000m)
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

    private AutonomousActionCandidate? ScoreSeekEmployment(
        AutonomousActionCandidate option,
        AutonomousHouseholdSnapshot snapshot)
    {
        return snapshot.FinancialState switch
        {
            AutonomousFinancialState.Critical => WithScore(option,
                AutonomyCategory.Solvency,
                AutonomousPriorityBands.HouseholdSolvency, 100),
            AutonomousFinancialState.Poor => WithScore(option,
                AutonomyCategory.Solvency,
                AutonomousPriorityBands.HouseholdSolvency, 93),
            _ => WithScore(option,
                AutonomyCategory.CareerDevelopment,
                AutonomousPriorityBands.LongTermImprovement, 62)
        };
    }

    private AutonomousActionCandidate? ScoreHelpSeekEmployment(
        AutonomousActionCandidate option,
        AutonomousHouseholdSnapshot snapshot,
        AutonomousMemberSnapshot? target)
    {
        if (target?.Career?.JobLevel != 0)
            return null;

        return snapshot.FinancialState switch
        {
            AutonomousFinancialState.Critical => WithScore(option,
                AutonomyCategory.Solvency,
                AutonomousPriorityBands.HouseholdSolvency, 95),
            AutonomousFinancialState.Poor => WithScore(option,
                AutonomyCategory.Solvency,
                AutonomousPriorityBands.HouseholdSolvency, 90),
            _ => WithScore(option,
                AutonomyCategory.CareerDevelopment,
                AutonomousPriorityBands.LongTermImprovement, 55)
        };
    }

    private AutonomousActionCandidate? ScoreFindAnotherJob(
        AutonomousActionCandidate option,
        AutonomousHouseholdSnapshot snapshot)
    {
        var level = _career.GetCareer(snapshot.Head).JobLevel;
        if (level <= 0)
            return null;

        if ((snapshot.FinancialState is AutonomousFinancialState.Critical
                or AutonomousFinancialState.Poor)
            && level <= 2)
        {
            return WithScore(option, AutonomyCategory.Solvency,
                AutonomousPriorityBands.HouseholdSolvency,
                snapshot.FinancialState == AutonomousFinancialState.Critical ? 78 : 72);
        }

        if (IsAtLeast(snapshot.FinancialState, AutonomousFinancialState.Stable))
        {
            return WithScore(option, AutonomyCategory.CareerDevelopment,
                AutonomousPriorityBands.LongTermImprovement, 58);
        }

        return null;
    }

    private AutonomousActionCandidate? ScoreAskMoney(
        AutonomousActionCandidate option,
        AutonomousHouseholdSnapshot snapshot)
    {
        if (!RequestIsReasonable(option))
            return null;

        var willingnessBonus = (option.RequestWillingness ?? 0) * 20;

        if ((snapshot.HasImmediateMedicalDanger || snapshot.HasSeriousMedicalDanger)
            && (snapshot.Finance?.Wealth ?? 0) < 1000m)
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

        var willingnessBonus = (option.RequestWillingness ?? 0) * 20;
        return snapshot.FinancialState switch
        {
            AutonomousFinancialState.Critical => WithScore(option,
                AutonomyCategory.FamilyRelations,
                AutonomousPriorityBands.HouseholdSolvency,
                96 + willingnessBonus),
            AutonomousFinancialState.Poor => WithScore(option,
                AutonomyCategory.FamilyRelations,
                AutonomousPriorityBands.HouseholdSolvency,
                88 + willingnessBonus),
            _ => WithScore(option,
                AutonomyCategory.CareerDevelopment,
                AutonomousPriorityBands.LongTermImprovement,
                58 + willingnessBonus * 0.5)
        };
    }

    private AutonomousActionCandidate? ScoreRepairMarriage(
        AutonomousActionCandidate option,
        AutonomousHouseholdSnapshot snapshot)
    {
        var satisfaction = snapshot.MarriageSatisfaction;
        if (satisfaction is null)
            return null;

        if (snapshot.HasRealisticReproductivePath
            && snapshot.LivingChildCount < 2
            && satisfaction < 45)
        {
            return WithScore(option, AutonomyCategory.RelationshipStability,
                AutonomousPriorityBands.FamilyContinuity,
                satisfaction < 25 ? 110 : 100);
        }

        if (satisfaction < 35)
        {
            return WithScore(option, AutonomyCategory.RelationshipStability,
                AutonomousPriorityBands.FamilyStability, 90);
        }

        if (satisfaction < 55)
        {
            return WithScore(option, AutonomyCategory.RelationshipStability,
                AutonomousPriorityBands.FamilyStability, 62);
        }

        return null;
    }

    private AutonomousActionCandidate? ScoreTryForBaby(
        AutonomousActionCandidate option,
        AutonomousHouseholdSnapshot snapshot)
    {
        if (!snapshot.CanActivelyTryForChild)
            return null;

        // A reproductively useful marriage that is nearing the automatic
        // divorce threshold must be repaired before another deliberate
        // conception attempt.
        if (snapshot.MarriageSatisfaction is < 45)
            return null;

        var score = snapshot.LivingChildCount == 0 ? 96.0 : 68.0;
        score += snapshot.ReproductiveUrgency * 18;

        return WithScore(option, AutonomyCategory.Continuity,
            AutonomousPriorityBands.FamilyContinuity, score);
    }

    private AutonomousActionCandidate? ScoreFindSpouse(
        AutonomousActionCandidate option,
        AutonomousHouseholdSnapshot snapshot)
    {
        if (_family.GetSex(snapshot.Head) != Sex.Male)
            return null;

        var stats = GetStats(snapshot.Head);
        var appeal = GetStat(stats, "appeal");
        var strength = GetStat(stats, "strength");
        var intellect = GetStat(stats, "intellect");
        var weakCareerProspects = Math.Max(strength, intellect) <= 2;

        if ((snapshot.FinancialState is AutonomousFinancialState.Critical
                or AutonomousFinancialState.Poor)
            && snapshot.Head.Age < 60
            && appeal >= 4
            && weakCareerProspects)
        {
            return WithScore(option, AutonomyCategory.Solvency,
                AutonomousPriorityBands.HouseholdSolvency,
                snapshot.FinancialState == AutonomousFinancialState.Critical ? 92 : 84);
        }

        if (snapshot.LivingChildCount < 2
            && CanSearchForReproductiveSpouse(snapshot.Head)
            && IsAtLeast(snapshot.FinancialState, AutonomousFinancialState.Stable))
        {
            return WithScore(option, AutonomyCategory.Continuity,
                AutonomousPriorityBands.FamilyContinuity,
                snapshot.LivingChildCount == 0 ? 92 : 62);
        }

        if (snapshot.LivingChildCount > 0
            && snapshot.Head.Age >= 60
            && IsAtLeast(snapshot.FinancialState, AutonomousFinancialState.Stable))
        {
            return WithScore(option, AutonomyCategory.Optional,
                AutonomousPriorityBands.OptionalDevelopment, 8);
        }

        return WithScore(option, AutonomyCategory.Optional,
            AutonomousPriorityBands.OptionalDevelopment,
            snapshot.Head.Age < 60 ? 28 : 10);
    }

    private AutonomousActionCandidate? ScoreRaiseChild(
        AutonomousActionCandidate option,
        AutonomousHouseholdSnapshot snapshot,
        AutonomousMemberSnapshot? target)
    {
        if (target is null || !target.IsChild
            || snapshot.FinancialState is AutonomousFinancialState.Critical
                or AutonomousFinancialState.Poor)
        {
            return null;
        }

        var happiness =
            _context.GetService<IChildHappinessService>()?
                .GetHappiness(target.Person)?
                .Value
            ?? 3;

        if (happiness <= 2)
        {
            return WithScore(option, AutonomyCategory.ChildProtection,
                AutonomousPriorityBands.FamilyStability,
                happiness == 1 ? 82 : 68);
        }

        return WithScore(option, AutonomyCategory.ChildProtection,
            AutonomousPriorityBands.LongTermImprovement,
            snapshot.HasRealisticReproductivePath ? 34 : 44);
    }

    private AutonomousActionCandidate? ScoreHelpLearning(
        AutonomousActionCandidate option,
        AutonomousHouseholdSnapshot snapshot,
        AutonomousMemberSnapshot? target)
    {
        if (target is null || !target.IsChild
            || snapshot.FinancialState is AutonomousFinancialState.Critical
                or AutonomousFinancialState.Poor
            || snapshot.HasSeriousMedicalDanger)
        {
            return null;
        }

        return WithScore(option, AutonomyCategory.ChildProtection,
            AutonomousPriorityBands.LongTermImprovement,
            snapshot.HasRealisticReproductivePath ? 52 : 60);
    }

    private AutonomousActionCandidate? ScoreEducation(
        AutonomousActionCandidate option,
        AutonomousHouseholdSnapshot snapshot)
    {
        if (!IsAtLeast(snapshot.FinancialState, AutonomousFinancialState.Stable)
            || snapshot.HasSeriousMedicalDanger)
        {
            return null;
        }

        return WithScore(option, AutonomyCategory.PersonalDevelopment,
            AutonomousPriorityBands.LongTermImprovement, 62);
    }

    private AutonomousActionCandidate? ScoreWorkHarder(
        AutonomousActionCandidate option,
        AutonomousHouseholdSnapshot snapshot)
    {
        var head = snapshot.Members.First(member => member.Person.Id == snapshot.Head.Id);
        if (head.Health.Percentage < 75
            || snapshot.Status?.IsLargeFamilyStrained == true
            || snapshot.HasSeriousMedicalDanger)
        {
            return null;
        }

        if (!IsAtLeast(snapshot.FinancialState, AutonomousFinancialState.Stable))
            return null;

        return WithScore(option, AutonomyCategory.CareerDevelopment,
            AutonomousPriorityBands.LongTermImprovement,
            head.Career?.JobSatisfaction >= 3 ? 62 : 48);
    }

    private AutonomousActionCandidate? ScoreQuitJob(
        AutonomousActionCandidate option,
        AutonomousHouseholdSnapshot snapshot,
        AutonomousMemberSnapshot? target)
    {
        if (target?.Career is null
            || target.Career.JobSatisfaction != 1
            || !IsAtLeast(snapshot.FinancialState, AutonomousFinancialState.Secure))
        {
            return null;
        }

        var incomeWithoutTarget = snapshot.ProjectedIncome - target.Career.AnnualIncome;
        if (incomeWithoutTarget < snapshot.ExpectedExpenses)
            return null;

        return WithScore(option, AutonomyCategory.PersonalDevelopment,
            AutonomousPriorityBands.LongTermImprovement, 48);
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
            || snapshot.LivingChildCount < 2 && snapshot.HasRealisticReproductivePath)
        {
            return null;
        }

        var farming = _context.GetService<IFarmingService>();
        if (farming is null || snapshot.Finance is null)
            return null;

        var farm = farming.GetSnapshot(snapshot.Head);
        if (farm.AvailableWorkers == 0)
            return null;

        var reserve = snapshot.ExpectedExpenses * 2m;
        if (snapshot.Finance.Wealth - farming.PurchasePrice < reserve)
            return null;

        var earlyEraBonus = Math.Clamp((2000 - _gameState.Year) / 25.0, 0, 12);
        var workerBonus = Math.Min(farm.AvailableWorkers, 2) * 5;

        return WithScore(option, AutonomyCategory.Property,
            AutonomousPriorityBands.LongTermImprovement,
            45 + earlyEraBonus + workerBonus);
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

    private AutonomousActionCandidate? ScoreMarryOffDaughter(
        AutonomousActionCandidate option,
        AutonomousHouseholdSnapshot snapshot)
    {
        if (!IsAtLeast(snapshot.FinancialState, AutonomousFinancialState.Stable)
            || snapshot.HasSeriousMedicalDanger)
        {
            return null;
        }

        return WithScore(option, AutonomyCategory.FamilyRelations,
            AutonomousPriorityBands.LongTermImprovement, 42);
    }

    private AutonomousActionCandidate? ScoreReligiousStudy(
        AutonomousActionCandidate option,
        AutonomousHouseholdSnapshot snapshot)
    {
        if (!IsAtLeast(snapshot.FinancialState, AutonomousFinancialState.Stable)
            || snapshot.HasSeriousMedicalDanger)
        {
            return null;
        }

        return WithScore(option, AutonomyCategory.Optional,
            AutonomousPriorityBands.OptionalDevelopment,
            snapshot.Head.Tags.Has("morals.good") ? 34 : 20);
    }

    private AutonomousActionCandidate? ScoreDrink(
        AutonomousActionCandidate option,
        AutonomousHouseholdSnapshot snapshot)
    {
        var head = snapshot.Members.First(member => member.Person.Id == snapshot.Head.Id);
        if (head.Health.Percentage < 85 || snapshot.HasSeriousMedicalDanger)
            return null;

        return WithScore(option, AutonomyCategory.Optional,
            AutonomousPriorityBands.OptionalDevelopment, 6);
    }

    private AutonomousActionCandidate? ScoreSelfImprovement(
        AutonomousActionCandidate option,
        AutonomousHouseholdSnapshot snapshot)
    {
        if (snapshot.FinancialState != AutonomousFinancialState.Secure
            || snapshot.HasSeriousMedicalDanger
            || snapshot.LivingChildCount < 2 && snapshot.HasRealisticReproductivePath)
        {
            return null;
        }

        return WithScore(option, AutonomyCategory.PersonalDevelopment,
            AutonomousPriorityBands.LongTermImprovement, 38);
    }

    private AutonomousActionCandidate ScorePass(
        AutonomousActionCandidate option,
        AutonomousHouseholdSnapshot snapshot)
    {
        var quietYear = !snapshot.HasSeriousMedicalDanger
            && IsAtLeast(snapshot.FinancialState, AutonomousFinancialState.Stable)
            && (!snapshot.HasRealisticReproductivePath || snapshot.LivingChildCount >= 2)
            && snapshot.Status?.IsLargeFamilyStrained != true;

        return WithScore(option, AutonomyCategory.Optional,
            AutonomousPriorityBands.OptionalDevelopment,
            quietYear ? 42 : 8);
    }

    private IReadOnlyDictionary<string, string>? BuildParametersForAction(
        string actionId,
        AutonomousHouseholdSnapshot snapshot,
        IPerson target,
        IReadOnlyDictionary<string, string>? baseParameters)
    {
        var parameters = baseParameters is null
            ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(baseParameters, StringComparer.OrdinalIgnoreCase);

        if (actionId.Equals("career.seek_employment", StringComparison.OrdinalIgnoreCase)
            || actionId.Equals("career.find_another_job", StringComparison.OrdinalIgnoreCase)
            || actionId.Equals("career.help_seek_employment", StringComparison.OrdinalIgnoreCase))
        {
            var applicant = actionId.Equals(
                    "career.help_seek_employment",
                    StringComparison.OrdinalIgnoreCase)
                ? target
                : snapshot.Head;

            var current = _career.GetCareer(applicant);
            var opportunities = _career.GetJobOpportunities(applicant);
            if (opportunities.Count == 0)
                return null;

            var salaryWeight = snapshot.FinancialState is
                    AutonomousFinancialState.Critical or
                    AutonomousFinancialState.Poor
                ? 1.6
                : 1.0;

            var best = opportunities
                .OrderByDescending(opportunity =>
                    (double)Math.Max(0m,
                        opportunity.AnnualSalary - current.AnnualIncome)
                        * salaryWeight
                        * opportunity.SuccessChance
                    + opportunity.SuccessChance * 1000.0)
                .ThenByDescending(opportunity => opportunity.AnnualSalary)
                .First();

            parameters["jobCareerId"] = best.CareerId;
            parameters["jobLevel"] = best.JobLevel.ToString(
                CultureInfo.InvariantCulture);
            parameters["jobRequiredAbility"] = best.RequiredAbilityLevel.ToString(
                CultureInfo.InvariantCulture);
            parameters["jobRequiredEducation"] = best.RequiredEducationLevel.ToString(
                CultureInfo.InvariantCulture);
            parameters["jobRequiredExperience"] = best.RequiredExperienceYears.ToString(
                CultureInfo.InvariantCulture);
            parameters["jobSuccessChance"] = best.SuccessChance.ToString(
                "R",
                CultureInfo.InvariantCulture);
        }
        else if (actionId.Equals("relationship.find_spouse", StringComparison.OrdinalIgnoreCase))
        {
            var partnerSearch = _context.GetService<IPartnerSearchService>();
            if (partnerSearch is null)
                return null;

            var candidates = partnerSearch.GetCandidates(snapshot.Head);
            if (candidates.Count == 0)
                return null;

            var needsChildren = snapshot.LivingChildCount < 2
                && snapshot.HasRealisticReproductivePath;
            var needsIncome = snapshot.FinancialState is
                AutonomousFinancialState.Critical or
                AutonomousFinancialState.Poor;

            var best = candidates
                .OrderByDescending(candidate =>
                candidate.AcceptanceChance * 60.0
                + candidate.PartnerValue * 0.35
                + (needsChildren && candidate.Sex == Sex.Female
                    ? Math.Max(0, 46 - candidate.Age) * 1.5
                    : 0)
                + (needsIncome
                    ? (double)candidate.AnnualIncome / 100.0
                    : 0))
                .First();

            foreach (var pair in partnerSearch.BuildActionParameters(best))
                parameters[pair.Key] = pair.Value;
        }
        else if (actionId.Equals("relationship.marry_off_daughter", StringComparison.OrdinalIgnoreCase))
        {
            var partnerSearch = _context.GetService<IPartnerSearchService>();
            if (partnerSearch is null)
                return null;

            var candidates = partnerSearch.GetCandidatesFor(
                target,
                Sex.Male,
                "arranged-marriage");
            if (candidates.Count == 0)
                return null;

            var best = candidates
                .OrderByDescending(candidate =>
                    candidate.AcceptanceChance * 70.0
                    + candidate.PartnerValue * 0.25
                    + (double)candidate.AnnualIncome / 150.0)
                .First();

            foreach (var pair in partnerSearch.BuildActionParameters(best))
                parameters[pair.Key] = pair.Value;
        }
        else if (actionId.Equals("household.sell_house", StringComparison.OrdinalIgnoreCase))
        {
            var investment = snapshot.Finance?.Houses
                .FirstOrDefault(house => house.IsRented);
            if (investment is null)
                return null;
            parameters["propertyId"] = investment.Id.ToString();
        }
        else if (actionId.Equals("loan.take", StringComparison.OrdinalIgnoreCase))
        {
            var loanParameters = BuildLoanParameters(snapshot, lending: false);
            if (loanParameters is null)
                return null;
            foreach (var pair in loanParameters)
                parameters[pair.Key] = pair.Value;
        }
        else if (actionId.Equals("loan.give", StringComparison.OrdinalIgnoreCase))
        {
            var loanParameters = BuildLoanParameters(snapshot, lending: true);
            if (loanParameters is null)
                return null;
            foreach (var pair in loanParameters)
                parameters[pair.Key] = pair.Value;
        }
        else if (actionId.Equals("family_relations.ask_money", StringComparison.OrdinalIgnoreCase))
        {
            var targetHead = _households.ResolveHouseholdHead(target);
            var targetWealth = targetHead is null
                ? 0m
                : _economy.GetHousehold(targetHead)?.Wealth ?? 0m;
            var amount = ChooseFamilyMoneyAmount(snapshot, targetWealth, receiving: true);
            if (amount < 1000m)
                return null;
            parameters["amount"] = amount.ToString(CultureInfo.InvariantCulture);
        }
        else if (actionId.Equals("family_relations.give_money", StringComparison.OrdinalIgnoreCase))
        {
            var amount = ChooseFamilyMoneyAmount(snapshot, snapshot.Finance?.Wealth ?? 0m, receiving: false);
            if (amount < 1000m)
                return null;
            parameters["amount"] = amount.ToString(CultureInfo.InvariantCulture);
        }
        else if (actionId.Equals("family_relations.give_house", StringComparison.OrdinalIgnoreCase))
        {
            var investment = snapshot.Finance?.Houses
                .FirstOrDefault(house => house.IsRented);
            if (investment is null)
                return null;
            parameters["propertyId"] = investment.Id.ToString();
        }
        else if (actionId.Equals("family_relations.give_farmland", StringComparison.OrdinalIgnoreCase))
        {
            var residence = _economy.GetResidenceTown(snapshot.Head);
            var parcel = _economy.GetFarmland(snapshot.Head)
                .OrderBy(asset => asset.Town.Id.Equals(residence.Id, StringComparison.OrdinalIgnoreCase) ? 1 : 0)
                .ThenBy(asset => asset.AcquiredYear)
                .ThenBy(asset => asset.Id)
                .FirstOrDefault();
            if (parcel is null)
                return null;
            parameters["farmlandId"] = parcel.Id.ToString();
        }

        return parameters;
    }

    private IReadOnlyDictionary<string, string>? BuildLoanParameters(
        AutonomousHouseholdSnapshot snapshot,
        bool lending)
    {
        var loans = _context.GetService<ILoanService>();
        if (loans is null)
            return null;

        if (lending)
        {
            if (snapshot.FinancialState != AutonomousFinancialState.Secure
                || snapshot.HasSeriousMedicalDanger
                || snapshot.Status?.IsLargeFamilyStrained == true
                || snapshot.LivingChildCount < 2 && snapshot.HasRealisticReproductivePath
                || (snapshot.Finance?.Wealth ?? 0m) < snapshot.ExpectedExpenses * 3m + 1000m)
            {
                return null;
            }

            return LoanParameters(1000m, 5);
        }

        if (snapshot.ProjectedIncome <= 0)
            return null;

        var wealth = snapshot.Finance?.Wealth ?? 0m;
        var shortfall = Math.Max(0m, snapshot.ExpectedExpenses - snapshot.ProjectedIncome - wealth);
        var need = Math.Max(1000m, shortfall + ((snapshot.HasImmediateMedicalDanger || snapshot.HasSeriousMedicalDanger) ? 2000m : 0m));
        var principal = Math.Clamp(
            Math.Ceiling(need / 1000m) * 1000m,
            1000m,
            10000m);

        foreach (var duration in new[] { 5, 10, 15, 20, 30, 50 })
        {
            var terms = loans.CalculateTerms(principal, duration);
            var affordablePayment = Math.Max(250m, snapshot.ProjectedIncome * 0.25m);
            if (terms.AnnualPayment <= affordablePayment)
                return LoanParameters(principal, duration);
        }

        return null;
    }

    private static IReadOnlyDictionary<string, string> LoanParameters(
        decimal principal,
        int duration) =>
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["principal"] = principal.ToString(CultureInfo.InvariantCulture),
            ["durationYears"] = duration.ToString(CultureInfo.InvariantCulture)
        };

    private decimal ChooseFamilyMoneyAmount(
        AutonomousHouseholdSnapshot snapshot,
        decimal availableWealth,
        bool receiving)
    {
        if (availableWealth < 1000m)
            return 0m;

        if (!receiving)
            return 1000m;

        var desired = snapshot.FinancialState == AutonomousFinancialState.Critical
            ? 3000m
            : 2000m;

        desired = Math.Min(desired, availableWealth);
        return Math.Floor(desired / 1000m) * 1000m;
    }

    private decimal EstimateOrdinaryExpenses(
        IPerson head,
        int memberCount,
        HouseholdFinanceSnapshot? finance,
        decimal debtPayments)
    {
        var town = _economy.GetResidenceTown(head);
        var expenses = memberCount * _economy.GetLivingCostPerPerson(town);

        var ownsLocalResidence = finance?.Houses.Any(house =>
            house.Town.Id.Equals(town.Id, StringComparison.OrdinalIgnoreCase)) == true;

        if (!ownsLocalResidence)
            expenses += _economy.GetResidenceRent(town);

        if (finance?.NannyId is not null)
            expenses += NannyExpenseEstimate;

        expenses += debtPayments;

        if (finance is not null && finance.LastExpenses > expenses)
            expenses = finance.LastExpenses + debtPayments;

        return expenses;
    }

    private AutonomousMemberSnapshot BuildMemberSnapshot(
        IPerson person)
    {
        var health = _health.GetHealth(person);
        var career = person.Age >= 18 ? _career.GetCareer(person) : null;
        var stats = GetStats(person);
        var serious = IsSeriousHealthRisk(health);
        var immediate = IsImmediateHealthRisk(health);

        return new AutonomousMemberSnapshot(
            person,
            health,
            career,
            stats,
            person.Age < 18,
            person.Age < 18,
            serious,
            immediate);
    }

    private IReadOnlyDictionary<string, int> GetStats(IPerson person) =>
        _stats.GetStats(person)
            .ToDictionary(stat => stat.Id, stat => stat.Value,
                StringComparer.OrdinalIgnoreCase);

    private bool HasRealisticReproductivePath(
        IPerson head,
        IPerson? spouse)
    {
        if (spouse is null)
            return CanSearchForReproductiveSpouse(head);

        var first = head;
        var second = spouse;
        var firstSex = _family.GetSex(first);
        var secondSex = _family.GetSex(second);

        if (firstSex == secondSex)
            return false;

        var female = firstSex == Sex.Female ? first : second;
        var male = firstSex == Sex.Male ? first : second;

        if (female.Age < 18 || female.Age > 45
            || female.Tags.Has("state.imprisoned"))
        {
            return false;
        }

        return GetStat(GetStats(female), "fertility") > 0
            && GetStat(GetStats(male), "fertility") > 0;
    }

    private bool CanSearchForReproductiveSpouse(IPerson head)
    {
        if (_family.GetSex(head) != Sex.Male
            || _family.GetSpouse(head) is not null)
        {
            return false;
        }

        return AutonomousStrategyRules.CanSearchForReproductiveFemale(
            head.Age,
            head.Tags.Has("morals.good"),
            head.Tags.Has("morals.evil"),
            head.Tags.Has("sexuality.homosexual"));
    }

    private bool CanActivelyTryForChild(
        IPerson head,
        IPerson? spouse,
        int livingChildren,
        int dependentChildren,
        AutonomousFinancialState financialState,
        HouseholdStatusSnapshot? status,
        bool reproductivePath)
    {
        if (spouse is null
            || _family.GetSex(head) != Sex.Male
            || _family.GetSex(spouse) != Sex.Female)
        {
            return false;
        }

        return AutonomousStrategyRules.CanActivelyTryForChild(
            livingChildren,
            financialState,
            status?.IsLargeFamilyStrained == true,
            dependentChildren,
            status?.EffectiveChildCapacity ?? int.MaxValue,
            reproductivePath);
    }

    private double CalculateReproductiveUrgency(
        IPerson head,
        IPerson? spouse,
        int livingChildren)
    {
        if (livingChildren >= 2)
            return 0;

        IPerson? female = null;
        if (_family.GetSex(head) == Sex.Female)
            female = head;
        else if (spouse is not null && _family.GetSex(spouse) == Sex.Female)
            female = spouse;

        var baseUrgency = livingChildren == 0 ? 1.0 : 0.55;
        if (female is null)
            return baseUrgency;

        if (female.Age >= 40)
            baseUrgency += 0.55;
        else if (female.Age >= 35)
            baseUrgency += 0.30;

        return Math.Min(1.5, baseUrgency);
    }

    private double HealthTargetImportance(
        AutonomousHouseholdSnapshot snapshot,
        AutonomousMemberSnapshot target)
    {
        var score = Math.Max(0, 100 - target.Health.Percentage) * 0.25;

        if (target.Person.Id == snapshot.Head.Id)
            score += 16;

        if (target.Career?.JobLevel > 0)
        {
            var workingAdults = snapshot.Members.Count(member => member.Career?.JobLevel > 0);
            if (workingAdults <= 1)
                score += 18;
        }

        if (target.IsChild)
        {
            score += 12;
            if (!snapshot.HasRealisticReproductivePath)
                score += 24;
            if (target.Person.Age <= 10)
                score += 6;
        }

        if (snapshot.Spouse?.Id == target.Person.Id
            && snapshot.LivingChildCount < 2
            && snapshot.HasRealisticReproductivePath)
        {
            score += 20;
        }

        if (target.Health.Conditions.Any(condition =>
            condition.Type.Equals("terminal", StringComparison.OrdinalIgnoreCase)))
        {
            score += 24;
        }

        return score;
    }

    private static bool IsSeriousHealthRisk(HealthSnapshot health) =>
        health.Percentage <= 55
        || health.Conditions.Any(condition =>
            condition.Type.Equals("terminal", StringComparison.OrdinalIgnoreCase)
            || condition.HealthImpact <= -10);

    private static bool IsImmediateHealthRisk(HealthSnapshot health) =>
        health.Percentage <= 40
        || health.Conditions.Any(condition =>
            condition.Type.Equals("terminal", StringComparison.OrdinalIgnoreCase)
                && health.Percentage <= 60
            || condition.HealthImpact <= -15
                && health.Percentage <= 45);

    private IReadOnlyList<GameActionDefinition> GetMechanicallyAvailableActions(
        IPerson actor,
        IPerson target,
        IReadOnlyDictionary<string, string> parameters)
    {
        var hadPlayable = actor.Tags.Has("control.playable");
        if (!hadPlayable)
            actor.Tags.Add("control.playable");

        try
        {
            return _actions.GetAvailableActions(actor, target, parameters);
        }
        finally
        {
            if (!hadPlayable)
                actor.Tags.Remove("control.playable");
        }
    }

    private AutonomousActionCandidate ApplyPersonality(
        AutonomousActionCandidate option,
        IPerson head)
    {
        var id = option.Action.Id.ToLowerInvariant();
        double melancholic = 0;
        double phlegmatic = 0;
        double sanguine = 0;
        double choleric = 0;
        double good = 0;
        double evil = 0;

        if (id is "wellbeing.recover" or "wellbeing.therapy" or "relationship.repair_marriage")
            melancholic += 0.12;

        if (id is "career.seek_employment" or "career.find_another_job" or
            "career.work_harder" or "relationship.find_spouse")
            sanguine += 0.10;

        if (id is "career.work_harder" or "career.find_another_job" or "loan.take")
            choleric += 0.12;

        if (id is "turn.pass" or "relationship.repair_marriage")
            phlegmatic += 0.10;
        else if (id.StartsWith("career.", StringComparison.OrdinalIgnoreCase)
            || id.StartsWith("household.buy", StringComparison.OrdinalIgnoreCase))
            phlegmatic -= 0.08;

        if (id is "personality.religious_study" or
            "family_relations.give_money" or
            "family_relations.give_house" or
            "family_relations.give_farmland" or
            "family_relations.give_job_help")
        {
            good += 0.12;
            evil -= 0.08;
        }

        var multiplier = PersonalityInfluence.Multiplier(
            head,
            melancholic,
            phlegmatic,
            sanguine,
            choleric,
            good: good,
            evil: evil);

        return option with { Score = option.Score * multiplier };
    }

    private static bool RequestIsReasonable(AutonomousActionCandidate option) =>
        option.RequestWillingness is >= AutonomousStrategyRules.MinimumUsefulRequestWillingness;

    private static AutonomousActionCandidate WithScore(
        AutonomousActionCandidate option,
        AutonomyCategory category,
        int band,
        double score) =>
        option with
        {
            Category = category,
            PriorityBand = band,
            Score = score
        };

    private static AutonomousActionCandidate NewCandidate(
        GameActionDefinition action,
        IPerson target,
        IReadOnlyDictionary<string, string> parameters,
        double? willingness = null) =>
        new(
            action,
            target,
            parameters,
            AutonomyCategory.Optional,
            0,
            0,
            willingness);

    private static IReadOnlyDictionary<string, string> EmptyParameters() =>
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    private static string BuildCandidateKey(AutonomousActionCandidate candidate)
    {
        var parameterKey = string.Join(
            ";",
            candidate.Parameters
                .OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
                .Select(pair => $"{pair.Key}={pair.Value}"));

        return $"{candidate.Action.Id}|{candidate.Target.Id}|{parameterKey}";
    }

    private static bool IsAtLeast(
        AutonomousFinancialState value,
        AutonomousFinancialState minimum) =>
        (int)value >= (int)minimum;

    private static int GetStat(
        IReadOnlyDictionary<string, int> stats,
        string id) =>
        stats.TryGetValue(id, out var value) ? value : 0;

    private IPerson? FindPerson(Guid id) =>
        _gameState.People.FirstOrDefault(person => person.Id == id);
}
