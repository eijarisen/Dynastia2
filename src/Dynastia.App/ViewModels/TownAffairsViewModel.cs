using Avalonia.Media;
using Dynastia.Contracts;

namespace Dynastia.App.ViewModels;

public enum TownAffairsTab
{
    Institutions = 0,
    Community = 1,
    Housing = 2,
    Jobs = 3,
    Health = 4,
    Church = 5,
    Education = 6,
    Bank = 7,
    Court = 8
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
    private static readonly IBrush UnfavorableBankBrush =
        new SolidColorBrush(Color.Parse("#A13A2B"));
    private static readonly IBrush ModestBankBrush =
        new SolidColorBrush(Color.Parse("#B57A33"));
    private static readonly IBrush StandardBankBrush =
        new SolidColorBrush(Color.Parse("#806633"));
    private static readonly IBrush FavorableBankBrush =
        new SolidColorBrush(Color.Parse("#2F6F3E"));

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
        FarmlandOffers = IsRemote
            ? Array.Empty<TownAffairsFarmlandOfferViewModel>()
            : owner.GetTownAffairsFarmlandOffers(snapshot.Town);
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

        if ((request.InitialTab == TownAffairsTab.Jobs
                || request.InitialTab == TownAffairsTab.Church
                || request.InitialTab == TownAffairsTab.Court)
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
                (int)TownAffairsTab.Court);
            var tab = (TownAffairsTab)normalized;
            if (!IsTabVisible(tab) || _selectedTabIndex == normalized)
                return;

            if ((tab == TownAffairsTab.Jobs
                    || tab == TownAffairsTab.Church
                    || tab == TownAffairsTab.Court)
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
        !IsRemote;

    public bool ShowBankTab =>
        !IsRemote && Snapshot.Bank.IsAvailable;

    public bool ShowChurchTab =>
        !IsRemote && Snapshot.Church.IsAvailable;

    public bool ShowCourtTab =>
        !IsRemote && _householdPeople.Any(person => person.Age >= 18);

    public bool ShowCommunityTab => !IsRemote;

    public CivicOfficeHeadInfo? CivicOffice => Snapshot.CivicOffice;

    public bool HasCivicOffice => CivicOffice is not null;

    public string MayorSummaryText => CivicOffice is null
        ? "No mayor is currently recorded."
        : CivicOffice.Name;

    public string CivicOfficeNameText => CivicOffice?.Name ?? string.Empty;

    public string CivicOfficeTitleText => CivicOffice?.OfficeTitle ?? string.Empty;

    public string CivicOfficeAgeText => CivicOffice is null
        ? string.Empty
        : $"Age: {CivicOffice.Age(_owner.Year)}";

    public string CivicOfficePortrait =>
        _owner.GetTownAffairsCivicOfficePortrait(CivicOffice);

    public string CivicOfficeRenownText => CivicOffice is null
        ? string.Empty
        : $"Renown: {_owner.GetTownAffairsRenownLabel(CivicOffice.Renown)}";

    public string CivicOfficeReputationText => CivicOffice is null
        ? string.Empty
        : $"Reputation: {_owner.GetTownAffairsReputationLabel(CivicOffice.Reputation)}";

    public string CivicOfficeApprovalText => CivicOffice is null
        ? string.Empty
        : $"Approval: {CivicOffice.Approval:0.#}%";

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

    public IReadOnlyList<TownAffairsFarmlandOfferViewModel> FarmlandOffers { get; }

    public bool HasHouseOffers => HouseOffers.Count > 0;

    public bool HasPropertyOffers =>
        HasHouseOffers || FarmlandOffers.Count > 0;

    public bool ShowNoHouseOffers => !HasPropertyOffers;

    public string NoHouseOffersText =>
        "No houses or farmland are currently offered for sale in this town.";

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

    public IBrush BankStatusBrush =>
        Snapshot.BankQuality.Tier switch
        {
            <= 1 => UnfavorableBankBrush,
            2 => ModestBankBrush,
            3 => StandardBankBrush,
            _ => FavorableBankBrush
        };

    public string HousingFundsText =>
        $"Funds: {_owner.GetTownAffairsCurrentFunds():N0} zł";

    public IReadOnlyList<TownAffairsHealthActionViewModel> HealthActions { get; private set; }
        = Array.Empty<TownAffairsHealthActionViewModel>();

    public IReadOnlyList<TownAffairsHealthActionViewModel> TreatmentHealthActions { get; private set; }
        = Array.Empty<TownAffairsHealthActionViewModel>();

    public IReadOnlyList<TownAffairsHealthActionViewModel> TherapyHealthActions { get; private set; }
        = Array.Empty<TownAffairsHealthActionViewModel>();

    public IReadOnlyList<TownAffairsHealthActionViewModel> MedicalImprovementActions { get; private set; }
        = Array.Empty<TownAffairsHealthActionViewModel>();

    public TownAffairsHealthSummaryViewModel HealthSummary { get; private set; } =
        new("Health information is unavailable.", "Conditions: unavailable");

    public string MedicalQualityText =>
        Snapshot.MedicalQuality.IsAvailable
            ? $"Local medical care: {Snapshot.MedicalQuality.DisplayName} · Tier {Snapshot.MedicalQuality.Tier}"
            : "No local medical facility is available.";

    public bool HasHealthActions => HealthActions.Count > 0;

    public bool HasTreatmentHealthActions => TreatmentHealthActions.Count > 0;

    public bool HasTherapyHealthActions => TherapyHealthActions.Count > 0;

    public bool HasMedicalImprovementActions => MedicalImprovementActions.Count > 0;

    public IReadOnlyList<TownAffairsChurchActionViewModel> ChurchActions { get; private set; }
        = Array.Empty<TownAffairsChurchActionViewModel>();

    public bool HasChurchActions => ChurchActions.Count > 0;

    public bool ShowChurchEmpty => !HasChurchActions;

    public bool ShowHealthEmpty => !HasHealthActions;

    public string HealthEmptyText =>
        "No health actions are currently available.";

    public TownAffairsCourtViewModel? CourtModel { get; private set; }

    public bool HasCourtModel => CourtModel is not null;

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
            TownAffairsTab.Court => ShowCourtTab,
            TownAffairsTab.Community => ShowCommunityTab,
            _ => false
        };

