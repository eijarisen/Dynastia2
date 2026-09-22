namespace Dynastia.Contracts;

public sealed record CriminalOccupationSnapshot(
    bool HasStartedLifeOfCrime,
    bool IsActive,
    int ActiveHeistYears,
    int LastHeistYear,
    int MasteryLevel,
    string MasteryName,
    string ArchetypeId,
    string ArchetypeName,
    decimal ExpectedAnnualIncome,
    decimal LastAnnualIncome,
    int LastIncomeYear);
