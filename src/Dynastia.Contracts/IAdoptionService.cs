namespace Dynastia.Contracts;

public interface IAdoptionService
{
    AdoptionPlacementInfo GetPlacement(
        IPerson person);

    IReadOnlyList<IPerson> GetHostedChildren(
        IPerson householdHead);

    bool HasOrphanTrait(
        IPerson person);
}
