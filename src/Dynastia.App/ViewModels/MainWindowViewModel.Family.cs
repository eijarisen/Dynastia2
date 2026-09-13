using System.Collections.ObjectModel;
using Avalonia.Threading;
using Dynastia.App.Persistence;
using Dynastia.Contracts;
using Dynastia.Core.Simulation;

namespace Dynastia.App.ViewModels;

public sealed partial class MainWindowViewModel
{
    private void RefreshPeople()
    {
        var selectedId =
            _selectionService.SelectedPersonId;

        People.Clear();

        foreach (var person in
            _gameState.People)
        {
            People.Add(
                new PersonRowViewModel(
                    person,
                    _familyService,
                    _healthService,
                    _economyService,
                    _careerService,
                    _householdService,
                    _locationService));
        }

        SelectedPerson =
            selectedId is null
                ? People.FirstOrDefault()
                : People.FirstOrDefault(
                    x => x.Id == selectedId)
                    ?? People.FirstOrDefault();

        RefreshSelectedStats();
        RefreshFamilyDetails();
        RefreshHealth();
        RefreshEconomy();
        RefreshEducation();
        RefreshCareer();
        RefreshJustice();
        RefreshNarrative();
        RefreshActions();
        RefreshFamilySection();
    }

    private void RefreshFamilySection()
    {
        PlayableTabs.Clear();
        BloodlineTabs.Clear();
        HouseholdMembers.Clear();
        ExtendedFamilyMembers.Clear();
        DeceasedFamilyMembers.Clear();

        var selectedId =
            SelectedPerson?.Id;

        var households =
            _householdService?
                .GetActiveHouseholds()
            ?? Array.Empty<HouseholdInfo>();

        var active =
            _succession.ActiveController;

        foreach (var household in
            households
                .Where(
                    household =>
                        household.Class
                        == HouseholdClass.Lineage)
                .OrderBy(
                    household =>
                        household.Generation
                        ?? int.MaxValue)
                .ThenBy(
                    GetHouseholdHeadBirthSortKey)
                .ThenBy(
                    household =>
                        household.HeadId))
        {
            var head =
                _gameState.People
                    .FirstOrDefault(
                        person =>
                            person.Id
                            == household.HeadId);

            if (head is null)
                continue;

            var queuedActions =
                _actionRegistry
                    .GetQueuedActions(
                        head);

            var summary =
                $"{household.HeadName}" +
                Environment.NewLine +
                BuildHouseholdActionSummary(
                    queuedActions.FirstOrDefault());

            PlayableTabs.Add(
                new PlayablePersonTabViewModel(
                    head.Id,
                    household.Generation is int generation
                        ? $"G{generation}"
                        : string.Empty,
                    $"{head.Name} ({head.Age})",
                    household.HeadName,
                    summary,
                    active?.Id
                        == head.Id,
                    queuedActions.Count > 0,
                    SwitchActiveHousehold));
        }

        foreach (var household in
            households
                .Where(
                    household =>
                        household.Class
                        == HouseholdClass.Bloodline)
                .OrderBy(
                    household =>
                        household.Generation
                        ?? int.MaxValue)
                .ThenBy(
                    household =>
                        household.Surname,
                    StringComparer.OrdinalIgnoreCase)
                .ThenBy(
                    GetHouseholdHeadBirthSortKey)
                .ThenBy(
                    household =>
                        household.HouseholdId))
        {
            var head =
                _gameState.People
                    .FirstOrDefault(
                        person =>
                            person.Id
                            == household.HeadId);

            if (head is null)
                continue;

            BloodlineTabs.Add(
                new PlayablePersonTabViewModel(
                    head.Id,
                    household.Generation is int generation
                        ? $"G{generation}"
                        : string.Empty,
                    household.Surname,
                    household.HeadName,
                    $"{household.HeadName}" +
                    Environment.NewLine +
                    "This household lives independently.",
                    _inspectedBloodlineHouseholdId
                        == household.HouseholdId,
                    false,
                    InspectBloodlineHousehold));
        }

        if (IsBloodlineFamilyView)
        {
            var availableIds =
                households
                    .Where(
                        household =>
                            household.Class
                            == HouseholdClass.Bloodline)
                    .Select(
                        household =>
                            household.HouseholdId)
                    .ToHashSet();

            if (_inspectedBloodlineHouseholdId is null
                || !availableIds.Contains(
                    _inspectedBloodlineHouseholdId.Value))
            {
                _inspectedBloodlineHouseholdId =
                    availableIds
                        .Cast<Guid?>()
                        .FirstOrDefault();
            }
        }

        var displayed =
            GetDisplayedHouseholdHead(
                households);

        if (IsLivingFamilyView
            && displayed is not null)
        {
            var info =
                households.FirstOrDefault(
                    household =>
                        household.HeadId
                        == displayed.Id);

            if (info is not null)
            {
                foreach (var member in
                    info.MemberIds
                        .Select(
                            id =>
                                _gameState.People
                                    .FirstOrDefault(
                                        person =>
                                            person.Id
                                            == id))
                        .Where(
                            person =>
                                person is not null
                                && person.Tags.Has(
                                    "state.alive")
                                && !person.Tags.Has(
                                    "role.nanny"))
                        .Cast<IPerson>()
                        .OrderByDescending(
                            person =>
                                person.Id
                                == displayed.Id)
                        .ThenBy(
                            GetBirthSortYear)
                        .ThenBy(
                            person =>
                                person.Id))
                {
                    AddHouseholdCard(
                        member,
                        selectedId,
                        displayed.Id);
                }
            }
        }

        foreach (var person in
            _gameState.People
                .Where(
                    person =>
                        person.Tags.Has(
                            "state.dead")
                        && !person.Tags.Has(
                            "role.nanny")
                        && IsDynastyRelevantDeceased(
                            person))
                .OrderByDescending(
                    person =>
                        person.DeathDate?.Year
                        ?? int.MinValue)
                .ThenByDescending(
                    person =>
                        person.DeathDate?.Month
                        ?? 1)
                .ThenByDescending(
                    person =>
                        person.DeathDate?.Day
                        ?? 1)
                .ThenByDescending(
                    GetBirthSortYear))
        {
            DeceasedFamilyMembers.Add(
                CreateFamilyCard(
                    person,
                    selectedId,
                    null));
        }

        OnPropertyChanged(
            nameof(HasActiveHousehold));

        OnPropertyChanged(
            nameof(HouseholdBudgetText));

        OnPropertyChanged(
            nameof(HouseholdHousesText));

        OnPropertyChanged(
            nameof(HouseholdHousesDetailsText));

        OnPropertyChanged(
            nameof(HouseholdIncomeText));

        OnPropertyChanged(
            nameof(HouseholdIncomeDetailsText));

        OnPropertyChanged(
            nameof(HouseholdExpensesText));

        OnPropertyChanged(
            nameof(HouseholdExpenseDetailsText));

        OnPropertyChanged(
            nameof(HouseholdIncomeExpensesText));

        OnPropertyChanged(
            nameof(HouseholdWarningText));

        OnPropertyChanged(
            nameof(ExtendedFamilyEmptyText));

        OnPropertyChanged(
            nameof(DeceasedFamilyEmptyText));

        OnPropertyChanged(
            nameof(ActiveHouseholdText));

        OnPropertyChanged(
            nameof(ActionsEmptyText));
    }

