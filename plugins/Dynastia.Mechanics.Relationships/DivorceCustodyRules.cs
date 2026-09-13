namespace Dynastia.Mechanics.Relationships;

public static class DivorceCustodyRules
{
    public static bool IsSharedBiologicalChild(
        Guid? fatherId,
        Guid? motherId,
        Guid firstParentId,
        Guid secondParentId)
    {
        return (fatherId == firstParentId && motherId == secondParentId)
            || (fatherId == secondParentId && motherId == firstParentId);
    }

    public static bool AssignToFather(double custodyRoll) =>
        custodyRoll < 0.50;
}
