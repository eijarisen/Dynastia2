using Dynastia.Contracts;

namespace Dynastia.App.ViewModels;

public sealed partial class MainWindowViewModel
{
    public bool HasFamilyRelations =>
        IsGameStarted
        && IsLineageFamilyView
        && _succession.ActiveController is not null
        && _familyRelationService is not null;

    internal string GetFamilyRelationsActiveHouseholdText()
    {
        var head = _succession.ActiveController;
        if (head is null)
            return "Selected household: None";

        var name = _familyService is null
            ? $"{head.Name} {head.Surname}"
            : _familyService.GetDisplayName(head);

        return $"Selected household: {name}";
    }

    internal IReadOnlyList<FamilyRelationHouseholdViewModel> GetFamilyRelationsHouseholds()
    {
        var actor = _succession.ActiveController;
        if (actor is null
            || _familyRelationService is null
            || _familyService is null
            || _householdService is null
            || _economyService is null)
        {
            return [];
        }

        _familyRelationService.ReconcileAll();
        var parameters = new Dictionary<string, string>
        {
            ["familyRelations"] = "true"
        };

        return _familyRelationService.GetRelatedHouseholds(actor)
            .Select(info =>
            {
                var primary = info.PrimaryRelation;
                var relative = _gameState.People.FirstOrDefault(p => p.Id == primary.RelativeId);
                if (relative is null)
                    return null;

                var targetHead = info.HeadId.HasValue
                    ? _gameState.People.FirstOrDefault(p => p.Id == info.HeadId.Value)
                    : null;
                if (targetHead is null)
                    return null;

                var finance = _economyService.GetHousehold(targetHead);
                if (finance is null)
                    return null;

                var members = _economyService.GetHouseholdMemberIds(targetHead)
                    .Select(id => _gameState.People.FirstOrDefault(person => person.Id == id))
                    .Where(person => person is not null
                        && person.Tags.Has("state.alive")
                        && !person.Tags.Has("role.nanny")
                        && !person.Tags.Has("role.family_nanny"))
                    .Cast<IPerson>()
                    .OrderBy(person => person.Id == targetHead.Id ? 0 : 1)
                    .ThenBy(person => person.Id == relative.Id ? 0 : 1)
                    .ThenBy(person => person.Age)
                    .ThenBy(person => person.Id)
                    .ToArray();

                var memberModels = members
                    .Select(member =>
                        FormatRelationHouseholdMember(member, info))
                    .ToList();

                var townText = _locationService is null
                    ? "Town: Unknown"
                    : $"Town: {_locationService.GetLocation(targetHead).HomeTown.Town}";
                var wealthText = $"Wealth: {finance.Wealth:N0} zł";
                var housesText = $"Houses: {_economyService.GetHouses(targetHead).Count}";
                var farmlandText = $"Farmland: {_economyService.GetFarmland(targetHead).Count} parcels";

                var actionModels = _actionRegistry
                    .GetAvailableActions(actor, relative, parameters)
                    .Where(action => action.Id.StartsWith("family_relations.", StringComparison.OrdinalIgnoreCase))
                    .OrderBy(action => RelationActionOrder(action.Id))
                    .Select(action => new FamilyRelationActionViewModel(
                        action.Id,
                        action.Label,
                        action.Description,
                        relative.Id,
                        action.Id.Equals(
                            "family_relations.give_house",
                            StringComparison.OrdinalIgnoreCase),
                        action.Id.Equals(
                            "family_relations.ask_money",
                            StringComparison.OrdinalIgnoreCase)
                        || action.Id.Equals(
                            "family_relations.give_money",
                            StringComparison.OrdinalIgnoreCase),
                        null))
                    .ToList();

                return new FamilyRelationHouseholdViewModel(
                    targetHead.Id,
                    relative.Id,
                    targetHead.Tags.Has("control.playable"),
                    NormalizeKinshipLabel(primary.Kinship),
                    _familyService.GetDisplayName(relative),
                    primary.FamiliarityState,
                    primary.SympathyState,
                    memberModels,
                    townText,
                    wealthText,
                    housesText,
                    farmlandText,
                    actionModels);
            })
            .Where(item => item is not null)
            .Cast<FamilyRelationHouseholdViewModel>()
            .ToList();
    }

