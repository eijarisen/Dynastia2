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
                        "family_relations.ask_farmland" or
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

            var candidates = partnerSearch.GetCandidates(snapshot.Head)
                .Where(candidate => IsSafeIncomingUnion(
                    snapshot, snapshot.Head, candidate, createsIndependentHousehold: false))
                .ToList();
            if (candidates.Count == 0)
                return null;

            var needsChildren = snapshot.NeedsFamilyExpansion
                && snapshot.HasRealisticReproductivePath;
            var needsIncome = snapshot.FinancialState is
                AutonomousFinancialState.Critical or AutonomousFinancialState.Poor;
            var best = AutonomousPartnerChoiceRules.Choose(
                candidates, Sex.Female, needsChildren, needsIncome);
            if (best is null)
                return null;

            foreach (var pair in partnerSearch.BuildActionParameters(best))
                parameters[pair.Key] = pair.Value;
        }
        else if (actionId.Equals("relationship.marry_off_daughter", StringComparison.OrdinalIgnoreCase)
            || actionId.Equals("relationship.marry_off_son", StringComparison.OrdinalIgnoreCase))
        {
            var partnerSearch = _context.GetService<IPartnerSearchService>();
            if (partnerSearch is null)
                return null;

            var partnerSex = actionId.Equals("relationship.marry_off_son", StringComparison.OrdinalIgnoreCase)
                ? Sex.Female : Sex.Male;
            var createsIndependentHousehold = partnerSex == Sex.Male;
            var candidates = partnerSearch.GetCandidatesFor(
                    target,
                    partnerSex,
                    "arranged-marriage")
                .Where(candidate => IsSafeIncomingUnion(
                    snapshot, target, candidate, createsIndependentHousehold))
                .ToList();
            if (candidates.Count == 0)
                return null;

            var targetMember = snapshot.Members.FirstOrDefault(member =>
                member.Person.Id == target.Id);
            var targetFertile = targetMember is not null
                && GetStat(targetMember.Stats, "fertility") > 0
                && (_context.GetService<IFamilyService>()?.GetSex(target) != Sex.Female
                    || target.Age <= 43);
            var best = AutonomousPartnerChoiceRules.Choose(
                candidates, partnerSex, needsContinuity: targetFertile,
                needsIncome: snapshot.FinancialState != AutonomousFinancialState.Secure);
            if (best is null)
                return null;

            foreach (var pair in partnerSearch.BuildActionParameters(best))
                parameters[pair.Key] = pair.Value;
        }
        else if (actionId.Equals("household.extend_house", StringComparison.OrdinalIgnoreCase))
        {
            var residence = snapshot.Finance?.Houses.FirstOrDefault(house => house.IsResidence);
            if (residence is null || residence.ExtensionCost <= 0m
                || !_economy.CanAfford(snapshot.Head, residence.ExtensionCost))
                return null;
            parameters["propertyId"] = residence.Id.ToString();
        }
        else if (actionId.Equals("household.buy_house", StringComparison.OrdinalIgnoreCase))
        {
            var market = _context.GetService<IHouseMarketService>();
            if (market is null)
                return null;

            var town = _economy.GetResidenceTown(snapshot.Head);
            var requiredCapacity = (snapshot.Status?.ResidentCount ?? snapshot.Members.Count)
                + (snapshot.NeedsFamilyExpansion && snapshot.HasRealisticReproductivePath ? 1 : 0);
            var offers = market.GetOffers(snapshot.Head, town, _gameState.Year)
                .Where(candidate => _economy.CanAfford(snapshot.Head, candidate.AskingPrice))
                .ToList();
            var offer = offers
                .Where(candidate => snapshot.HasResidence || candidate.BaseResidentCapacity >= requiredCapacity)
                .OrderBy(candidate => candidate.AskingPrice)
                .ThenByDescending(candidate => candidate.BaseResidentCapacity)
                .FirstOrDefault();
            // Large rented families may need to buy first, then extend next year.
            // The scorer checks the combined purchase/construction budget.
            offer ??= offers
                .Where(candidate => !snapshot.HasResidence
                    && candidate.BaseResidentCapacity >= Math.Min(requiredCapacity,
                        snapshot.Status?.OvercrowdingThreshold ?? 8))
                .OrderByDescending(candidate => candidate.BaseResidentCapacity)
                .ThenBy(candidate => candidate.AskingPrice)
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
            // No property parameter means the normal shared action attempts a
            // rented independent household. Do not suppress that legal route.
        }
        else if (actionId.Equals("household.sell_house", StringComparison.OrdinalIgnoreCase))
        {
            var investment = snapshot.Finance?.Houses
                .FirstOrDefault(house => house.IsRented);
            if (investment is null)
                return null;
            parameters["propertyId"] = investment.Id.ToString();
        }
        else if (actionId.Equals("farming.sell_farmland", StringComparison.OrdinalIgnoreCase))
        {
            var town = _economy.GetResidenceTown(snapshot.Head);
            var parcel = _economy.GetFarmland(snapshot.Head)
                .OrderBy(asset => asset.Town.Id.Equals(town.Id, StringComparison.OrdinalIgnoreCase) ? 1 : 0)
                .ThenBy(asset => asset.AcquiredYear)
                .ThenBy(asset => asset.Id)
                .FirstOrDefault();
            if (parcel is null)
                return null;
            parameters["farmlandId"] = parcel.Id.ToString();
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
            var recipientHead = _households.ResolveHouseholdHead(target);
            var recipient = recipientHead is null ? null : _economy.GetHousehold(recipientHead);
            if (recipient is null || recipientHead!.Id == snapshot.Head.Id)
                return null;
            var forecast = _economy.GetAnnualForecast(recipientHead);
            var shortfall = Math.Max(1000m, forecast?.ProjectedExpenses ?? 0m) - recipient.Wealth;
            var need = Math.Ceiling(Math.Max(0m, shortfall) / 1000m) * 1000m;
            var reserve = Math.Max(1000m, snapshot.ExpectedExpenses * 2m);
            var available = Math.Floor(Math.Max(0m, (snapshot.Finance?.Wealth ?? 0m) - reserve) / 1000m) * 1000m;
            var amount = Math.Min(need, available);
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


    private bool IsSafeIncomingUnion(
        AutonomousHouseholdSnapshot snapshot,
        IPerson target,
        PartnerCandidateInfo candidate,
        bool createsIndependentHousehold)
    {
        var currentTown = _economy.GetResidenceTown(snapshot.Head);
        var currentLivingCost = _economy.GetLivingCostPerPerson(currentTown);

        if (!createsIndependentHousehold)
        {
            if (snapshot.Status is { } status
                && status.ResidentCount + 1 > status.OvercrowdingThreshold)
            {
                return false;
            }

            var postIncome = snapshot.ProjectedIncome + candidate.AnnualIncome;
            var postExpenses = snapshot.ExpectedExpenses + currentLivingCost;
            return CoversEssentialBudget(
                snapshot.Finance?.Wealth ?? 0m,
                postIncome,
                postExpenses,
                requireReserve: snapshot.NeedsFamilyExpansion
                    || snapshot.ExistingChildren.Any(child => child.Id == target.Id));
        }

        // An arranged husband forms the destination household immediately.
        // Evaluate both the new branch and the source household before offering
        // the candidate. No AI-only cash or housing is assumed.
        var locations = _context.GetService<ILocationService>();
        var destination = locations?.FindTown(candidate.TownId) ?? currentTown;
        var destinationLivingCost = _economy.GetLivingCostPerPerson(destination);
        var family = _context.GetService<IFamilyService>();
        var residentChildren = family is null
            ? 0
            : family.GetChildren(target).Count(child =>
                child.Age < 18 && child.Tags.Has("state.alive"));
        var branchMembers = 2 + residentChildren;
        var targetIncome = snapshot.Members.FirstOrDefault(member =>
            member.Person.Id == target.Id)?.Career?.AnnualIncome ?? 0m;
        var branchIncome = targetIncome + candidate.AnnualIncome;
        var branchExpenses = destinationLivingCost * branchMembers
            + (candidate.EstimatedHouses > 0
                ? 0m
                : _economy.GetResidenceRent(destination));
        if (!CoversEssentialBudget(
            Math.Max(0m, candidate.EstimatedWealth),
            branchIncome,
            branchExpenses,
            requireReserve: true))
        {
            return false;
        }

        var sourceIncome = Math.Max(0m, snapshot.ProjectedIncome - targetIncome);
        var sourceExpenses = Math.Max(0m, snapshot.ExpectedExpenses - currentLivingCost);
        return CoversEssentialBudget(
            snapshot.Finance?.Wealth ?? 0m,
            sourceIncome,
            sourceExpenses,
            requireReserve: false);
    }

    private static bool CoversEssentialBudget(
        decimal wealth,
        decimal income,
        decimal expenses,
        bool requireReserve)
    {
        if (expenses <= 0m)
            return true;

        var reserve = requireReserve
            ? expenses * AutonomousStrategyRules.ForecastReserveFraction
            : 0m;
        var twoYearDeficit = Math.Max(0m, expenses - income) * 2m;
        return wealth >= twoYearDeficit + reserve;
    }

    private static int GetStat(
        IReadOnlyDictionary<string, int> stats,
        string id) =>
        stats.TryGetValue(id, out var value) ? value : 0;

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
                || snapshot.HasImmediateMedicalDanger
                || snapshot.HasMaterialUnmetDependentNeed
                || snapshot.Status?.IsLargeFamilyStrained == true
                || snapshot.NeedsFamilyContinuity
                || (snapshot.Finance?.Wealth ?? 0m) < snapshot.ExpectedExpenses * 3m + 1000m)
            {
                return null;
            }

            var lendingOffer = loans.GetOffers(snapshot.Head, isGivingLoan: true, 1000m)
                .Where(offer => offer.Terms.Principal == 1000m && offer.Terms.DurationYears == 5)
                .OrderByDescending(offer => offer.Terms.TotalInterestRate)
                .FirstOrDefault();
            return lendingOffer is null ? null : LoanParameters(lendingOffer);
        }

        if (snapshot.ProjectedIncome <= 0)
            return null;

        var wealth = snapshot.Finance?.Wealth ?? 0m;
        var shortfall = Math.Max(0m, snapshot.ExpectedExpenses - snapshot.ProjectedIncome - wealth);
        var need = Math.Max(1000m, shortfall + ((snapshot.HasImmediateMedicalDanger || snapshot.HasMaterialUnmetDependentNeed) ? 3000m : 0m));
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
