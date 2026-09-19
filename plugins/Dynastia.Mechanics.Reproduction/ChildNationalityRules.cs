using Dynastia.Contracts;

namespace Dynastia.Mechanics.Reproduction;

public static class ChildNationalityRules
{
    public static string Resolve(
        string? fatherNationalityId,
        string? motherNationalityId,
        TownInfo birthTown,
        int year,
        INationalityService nationalities,
        IGameRandom random)
    {
        ArgumentNullException.ThrowIfNull(birthTown);
        ArgumentNullException.ThrowIfNull(nationalities);
        ArgumentNullException.ThrowIfNull(random);

        var hasFather =
            !string.IsNullOrWhiteSpace(
                fatherNationalityId);
        var hasMother =
            !string.IsNullOrWhiteSpace(
                motherNationalityId);

        // Dynastia uses patrilineal nationality inheritance: whenever a
        // father is known, every child keeps the father's nationality.
        // The remaining branches are defensive fallbacks for generated
        // people that do not have a resolved father.
        if (hasFather)
            return fatherNationalityId!;

        if (hasMother)
            return motherNationalityId!;

        if (string.IsNullOrWhiteSpace(
                birthTown.RegionId))
        {
            throw new InvalidOperationException(
                $"Birth town '{birthTown.Id}' has no RegionId for nationality generation.");
        }

        return nationalities.GenerateNationality(
            birthTown.RegionId,
            year,
            random);
    }
}