    internal bool SelectPlayableFamilyRelationHousehold(
        Guid householdHeadId,
        Guid relativeId)
    {
        var head =
            _gameState.People.FirstOrDefault(
                person => person.Id == householdHeadId);

        if (head is null
            || !head.Tags.Has("state.alive")
            || !head.Tags.Has("control.playable"))
        {
            return false;
        }

        SwitchActiveHousehold(householdHeadId);
        SelectFamilyMember(relativeId);
        return _succession.ActiveController?.Id == householdHeadId;
    }

    internal IReadOnlyList<PropertySelectionOption> GetFamilyRelationGiveHouseOptions(Guid relativeId)
    {
        var actor = _succession.ActiveController;
        if (actor is null || _economyService is null)
            return [];

        return _economyService.GetHouses(actor)
            .Where(house => house.IsRented)
            .Select(house => new PropertySelectionOption(
                house.Id.ToString(),
                house.Town.Town,
                house.Town.County,
                $"{house.Town.SettlementClassDisplayName} — rented investment",
                $"Value {_economyService.GetHouseSaleValue(house.Town):N0} zł",
                $"{house.Town.Town} {house.Town.County} {house.Town.RegionId}"))
            .ToList();
    }

    internal decimal GetFamilyRelationMoneyMaximum(
        Guid relativeId,
        string actionId)
    {
        var actor = _succession.ActiveController;
        var relative = _gameState.People.FirstOrDefault(person => person.Id == relativeId);

        if (actor is null
            || relative is null
            || _economyService is null
            || _householdService is null)
        {
            return 0m;
        }

        IPerson? payingHead;

        if (actionId.Equals("family_relations.ask_money", StringComparison.OrdinalIgnoreCase))
        {
            payingHead = _householdService.ResolveHouseholdHead(relative);
        }
        else if (actionId.Equals("family_relations.give_money", StringComparison.OrdinalIgnoreCase))
        {
            payingHead = actor;
        }
        else
        {
            return 0m;
        }

        var finance = payingHead is null
            ? null
            : _economyService.GetHousehold(payingHead);

        if (finance is null)
            return 0m;

        return Math.Floor(Math.Max(0m, finance.Wealth) / 1000m) * 1000m;
    }

    internal GameActionResult QueueFamilyRelationAction(
        Guid relativeId,
        string actionId,
        string? propertyId,
        decimal? moneyAmount)
    {
        var actor = _succession.ActiveController;
        var relative = _gameState.People.FirstOrDefault(p => p.Id == relativeId);
        if (actor is null || relative is null)
            return new GameActionResult(false, "The family relationship is no longer available.");

        var parameters = new Dictionary<string, string>
        {
            ["familyRelations"] = "true"
        };
        if (!string.IsNullOrWhiteSpace(propertyId))
            parameters["propertyId"] = propertyId;

        if (moneyAmount is decimal amount)
        {
            parameters["amount"] = amount.ToString(
                System.Globalization.CultureInfo.InvariantCulture);
        }

        var result = _actionRegistry.Execute(actionId, actor, relative, parameters);
        RefreshActions();
        RefreshFamilySection();
        OnPropertyChanged(nameof(HasQueuedAction));
        OnPropertyChanged(nameof(QueuedActionText));
        return result;
    }

