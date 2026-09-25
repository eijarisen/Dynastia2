using System.Security.Cryptography;
using System.Text;
using Dynastia.Contracts;
using static Dynastia.App.ViewModels.Actions.ActionSurfaceDefinitions;

namespace Dynastia.App.ViewModels;

public sealed partial class MainWindowViewModel
{
    private static readonly string[] ChurchDonationTiers =
        ["modest", "generous", "major"];

    private const string TownAffairsCommunityLobbyActionId =
        "community.lobby_policy";

    private const string TownAffairsOfficeDutiesActionId =
        "community.perform_office_duties";

    public string TownLifeNavigationLabel =>
        _actionSurfaces.GetTownLifeNavigationLabel(FindSelectedPerson() ?? GetDisplayedHouseholdHead());

    internal bool CanOpenTownAffairs =>
        _actionSurfaces.CanOpenTownAffairs(_succession.ActiveController, FindSelectedPerson());

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
            "education.private_tutor" => TownAffairsTab.Education,
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

    internal IReadOnlyList<TownAffairsFarmlandOfferViewModel>
        GetTownAffairsFarmlandOffers(TownInfo town)
    {
        var actor = _succession.ActiveController;
        if (actor is null || _farmingService is null || _economyService is null)
            return [];

        var residenceTown = _economyService.GetResidenceTown(actor);
        if (!residenceTown.Id.Equals(town.Id, StringComparison.OrdinalIgnoreCase))
            return [];

        var askingPrice = _farmingService.GetPurchasePrice(town, _gameState.Year);
        var parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["townId"] = town.Id,
            ["farmlandOfferYear"] = _gameState.Year.ToString(
                System.Globalization.CultureInfo.InvariantCulture),
            ["farmlandAskingPrice"] = askingPrice.ToString(
                System.Globalization.CultureInfo.InvariantCulture),
            ["summaryTown"] = town.Town,
            ["summaryPrice"] = askingPrice.ToString(
                System.Globalization.CultureInfo.InvariantCulture)
        };
        var evaluation = _actionRegistry.Evaluate(
            "farming.buy_farmland",
            actor,
            actor,
            parameters);

