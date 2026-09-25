using System.Globalization;
using Dynastia.Contracts;

namespace Dynastia.Mechanics.Households;

/// <summary>Discovers mechanically available actions and preserves their queue parameters and order.</summary>
internal sealed class AutonomousActionCandidateBuilder
{
    private readonly IGamePluginContext _context;
    private readonly IGameState _gameState;
    private readonly IHouseholdService _households;
    private readonly IActionRegistry _actions;
    private readonly IEconomyService _economy;
    private readonly ICareerService _career;

    public AutonomousActionCandidateBuilder(
        IGamePluginContext context,
        IGameState gameState,
        IHouseholdService households,
        IActionRegistry actions,
        IEconomyService economy,
        ICareerService career)
    {
        _context = context;
        _gameState = gameState;
        _households = households;
        _actions = actions;
        _economy = economy;
        _career = career;
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
                parameters,
                snapshot.Household.HouseholdId))
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
                    baseParameters,
                    snapshot.Household.HouseholdId))
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
            || actionId.Equals("career.help_seek_employment", StringComparison.OrdinalIgnoreCase)
            || actionId.Equals("career.help_find_better_job", StringComparison.OrdinalIgnoreCase))
        {
            var applicant = actionId.Equals(
                    "career.help_seek_employment",
                    StringComparison.OrdinalIgnoreCase)
                || actionId.Equals(
                    "career.help_find_better_job",
                    StringComparison.OrdinalIgnoreCase)
                ? target
                : snapshot.Head;

            var opportunities = _career.GetJobOpportunities(applicant);
            if (opportunities.Count == 0)
                return null;

            var urgent = snapshot.FinancialState is
                AutonomousFinancialState.Critical or
                AutonomousFinancialState.Poor;

            var best = opportunities
                .OrderByDescending(opportunity =>
                    AutonomousWorkChoiceRules.GetCareerOpportunityValue(
                        opportunity.AnnualSalary,
                        opportunity.SuccessChance,
                        urgent))
                .ThenByDescending(opportunity => opportunity.AnnualSalary)
                .ThenByDescending(opportunity => opportunity.SuccessChance)
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
            parameters["jobAnnualSalary"] = best.AnnualSalary.ToString(
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
        else if (actionId.Equals("household.buy_house", StringComparison.OrdinalIgnoreCase))
        {
            var market = _context.GetService<IHouseMarketService>();
            if (market is null)
                return null;

            var town = _economy.GetResidenceTown(snapshot.Head);
            var offer = market.GetOffers(snapshot.Head, town, _gameState.Year)
                .Where(candidate => _economy.CanAfford(snapshot.Head, candidate.AskingPrice))
                .OrderBy(candidate => candidate.AskingPrice)
                .ThenByDescending(candidate => candidate.BaseResidentCapacity)
                .FirstOrDefault();

            if (offer is null)
                return null;

            parameters["townId"] = offer.Town.Id;
            parameters["houseOfferId"] = offer.OfferId;
            parameters["houseOfferYear"] = offer.OfferYear.ToString(CultureInfo.InvariantCulture);
            parameters["houseCapacity"] = offer.BaseResidentCapacity.ToString(CultureInfo.InvariantCulture);
            parameters["houseAskingPrice"] = offer.AskingPrice.ToString(CultureInfo.InvariantCulture);
            parameters["summaryTown"] = offer.Town.Town;
            parameters["summaryPrice"] = offer.AskingPrice.ToString(CultureInfo.InvariantCulture);
            parameters["summaryCapacity"] = offer.BaseResidentCapacity.ToString(CultureInfo.InvariantCulture);
        }
        else if (actionId.Equals("farming.buy_farmland", StringComparison.OrdinalIgnoreCase))
        {
            var farming = _context.GetService<IFarmingService>();
            if (farming is null)
                return null;

            var town = _economy.GetResidenceTown(snapshot.Head);
            var askingPrice = farming.GetPurchasePrice(town, _gameState.Year);
            if (!_economy.CanAfford(snapshot.Head, askingPrice))
                return null;

            parameters["townId"] = town.Id;
            parameters["farmlandOfferYear"] = _gameState.Year.ToString(CultureInfo.InvariantCulture);
            parameters["farmlandAskingPrice"] = askingPrice.ToString(CultureInfo.InvariantCulture);
            parameters["summaryTown"] = town.Town;
            parameters["summaryPrice"] = askingPrice.ToString(CultureInfo.InvariantCulture);
        }
        else if (actionId.Equals("household.ask_move_out", StringComparison.OrdinalIgnoreCase))
        {
            var residence = _economy.GetResidenceTown(snapshot.Head);
            var spareHouse = snapshot.Finance?.Houses
                .Where(house => !house.IsResidence)
                .OrderByDescending(house => house.Town.Id.Equals(
                    residence.Id,
                    StringComparison.OrdinalIgnoreCase))
                .ThenBy(house => house.Town.Town, StringComparer.CurrentCultureIgnoreCase)
                .FirstOrDefault();

            if (spareHouse is not null)
                parameters["propertyId"] = spareHouse.Id.ToString();
            else if (snapshot.Status?.IsOvercrowded != true)
                return null;
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
        var need = Math.Max(1000m, shortfall + ((snapshot.HasImmediateMedicalDanger || snapshot.HasSeriousMedicalDanger) ? 3000m : 0m));
        var requiredPrincipal = Math.Clamp(
            Math.Ceiling(need / 1000m) * 1000m,
            1000m,
            10000m);
        var affordablePayment = Math.Max(250m, snapshot.ProjectedIncome * 0.25m);

        var offer = loans.GetOffers(snapshot.Head, isGivingLoan: false, 10000m)
            .Where(candidate => candidate.Terms.Principal >= requiredPrincipal)
            .Where(candidate => candidate.Terms.AnnualPayment <= affordablePayment)
            .OrderBy(candidate => candidate.Terms.TotalInterestRate)
            .ThenByDescending(candidate => candidate.Terms.Principal)
            .FirstOrDefault();

        return offer is null
            ? null
            : LoanParameters(offer);
    }

    private static IReadOnlyDictionary<string, string> LoanParameters(
        decimal principal,
        int duration) =>
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["principal"] = principal.ToString(CultureInfo.InvariantCulture),
            ["durationYears"] = duration.ToString(CultureInfo.InvariantCulture)
        };

    private static IReadOnlyDictionary<string, string> LoanParameters(
        LoanOfferInfo offer) =>
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["principal"] = offer.Terms.Principal.ToString(CultureInfo.InvariantCulture),
            ["durationYears"] = offer.Terms.DurationYears.ToString(CultureInfo.InvariantCulture),
            ["interestMultiplier"] = offer.Terms.InterestMultiplier.ToString(CultureInfo.InvariantCulture),
            ["counterpartyName"] = offer.CounterpartyName,
            ["counterpartyTownId"] = offer.OriginTownId,
            ["counterpartyNationalityId"] = offer.NationalityId
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

    private IReadOnlyList<GameActionDefinition> GetMechanicallyAvailableActions(
        IPerson actor,
        IPerson target,
        IReadOnlyDictionary<string, string> parameters,
        Guid actorHouseholdId) =>
        _actions.GetAvailableActions(
            actor,
            target,
            parameters,
            ActionExecutionContext.Autonomous(actorHouseholdId));

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

    private IPerson? FindPerson(Guid id) =>
        _gameState.People.FirstOrDefault(person => person.Id == id);
}