    private FamilyRelationMemberViewModel FormatRelationHouseholdMember(
        IPerson member,
        RelatedFamilyHouseholdInfo info)
    {
        var kinship = ResolveRelationHouseholdKinship(member, info);
        var name = _familyService?.GetDisplayName(member)
            ?? $"{member.Name} {member.Surname}";
        var career = _careerService?.GetCareer(member);
        var careerText = career is null
            ? string.Empty
            : _farmingService?.IsWorkingFarmWorker(member, member) == true
                ? "Farm Worker"
                : career.IsEmployed && career.JobLevel > 0
                    ? $"{career.JobTitle} ({career.JobLevel})"
                    : career.JobTitle;

        var text = string.IsNullOrWhiteSpace(careerText)
            ? $"{kinship} — {name}"
            : $"{kinship} — {name} — {careerText}";

        var portrait = _appearanceService is not null
            ? _appearanceService.GetPortrait(
                member,
                useDeadOverride: false)
            : ResolveRelationPortraitFallback(member);

        return new FamilyRelationMemberViewModel(
            portrait,
            text);
    }

    private string ResolveRelationHouseholdKinship(
        IPerson member,
        RelatedFamilyHouseholdInfo info)
    {
        var direct = info.Relations.FirstOrDefault(link => link.RelativeId == member.Id);
        if (direct is not null)
            return NormalizeKinshipLabel(direct.Kinship);

        if (_familyService is null)
            return "Household member";

        foreach (var link in info.Relations)
        {
            var relative = _gameState.People.FirstOrDefault(person => person.Id == link.RelativeId);
            if (relative is null)
                continue;

            if (_familyService.GetSpouse(relative)?.Id == member.Id)
                return ResolveInLawKinship(link.Kinship, _familyService.GetSex(member));

            if (_familyService.GetChildren(relative).Any(child => child.Id == member.Id))
                return ResolveDescendantKinship(link.Kinship, _familyService.GetSex(member));
        }

        return "Household member";
    }

    private static string ResolveInLawKinship(string directKinship, Sex sex) => directKinship switch
    {
        "Son" or "Daughter" => sex == Sex.Male ? "Son-in-law" : "Daughter-in-law",
        "Brother" or "Sister" => sex == Sex.Male ? "Brother-in-law" : "Sister-in-law",
        "Father" or "Mother" => sex == Sex.Male ? "Stepfather" : "Stepmother",
        _ => sex == Sex.Male ? "Uncle" : "Aunt"
    };

    private static string ResolveDescendantKinship(string directKinship, Sex sex) => directKinship switch
    {
        "Son" or "Daughter" => sex == Sex.Male ? "Grandson" : "Granddaughter",
        "Brother" or "Sister" => sex == Sex.Male ? "Nephew" : "Niece",
        _ => sex == Sex.Male ? "Male relative" : "Female relative"
    };

    private static string NormalizeKinshipLabel(string kinship)
    {
        if (kinship.Equals(
                "First cousin",
                StringComparison.OrdinalIgnoreCase))
        {
            return "Cousin";
        }

        if (kinship.Equals(
                "Female relative by marriage",
                StringComparison.OrdinalIgnoreCase))
        {
            return "Aunt";
        }

        if (kinship.Equals(
                "Male relative by marriage",
                StringComparison.OrdinalIgnoreCase))
        {
            return "Uncle";
        }

        return kinship;
    }

    private string ResolveRelationPortraitFallback(IPerson person)
    {
        var sex = _familyService?.GetSex(person)
            ?? (person.Tags.Has("sex.female")
                ? Sex.Female
                : Sex.Male);

        if (person.Age <= 4)
            return "👶🏻";
        if (person.Age <= 11)
            return sex == Sex.Male ? "👦🏻" : "👧🏻";
        if (person.Age <= 17)
            return "🧑🏻";
        if (person.Age >= 70)
            return sex == Sex.Male ? "👴🏻" : "👵🏻";

        return sex == Sex.Male ? "👨🏻" : "👩🏻";
    }

    private static int RelationActionOrder(string id) => id switch
    {
        "family_relations.improve" => 0,
        "family_relations.give_money" => 10,
        "family_relations.ask_money" => 20,
        "family_relations.give_house" => 30,
        "family_relations.ask_house" => 40,
        "family_relations.give_farmland" => 45,
        "family_relations.ask_farmland" => 46,
        "family_relations.give_job_help" => 50,
        "family_relations.ask_job_help" => 60,
        _ => 100
    };
}