    private bool IsDynastyRelevantDeceased(
        IPerson person)
    {
        if (_familyService is null)
            return true;

        if (_familyService.IsBloodline(
            person))
        {
            return true;
        }

        return _familyService
            .GetRelationshipHistory(
                person)
            .Any(
                relationship =>
                {
                    var spouse =
                        _gameState.People
                            .FirstOrDefault(
                                candidate =>
                                    candidate.Id
                                    == relationship.SpouseId);

                    return spouse is not null
                        && _familyService.IsBloodline(
                            spouse);
                });
    }

    private long GetHouseholdHeadBirthSortKey(
        HouseholdInfo household)
    {
        var head =
            _gameState.People
                .FirstOrDefault(
                    person =>
                        person.Id
                        == household.HeadId);

        if (head is null)
            return long.MaxValue;

        var year =
            head.BirthDate?.Year
            ?? GetBirthSortYear(
                head);

        var month =
            head.BirthDate?.Month
            ?? 1;

        var day =
            head.BirthDate?.Day
            ?? 1;

        return year * 10000L
            + month * 100L
            + day;
    }

    private IPerson? GetDisplayedHouseholdHead()
    {
        return GetDisplayedHouseholdHead(
            _householdService?
                .GetActiveHouseholds()
            ?? Array.Empty<HouseholdInfo>());
    }