        return
        [
            new TownAffairsFarmlandOfferViewModel(
                askingPrice,
                evaluation.Available,
                evaluation.Reason)
        ];
    }

    internal void QueueTownAffairsFarmlandPurchase(
        TownAffairsFarmlandOfferViewModel offer)
    {
        var actor = _succession.ActiveController;
        if (actor is null || _succession.IsGameOver || _economyService is null)
            return;

        var town = _economyService.GetResidenceTown(actor);
        var parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["townId"] = town.Id,
            ["farmlandOfferYear"] = _gameState.Year.ToString(
                System.Globalization.CultureInfo.InvariantCulture),
            ["farmlandAskingPrice"] = offer.AskingPrice.ToString(
                System.Globalization.CultureInfo.InvariantCulture),
            ["summaryTown"] = town.Town,
            ["summaryPrice"] = offer.AskingPrice.ToString(
                System.Globalization.CultureInfo.InvariantCulture)
        };

        var result = _actionRegistry.Execute(
            "farming.buy_farmland",
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

    internal string GetTownAffairsRenownLabel(double value)
    {
        var label = _statusService?.GetRenownLabel(value);
        return string.IsNullOrWhiteSpace(label) ? "Unknown" : label;
    }

    internal string GetTownAffairsReputationLabel(double value)
    {
        var label = _statusService?.GetReputationLabel(value);
        return string.IsNullOrWhiteSpace(label) ? "Unknown" : label;
    }

    internal decimal GetTownAffairsCurrentFunds()
    {
        var actor = _succession.ActiveController;
        return actor is null
            ? 0m
            : _economyService?.GetHousehold(actor)?.Wealth ?? 0m;
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
            $"Health: {health.Current:N0} / {health.Maximum:N0}",
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
                    StringComparison.OrdinalIgnoreCase))
            .Select(item => new TownAffairsHealthActionViewModel(
                item.Action.Id,
                item.Action.Label,
                item.Action.Description,
                ActionEmojiMap.GetEmoji(item.Action),
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

        AddChurchAction("church.attend", isBenefit: false);
        AddChurchAction("personality.religious_study", isBenefit: false);
        AddChurchMoneyAction("church.donate");
        AddChurchMoneyAction("church.aid_poor_family");
        AddChurchAction("church.ask_welfare", isBenefit: true);

        return result;

        void AddChurchMoneyAction(string actionId)
        {
            if (!definitions.TryGetValue(actionId, out var action))
                return;

            var parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["churchTier"] = ChurchDonationTiers[0]
            };
            var evaluation = _actionRegistry.Evaluate(
                actionId,
                actor,
                actor,
                parameters);

            var minimum = 100m;
            if (evaluation.PresentationMetadata.TryGetValue("amount", out var rawMinimum)
                && decimal.TryParse(
                    rawMinimum,
                    System.Globalization.NumberStyles.Number,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out var parsedMinimum))
            {
                minimum = parsedMinimum;
            }

            var rawMaximum = _economyService?.GetHousehold(actor)?.Wealth ?? 0m;
            var maximum = Math.Floor(Math.Max(0m, rawMaximum) / 100m) * 100m;
            var description = action.Description
                + " Choose the amount with the money slider; larger gifts qualify for stronger donation effects.";

            result.Add(new TownAffairsChurchActionViewModel(
                action.Id,
                action.Label,
                description,
                ActionEmojiMap.GetEmoji(action),
                null,
                false,
                evaluation.Available,
                evaluation.Reason,
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                RequiresMoneySelection: true,
                MinimumAmount: minimum,
                MaximumAmount: Math.Max(0m, maximum)));
        }

        void AddChurchAction(
            string actionId,
            bool isBenefit)
        {
            if (!definitions.TryGetValue(actionId, out var action))
                return;

            var parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
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
                action.Label,
                description,
                ActionEmojiMap.GetEmoji(action),
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
        var protectionPercent = Math.Clamp(
            1m - protection.SentenceMultiplier,
            0m,
            1m);
        var protectionText = $"Court protection: {protectionPercent:P0}";
        var relatives = _justiceService.GetCourtProtectionRelatives(subject);
        var status = _justiceService.GetStatus(subject);

        return new TownAffairsCourtViewModel(
            courtText,
            hasLocalCourt,
            protectionText,
            relatives,
            status.KnownCriminalRecord);
    }


    internal TownAffairsCivicOfficeActionViewModel? GetTownAffairsCivicOfficeAction(
        TownLifeSnapshot snapshot)
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
            ActionEmojiMap.GetEmoji(definition),
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
                    GetTownAffairsCommunityProposerPortrait(proposal.Proposer),
                    evaluation.Available,
                    evaluation.Reason,
                    bonus,
                    parameters);
            })
            .ToArray();
    }

    internal string GetTownAffairsCivicOfficePortrait(
        CivicOfficeHeadInfo? office)
    {
        if (office is null)
            return string.Empty;

        if (office.PersonId is Guid personId
            && _gameState.People.FirstOrDefault(person => person.Id == personId) is { } person)
        {
            return _appearanceService?.GetPortrait(person)
                ?? (office.Sex == Sex.Male ? "👨🏻" : "👩🏻");
        }

        if (_appearanceService is null)
            return office.Sex == Sex.Male ? "👨🏻" : "👩🏻";

        var seed = StablePortraitGuid(
            $"civic|{office.TownId}|{office.Name}|{office.BirthYear}|{office.OfficeStartYear}");
        var appearance = _appearanceService.GenerateCandidateAppearance(seed, office.Sex);
        return _appearanceService.GetPortrait(
            appearance,
            office.Sex,
            office.Age(_gameState.Year),
            seed);
    }

    private string GetTownAffairsCommunityProposerPortrait(
        CommunityPolicyProposerInfo proposer)
    {
        if (_appearanceService is null)
            return proposer.Sex == Sex.Male ? "👨🏻" : "👩🏻";

        var seed = Guid.TryParse(proposer.Id, out var parsed)
            ? parsed
            : StablePortraitGuid(proposer.Id);
        var appearance = _appearanceService.GenerateCandidateAppearance(seed, proposer.Sex);
        return _appearanceService.GetPortrait(
            appearance,
            proposer.Sex,
            proposer.Age,
            seed);
    }

    private static Guid StablePortraitGuid(string key)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(key));
        return new Guid(bytes.AsSpan(0, 16));
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
        TownAffairsChurchActionViewModel action,
        decimal? selectedAmount = null)
    {
        if (!TownAffairsChurchActionIds.Contains(action.ActionId)
            || !action.IsAvailable)
        {
            return;
        }

        var actor = _succession.ActiveController;
        if (actor is null || _succession.IsGameOver)
            return;

        var parameters = new Dictionary<string, string>(
            action.Parameters,
            StringComparer.OrdinalIgnoreCase);

        if (action.RequiresMoneySelection)
        {
            if (selectedAmount is not decimal amount
                || amount < action.MinimumAmount
                || amount > action.MaximumAmount)
            {
                return;
            }

            var tier = ResolveChurchDonationTier(
                action.ActionId,
                actor,
                amount);
            if (tier is null)
                return;

            parameters["churchTier"] = tier;
            parameters["churchAmount"] = amount.ToString(
                System.Globalization.CultureInfo.InvariantCulture);

            var evaluation = _actionRegistry.Evaluate(
                action.ActionId,
                actor,
                actor,
                parameters);
            if (!evaluation.Available)
            {
                PersistenceStatusText = evaluation.Reason ?? "The Church action is no longer available.";
                return;
            }
        }

        var result = _actionRegistry.Execute(
            action.ActionId,
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


    private string? ResolveChurchDonationTier(
        string actionId,
        IPerson actor,
        decimal selectedAmount)
    {
        string? chosen = null;
        foreach (var tier in ChurchDonationTiers)
        {
            var parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["churchTier"] = tier
            };
            var evaluation = _actionRegistry.Evaluate(
                actionId,
                actor,
                actor,
                parameters);
            if (!evaluation.PresentationMetadata.TryGetValue("amount", out var raw)
                || !decimal.TryParse(
                    raw,
                    System.Globalization.NumberStyles.Number,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out var threshold))
            {
                continue;
            }

            if (selectedAmount >= threshold)
                chosen = tier;
        }

        return chosen;
    }
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
