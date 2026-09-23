namespace Dynastia.Contracts;

public interface IPersonLookup
{
    IPerson? FindPerson(Guid id);

    IPerson? FindPerson(Guid? id);
}
