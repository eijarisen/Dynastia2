namespace Dynastia.Contracts;

public interface IAppearanceService
{
    AppearanceSnapshot EnsureAppearance(IPerson person);

    AppearanceSnapshot GenerateCandidateAppearance(
        Guid deterministicId,
        Sex sex);

    void SetAppearance(
        IPerson person,
        AppearanceSnapshot appearance,
        Guid? portraitSeedId = null);

    string GetPortrait(
        IPerson person,
        bool useDeadOverride = true);

    string GetPortrait(
        AppearanceSnapshot appearance,
        Sex sex,
        int age,
        Guid deterministicId,
        bool isDead = false);
}
