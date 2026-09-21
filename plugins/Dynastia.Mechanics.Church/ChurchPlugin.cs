using System.Globalization;
using Dynastia.Contracts;

namespace Dynastia.Mechanics.Church;

public sealed class ChurchPlugin : IGamePlugin
{
    private const string TierParameter = "churchTier";
    private const string AmountParameter = "churchAmount";

    public void Initialize(IGamePluginContext context)
    {
        var data = Required<IGameDataService>(context, "game data service");
        var actions = Required<IActionRegistry>(context, "action registry");
        var economy = Required<IEconomyService>(context, "economy service");
        var family = Required<IFamilyService>(context, "family service");
        var personality = Required<IPersonalityService>(context, "personality service");
        var status = Required<IStatusService>(context, "status service");
        var institutions = Required<ITownInstitutionService>(context, "town institution service");
        var nationalities = Required<INationalityService>(context, "nationality service");
        var names = Required<IHistoricalNameService>(context, "historical name service");

        var rules = ChurchRules.Load(data);

        RegisterAttend(actions, economy, family, personality, institutions, rules);
        RegisterDonation(
            actions,
            economy,
            family,
            personality,
            institutions,
            rules,
            "church.donate",
            "Donate to Church",
            "Make a visible donation to the local Church. Larger gifts bring more Renown but only a small Reputation and Morals effect.",
            rules.ChurchDonationTiers,
            aidPoorFamily: false,
            nationalities,
            names);
        RegisterDonation(
            actions,
            economy,
            family,
            personality,
            institutions,
            rules,
            "church.aid_poor_family",
            "Aid a Poor Family",
            "Give directly to a struggling local family. This is less visible than a Church donation but has a stronger Reputation and Morals effect.",
            rules.PoorFamilyTiers,
            aidPoorFamily: true,
            nationalities,
            names);
        RegisterWelfare(
            actions,
            economy,
            status,
            institutions,
            rules,
            () => context.GetService<ICommunityPolicyService>());

        context.Log("Church actions and welfare registered.");
    }

    private static void RegisterAttend(
        IActionRegistry actions,
        IEconomyService economy,
        IFamilyService family,
        IPersonalityService personality,
        ITownInstitutionService institutions,
        ChurchRules rules)
    {
        actions.Register(new GameActionDefinition
        {
            Id = "church.attend",
            Label = "Attend Church",
            Description = "Attend the local Church for a small social benefit and a 5% chance to improve Morals or protect already Good Morals.",
            Mode = ActionExecutionMode.Queued,
            QueuePhase = YearPhase.QueuedActionsEarly,
            EvaluateAvailability = actionContext =>
                EvaluateBase(actionContext, economy, institutions),
            Execute = actionContext =>
            {
                var church = ResolveChurch(actionContext, economy, institutions);
                ApplyMoralsChance(
                    actionContext,
                    personality,
                    rules.Attend.MoralsImproveChance,
                    rules.Attend.GoodMoralsProtectionChance);

                actionContext.EventBus.Publish(new GameEvent
                {
                    Type = "church.attend",
                    Year = actionContext.GameState.Year,
                    SubjectId = actionContext.Actor.Id,
                    Data = new Dictionary<string, string>
                    {
                        ["churchName"] = church.TierName,
                        ["text"] = $"{family.GetDisplayName(actionContext.Actor)} attended {church.TierName}."
                    }
                });

                return new GameActionResult(true);
            }
        });
    }

