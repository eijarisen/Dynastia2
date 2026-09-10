using Dynastia.Contracts;

namespace Dynastia.App.ViewModels;

public sealed class PersonRowViewModel
{
    private readonly IPerson _person;
    private readonly IFamilyService? _familyService;

    public PersonRowViewModel(
        IPerson person,
        IFamilyService? familyService,
        IHealthService? healthService)
    {
        _person = person;
        _familyService = familyService;

        if (healthService is not null)
        {
            var health =
                healthService.GetHealth(person);

            HealthValue =
                health.Percentage;

            HealthText =
                $"{Math.Round(health.Current)}/" +
                $"{Math.Round(health.Maximum)}";
        }
    }

    public Guid Id =>
        _person.Id;

    public string FullName =>
        _familyService is null
            ? $"{_person.Name} {_person.Surname}"
            : _familyService.GetDisplayName(
                _person);

    public string AgeText =>
        $"Age: {_person.Age}";

    public string BirthDateText =>
        _person.BirthDate is GameDate date
            ? $"Born: {date}"
            : "Born: Unknown";

    public string DeathDateText =>
        _person.DeathDate is GameDate date
            ? $"Died: {date}"
            : string.Empty;

    public string MaidenNameText
    {
        get
        {
            if (string.IsNullOrWhiteSpace(
                _person.MaidenName))
            {
                return string.Empty;
            }

            var maidenName =
                _familyService is null
                    ? _person.MaidenName
                    : _familyService.FormatSurname(
                        _person.MaidenName,
                        Sex.Female);

            return $"Maiden name: {maidenName}";
        }
    }

    public string StatusText =>
        _person.Tags.Has("state.dead")
            ? "Deceased"
            : "Living";

    public double HealthValue { get; }

    public string HealthText { get; } =
        string.Empty;

    public bool ShowHealth =>
        !_person.Tags.Has("state.dead");

    public string GenerationText
    {
        get
        {
            if (_familyService is null)
                return string.Empty;

            var generation =
                _familyService.GetGeneration(
                    _person);

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
                _person.Tags.All.OrderBy(
                    x => x));
}
