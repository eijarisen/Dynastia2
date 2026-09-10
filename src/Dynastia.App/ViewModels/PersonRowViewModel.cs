using Dynastia.Contracts;

namespace Dynastia.App.ViewModels;

public sealed class PersonRowViewModel
{
    private readonly IPerson _person;

    public PersonRowViewModel(IPerson person)
    {
        _person = person;
    }

    public string FullName =>
        $"{_person.Name} {_person.Surname}";

    public string AgeText =>
        $"Age: {_person.Age}";

    public string StatusText =>
        _person.Tags.Has("state.dead")
            ? "Deceased"
            : "Living";
}