    private static void RegisterDonation(
        IActionRegistry actions,
        IEconomyService economy,
        IFamilyService family,
        IPersonalityService personality,
        ITownInstitutionService institutions,
        ChurchRules rules,
        string actionId,
        string label,
        string description,
        IReadOnlyDictionary<string, ChurchRules.DonationTierRule> tiers,
        bool aidPoorFamily,
        INationalityService nationalities,
        IHistoricalNameService names)
    {
        actions.Register(new GameActionDefinition
        {
            Id = actionId,
            Label = label,
            Description = description,
            Mode = ActionExecutionMode.Queued,
            QueuePhase = YearPhase.QueuedActionsEarly,
            EvaluateAvailability = actionContext =>
            {
                var basic = EvaluateBase(actionContext, economy, institutions);
                if (!basic.Available)
                    return basic;

                if (!TryGetTier(actionContext.Parameters, tiers, out var tierId, out _))
                {
                    return ActionEvaluationResult.Denied(
                        ActionReasonCodes.InvalidParameter,
                        "Choose Modest, Generous or Major support.");
                }

                var amount = ResolveMoneyAmount(
                    actionContext,
                    economy,
                    rules,
                    tiers,
                    tierId);
                var household = economy.GetHousehold(actionContext.Actor);
                var available = household?.Wealth ?? 0m;
                var requirements = new[]
                {
                    new ActionResourceRequirement(
                        "household.wealth",
                        amount,
                        available,
                        "Household wealth",
                        "zł")
                };
                var metadata = MoneyMetadata(tierId, amount);

                return available >= amount
                    ? ActionEvaluationResult.Allowed(
                        economy.GetHouseholdId(actionContext.Actor),
                        requirements,
                        metadata)
                    : ActionEvaluationResult.Denied(
                        ActionReasonCodes.InsufficientFunds,
                        $"This {tierId} gift requires {amount:N0} zł.",
                        economy.GetHouseholdId(actionContext.Actor),
                        requirements,
                        metadata);
            },
            Execute = actionContext =>
            {
                if (!TryGetTier(actionContext.Parameters, tiers, out var tierId, out var tier))
                {
                    return new GameActionResult(
                        false,
                        "The selected Church support level is invalid.",
                        ActionReasonCodes.InvalidParameter);
                }

                var amount = ResolveMoneyAmount(
                    actionContext,
                    economy,
                    rules,
                    tiers,
                    tierId);
                if (!economy.CanAfford(actionContext.Actor, amount))
                {
                    return new GameActionResult(
                        false,
                        "The household can no longer afford this gift.",
                        ActionReasonCodes.InsufficientFunds);
                }

                economy.ChangeWealth(actionContext.Actor, -amount);
                ApplyMoralsChance(
                    actionContext,
                    personality,
                    tier.MoralsImproveChance,
                    tier.MoralsImproveChance);

                var church = ResolveChurch(actionContext, economy, institutions);
                var eventType = aidPoorFamily
                    ? "church.aid_poor"
                    : "church.donate";
                var eventData = new Dictionary<string, string>
                {
                    ["amount"] = amount.ToString(CultureInfo.InvariantCulture),
                    ["tier"] = tierId,
                    ["statusRenownDelta"] = tier.Renown.ToString("R", CultureInfo.InvariantCulture),
                    ["statusReputationDelta"] = tier.Reputation.ToString("R", CultureInfo.InvariantCulture)
                };

                if (aidPoorFamily)
                {
                    var familyName = GeneratePoorFamilyName(
                        actionContext,
                        economy,
                        nationalities,
                        names);
                    eventData["familyName"] = familyName;
                    eventData["text"] =
                        $"{family.GetDisplayName(actionContext.Actor)} gave {amount:N0} zł to the struggling {familyName} family.";
                }
                else
                {
                    eventData["churchName"] = church.TierName;
                    eventData["text"] =
                        $"{family.GetDisplayName(actionContext.Actor)} donated {amount:N0} zł to {church.TierName}.";
                }

                actionContext.EventBus.Publish(new GameEvent
                {
                    Type = eventType,
                    Year = actionContext.GameState.Year,
                    SubjectId = actionContext.Actor.Id,
                    Data = eventData
                });

                return new GameActionResult(true);
            }
        });
    }

