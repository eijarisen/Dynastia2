using Avalonia.Media;
using Dynastia.Contracts;

namespace Dynastia.App.ViewModels;

public enum TownAffairsTab
{
    Institutions = 0,
    Housing = 1,
    Jobs = 2,
    Health = 3,
    Education = 4,
    Bank = 5,
    Church = 6,
    Community = 7
}

public enum TownAffairsMode
{
    Resident,
    RemoteHousingBrowse
}

public sealed record TownAffairsRequest(
    string TownId,
    Guid? SubjectPersonId,
    TownAffairsTab InitialTab,
    TownAffairsMode Mode = TownAffairsMode.Resident,
    string? PreferredActionId = null);

public sealed class TownAffairsViewModel : ViewModelBase
{
    private static readonly IBrush DepressedProsperityBrush =
        new SolidColorBrush(Color.FromRgb(166, 59, 50));
    private static readonly IBrush StrugglingProsperityBrush =
        new SolidColorBrush(Color.FromRgb(181, 106, 45));
    private static readonly IBrush StableProsperityBrush =
        new SolidColorBrush(Color.FromRgb(122, 86, 30));
    private static readonly IBrush ProsperousProsperityBrush =
        new SolidColorBrush(Color.FromRgb(63, 122, 69));
    private static readonly IBrush BoomingProsperityBrush =
        new SolidColorBrush(Color.FromRgb(39, 105, 51));

    private readonly MainWindowViewModel _owner;
    private readonly IReadOnlyList<IPerson> _householdPeople;
    private int _selectedTabIndex;

    public TownAffairsViewModel(
        MainWindowViewModel owner,
        TownLifeSnapshot snapshot,
        IPerson? subject,
        TownAffairsRequest request)
    {
        _owner = owner;
        Snapshot = snapshot;
        Request = request;

        HouseOffers = owner.GetTownAffairsHouseOffers(snapshot.Town);
        CanTakeLoan = !IsRemote
            && owner.IsTownAffairsHouseholdActionAvailable("loan.take");
        CanGiveLoan = !IsRemote
            && owner.IsTownAffairsHouseholdActionAvailable("loan.give");

        _householdPeople = IsRemote
            ? []
            : owner.GetTownAffairsHouseholdMembers();
        Subject = subject is not null
            && _householdPeople.Any(person => person.Id == subject.Id)
                ? subject
                : _householdPeople.FirstOrDefault();

        if (request.InitialTab == TownAffairsTab.Jobs
            && Subject is { Age: < 18 })
        {
            Subject = _householdPeople.FirstOrDefault(person => person.Age >= 18)
                ?? Subject;
        }

        CommunityProposals = IsRemote
            ? Array.Empty<TownAffairsCommunityProposalViewModel>()
            : owner.GetTownAffairsCommunityProposals(snapshot);
        ActiveCommunityPolicies = IsRemote
            ? Array.Empty<TownAffairsActivePolicyViewModel>()
            : owner.GetTownAffairsActiveCommunityPolicies(snapshot);
        OfficeDutiesAction = IsRemote
            ? null
            : owner.GetTownAffairsCivicOfficeAction(snapshot);

        RefreshMemberTabs();
        RefreshSubjectContent();

        _selectedTabIndex = IsTabVisible(request.InitialTab)
            ? (int)request.InitialTab
            : (int)TownAffairsTab.Institutions;
    }

    public TownLifeSnapshot Snapshot { get; }

    public TownAffairsRequest Request { get; }

    public IPerson? Subject { get; private set; }

    public IReadOnlyList<TownAffairsMemberTabViewModel> HouseholdMembers { get; private set; }
        = Array.Empty<TownAffairsMemberTabViewModel>();

    public IReadOnlyList<TownAffairsMemberTabViewModel> JobHouseholdMembers { get; private set; }
        = Array.Empty<TownAffairsMemberTabViewModel>();

    public bool ShowHouseholdMemberTabs =>
        !IsRemote && HouseholdMembers.Count > 0;

