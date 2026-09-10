namespace Dynastia.Contracts;

public interface IPerson
{
    Guid Id { get; }
    string Name { get; set; }
    string Surname { get; set; }
    int Age { get; set; }
    ITagSet Tags { get; }
}
