namespace Dynastia.Contracts;

public interface ICraftService
{
    IReadOnlyList<CraftInfo> Catalog { get; }

    CraftSnapshot GetSnapshot(IPerson person);

    IReadOnlyList<CraftInfo> GetKnownCrafts(IPerson person);

    bool KnowsCraft(IPerson person, string craftId);

    bool IsSelfEmployed(IPerson person);

    CraftInfo? GetActiveCraft(IPerson person);

    bool CanLearnCraft(IPerson person, string craftId);

    double GetLearningWeight(IPerson person, string craftId);

    bool LearnCraft(IPerson person, string craftId);

    void SetCrafts(IPerson person, IEnumerable<string> craftIds);

    IReadOnlyList<string> GenerateCandidateCraftIds(
        string deterministicKey,
        string? formalCareerId,
        int year,
        Sex sex,
        int age,
        int strength,
        int intellect,
        string temperament,
        TownInfo town);

    bool StartOccupation(IPerson person, string craftId);

    bool EndOccupation(IPerson person, string reason = "ended");

    double GetApplicationBonus(IPerson person, string careerId);

    CraftCareerExperience GetCareerExperience(IPerson person, string careerId);

    decimal GetExpectedAnnualIncome(IPerson person);
}
