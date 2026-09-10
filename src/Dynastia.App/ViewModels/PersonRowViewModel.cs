using Dynastia.Contracts;

namespace Dynastia.App.ViewModels;

public sealed class PersonRowViewModel
{
    private readonly IPerson _person;
    private readonly IFamilyService? _familyService;

    public PersonRowViewModel(
        IPerson person,
        IFamilyService? familyService)
    {
        _person = person;
        _familyService = familyService;
    }

    public Guid Id => _person.Id;

    public string FullName =>
        $"{_person.Name} {_person.Surname}";

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

    public string MaidenNameText =>
        string.IsNullOrWhiteSpace(_person.MaidenName)
            ? string.Empty
            : $"Maiden name: {_person.MaidenName}";

    public string StatusText =>
        _person.Tags.Has("state.dead")
            ? "Deceased"
            : "Living";

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