    private static void RegisterWelfare(
        IActionRegistry actions,
        IEconomyService economy,
        IStatusService status,
        ITownInstitutionService institutions,
        ChurchRules rules,
        Func<ICommunityPolicyService?> communityResolver)
    {
        actions.Register(new GameActionDefinition
        {
            Id = "church.ask_welfare",
            Label = "Ask for Welfare",
            Description = "Ask the local Church for emergency household relief. Available only at 0 zł or less wealth, with Household Reputation of at least -10, once per household per year.",
            Mode = ActionExecutionMode.Queued,
            QueuePhase = YearPhase.QueuedActionsEarly,
            EvaluateAvailability = actionContext =>
            {
                var basic = EvaluateBase(actionContext, economy, institutions);
                if (!basic.Available)
                    return basic;

                var household = economy.GetHousehold(actionContext.Actor);
                if (household is null)
                {
                    return ActionEvaluationResult.Denied(
                        ActionReasonCodes.ResourceUnavailable,
                        "This person does not have an active household.");
                }

                var householdStatus = status.GetHouseholdStatus(actionContext.Actor);
                if (!rules.IsWelfareEligible(
                        household.Wealth,
                        householdStatus.Reputation))
                {
                    var reason = household.Wealth > rules.Welfare.MaximumWealth
                        ? "Church welfare is reserved for households with no available wealth."
                        : "Household Reputation is too poor for Church welfare.";
                    return ActionEvaluationResult.Denied(
                        ActionReasonCodes.NoLongerEligible,
                        reason);
                }

                var welfareYear = ResolveWelfareEligibilityYear(actionContext);
                if (HasWelfareForYear(actionContext, economy, welfareYear))
                {
                    return ActionEvaluationResult.Denied(
                        ActionReasonCodes.NoLongerEligible,
                        "This household has already received Church welfare for that year.");
                }

                var church = ResolveChurch(actionContext, economy, institutions);
                var amount = ResolveFixedAmount(actionContext.Parameters)
                    ?? ApplyWelfarePolicy(
                        rules.CalculateWelfareAmount(
                            GetAnnualExpenses(economy, actionContext.Actor),
                            church.Tier),
                        actionContext,
                        economy,
                        communityResolver);

                return ActionEvaluationResult.Allowed(
                    economy.GetHouseholdId(actionContext.Actor),
                    presentationMetadata: new Dictionary<string, string>
                    {
                        ["amount"] = amount.ToString(CultureInfo.InvariantCulture)
                    });
            },
            Execute = actionContext =>
            {
                var church = ResolveChurch(actionContext, economy, institutions);
                var amount = ResolveFixedAmount(actionContext.Parameters)
                    ?? ApplyWelfarePolicy(
                        rules.CalculateWelfareAmount(
                            GetAnnualExpenses(economy, actionContext.Actor),
                            church.Tier),
                        actionContext,
                        economy,
                        communityResolver);

                economy.ChangeWealth(actionContext.Actor, amount);
                MarkWelfareForYear(
                    actionContext,
                    economy,
                    actionContext.GameState.Year);

                actionContext.EventBus.Publish(new GameEvent
                {
                    Type = "church.welfare",
                    Year = actionContext.GameState.Year,
                    SubjectId = actionContext.Actor.Id,
                    Data = new Dictionary<string, string>
                    {
                        ["amount"] = amount.ToString(CultureInfo.InvariantCulture),
                        ["churchName"] = church.TierName,
                        ["text"] =
                            $"{church.TierName} provided {amount:N0} zł in emergency welfare to the household."
                    }
                });

                return new GameActionResult(true);
            }
        });
    }

    private static decimal ApplyWelfarePolicy(
        decimal baseAmount,
        GameActionContext context,
        IEconomyService economy,
        Func<ICommunityPolicyService?> communityResolver)
    {
        var community = communityResolver();
        if (community is null)
            return baseAmount;

        var town = economy.GetResidenceTown(context.Actor);
        var multiplier = community
            .GetModifiers(town, context.GameState.Year)
            .ChurchWelfareMultiplier;
        return Math.Round(
            baseAmount * multiplier,
            0,
            MidpointRounding.AwayFromZero);
    }

    private static ActionEvaluationResult EvaluateBase(
        GameActionContext context,
        IEconomyService economy,
        ITownInstitutionService institutions)
    {
        if (context.Actor.Id != context.Target.Id
            || !context.Actor.Tags.Has("state.alive")
            || context.Actor.Age < 18
            || !context.ActorHasControl)
        {
            return ActionEvaluationResult.Denied(
                ActionReasonCodes.NoLongerEligible,
                "Church actions are performed by the active adult household controller.");
        }

        var householdId = economy.GetHouseholdId(context.Actor);
        if (householdId is null)
        {
            return ActionEvaluationResult.Denied(
                ActionReasonCodes.ResourceUnavailable,
                "An active household is required.");
        }

        var church = ResolveChurch(context, economy, institutions);
        return church.Tier > 0
            ? ActionEvaluationResult.Allowed(householdId)
            : ActionEvaluationResult.Denied(
                ActionReasonCodes.ResourceUnavailable,
                "No Church is available in this town.",
                householdId);
    }

    private static TownInstitutionInfo ResolveChurch(
        GameActionContext context,
        IEconomyService economy,
        ITownInstitutionService institutions)
    {
        var town = economy.GetResidenceTown(context.Actor);
        return institutions.Resolve(town, context.GameState.Year).Find("church")
            ?? new TownInstitutionInfo("church", "Church", 0, "Unavailable");
    }

    private static decimal ResolveMoneyAmount(
        GameActionContext context,
        IEconomyService economy,
        ChurchRules rules,
        IReadOnlyDictionary<string, ChurchRules.DonationTierRule> tiers,
        string tierId) =>
        ResolveFixedAmount(context.Parameters)
        ?? rules.CalculateDonationAmount(
            tiers,
            tierId,
            GetAnnualExpenses(economy, context.Actor));

