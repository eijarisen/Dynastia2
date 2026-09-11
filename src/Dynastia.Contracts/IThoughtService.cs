namespace Dynastia.Contracts;

public interface IThoughtService
{
    PersonThoughtSnapshot? GetCurrentThought(
        IPerson person);

    void EnsureCurrentThoughts();

    void ResetAfterLoad();
}
