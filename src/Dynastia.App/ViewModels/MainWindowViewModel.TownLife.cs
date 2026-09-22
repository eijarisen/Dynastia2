using Dynastia.Contracts;

namespace Dynastia.App.ViewModels;

public sealed partial class MainWindowViewModel
{
    private const string TownAffairsUiActionId =
        "ui.town_affairs";

    private static readonly HashSet<string> TownAffairsJobActionIds =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "career.seek_employment",
            "career.find_another_job",
            "career.help_seek_employment",
            "career.help_find_better_job"
        };

    private static readonly HashSet<string> TownAffairsHealthActionIds =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "wellbeing.heal_relative",
            "wellbeing.therapy",
            "stats.improve_strength",
            "stats.improve_intellect",
            "stats.improve_immunity",
            "stats.improve_appeal",
            "stats.improve_longevity",
            "stats.improve_fertility"
        };

    private static readonly HashSet<string> TownAffairsChurchActionIds =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "church.attend",
            "church.donate",
            "church.aid_poor_family",
            "church.ask_welfare",
            "personality.religious_study"
        };

    private static readonly string[] ChurchDonationTiers =
        ["modest", "generous", "major"];

    private static readonly HashSet<string> TownAffairsCourtActionIds =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "justice.bail_out",
            "justice.attempt_escape"
        };

    private const string TownAffairsCommunityLobbyActionId =
        "community.lobby_policy";

    private const string TownAffairsOfficeDutiesActionId =
        "community.perform_office_duties";

    public string TownLifeNavigationLabel
    {
        get
        {
            var representative = FindSelectedPerson()
                ?? GetDisplayedHouseholdHead();
            if (representative is null || _locationService is null)
                return "Town Affairs";

            try
            {
                var town = _locationService.GetLocation(representative).HomeTown;
                return town.SettlementClass is SettlementClass.City
                    or SettlementClass.MajorCity
                        ? "City Affairs"
                        : "Town Affairs";
            }
            catch (InvalidOperationException)
            {
                return "Town Affairs";
            }
        }
    }

    private GameActionDefinition CreateTownAffairsPresentationAction() =>
        new()
        {
            Id = TownAffairsUiActionId,
            Label = TownLifeNavigationLabel,
            Description =
                "Inspect the selected adult household member's town, institutions and local services.",
            Mode = ActionExecutionMode.Immediate,
            IsAvailable = _ => true,
            Execute = _ => new GameActionResult(false)
        };

    internal bool CanOpenTownAffairs
    {
        get
        {
            var actor = _succession.ActiveController;
            var target = FindSelectedPerson();
            if (_locationService is null
                || _economyService is null
                || actor is null
                || target is null
                || target.Age < 18
                || !target.Tags.Has("state.alive"))
            {
                return false;
            }

            return target.Id == actor.Id
                || _economyService
                    .GetHouseholdMemberIds(actor)
                    .Contains(target.Id);
        }
    }

    internal TownAffairsRequest? CreateTownAffairsRequest(
        string actionId)
    {
        if (_locationService is null)
            return null;

        var target = FindSelectedPerson();
        if (target is null)
            return null;

        // Runtime counterpart of data/TownAffairs/shortcut_routes.csv. Keep
        // action IDs intact so shortcuts only change presentation/routing.
        TownAffairsTab? tab = actionId.ToLowerInvariant() switch
        {
            TownAffairsUiActionId => TownAffairsTab.Institutions,
            "career.seek_employment" => TownAffairsTab.Jobs,
            "career.find_another_job" => TownAffairsTab.Jobs,
            "career.help_seek_employment" => TownAffairsTab.Jobs,
            "career.help_find_better_job" => TownAffairsTab.Jobs,
            "education.get_education" => TownAffairsTab.Education,
            "loan.take" => TownAffairsTab.Bank,
            "loan.give" => TownAffairsTab.Bank,
            "wellbeing.heal_relative" => TownAffairsTab.Health,
            "wellbeing.therapy" => TownAffairsTab.Health,
            "stats.improve_strength" => TownAffairsTab.Health,
            "stats.improve_intellect" => TownAffairsTab.Health,
            "stats.improve_immunity" => TownAffairsTab.Health,
            "stats.improve_appeal" => TownAffairsTab.Health,
            "stats.improve_longevity" => TownAffairsTab.Health,
            "stats.improve_fertility" => TownAffairsTab.Health,
            "personality.religious_study" => TownAffairsTab.Church,
            "church.attend" => TownAffairsTab.Church,
            "church.donate" => TownAffairsTab.Church,
            "church.aid_poor_family" => TownAffairsTab.Church,
            "church.ask_welfare" => TownAffairsTab.Church,
            TownAffairsCommunityLobbyActionId => TownAffairsTab.Community,
            TownAffairsOfficeDutiesActionId => TownAffairsTab.Community,
            _ => null
        };

        if (tab is null)
            return null;

        if (actionId.Equals(TownAffairsUiActionId, StringComparison.OrdinalIgnoreCase)
            && !CanOpenTownAffairs)
        {
            return null;
        }

        var town = _locationService.GetLocation(target).HomeTown;
        return new TownAffairsRequest(
            town.Id,
            target.Id,
            tab.Value,
            TownAffairsMode.Resident,
            actionId.Equals(TownAffairsUiActionId, StringComparison.OrdinalIgnoreCase)
                ? null
                : actionId);
    }

    internal TownAffairsRequest CreateHousingTownAffairsRequest(
        string townId)
    {
        var actor = _succession.ActiveController;
        var currentTownId = actor is null || _economyService is null
            ? null
            : _economyService.GetResidenceTown(actor).Id;

        return new TownAffairsRequest(
            townId,
            actor?.Id,
            TownAffairsTab.Housing,
            currentTownId is not null
                && currentTownId.Equals(townId, StringComparison.OrdinalIgnoreCase)
                    ? TownAffairsMode.Resident
                    : TownAffairsMode.RemoteHousingBrowse);
    }

    internal TownAffairsRequest CreateRemoteHousingTownAffairsRequest(
        string townId) =>
        new(
            townId,
            _succession.ActiveController?.Id,
            TownAffairsTab.Housing,
            TownAffairsMode.RemoteHousingBrowse);

    internal TownAffairsRequest? CreateActiveHouseholdTownAffairsRequest(
        TownAffairsTab initialTab)
    {
        var actor = _succession.ActiveController;
        if (actor is null || _economyService is null)
            return null;

        var town = _economyService.GetResidenceTown(actor);
        return new TownAffairsRequest(
            town.Id,
            actor.Id,
            initialTab,
            TownAffairsMode.Resident);
    }

    internal IPerson? GetTownAffairsSubject(Guid? subjectPersonId) =>
        subjectPersonId is Guid id
            ? _gameState.People.FirstOrDefault(person => person.Id == id)
            : null;

    internal TownAffairsViewModel CreateTownAffairsViewModel(
        TownLifeSnapshot snapshot,
        TownAffairsRequest request) =>
        new(
            this,
            snapshot,
            GetTownAffairsSubject(request.SubjectPersonId),
            request);

    internal IReadOnlyList<TownAffairsHouseOfferViewModel>
        GetTownAffairsHouseOffers(TownInfo town)
    {
        var actor = _succession.ActiveController;
        if (actor is null || _houseMarketService is null || _economyService is null)
            return [];

        return _houseMarketService
            .GetOffers(actor, town, _gameState.Year)
            .Select(offer =>
                new TownAffairsHouseOfferViewModel(
                    offer,
                    _economyService.CanAfford(actor, offer.AskingPrice)))
            .ToArray();
    }

    internal void QueueTownAffairsHousePurchase(HousePurchaseOfferInfo offer)
    {
        var actor = _succession.ActiveController;
        if (actor is null || _succession.IsGameOver)
            return;

        var parameters = new Dictionary<string, string>(
            StringComparer.OrdinalIgnoreCase)
        {
            ["townId"] = offer.Town.Id,
            ["houseOfferId"] = offer.OfferId,
            ["houseOfferYear"] = offer.OfferYear.ToString(
                System.Globalization.CultureInfo.InvariantCulture),
            ["houseCapacity"] = offer.BaseResidentCapacity.ToString(
                System.Globalization.CultureInfo.InvariantCulture),
            ["houseAskingPrice"] = offer.AskingPrice.ToString(
                System.Globalization.CultureInfo.InvariantCulture),
            ["summaryTown"] = offer.Town.Town,
            ["summaryPrice"] = offer.AskingPrice.ToString(
                System.Globalization.CultureInfo.InvariantCulture),
            ["summaryCapacity"] = offer.BaseResidentCapacity.ToString(
                System.Globalization.CultureInfo.InvariantCulture)
        };

        var result = _actionRegistry.Execute(
            "household.buy_house",
            actor,
            actor,
            parameters);

        if (!result.Success && !string.IsNullOrWhiteSpace(result.Message))
            PersistenceStatusText = result.Message;

        RefreshPeople();
        RefreshAlbum();
        RefreshHealth();
        RefreshEconomy();
        RefreshEducation();
        RefreshCareer();
        RefreshJustice();
        RefreshNarrative();
        RefreshActions();
    }

    internal string GetTownAffairsSubjectName(IPerson person) =>
        _familyService?.GetDisplayName(person)
        ?? $"{person.Name} {person.Surname}";

    internal IReadOnlyList<IPerson> GetTownAffairsHouseholdMembers()
    {
        var actor = _succession.ActiveController;
        if (actor is null || _economyService is null)
            return [];

        var memberIds = _economyService
            .GetHouseholdMemberIds(actor)
            .Append(actor.Id)
            .ToHashSet();

        return _gameState.People
            .Where(person => memberIds.Contains(person.Id)
                && person.Tags.Has("state.alive"))
            .OrderByDescending(person => person.Id == actor.Id)
            .ThenByDescending(person => person.Age)
            .ThenBy(person => GetTownAffairsSubjectName(person),
                StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    internal string? ResolveTownAffairsJobActionId(
        IPerson subject,
        string? preferredActionId)
    {
        if (!string.IsNullOrWhiteSpace(preferredActionId)
            && TownAffairsJobActionIds.Contains(preferredActionId))
        {
            return preferredActionId;
        }

        var actor = _succession.ActiveController;
        if (actor is null || _careerService is null)
            return null;

        var employed = _careerService.IsEmployed(subject);
        if (subject.Id == actor.Id)
        {
            return employed
                ? "career.find_another_job"
                : "career.seek_employment";
        }

        return employed
            ? "career.help_find_better_job"
            : "career.help_seek_employment";
    }

    internal bool IsTownAffairsActionAvailable(
        string actionId,
        IPerson target)
    {
        var actor = _succession.ActiveController;
        return actor is not null
            && _actionRegistry.Evaluate(actionId, actor, target).Available;
    }

    internal bool IsTownAffairsHouseholdActionAvailable(string actionId)
    {
        var actor = _succession.ActiveController;
        return actor is not null
            && _actionRegistry.Evaluate(actionId, actor, actor).Available;
    }

    internal TownAffairsHealthSummaryViewModel GetTownAffairsHealthSummary(
        IPerson subject)
    {
        if (_healthService is null)
        {
            return new TownAffairsHealthSummaryViewModel(
                "Health information is unavailable.",
                "Conditions: unavailable");
        }

        var health = _healthService.GetHealth(subject);
        var conditions = health.Conditions.Count == 0
            ? "Conditions: None"
            : "Conditions: " + string.Join(
                ", ",
                health.Conditions.Select(condition => condition.Name));

        return new TownAffairsHealthSummaryViewModel(
            $"Health: {health.Current:N0} / {health.Maximum:N0} ({health.Percentage:N0}%)",
            conditions);
    }

    internal IReadOnlyList<TownAffairsHealthActionViewModel>
        GetTownAffairsHealthActions(IPerson subject)
    {
        var actor = _succession.ActiveController;
        if (actor is null)
            return [];

        return _actionRegistry
            .GetCandidateActions(actor, subject)
            .Where(action => TownAffairsHealthActionIds.Contains(action.Id))
            .Select(action => new
            {
                Action = action,
                Evaluation = _actionRegistry.Evaluate(
                    action.Id,
                    actor,
                    subject)
            })
            .Where(item =>
                item.Evaluation.Available
                || item.Evaluation.ReasonCode.Equals(
                    ActionReasonCodes.InsufficientFunds,
                    StringComparison.OrdinalIgnoreCase)
                || (item.Action.Id.StartsWith(
                        "stats.improve_",
                        StringComparison.OrdinalIgnoreCase)
                    && item.Evaluation.ReasonCode.Equals(
                        ActionReasonCodes.ResourceUnavailable,
                        StringComparison.OrdinalIgnoreCase)))
            .Select(item => new TownAffairsHealthActionViewModel(
                item.Action.Id,
                item.Action.Label,
                item.Action.Description,
                item.Action.DisplayCost,
                item.Evaluation.Available,
                item.Evaluation.Reason))
            .ToArray();
    }

    internal IReadOnlyList<TownAffairsChurchActionViewModel>
        GetTownAffairsChurchActions()
    {
        var actor = _succession.ActiveController;
        if (actor is null)
            return [];

        var definitions = _actionRegistry
            .GetCandidateActions(actor, actor)
            .Where(action => TownAffairsChurchActionIds.Contains(action.Id))
            .ToDictionary(action => action.Id, StringComparer.OrdinalIgnoreCase);
        var result = new List<TownAffairsChurchActionViewModel>();

        AddChurchAction("church.attend", null, null, isBenefit: false);
        AddChurchAction("personality.religious_study", null, null, isBenefit: false);
        foreach (var tier in ChurchDonationTiers)
            AddChurchAction("church.donate", tier, TitleCaseTier(tier), isBenefit: false);
        foreach (var tier in ChurchDonationTiers)
            AddChurchAction("church.aid_poor_family", tier, TitleCaseTier(tier), isBenefit: false);
        AddChurchAction("church.ask_welfare", null, null, isBenefit: true);

        return result;

        void AddChurchAction(
            string actionId,
            string? tier,
            string? optionLabel,
            bool isBenefit)
        {
            if (!definitions.TryGetValue(actionId, out var action))
                return;

            var parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (!string.IsNullOrWhiteSpace(tier))
                parameters["churchTier"] = tier;

            var evaluation = _actionRegistry.Evaluate(
                actionId,
                actor,
                actor,
                parameters);

            decimal? amount = null;
            if (evaluation.PresentationMetadata.TryGetValue("amount", out var amountText)
                && decimal.TryParse(
                    amountText,
                    System.Globalization.NumberStyles.Number,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out var parsedAmount))
            {
                amount = parsedAmount;
                parameters["churchAmount"] = parsedAmount.ToString(
                    System.Globalization.CultureInfo.InvariantCulture);
            }
            else if (action.DisplayCost is decimal displayCost)
            {
                amount = displayCost;
            }

            var description = action.Description;
            if (evaluation.PresentationMetadata.TryGetValue("successChance", out var chanceText)
                && double.TryParse(
                    chanceText,
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out var successChance))
            {
                description += $" Current success chance: {successChance:P0}.";
            }

            result.Add(new TownAffairsChurchActionViewModel(
                action.Id,
                string.IsNullOrWhiteSpace(optionLabel)
                    ? action.Label
                    : $"{action.Label} — {optionLabel}",
                description,
                amount,
                isBenefit,
                evaluation.Available,
                evaluation.Reason,
                parameters));
        }
    }


    internal TownAffairsCourtViewModel? GetTownAffairsCourtModel(
        TownLifeSnapshot snapshot,
        IPerson subject)
    {
        if (_justiceService is null || subject.Age < 18)
            return null;

        var court = snapshot.Institutions.Find("court");
        var hasLocalCourt = court?.IsAvailable == true;
        var courtText = hasLocalCourt
            ? $"{court!.TierName} (Tier {court.Tier})"
            : "No local court. Criminal matters are handled by outside authorities.";
        var protection = _justiceService.GetCourtProtection(subject);
        var status = _justiceService.GetStatus(subject);
        var protectionText =
            $"Court Protection: {protection.DisplayName} · sentence ×{protection.SentenceMultiplier:0.00}";
        var helperText = protection.HelperPersonId.HasValue
            ? $"Protected by {protection.HelperName} — {protection.HelperCareerName}, Level {protection.HelperJobLevel}"
            : string.Empty;
        var imprisonmentText = !status.IsImprisoned
            ? "Not imprisoned."
            : status.IsLifeSentence
                ? $"Imprisoned · {status.RemainingYears} years remaining (Life)."
                : $"Imprisoned · {status.RemainingYears} {(status.RemainingYears == 1 ? "year" : "years")} remaining.";
        var bailCostText = status.IsImprisoned
            ? $"Bail: {_justiceService.GetBailCost(subject):N0} zł"
            : string.Empty;
        var stolenRisk =
            $"Selling a stolen Heirloom: {_justiceService.GetStolenHeirloomSaleDetectionChance(subject):P0} detection risk. Keeping it is harmless.";

        var actor = _succession.ActiveController;
        var actions = new List<TownAffairsCourtActionViewModel>();
        if (actor is not null)
        {
            foreach (var definition in _actionRegistry
                .GetCandidateActions(actor, subject)
                .Where(action => TownAffairsCourtActionIds.Contains(action.Id)))
            {
                var evaluation = _actionRegistry.Evaluate(
                    definition.Id,
                    actor,
                    subject);
                actions.Add(new TownAffairsCourtActionViewModel(
                    definition.Id,
                    definition.Label,
                    definition.Description,
                    evaluation.Available,
                    evaluation.Reason));
            }
        }

        return new TownAffairsCourtViewModel(
            courtText,
            hasLocalCourt,
            protectionText,
            helperText,
            imprisonmentText,
            bailCostText,
            stolenRisk,
            status.KnownCriminalRecord,
            actions);
    }

    internal void QueueTownAffairsCourtAction(
        string actionId,
        IPerson target)
    {
        if (!TownAffairsCourtActionIds.Contains(actionId))
            return;

        var actor = _succession.ActiveController;
        if (actor is null || _succession.IsGameOver)
            return;

        var result = _actionRegistry.Execute(actionId, actor, target);
        if (!result.Success && !string.IsNullOrWhiteSpace(result.Message))
            PersistenceStatusText = result.Message;

        RefreshPeople();
        RefreshAlbum();
        RefreshEconomy();
        RefreshCareer();
        RefreshJustice();
        RefreshNarrative();
        RefreshActions();
    }

    internal TownAffairsCivicOfficeActionViewModel?
        GetTownAffairsCivicOfficeAction(TownLifeSnapshot snapshot)
    {
        var actor = _succession.ActiveController;
        if (actor is null
            || snapshot.CivicOffice?.PersonId != actor.Id)
        {
            return null;
        }

        var definition = _actionRegistry
            .GetCandidateActions(actor, actor)
            .FirstOrDefault(action => action.Id.Equals(
                TownAffairsOfficeDutiesActionId,
                StringComparison.OrdinalIgnoreCase));
        if (definition is null)
            return null;

        var evaluation = _actionRegistry.Evaluate(
            TownAffairsOfficeDutiesActionId,
            actor,
            actor);
        return new TownAffairsCivicOfficeActionViewModel(
            definition.Id,
            definition.Label,
            definition.Description,
            evaluation.Available,
            evaluation.Reason);
    }

    internal void QueueTownAffairsOfficeDuties(
        TownAffairsCivicOfficeActionViewModel action)
    {
        if (!action.IsAvailable)
            return;

        var actor = _succession.ActiveController;
        if (actor is null || _succession.IsGameOver)
            return;

        var result = _actionRegistry.Execute(
            TownAffairsOfficeDutiesActionId,
            actor,
            actor);
        if (!result.Success && !string.IsNullOrWhiteSpace(result.Message))
            PersistenceStatusText = result.Message;

        RefreshPeople();
        RefreshAlbum();
        RefreshEconomy();
        RefreshCareer();
        RefreshNarrative();
        RefreshActions();
    }

    internal IReadOnlyList<TownAffairsCommunityProposalViewModel>
        GetTownAffairsCommunityProposals(TownLifeSnapshot snapshot)
    {
        var actor = _succession.ActiveController;
        if (actor is null)
            return [];

        var definition = _actionRegistry
            .GetCandidateActions(actor, actor)
            .FirstOrDefault(action => action.Id.Equals(
                TownAffairsCommunityLobbyActionId,
                StringComparison.OrdinalIgnoreCase));
        if (definition is null)
            return [];

        return snapshot.CommunityAffairs.Proposals
            .Select(proposal =>
            {
                var parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["communityTownId"] = proposal.TownId,
                    ["communityProposalYear"] = proposal.ProposalYear.ToString(
                        System.Globalization.CultureInfo.InvariantCulture),
                    ["communityPolicyId"] = proposal.PolicyId
                };
                var evaluation = _actionRegistry.Evaluate(
                    TownAffairsCommunityLobbyActionId,
                    actor,
                    actor,
                    parameters);
                double? bonus = null;
                if (evaluation.PresentationMetadata.TryGetValue("supportBonus", out var rawBonus)
                    && double.TryParse(
                        rawBonus,
                        System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture,
                        out var parsedBonus))
                {
                    bonus = parsedBonus;
                }

                return new TownAffairsCommunityProposalViewModel(
                    proposal,
                    evaluation.Available,
                    evaluation.Reason,
                    bonus,
                    parameters);
            })
            .ToArray();
    }

    internal IReadOnlyList<TownAffairsActivePolicyViewModel>
        GetTownAffairsActiveCommunityPolicies(TownLifeSnapshot snapshot) =>
        snapshot.CommunityAffairs.ActivePolicies
            .Select(policy => new TownAffairsActivePolicyViewModel(
                policy,
                policy.RemainingYears(_gameState.Year)))
            .ToArray();

    internal void QueueTownAffairsCommunityLobby(
        TownAffairsCommunityProposalViewModel proposal)
    {
        if (!proposal.IsAvailable)
            return;

        var actor = _succession.ActiveController;
        if (actor is null || _succession.IsGameOver)
            return;

        var result = _actionRegistry.Execute(
            TownAffairsCommunityLobbyActionId,
            actor,
            actor,
            proposal.Parameters);
        if (!result.Success && !string.IsNullOrWhiteSpace(result.Message))
            PersistenceStatusText = result.Message;

        RefreshPeople();
        RefreshAlbum();
        RefreshEconomy();
        RefreshCareer();
        RefreshNarrative();
        RefreshActions();
    }

    internal void QueueTownAffairsChurchAction(
        TownAffairsChurchActionViewModel action)
    {
        if (!TownAffairsChurchActionIds.Contains(action.ActionId)
            || !action.IsAvailable)
        {
            return;
        }

        var actor = _succession.ActiveController;
        if (actor is null || _succession.IsGameOver)
            return;

        var result = _actionRegistry.Execute(
            action.ActionId,
            actor,
            actor,
            action.Parameters);
        if (!result.Success && !string.IsNullOrWhiteSpace(result.Message))
            PersistenceStatusText = result.Message;

        RefreshPeople();
        RefreshAlbum();
        RefreshHealth();
        RefreshEconomy();
        RefreshEducation();
        RefreshCareer();
        RefreshJustice();
        RefreshNarrative();
        RefreshActions();
    }

    private static string TitleCaseTier(string tier) =>
        System.Globalization.CultureInfo.CurrentCulture.TextInfo
            .ToTitleCase(tier);

    internal void QueueTownAffairsHealthAction(
        string actionId,
        IPerson target)
    {
        if (!TownAffairsHealthActionIds.Contains(actionId))
            return;

        var actor = _succession.ActiveController;
        if (actor is null || _succession.IsGameOver)
            return;

        var result = _actionRegistry.Execute(actionId, actor, target);
        if (!result.Success && !string.IsNullOrWhiteSpace(result.Message))
            PersistenceStatusText = result.Message;

        RefreshPeople();
        RefreshAlbum();
        RefreshHealth();
        RefreshEconomy();
        RefreshEducation();
        RefreshCareer();
        RefreshJustice();
        RefreshNarrative();
        RefreshActions();
    }
}
