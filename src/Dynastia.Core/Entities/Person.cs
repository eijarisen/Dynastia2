using Dynastia.Contracts;

namespace Dynastia.Core.Entities;

public sealed class Person : IPerson
{
    public Person(
        string name,
        string surname,
        int age)
    {
        Id = Guid.NewGuid();

        Name = name;
        Surname = surname;
        Age = age;

        Tags = new TagSet();
    }

    public Guid Id { get; }

    public string Name { get; set; }

    public string Surname { get; set; }

    public int Age { get; set; }

    public ITagSet Tags { get; }
}