namespace Dynastia.Contracts;

public interface IFamilyService
{
    void InitializePerson(
        IPerson person,
        Sex sex,
        int? generation = null);

    Sex GetSex(IPerson person);
    int? GetGeneration(IPerson person);

    IPerson? GetFather(IPerson person);
    IPerson? GetMother(IPerson person);
    IPerson? GetSpouse(IPerson person);

    IReadOnlyList<IPerson> GetChildren(IPerson person);

    void SetParents(
        IPerson child,
        IPerson? father,
        IPerson? mother);

    void SetSpouses(
        IPerson first,
        IPerson second);

    void ClearCurrentSpouse(IPerson person);

    bool IsBloodline(IPerson person);
    bool IsMaleLineage(IPerson person);
}