    public bool ShowJobHouseholdMemberTabs =>
        !IsRemote && JobHouseholdMembers.Count > 0;

    public IBrush ProsperityBrush =>
        Snapshot.Prosperity.Index switch
        {
            <= 84 => DepressedProsperityBrush,
            <= 94 => StrugglingProsperityBrush,
            <= 105 => StableProsperityBrush,
            <= 115 => ProsperousProsperityBrush,
            _ => BoomingProsperityBrush
        };

    public int SelectedTabIndex
    {
        get => _selectedTabIndex;
        set
        {
            var normalized = Math.Clamp(
                value,
                (int)TownAffairsTab.Institutions,
                (int)TownAffairsTab.Community);
            var tab = (TownAffairsTab)normalized;
            if (!IsTabVisible(tab) || _selectedTabIndex == normalized)
                return;

            if (tab == TownAffairsTab.Jobs
                && Subject is { Age: < 18 })
            {
                var adult = _householdPeople.FirstOrDefault(person => person.Age >= 18);
                if (adult is not null)
                {
                    Subject = adult;
                    OnPropertyChanged(nameof(Subject));
                    RefreshMemberTabs();
                    RefreshSubjectContent();
                }
            }

            _selectedTabIndex = normalized;
            OnPropertyChanged();
        }
    }

    public bool IsRemote =>
        Request.Mode == TownAffairsMode.RemoteHousingBrowse;

    public bool ShowJobsTab => !IsRemote;

    public bool ShowHealthTab => !IsRemote;

    public bool ShowEducationTab =>
        !IsRemote && Snapshot.School.IsAvailable;

    public bool ShowBankTab =>
        !IsRemote && Snapshot.Bank.IsAvailable;

    public bool ShowChurchTab =>
        !IsRemote && Snapshot.Church.IsAvailable;

    public bool ShowCommunityTab => !IsRemote;

    public CivicOfficeHeadInfo? CivicOffice => Snapshot.CivicOffice;

    public bool HasCivicOffice => CivicOffice is not null;

    public string CivicOfficePersonText => CivicOffice is null
        ? string.Empty
        : $"{CivicOffice.Name} · age {CivicOffice.Age(Snapshot.Year)}";

    public string CivicOfficeStandingText => CivicOffice is null
        ? string.Empty
        : $"Renown {CivicOffice.Renown:0.#} · Reputation {CivicOffice.Reputation:0.#} · Approval {CivicOffice.Approval:0.#}%";

    public string CivicOfficeTermText => CivicOffice is null
        ? string.Empty
        : $"In office since {CivicOffice.OfficeStartYear}";

    public TownAffairsCivicOfficeActionViewModel? OfficeDutiesAction { get; }

    public bool HasOfficeDutiesAction => OfficeDutiesAction is not null;

    public bool CanPerformOfficeDuties => OfficeDutiesAction?.IsAvailable == true;

    public string? OfficeDutiesUnavailableReason => OfficeDutiesAction?.UnavailableReason;

    public IReadOnlyList<TownAffairsCommunityProposalViewModel> CommunityProposals { get; }
        = Array.Empty<TownAffairsCommunityProposalViewModel>();

    public IReadOnlyList<TownAffairsActivePolicyViewModel> ActiveCommunityPolicies { get; }
        = Array.Empty<TownAffairsActivePolicyViewModel>();

    public bool HasCommunityProposals => CommunityProposals.Count > 0;

    public bool ShowCommunityEmpty => !HasCommunityProposals;

    public bool HasActiveCommunityPolicies => ActiveCommunityPolicies.Count > 0;

    public bool ShowNoActiveCommunityPolicies => !HasActiveCommunityPolicies;

    public IReadOnlyList<TownAffairsHouseOfferViewModel> HouseOffers { get; }

    public bool HasHouseOffers => HouseOffers.Count > 0;

    public bool ShowNoHouseOffers => !HasHouseOffers;

    public string NoHouseOffersText =>
        "No houses are currently offered for sale in this town.";

