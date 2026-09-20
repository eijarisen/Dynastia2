using Dynastia.Contracts;

namespace Dynastia.Mechanics.Career;

public sealed partial class StandardCareerService
{
    private const decimal LegacyCareerHistoryIncomeFactor = 0.60m;

    internal void RecordLifetimeCareerEarnings(
        IPerson person,
        decimal realizedCareerIncome,
        int year)
    {
        var career = GetRequired(person);
        EnsureLifetimeEarningsInitialized(person, career);

        if (career.LastLifetimeEarningsYear == year
            || realizedCareerIncome <= 0m)
        {
            return;
        }

        career.LifetimeCareerEarnings = Math.Round(
            career.LifetimeCareerEarnings + realizedCareerIncome,
            0,
            MidpointRounding.AwayFromZero);
        career.LastLifetimeEarningsYear = year;
    }

    private void EnsureLifetimeEarningsInitialized(
        IPerson person,
        CareerComponent career)
    {
        if (career.LifetimeEarningsInitialized)
            return;

        career.LifetimeCareerEarnings = Math.Max(
            0m,
            career.LifetimeCareerEarnings);

        // Old saves and generated adults predate the lifetime ledger. Seed a
        // conservative career-history estimate once so an established worker
        // does not reach retirement with a zero pension merely because the
        // save was created before this field existed. This also covers someone
        // who happens to be unemployed when an older save is first loaded.
        var recordedYears = career.ExperienceYearsByCareer?.Values.Sum() ?? 0;
        var hasCareerHistory = career.JobLevel > 0
            || career.IsRetired
            || career.PeakJobLevel > 0
            || recordedYears > 0;

        if (career.LifetimeCareerEarnings <= 0m
            && hasCareerHistory)
        {
            var estimatedYears = recordedYears > 0
                ? recordedYears
                : Math.Clamp(person.Age - 18, 0, 47);

            var referenceIncome = GetLegacyReferenceIncome(person, career);

            career.LifetimeCareerEarnings = Math.Round(
                referenceIncome
                * estimatedYears
                * LegacyCareerHistoryIncomeFactor,
                0,
                MidpointRounding.AwayFromZero);
        }

        career.LastLifetimeEarningsYear = Math.Min(
            career.LastLifetimeEarningsYear,
            _gameState.Year - 1);
        career.LifetimeEarningsInitialized = true;
    }
    private decimal GetLegacyReferenceIncome(
        IPerson person,
        CareerComponent career)
    {
        if (career.IsRetired && career.LastIncome > 0m)
            return career.LastIncome;

        if (career.JobLevel > 0)
            return GetActiveSalary(person, career);

        var careerId = career.PeakCareerId ?? career.CareerId;
        var definition = careerId is null
            ? null
            : _catalog.Find(careerId);

        if (definition is null || career.PeakJobLevel <= 0)
            return 0m;

        return CareerBalanceRules.CalculateAnnualSalary(
            definition.BaseSalary,
            career.PeakJobLevel);
    }

}