    private IPerson? GetDisplayedHouseholdHead(
        IReadOnlyList<HouseholdInfo> households)
    {
        if (IsBloodlineFamilyView)
        {
            if (_inspectedBloodlineHouseholdId
                is not Guid inspectedId)
            {
                return null;
            }

            var household =
                households.FirstOrDefault(
                    candidate =>
                        candidate.HouseholdId
                            == inspectedId
                        && candidate.Class
                            == HouseholdClass.Bloodline);

            if (household is null)
                return null;

            return _gameState.People
                .FirstOrDefault(
                    person =>
                        person.Id
                        == household.HeadId);
        }

        if (IsDeceasedFamilyView)
            return null;

        return _succession.ActiveController;
    }

    private void InspectBloodlineHousehold(
        Guid personId)
    {
        var household =
            _householdService?
                .GetActiveHouseholds()
                .FirstOrDefault(
                    candidate =>
                        candidate.Class
                            == HouseholdClass.Bloodline
                        && candidate.HeadId
                            == personId);

        _inspectedBloodlineHouseholdId =
            household?.HouseholdId;

        SetHouseholdViewMode(
            HouseholdViewMode.Bloodline);

        var row =
            People.FirstOrDefault(
                candidate =>
                    candidate.Id
                    == personId);

        if (row is not null)
        {
            SelectedPerson =
                row;
        }

        RefreshFamilySection();
        RefreshActions();
    }

    private void AddHouseholdCard(
        IPerson person,
        Guid? selectedId,
        Guid activeHeadId)
    {
        if (person.Tags.Has(
            "role.nanny"))
        {
            return;
        }

        HouseholdMembers.Add(
            CreateFamilyCard(
                person,
                selectedId,
                activeHeadId));
    }

    private FamilyMemberCardViewModel
        CreateFamilyCard(
            IPerson person,
            Guid? selectedId,
            Guid? activeHeadId)
    {
        return new FamilyMemberCardViewModel(
            person,
            _familyService,
            _healthService,
            _educationService,
            _careerService,
            _justiceService,
            _statsService,
            _locationService,
            _marriageSatisfactionService,
            _childHappinessService,
            _thoughtService,
            selectedId == person.Id,
            activeHeadId == person.Id,
            SelectFamilyMember);
    }

    private string BuildHouseholdActionSummary(
        QueuedActionInfo? queued)
    {
        if (queued is null)
        {
            return
                "No action selected for this household.";
        }

        if (queued.ActionId.Equals(
            "turn.pass",
            StringComparison.OrdinalIgnoreCase))
        {
            return
                $"{ActionEmojiMap.Format(queued.ActionId, queued.Label)}" +
                Environment.NewLine +
                "No planned action for this year.";
        }

        var target =
            _gameState.People
                .FirstOrDefault(
                    person =>
                        person.Id
                        == queued.TargetId);

        var targetName =
            target is null
                ? "Unknown"
                : _familyService is null
                    ? $"{target.Name} {target.Surname}"
                    : _familyService.GetDisplayName(
                        target);

        var description =
            string.IsNullOrWhiteSpace(
                queued.Description)
                ? "This action is queued for the next annual pass."
                : queued.Description;

        return
            $"{ActionEmojiMap.Format(queued.ActionId, queued.Label)}" +
            Environment.NewLine +
            $"Target: {targetName}" +
            Environment.NewLine +
            description;
    }

    private int GetBirthSortYear(
        IPerson person)
    {
        return person.BirthDate?.Year
            ?? _gameState.Year
               - person.Age;
    }

    private void SelectFamilyMember(
        Guid personId)
    {
        var row =
            People.FirstOrDefault(
                person =>
                    person.Id == personId);

        if (row is not null)
            SelectedPerson = row;
    }

    private void SwitchActiveHousehold(
        Guid personId)
    {
        var person =
            _gameState.People
                .FirstOrDefault(
                    candidate =>
                        candidate.Id == personId);

        if (person is null
            || !_succession.SetActiveController(
                person))
        {
            return;
        }

        var row =
            People.FirstOrDefault(
                candidate =>
                    candidate.Id == personId);

        if (row is not null)
            SelectedPerson = row;

        SetHouseholdViewMode(
            HouseholdViewMode.Lineage);

        RefreshFamilySection();
        RefreshActions();
    }

}
