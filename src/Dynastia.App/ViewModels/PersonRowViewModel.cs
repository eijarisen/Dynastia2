using Dynastia.Contracts;

namespace Dynastia.App.ViewModels;

public sealed class PersonRowViewModel
{
    private readonly IPerson _person;
    private readonly IFamilyService? _familyService;

    public PersonRowViewModel(
        IPerson person,
        IFamilyService? familyService,
        IHealthService? healthService,
        IEconomyService? economyService,
        ICareerService? careerService,
        IFarmingService? farmingService,
        IHouseholdService? householdService,
        ILocationService? locationService)
    {
        _person = person;
        _familyService = familyService;

        // Family reads also run small compatibility reconciliation for
        // legacy founding-parent metadata. Do this before binding any
        // identity fields such as maiden name.
        _ = familyService?.IsBloodline(
            person);

        if (healthService is not null)
        {
            var health = healthService.GetHealth(person);

            HealthValue = health.Percentage;
            HealthText =
                $"{Math.Round(health.Current)}/{Math.Round(health.Maximum)}";
        }

        var economy = economyService?.GetHousehold(person);

        if (economy is not null)
        {
            var projectedIncome =
                economyService?.GetProjectedAnnualIncome(person)
                ?? economy.LastIncome;

            ShowHousehold = true;
            BudgetText = $"${economy.Wealth:N0}";
            var farmlandCount = farmingService?.GetSnapshot(person).TotalParcelCount
                ?? economyService?.GetFarmland(person).Count
                ?? 0;

            HousesText = $"Houses: {economy.HousesOwned}   •   Farmland: {farmlandCount}";
            IncomeExpensesText =
                $"+${projectedIncome:N0} / -${economy.LastExpenses:N0}";

            var status =
                householdService?.GetStatus(person);

            HouseholdWarningText =
                status is null
                    ? string.Empty
                    : string.Join(
                        " ",
                        status.Warnings);
        }

        if (careerService is not null)
        {
            var career =
                careerService.GetCareer(
                    person);

            OccupationText =
                farmingService?.IsWorkingFarmWorker(person, person) == true
                    ? "Farm Worker"
                    : career.IsEmployed && career.JobLevel > 0
                        ? $"{career.JobTitle} ({career.JobLevel})"
                        : career.JobTitle;
        }

        if (locationService is not null)
        {
            var location =
                locationService.GetLocation(
                    person);

            BirthplaceText =
                $"Birthplace: " +
                $"{location.Birthplace.DisplayName}";

            if (person.Tags.Has(
                    "state.dead"))
            {
                var deathTown =
                    location.DeathTown
                    ?? location.HomeTown;

                DeathplaceText =
                    $"Death place: " +
                    $"{deathTown.DisplayName}";
            }
            else
            {
                ResidenceText =
                    $"Residence: " +
                    $"{location.HomeTown.DisplayName}";
            }
        }
    }

    public Guid Id => _person.Id;

    public string FullName =>
        _familyService is null
            ? $"{_person.Name} {_person.Surname}"
            : _familyService.GetDisplayName(_person);

    public string AgeText => $"Age: {_person.Age}";

    public string PersonalityText =>
        PersonalityInfluence.GetDisplayName(
            _person)
        ?? string.Empty;

    public bool HasPersonality =>
        !string.IsNullOrWhiteSpace(
            PersonalityText);

    public string BirthDateText =>
        _person.BirthDate is GameDate date
            ? $"Born: {date}"
            : "Born: Unknown";

    public string DeathDateText =>
        _person.DeathDate is GameDate date
            ? $"Died: {date}"
            : string.Empty;

    public bool HasDeathDate =>
        _person.DeathDate is not null;

    public string BirthplaceText { get; } =
        string.Empty;

    public string DeathplaceText { get; } =
        string.Empty;

    public string ResidenceText { get; } =
        string.Empty;

    public bool HasResidence =>
        !string.IsNullOrWhiteSpace(
            ResidenceText);

    public bool HasDeathplace =>
        _person.Tags.Has(
            "state.dead")
        && !string.IsNullOrWhiteSpace(
            DeathplaceText);

    public string MaidenNameText
    {
        get
        {
            if (string.IsNullOrWhiteSpace(_person.MaidenName))
                return string.Empty;

            var maidenName =
                _familyService is null
                    ? _person.MaidenName
                    : _familyService.FormatSurname(
                        _person.MaidenName,
                        Sex.Female);

            return $"Maiden name: {maidenName}";
        }
    }

    public bool HasMaidenName =>
        !string.IsNullOrWhiteSpace(
            _person.MaidenName);

    public string StatusText
    {
        get
        {
            if (_person.Tags.Has("state.dead"))
                return "Deceased";

            if (_person.Tags.Has("control.playable"))
                return "Living · Playable";

            return "Living";
        }
    }

    public bool IsPlayable =>
        _person.Tags.Has("control.playable");

    public string PlayableText =>
        IsPlayable
            ? "Playable: Yes"
            : "Playable: No";

    public string OrphanTraitText =>
        _person.Tags.Has(
            "trait.orphan")
                ? "Trait: Orphan (-1 Health/year)"
                : string.Empty;

    public bool HasOrphanTrait =>
        _person.Tags.Has(
            "trait.orphan");

    public string OccupationText { get; } = string.Empty;

    public double HealthValue { get; }

    public string HealthText { get; } = string.Empty;

    public bool ShowHealth =>
        !_person.Tags.Has("state.dead");

    public bool ShowHousehold { get; }

    public string BudgetText { get; } = string.Empty;

    public string HousesText { get; } = string.Empty;

    public string IncomeExpensesText { get; } = string.Empty;

    public string HouseholdWarningText { get; } = string.Empty;

    public string GenerationText
    {
        get
        {
            if (_familyService is null)
                return string.Empty;

            var generation =
                _familyService.GetGeneration(_person);

            return generation is null
                ? string.Empty
                : $"G{generation}";
        }
    }

    public string TagsText =>
        _person.Tags.All.Count == 0
            ? "No tags"
            : string.Join(
                ", ",
                _person.Tags.All.OrderBy(x => x));
}