    private static decimal? ResolveFixedAmount(
        IReadOnlyDictionary<string, string> parameters)
    {
        if (!parameters.TryGetValue(AmountParameter, out var raw)
            || !decimal.TryParse(
                raw,
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out var amount)
            || amount <= 0)
        {
            return null;
        }

        return amount;
    }

    private static decimal GetAnnualExpenses(
        IEconomyService economy,
        IPerson actor)
    {
        var forecast = economy.GetAnnualForecast(actor);
        if (forecast is { ProjectedExpenses: > 0m })
            return forecast.ProjectedExpenses;

        var household = economy.GetHousehold(actor);
        if (household is { LastExpenses: > 0m })
            return household.LastExpenses;

        var memberCount = economy.GetHouseholdMemberIds(actor)
            .Append(actor.Id)
            .Distinct()
            .Count();
        return economy.GetLivingCostPerPerson(economy.GetResidenceTown(actor))
            * Math.Max(1, memberCount);
    }

    private static IReadOnlyDictionary<string, string> MoneyMetadata(
        string tierId,
        decimal amount) =>
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["tier"] = tierId,
            ["amount"] = amount.ToString(CultureInfo.InvariantCulture)
        };

    private static bool TryGetTier(
        IReadOnlyDictionary<string, string> parameters,
        IReadOnlyDictionary<string, ChurchRules.DonationTierRule> tiers,
        out string tierId,
        out ChurchRules.DonationTierRule tier)
    {
        tierId = string.Empty;
        tier = null!;
        if (!parameters.TryGetValue(TierParameter, out var raw)
            || string.IsNullOrWhiteSpace(raw)
            || !tiers.TryGetValue(raw.Trim(), out tier))
        {
            return false;
        }

        tierId = raw.Trim().ToLowerInvariant();
        return true;
    }

    private static void ApplyMoralsChance(
        GameActionContext context,
        IPersonalityService personality,
        double improveChance,
        double protectionChance)
    {
        var current = personality.GetPersonality(context.Actor)?.Morals;
        if (current is null)
            return;

        if (current.Equals("Good", StringComparison.OrdinalIgnoreCase))
        {
            if (context.Random.NextDouble() < protectionChance)
                personality.GrantMoralsProtection(context.Actor);
            return;
        }

        if (context.Random.NextDouble() < improveChance)
            personality.ShiftMorals(context.Actor, 1);
    }

    private static string GeneratePoorFamilyName(
        GameActionContext context,
        IEconomyService economy,
        INationalityService nationalities,
        IHistoricalNameService names)
    {
        var town = economy.GetResidenceTown(context.Actor);
        var nationalityId = string.IsNullOrWhiteSpace(town.RegionId)
            ? nationalities.GetNationality(context.Actor)
            : nationalities.GenerateNationality(
                town.RegionId,
                context.GameState.Year,
                context.Random);
        var cultureId = nationalities.GetNameCultureId(nationalityId);
        return names.GetRandomSurname(
            Sex.Male,
            cultureId,
            context.Random);
    }

    private static int ResolveWelfareEligibilityYear(GameActionContext context) =>
        context.ExecutionPhase.HasValue
            ? context.GameState.Year
            : context.GameState.Year + 1;

    private static bool HasWelfareForYear(
        GameActionContext context,
        IEconomyService economy,
        int year)
    {
        var ids = economy.GetHouseholdMemberIds(context.Actor)
            .Append(context.Actor.Id)
            .ToHashSet();

        return context.GameState.People
            .Where(person => ids.Contains(person.Id))
            .Select(person => person.Components.Get<ChurchHouseholdStateComponent>())
            .Any(component => component?.LastWelfareYear == year);
    }

    private static void MarkWelfareForYear(
        GameActionContext context,
        IEconomyService economy,
        int year)
    {
        var ids = economy.GetHouseholdMemberIds(context.Actor)
            .Append(context.Actor.Id)
            .ToHashSet();

        foreach (var person in context.GameState.People.Where(person => ids.Contains(person.Id)))
        {
            var component = person.Components.Get<ChurchHouseholdStateComponent>()
                ?? new ChurchHouseholdStateComponent();
            component.LastWelfareYear = year;
            person.Components.Set(component);
        }
    }

    private static T Required<T>(IGamePluginContext context, string label)
        where T : class =>
        context.GetService<T>()
        ?? throw new InvalidOperationException($"{label} is unavailable.");
}