    public JobOpportunityDialogViewModel? JobModel { get; private set; }

    public string? JobActionId { get; private set; }

    public bool JobActionAvailable { get; private set; }

    public bool HasJobOpportunities =>
        JobModel is { Opportunities.Count: > 0 };

    public bool ShowJobsEmpty => !HasJobOpportunities;

    public string JobsEmptyText =>
        string.IsNullOrWhiteSpace(JobActionId)
            ? "No job-search action applies to this resident."
            : !JobActionAvailable
                ? "Job search is not currently available for this resident."
                : "No suitable vacancies are currently available.";

    public IReadOnlyList<PropertySelectionOption> EducationOptions { get; private set; }
        = Array.Empty<PropertySelectionOption>();

    public string EducationContextText { get; private set; } = string.Empty;

    public bool HasEducationOptions =>
        EducationOptions.Count > 0;

    public bool ShowEducationEmpty => !HasEducationOptions;

    public string EducationEmptyText =>
        "No education option is currently available.";

    public bool CanTakeLoan { get; }

    public bool CanGiveLoan { get; }

    public string BankStatusText =>
        Snapshot.FinanceCapacityText;

    public IReadOnlyList<TownAffairsHealthActionViewModel> HealthActions { get; private set; }
        = Array.Empty<TownAffairsHealthActionViewModel>();

    public bool HasHealthActions => HealthActions.Count > 0;

    public IReadOnlyList<TownAffairsChurchActionViewModel> ChurchActions { get; private set; }
        = Array.Empty<TownAffairsChurchActionViewModel>();

    public bool HasChurchActions => ChurchActions.Count > 0;

    public bool ShowChurchEmpty => !HasChurchActions;

    public bool ShowHealthEmpty => !HasHealthActions;

    public string HealthEmptyText =>
        "No health actions are currently available.";

    public bool HasQueuedAction => _owner.HasQueuedAction;

    public bool IsTabVisible(TownAffairsTab tab) =>
        tab switch
        {
            TownAffairsTab.Institutions => true,
            TownAffairsTab.Housing => true,
            TownAffairsTab.Jobs => ShowJobsTab,
            TownAffairsTab.Health => ShowHealthTab,
            TownAffairsTab.Education => ShowEducationTab,
            TownAffairsTab.Bank => ShowBankTab,
            TownAffairsTab.Church => ShowChurchTab,
            TownAffairsTab.Community => ShowCommunityTab,
            _ => false
        };

    public bool OpenInstitution(string institutionId)
    {
        var tab = institutionId.ToLowerInvariant() switch
        {
            "school" => TownAffairsTab.Education,
            "bank" => TownAffairsTab.Bank,
            "medical" => TownAffairsTab.Health,
            "church" => TownAffairsTab.Church,
            _ => (TownAffairsTab?)null
        };

        if (tab is null || !IsTabVisible(tab.Value))
            return false;

        SelectedTabIndex = (int)tab.Value;
        return true;
    }

    public void SelectSubject(Guid personId)
    {
        if (IsRemote || Subject?.Id == personId)
            return;

        var subject = _householdPeople.FirstOrDefault(person => person.Id == personId);
        if (subject is null)
            return;

        Subject = subject;
        OnPropertyChanged(nameof(Subject));
        RefreshMemberTabs();
        RefreshSubjectContent();
    }

    public IReadOnlyList<LoanOfferInfo> GetLoanOffers(bool isGivingLoan)
    {
        if (!ShowBankTab)
            return [];

        var actionId = isGivingLoan ? "loan.give" : "loan.take";
        var maximumPrincipal = _owner.GetMaximumLoanPrincipal(actionId);
        return _owner.GetLoanOffers(isGivingLoan, maximumPrincipal);
    }

    public void QueueLoan(
        bool isGivingLoan,
        LoanSelectionResult selection)
    {
        if (!ShowBankTab)
            return;

        _owner.QueueLoanAction(
            isGivingLoan ? "loan.give" : "loan.take",
            selection);
    }

