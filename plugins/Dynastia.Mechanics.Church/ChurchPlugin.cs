using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Dynastia.Contracts;

namespace Dynastia.Mechanics.Church;

public sealed class ChurchPlugin : IGamePlugin
{
    private const string TierParameter = "churchTier";
    private const string AmountParameter = "churchAmount";
    private const string PoorFamilyContactIdParameter = "poorFamilyContactId";
    private const string PoorFamilyNameParameter = "poorFamilyName";
    private const string PoorFamilyContactNameParameter = "poorFamilyContactName";
    private const string PoorFamilyContactSexParameter = "poorFamilyContactSex";
    private const string PoorFamilyContactBirthYearParameter = "poorFamilyContactBirthYear";
    private const string PoorFamilyNationalityIdParameter = "poorFamilyNationalityId";
    private const string PoorFamilyTownIdParameter = "poorFamilyTownId";
    private const string PoorFamilySummaryParameter = "poorFamilySummary";
    private const string PoorFamilySpouseNameParameter = "poorFamilySpouseName";
    private const string PoorFamilyChildrenParameter = "poorFamilyChildren";
    private const string PoorFamilyOriginId = "church.aid_poor_family";

    public void Initialize(IGamePluginContext context)
    {
        EventPresentationRegistration.Register(context);
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
            names,
            () => context.GetService<IHouseholdConnectionService>());
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
            names,
            () => context.GetService<IHouseholdConnectionService>());
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
            Presentation = new()
            {
                Emoji = "⛪",
                Categories = [ActionPresentationCategories.Personal],
                ShowInPrimaryActionList = false
            },
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
                    SubjectId = actionContext.Target.Id,
                    RelatedPersonIds = actionContext.Actor.Id == actionContext.Target.Id
                        ? []
                        : [actionContext.Actor.Id],
                    Data = new Dictionary<string, string>
                    {
                        ["churchName"] = church.TierName,
                        ["text"] = $"{family.GetDisplayName(actionContext.Target)} attended {church.TierName}."
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
        IHistoricalNameService names,
        Func<IHouseholdConnectionService?> connectionsResolver)
    {
        actions.Register(new GameActionDefinition
        {
            Id = actionId,
            Presentation = new()
            {
                Emoji = aidPoorFamily ? "🤝" : "⛪",
                Categories = [ActionPresentationCategories.Personal]
            },
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
                if (aidPoorFamily)
                {
                    var prospectData = ResolvePoorFamilyProspectData(
                        actionContext,
                        economy,
                        nationalities,
                        names);
                    metadata = WithPoorFamilyMetadata(metadata, prospectData);
                }

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
                    var prospectData = ResolvePoorFamilyProspectData(
                        actionContext,
                        economy,
                        nationalities,
                        names);
                    var prospect = prospectData.Prospect;
                    var householdId = economy.GetHouseholdId(actionContext.Actor);
                    if (householdId is Guid resolvedHouseholdId
                        && connectionsResolver() is { } connections)
                    {
                        connections.EnsureLocalConnection(new HouseholdConnectionSeed(
                            prospect.ContactId,
                            resolvedHouseholdId,
                            prospect.ContactName,
                            prospect.ContactSex,
                            prospectData.ContactBirthYear,
                            prospect.NationalityId,
                            prospect.TownId,
                            "Local Resident",
                            "poor_family",
                            "Poor",
                            10,
                            10,
                            30,
                            20,
                            prospectData.SpouseName,
                            prospectData.Children,
                            PoorFamilyOriginId));
                    }

                    eventData["connectionId"] = prospect.ContactId.ToString("D");
                    eventData["familyName"] = prospect.FamilyName;
                    eventData["contactName"] = prospect.ContactName;
                    eventData["familySummary"] = prospect.HouseholdSummary;
                    eventData["text"] =
                        $"{family.GetDisplayName(actionContext.Target)} gave {amount:N0} zł to the struggling {prospect.FamilyName} family.";
                }
                else
                {
                    eventData["churchName"] = church.TierName;
                    eventData["text"] =
                        $"{family.GetDisplayName(actionContext.Target)} donated {amount:N0} zł to {church.TierName}.";
                }

                actionContext.EventBus.Publish(new GameEvent
                {
                    Type = eventType,
                    Year = actionContext.GameState.Year,
                    SubjectId = actionContext.Target.Id,
                    RelatedPersonIds = actionContext.Actor.Id == actionContext.Target.Id
                        ? []
                        : [actionContext.Actor.Id],
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
            Presentation = new()
            {
                Emoji = "🥖",
                Categories = [ActionPresentationCategories.Personal]
            },
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
        if (!context.Actor.Tags.Has("state.alive")
            || context.Actor.Age < 18
            || !context.ActorHasControl
            || context.Actor.Tags.Has("state.imprisoned")
            || !context.Target.Tags.Has("state.alive")
            || context.Target.Tags.Has("state.imprisoned")
            || !HouseholdKinshipRules.IsResidentHouseholdMember(
                context.Actor,
                context.Target,
                economy,
                requireAdult: true))
        {
            return ActionEvaluationResult.Denied(
                ActionReasonCodes.NoLongerEligible,
                "Church actions are available to living adult members of the active household.");
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
        return institutions.Resolve(town, context.ScheduledExecutionYear).Find("church")
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
            || !tiers.TryGetValue(raw.Trim(), out var resolvedTier))
        {
            return false;
        }

        tierId = raw.Trim().ToLowerInvariant();
        tier = resolvedTier;
        return true;
    }

    private static void ApplyMoralsChance(
        GameActionContext context,
        IPersonalityService personality,
        double improveChance,
        double protectionChance)
    {
        var current = personality.GetPersonality(context.Target)?.Morals;
        if (current is null)
            return;

        if (current.Equals("Good", StringComparison.OrdinalIgnoreCase))
        {
            if (context.Random.NextDouble() < protectionChance)
                personality.GrantMoralsProtection(context.Target);
            return;
        }

        if (context.Random.NextDouble() < improveChance)
            personality.ShiftMorals(context.Target, 1);
    }

    internal static PoorFamilyProspectInfo BuildPoorFamilyProspect(
        GameActionContext context,
        IEconomyService economy,
        INationalityService nationalities,
        IHistoricalNameService names) =>
        BuildPoorFamilyProspectData(context, economy, nationalities, names).Prospect;

    private static PoorFamilyProspectData ResolvePoorFamilyProspectData(
        GameActionContext context,
        IEconomyService economy,
        INationalityService nationalities,
        IHistoricalNameService names)
    {
        if (TryReadPoorFamilyProspectData(context, out var persisted))
            return persisted;

        return BuildPoorFamilyProspectData(context, economy, nationalities, names);
    }

    private static PoorFamilyProspectData BuildPoorFamilyProspectData(
        GameActionContext context,
        IEconomyService economy,
        INationalityService nationalities,
        IHistoricalNameService names)
    {
        var town = economy.GetResidenceTown(context.Actor);
        var householdId = economy.GetHouseholdId(context.Actor) ?? context.Actor.Id;
        var anchor = context.GameState.People.FirstOrDefault()?.Id.ToString("N") ?? "no-anchor";
        var key = $"{anchor}|{context.GameState.StartYear}|{town.Id}|{householdId:N}|{context.GameState.Year}|church-poor-family";
        var random = new StableGameRandom(StableHash64(key));

        var contactId = StableGuid(key + "|contact");
        var contactSex = random.Chance(0.5) ? Sex.Male : Sex.Female;
        var contactAge = random.NextInt(22, 55);
        var contactBirthYear = context.GameState.Year - contactAge;
        var nationalityId = string.IsNullOrWhiteSpace(town.RegionId)
            ? nationalities.GetNationality(context.Actor)
            : nationalities.GenerateNationality(
                town.RegionId,
                context.GameState.Year,
                random);
        var cultureId = nationalities.GetNameCultureId(nationalityId);
        var familyName = names.GetRandomSurname(Sex.Male, cultureId, random);
        var contactFirstName = names.GetRandomFirstName(
            contactSex,
            contactBirthYear,
            cultureId,
            random);
        var contactSurname = names.FormatSurname(
            familyName,
            contactSex,
            cultureId);
        var contactName = $"{contactFirstName} {contactSurname}";

        var hasSpouse = random.Chance(0.65);
        var maximumChildren = contactAge switch
        {
            <= 25 => 1,
            <= 30 => 2,
            <= 35 => 3,
            <= 40 => 4,
            _ => 5
        };
        var childCount = random.NextInt(1, Math.Max(1, maximumChildren));

        string? spouseName = null;
        if (hasSpouse)
        {
            var spouseSex = contactSex == Sex.Male ? Sex.Female : Sex.Male;
            var spouseAge = Math.Clamp(contactAge + random.NextInt(-5, 5), 18, 60);
            var spouseFirstName = names.GetRandomFirstName(
                spouseSex,
                context.GameState.Year - spouseAge,
                cultureId,
                random);
            spouseName = $"{spouseFirstName} {names.FormatSurname(familyName, spouseSex, cultureId)}";
        }

        var children = new List<string>(childCount);
        for (var index = 0; index < childCount; index++)
        {
            var childSex = random.Chance(0.5) ? Sex.Male : Sex.Female;
            var oldestPossible = Math.Clamp(contactAge - 18, 0, 17);
            var childAge = oldestPossible == 0
                ? 0
                : random.NextInt(0, oldestPossible);
            var childFirstName = names.GetRandomFirstName(
                childSex,
                context.GameState.Year - childAge,
                cultureId,
                random);
            children.Add(
                $"{childFirstName} {names.FormatSurname(familyName, childSex, cultureId)}");
        }

        var childLabel = childCount == 1 ? "1 child" : $"{childCount} children";
        var summary = hasSpouse
            ? $"spouse and {childLabel}"
            : $"widowed parent with {childLabel}";
        var prospect = new PoorFamilyProspectInfo(
            contactId,
            familyName,
            contactName,
            contactSex,
            contactAge,
            nationalityId,
            town.Id,
            summary,
            string.Empty);
        return new PoorFamilyProspectData(
            prospect,
            contactBirthYear,
            spouseName,
            children);
    }

    private static bool TryReadPoorFamilyProspectData(
        GameActionContext context,
        out PoorFamilyProspectData data)
    {
        var parameters = context.Parameters;
        data = null!;
        if (!parameters.TryGetValue(PoorFamilyContactIdParameter, out var rawId)
            || !Guid.TryParse(rawId, out var contactId)
            || !parameters.TryGetValue(PoorFamilyNameParameter, out var familyName)
            || string.IsNullOrWhiteSpace(familyName)
            || !parameters.TryGetValue(PoorFamilyContactNameParameter, out var contactName)
            || string.IsNullOrWhiteSpace(contactName)
            || !parameters.TryGetValue(PoorFamilyContactSexParameter, out var rawSex)
            || !Enum.TryParse<Sex>(rawSex, ignoreCase: true, out var contactSex)
            || !parameters.TryGetValue(PoorFamilyContactBirthYearParameter, out var rawBirthYear)
            || !int.TryParse(rawBirthYear, NumberStyles.Integer, CultureInfo.InvariantCulture, out var birthYear)
            || !parameters.TryGetValue(PoorFamilyNationalityIdParameter, out var nationalityId)
            || string.IsNullOrWhiteSpace(nationalityId)
            || !parameters.TryGetValue(PoorFamilyTownIdParameter, out var townId)
            || string.IsNullOrWhiteSpace(townId)
            || !parameters.TryGetValue(PoorFamilySummaryParameter, out var summary)
            || string.IsNullOrWhiteSpace(summary))
        {
            return false;
        }

        parameters.TryGetValue(PoorFamilySpouseNameParameter, out var spouseName);
        parameters.TryGetValue(PoorFamilyChildrenParameter, out var rawChildren);
        var children = string.IsNullOrWhiteSpace(rawChildren)
            ? Array.Empty<string>()
            : rawChildren.Split(
                '\u001F',
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var prospect = new PoorFamilyProspectInfo(
            contactId,
            familyName,
            contactName,
            contactSex,
            Math.Max(0, context.GameState.Year - birthYear),
            nationalityId,
            townId,
            summary,
            string.Empty);
        data = new PoorFamilyProspectData(
            prospect,
            birthYear,
            string.IsNullOrWhiteSpace(spouseName) ? null : spouseName,
            children);
        return true;
    }

    private static IReadOnlyDictionary<string, string> WithPoorFamilyMetadata(
        IReadOnlyDictionary<string, string> source,
        PoorFamilyProspectData data)
    {
        var prospect = data.Prospect;
        return new Dictionary<string, string>(source, StringComparer.OrdinalIgnoreCase)
        {
            [PoorFamilyContactIdParameter] = prospect.ContactId.ToString("D"),
            [PoorFamilyNameParameter] = prospect.FamilyName,
            [PoorFamilyContactNameParameter] = prospect.ContactName,
            [PoorFamilyContactSexParameter] = prospect.ContactSex.ToString(),
            [PoorFamilyContactBirthYearParameter] = data.ContactBirthYear.ToString(CultureInfo.InvariantCulture),
            [PoorFamilyNationalityIdParameter] = prospect.NationalityId,
            [PoorFamilyTownIdParameter] = prospect.TownId,
            [PoorFamilySummaryParameter] = prospect.HouseholdSummary,
            [PoorFamilySpouseNameParameter] = data.SpouseName ?? string.Empty,
            [PoorFamilyChildrenParameter] = string.Join("\u001F", data.Children)
        };
    }

    private static ulong StableHash64(string key)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(key));
        return BinaryPrimitives.ReadUInt64LittleEndian(bytes);
    }

    private static Guid StableGuid(string key)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(key));
        return new Guid(bytes.AsSpan(0, 16));
    }

    private sealed record PoorFamilyProspectData(
        PoorFamilyProspectInfo Prospect,
        int ContactBirthYear,
        string? SpouseName,
        IReadOnlyList<string> Children);

    private sealed class StableGameRandom : IGameRandom
    {
        private ulong _state;

        public StableGameRandom(ulong seed) =>
            _state = seed == 0 ? 0x9E3779B97F4A7C15UL : seed;

        public int NextInt(int minInclusive, int maxInclusive)
        {
            if (maxInclusive < minInclusive)
                throw new ArgumentOutOfRangeException(nameof(maxInclusive));
            var width = (ulong)((long)maxInclusive - minInclusive + 1L);
            return minInclusive + (int)(NextUInt64() % width);
        }

        public double NextDouble() =>
            (NextUInt64() >> 11) * (1.0 / (1UL << 53));

        public bool Chance(double probability) =>
            NextDouble() < Math.Clamp(probability, 0, 1);

        private ulong NextUInt64()
        {
            var value = _state;
            value ^= value >> 12;
            value ^= value << 25;
            value ^= value >> 27;
            _state = value;
            return value * 2685821657736338717UL;
        }
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
