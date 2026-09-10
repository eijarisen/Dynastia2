using Dynastia.Contracts;

namespace Dynastia.App.ViewModels;

public sealed class PersonRowViewModel
{
    private readonly IPerson _person;

    public PersonRowViewModel(IPerson person)
    {
        _person = person;
    }

    public Guid Id => _person.Id;

    public string FullName =>
        $"{_person.Name} {_person.Surname}";

    public string AgeText =>
        $"Age: {_person.Age}";

    public string StatusText =>
        _person.Tags.Has("state.dead")
            ? "Deceased"
            : "Living";

    public string TagsText =>
        _person.Tags.All.Count == 0
            ? "No tags"
            : string.Join(", ", _person.Tags.All.OrderBy(x => x));

    public bool IsDead =>
        _person.Tags.Has("state.dead");
}