    public void QueueHousePurchase(TownAffairsHouseOfferViewModel offer)
    {
        if (!offer.IsAffordable)
            return;

        _owner.QueueTownAffairsHousePurchase(offer.Offer);
    }

    public void QueueJob(JobOpportunityInfo opportunity)
    {
        if (!ShowJobsTab
            || Subject is null
            || string.IsNullOrWhiteSpace(JobActionId))
        {
            return;
        }

        _owner.QueueJobApplication(JobActionId, opportunity, Subject);
    }

    public void QueueEducation(string optionId)
    {
        if (!ShowEducationTab || Subject is null)
            return;

        _owner.QueueEducationAction(optionId, Subject);
    }

    public void QueueHealth(string actionId)
    {
        if (!ShowHealthTab || Subject is null)
            return;

        _owner.QueueTownAffairsHealthAction(actionId, Subject);
    }

    public void QueueChurch(TownAffairsChurchActionViewModel action)
    {
        if (!ShowChurchTab || !action.IsAvailable)
            return;

        _owner.QueueTownAffairsChurchAction(action);
    }

    public void QueueCommunity(TownAffairsCommunityProposalViewModel proposal)
    {
        if (!ShowCommunityTab || !proposal.IsAvailable)
            return;

        _owner.QueueTownAffairsCommunityLobby(proposal);
    }

    public void QueueOfficeDuties(TownAffairsCivicOfficeActionViewModel action)
    {
        if (!ShowCommunityTab || !action.IsAvailable)
            return;

        _owner.QueueTownAffairsOfficeDuties(action);
    }

    private void RefreshMemberTabs()
    {
        HouseholdMembers = _householdPeople
            .Select(CreateMemberTab)
            .ToArray();
        JobHouseholdMembers = _householdPeople
            .Where(person => person.Age >= 18)
            .Select(CreateMemberTab)
            .ToArray();

        OnPropertyChanged(nameof(HouseholdMembers));
        OnPropertyChanged(nameof(JobHouseholdMembers));
        OnPropertyChanged(nameof(ShowHouseholdMemberTabs));
        OnPropertyChanged(nameof(ShowJobHouseholdMemberTabs));
    }

    private TownAffairsMemberTabViewModel CreateMemberTab(IPerson person) =>
        new(
            person.Id,
            _owner.GetTownAffairsSubjectName(person),
            Subject?.Id == person.Id);

    private void RefreshSubjectContent()
    {
        JobActionId = null;
        JobActionAvailable = false;
        JobModel = null;
        EducationOptions = Array.Empty<PropertySelectionOption>();
        EducationContextText = string.Empty;
        HealthActions = Array.Empty<TownAffairsHealthActionViewModel>();
        ChurchActions = Array.Empty<TownAffairsChurchActionViewModel>();

        if (!IsRemote)
            ChurchActions = _owner.GetTownAffairsChurchActions();

        if (!IsRemote && Subject is not null)
        {
            var preferredActionId = Subject.Id == Request.SubjectPersonId
                ? Request.PreferredActionId
                : null;
            JobActionId = _owner.ResolveTownAffairsJobActionId(
                Subject,
                preferredActionId);

            if (!string.IsNullOrWhiteSpace(JobActionId))
            {
                JobActionAvailable = _owner.IsTownAffairsActionAvailable(
                    JobActionId,
                    Subject);
                if (JobActionAvailable)
                    JobModel = _owner.GetJobOpportunityDialog(JobActionId, Subject);
            }

            EducationOptions = _owner.GetEducationSelectionOptions(Subject);
            EducationContextText = _owner.GetEducationSelectionContextText(Subject);
            HealthActions = _owner.GetTownAffairsHealthActions(Subject);
        }

        OnPropertyChanged(nameof(JobActionId));
        OnPropertyChanged(nameof(JobActionAvailable));
        OnPropertyChanged(nameof(JobModel));
        OnPropertyChanged(nameof(HasJobOpportunities));
        OnPropertyChanged(nameof(ShowJobsEmpty));
        OnPropertyChanged(nameof(JobsEmptyText));
        OnPropertyChanged(nameof(EducationOptions));
        OnPropertyChanged(nameof(EducationContextText));
        OnPropertyChanged(nameof(HasEducationOptions));
        OnPropertyChanged(nameof(ShowEducationEmpty));
        OnPropertyChanged(nameof(HealthActions));
        OnPropertyChanged(nameof(HasHealthActions));
        OnPropertyChanged(nameof(ShowHealthEmpty));
        OnPropertyChanged(nameof(ChurchActions));
        OnPropertyChanged(nameof(HasChurchActions));
        OnPropertyChanged(nameof(ShowChurchEmpty));
    }
}

