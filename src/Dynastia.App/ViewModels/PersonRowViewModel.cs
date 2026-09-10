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
        IHouseholdService? householdService,
        ILocationService? locationService)
    {
        _person = person;
        _familyService = familyService;

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
            ShowHousehold = true;
            BudgetText = $"${economy.Wealth:N0}";
            HousesText = $"Houses: {economy.HousesOwned}";
            IncomeExpensesText =
                $"+${economy.LastIncome:N0} / -${economy.LastExpenses:N0}";

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
            OccupationText =
                careerService.GetCareer(person).JobTitle;
        }

        if (locationService is not null)
        {
            var location =
                locationService.GetLocation(
                    person);

            BirthplaceText =
                $"Birthplace: " +
                $"{location.Birthplace.DisplayName}";
        }
    }

    public Guid Id => _person.Id;

    public string FullName =>
        _familyService is null
            ? $"{_person.Name} {_person.Surname}"
            : _familyService.GetDisplayName(_person);

    public string AgeText => $"Age: {_person.Age}";

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
