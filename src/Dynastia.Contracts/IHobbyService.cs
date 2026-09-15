namespace Dynastia.Contracts;

public interface IHobbyService
{
    HobbyPersonSnapshot GetHobbies(
        IPerson person);

    void ReconcileAll();

    void ReconcileAfterLoad();
}