public sealed record TownAffairsMemberTabViewModel(
    Guid PersonId,
    string DisplayName,
    bool IsSelected)
{
    public bool CanSelect => !IsSelected;
}

public sealed record TownAffairsHouseOfferViewModel(
    HousePurchaseOfferInfo Offer,
    bool IsAffordable)
{
    public string CapacityText =>
        $"House for {Offer.BaseResidentCapacity} residents";

    public string AskingPriceText =>
        $"{Offer.AskingPrice:N0} zł";
}

public sealed record TownAffairsHealthActionViewModel(
    string ActionId,
    string Label,
    string Description,
    decimal? Cost,
    bool IsAvailable,
    string? UnavailableReason = null)
{
    public string Emoji => ActionEmojiMap.GetEmoji(ActionId);

    public double DisplayOpacity =>
        IsAvailable ? 1.0 : 0.42;

    public string CostText =>
        Cost is decimal amount
            ? $"{amount:N0} zł"
            : string.Empty;

    public bool HasCost => Cost.HasValue;
}

public sealed record TownAffairsChurchActionViewModel(
    string ActionId,
    string Label,
    string Description,
    decimal? Amount,
    bool IsBenefit,
    bool IsAvailable,
    string? UnavailableReason,
    IReadOnlyDictionary<string, string> Parameters)
{
    public string Emoji => ActionEmojiMap.GetEmoji(ActionId);

    public double DisplayOpacity =>
        IsAvailable ? 1.0 : 0.42;

    public bool HasAmount => Amount.HasValue;

    public string AmountText =>
        Amount is decimal amount
            ? IsBenefit
                ? $"Relief: {amount:N0} zł"
                : $"Amount: {amount:N0} zł"
            : string.Empty;
}

public sealed record TownAffairsCivicOfficeActionViewModel(
    string ActionId,
    string Label,
    string Description,
    bool IsAvailable,
    string? UnavailableReason)
{
    public string Emoji => ActionEmojiMap.GetEmoji(ActionId);

    public double DisplayOpacity => IsAvailable ? 1.0 : 0.42;
}

public sealed record TownAffairsCommunityProposalViewModel(
    CommunityPolicyProposalInfo Proposal,
    bool IsAvailable,
    string? UnavailableReason,
    double? LobbySupportBonus,
    IReadOnlyDictionary<string, string> Parameters)
{
    public string Emoji => "🏛️";

    public double DisplayOpacity =>
        IsAvailable ? 1.0 : 0.50;

    public string ProposerText =>
        $"Proposed by {Proposal.Proposer.Name}, {Proposal.Proposer.Occupation}";

    public string DetailText =>
        $"{Proposal.Rarity} · {Proposal.ImpactTier} · {Proposal.Favorability} · {Proposal.DurationYears} years · base support {Proposal.BaseSupport:P0}";

    public bool HasLobbyBonus => LobbySupportBonus.HasValue;

    public string LobbyBonusText => LobbySupportBonus is double bonus
        ? $"Lobby support: +{bonus:P0}"
        : string.Empty;
}

public sealed record TownAffairsActivePolicyViewModel(
    CommunityActivePolicyInfo Policy,
    int RemainingYears)
{
    public string DurationText => RemainingYears == 1
        ? "1 year remaining"
        : $"{RemainingYears} years remaining";
}
