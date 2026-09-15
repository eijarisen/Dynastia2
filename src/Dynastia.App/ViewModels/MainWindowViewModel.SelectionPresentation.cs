namespace Dynastia.App.ViewModels;

public sealed partial class MainWindowViewModel
{
    public PersonRowViewModel? SelectedPerson
    {
        get => _selectedPerson;
        set
        {
            if (ReferenceEquals(
                _selectedPerson,
                value))
            {
                return;
            }

            _selectedPerson = value;

            _selectionService.SelectedPersonId =
                value?.Id;

            RefreshSelectedStats();
            RefreshFamilyDetails();
            RefreshHealth();
            RefreshEconomy();
            RefreshEducation();
            RefreshHobbies();
            RefreshCareer();
            RefreshMarriageSatisfaction();
            RefreshChildHappiness();
            RefreshJustice();
            RefreshNarrative();
            RefreshActions();
            RefreshFamilySection();

            OnPropertyChanged();
            OnPropertyChanged(
                nameof(SelectedPersonEmoji));
        }
    }

    public FamilyDetailsViewModel? SelectedFamily
    {
        get => _selectedFamily;
        private set
        {
            _selectedFamily = value;
            OnPropertyChanged();
        }
    }

    public HealthViewModel? SelectedHealth
    {
        get => _selectedHealth;
        private set
        {
            _selectedHealth = value;
            OnPropertyChanged();
        }
    }

    public EconomyViewModel? SelectedEconomy
    {
        get => _selectedEconomy;
        private set
        {
            _selectedEconomy = value;
            OnPropertyChanged();
            OnPropertyChanged(
                nameof(HasSelectedEconomy));
        }
    }

    public EducationViewModel? SelectedEducation
    {
        get => _selectedEducation;
        private set
        {
            _selectedEducation = value;
            OnPropertyChanged();
        }
    }

    public CareerViewModel? SelectedCareer
    {
        get => _selectedCareer;
        private set
        {
            _selectedCareer = value;
            OnPropertyChanged();
        }
    }


    public string SelectedHobbiesText
    {
        get
        {
            var person = FindSelectedPerson();
            if (person is null || _hobbyService is null)
                return "Hobbies: None";

            var hobbies = _hobbyService
                .GetHobbies(person)
                .Hobbies;

            return hobbies.Count == 0
                ? "Hobbies: None"
                : $"Hobbies: {string.Join(", ", hobbies.Select(hobby => hobby.Name))}";
        }
    }

    public string SelectedMarriageSatisfactionText
    {
        get
        {
            var person =
                FindSelectedPerson();

            var snapshot =
                person is null
                    ? null
                    : _marriageSatisfactionService?
                        .GetSatisfaction(
                            person);

            return snapshot is null
                ? string.Empty
                : $"Marriage: {snapshot.Label}";
        }
    }

    public string SelectedMarriageSatisfactionLabel
    {
        get
        {
            var person =
                FindSelectedPerson();

            var snapshot =
                person is null
                    ? null
                    : _marriageSatisfactionService?
                        .GetSatisfaction(
                            person);

            return snapshot?.Label
                ?? string.Empty;
        }
    }

    public string SelectedMarriageSatisfactionDetailsText
    {
        get
        {
            var person =
                FindSelectedPerson();

            var snapshot =
                person is null
                    ? null
                    : _marriageSatisfactionService?
                        .GetSatisfaction(
                            person);

            if (snapshot is null)
                return string.Empty;

            return snapshot.CurrentIssues.Count == 0
                ? "No current marriage strains."
                : "Current strains:" +
                  Environment.NewLine +
                  string.Join(
                      Environment.NewLine,
                      snapshot.CurrentIssues.Select(
                          issue =>
                              $"• {issue}"));
        }
    }

    public bool HasSelectedMarriageSatisfaction =>
        !string.IsNullOrWhiteSpace(
            SelectedMarriageSatisfactionText);

    public string SelectedChildHappinessText
    {
        get
        {
            var person = FindSelectedPerson();
            var snapshot = person is null
                ? null
                : _childHappinessService?.GetHappiness(person);
            return snapshot is null ? string.Empty : $"Happiness: {snapshot.Label}";
        }
    }

    public string SelectedChildHappinessLabel
    {
        get
        {
            var person = FindSelectedPerson();
            return person is null
                ? string.Empty
                : _childHappinessService?.GetHappiness(person)?.Label ?? string.Empty;
        }
    }

    public bool HasSelectedChildHappiness =>
        !string.IsNullOrWhiteSpace(SelectedChildHappinessText);

    public JusticeViewModel? SelectedJustice
    {
        get => _selectedJustice;

        private set
        {
            _selectedJustice = value;
            OnPropertyChanged();
        }
    }

    public bool HasSelectedEconomy =>
        SelectedEconomy is not null;

    public string SelectedAboutText
    {
        get => _selectedAboutText;

        private set
        {
            if (_selectedAboutText == value)
                return;

            _selectedAboutText = value;
            OnPropertyChanged();
        }
    }

    public string BiographyEmptyText =>
        SelectedBiography.Count == 0
            ? "No significant events recorded."
            : string.Empty;

    public int DetailsTabIndex
    {
        get => _detailsTabIndex;

        set
        {
            var clamped =
                Math.Clamp(
                    value,
                    0,
                    3);

            if (_detailsTabIndex == clamped)
                return;

            _detailsTabIndex =
                clamped;

            OnPropertyChanged();
        }
    }

    public string PersistenceStatusText
    {
        get => _persistenceStatusText;

        private set
        {
            if (_persistenceStatusText == value)
                return;

            _persistenceStatusText = value;

            OnPropertyChanged();
            OnPropertyChanged(
                nameof(HasPersistenceStatus));
            OnPropertyChanged(
                nameof(IsStatusMessageVisible));
        }
    }

    public bool HasPersistenceStatus =>
        !string.IsNullOrWhiteSpace(
            PersistenceStatusText);

    public bool IsStatusMessageVisible =>
        HasPersistenceStatus;

}