    public bool OpenInstitution(string institutionId)
    {
        var tab = institutionId.ToLowerInvariant() switch
        {
            "administration" => TownAffairsTab.Community,
            "school" => TownAffairsTab.Education,
            "bank" => TownAffairsTab.Bank,
            "medical" => TownAffairsTab.Health,
            "church" => TownAffairsTab.Church,
            "court" => TownAffairsTab.Court,
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

    public void QueueFarmlandPurchase(TownAffairsFarmlandOfferViewModel offer)
    {
        if (!offer.CanBuy)
            return;

        _owner.QueueTownAffairsFarmlandPurchase(offer);
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

    public void QueueChurch(
        TownAffairsChurchActionViewModel action,
        decimal? selectedAmount = null)
    {
        if (!ShowChurchTab
            || Subject is null
            || Subject.Age < 18
            || !action.IsAvailable)
        {
            return;
        }

        _owner.QueueTownAffairsChurchAction(
            action,
            Subject,
            selectedAmount);
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
        TreatmentHealthActions = Array.Empty<TownAffairsHealthActionViewModel>();
        TherapyHealthActions = Array.Empty<TownAffairsHealthActionViewModel>();
        MedicalImprovementActions = Array.Empty<TownAffairsHealthActionViewModel>();
        HealthSummary = new TownAffairsHealthSummaryViewModel(
            "Health information is unavailable.",
            "Conditions: unavailable");
        ChurchActions = Array.Empty<TownAffairsChurchActionViewModel>();
        CourtModel = null;

        if (!IsRemote && Subject is not null)
        {
            if (Subject.Age >= 18)
                ChurchActions = _owner.GetTownAffairsChurchActions(Subject);

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
            HealthSummary = _owner.GetTownAffairsHealthSummary(Subject);
            HealthActions = _owner.GetTownAffairsHealthActions(Subject);
            TreatmentHealthActions = HealthActions
                .Where(action => action.ActionId.Equals(
                    "wellbeing.heal_relative",
                    StringComparison.OrdinalIgnoreCase))
                .ToArray();
            TherapyHealthActions = HealthActions
                .Where(action => action.ActionId.Equals(
                    "wellbeing.therapy",
                    StringComparison.OrdinalIgnoreCase))
                .ToArray();
            MedicalImprovementActions = HealthActions
                .Where(action => action.ActionId.StartsWith(
                    "stats.improve_",
                    StringComparison.OrdinalIgnoreCase))
                .ToArray();
            if (Subject.Age >= 18)
                CourtModel = _owner.GetTownAffairsCourtModel(Snapshot, Subject);
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
        OnPropertyChanged(nameof(TreatmentHealthActions));
        OnPropertyChanged(nameof(TherapyHealthActions));
        OnPropertyChanged(nameof(MedicalImprovementActions));
        OnPropertyChanged(nameof(HealthSummary));
        OnPropertyChanged(nameof(MedicalQualityText));
        OnPropertyChanged(nameof(HasHealthActions));
        OnPropertyChanged(nameof(HasTreatmentHealthActions));
        OnPropertyChanged(nameof(HasTherapyHealthActions));
        OnPropertyChanged(nameof(HasMedicalImprovementActions));
        OnPropertyChanged(nameof(ShowHealthEmpty));
        OnPropertyChanged(nameof(ChurchActions));
        OnPropertyChanged(nameof(HasChurchActions));
        OnPropertyChanged(nameof(ShowChurchEmpty));
        OnPropertyChanged(nameof(CourtModel));
        OnPropertyChanged(nameof(HasCourtModel));
        OnPropertyChanged(nameof(ShowCourtTab));
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

public sealed record TownAffairsFarmlandOfferViewModel(
    decimal AskingPrice,
    bool CanBuy,
    string? UnavailableReason = null)
{
    public string DescriptionText => "Farmland parcel";

    public string AskingPriceText =>
        $"{AskingPrice:N0} zł";
}

public sealed record TownAffairsHealthSummaryViewModel(
    string HealthText,
    string ConditionsText);

public sealed record TownAffairsHealthActionViewModel(
    string ActionId,
    string Label,
    string Description,
    string Emoji,
    decimal? Cost,
    bool IsAvailable,
    string? UnavailableReason = null)
{
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
    string Emoji,
    decimal? Amount,
    bool IsBenefit,
    bool IsAvailable,
    string? UnavailableReason,
    IReadOnlyDictionary<string, string> Parameters,
    bool RequiresMoneySelection = false,
    decimal MinimumAmount = 0m,
    decimal MaximumAmount = 0m)
{
    public double DisplayOpacity =>
        IsAvailable ? 1.0 : 0.42;

    public bool HasAmount => RequiresMoneySelection || Amount.HasValue;

    public string AmountText =>
        RequiresMoneySelection
            ? $"Choose amount: {MinimumAmount:N0}–{MaximumAmount:N0} zł"
            : Amount is decimal amount
                ? ActionId.Equals(
                    "personality.religious_study",
                    StringComparison.OrdinalIgnoreCase)
                        ? $"Cost: {amount:N0} zł"
                        : IsBenefit
                            ? $"Relief: {amount:N0} zł"
                            : $"Amount: {amount:N0} zł"
                : string.Empty;
}

public sealed record TownAffairsCourtViewModel(
    string CourtText,
    bool HasLocalCourt,
    string ProtectionText,
    IReadOnlyList<CourtProtectionRelativeInfo> ProtectionRelatives,
    IReadOnlyList<CriminalRecordEntryInfo> CriminalRecord)
{
    public bool ShowNoLocalCourt => !HasLocalCourt;
    public bool HasProtectionRelatives => ProtectionRelatives.Count > 0;
    public bool ShowNoProtectionRelatives => !HasProtectionRelatives;
    public bool HasCriminalRecord => CriminalRecord.Count > 0;
    public bool ShowNoCriminalRecord => !HasCriminalRecord;
}


public sealed record TownAffairsCivicOfficeActionViewModel(
    string ActionId,
    string Label,
    string Description,
    string Emoji,
    bool IsAvailable,
    string? UnavailableReason)
{
    public double DisplayOpacity => IsAvailable ? 1.0 : 0.42;
}

public sealed record TownAffairsCommunityProposalViewModel(
    CommunityPolicyProposalInfo Proposal,
    string ProposerPortrait,
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
