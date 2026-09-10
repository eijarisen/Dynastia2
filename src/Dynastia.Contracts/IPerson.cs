namespace Dynastia.Contracts;

public interface IPerson
{
    Guid Id { get; }

    string Name { get; set; }
    string Surname { get; set; }
    string? MaidenName { get; set; }

    int Age { get; set; }

    GameDate? BirthDate { get; set; }
    GameDate? DeathDate { get; set; }

    ITagSet Tags { get; }
    IComponentContainer Components { get; }
}
