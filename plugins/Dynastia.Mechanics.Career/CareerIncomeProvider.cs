using Dynastia.Contracts;

namespace Dynastia.Mechanics.Career;

public sealed class CareerIncomeProvider : IIncomeProvider
{
    private const string WorkHarderPrefix =
        "modifier.salary.work_harder.";

    private const string RecoverPrefix =
        "modifier.salary.recover.";

    private readonly ICareerService _career;

    public CareerIncomeProvider(ICareerService career)
    {
        _career = career;
    }

    public string Id => "career.salary_and_pension";

    public decimal GetAnnualIncome(IPerson person)
    {
        if (person.Tags.Has("state.dead"))
            return 0;

        var income =
            _career.GetAnnualIncome(person);

        var career =
            _career.GetCareer(person);

        if (income <= 0
            || career.IsRetired
            || career.JobLevel <= 0)
        {
            return income;
        }

        var adjustmentPercent =
            ReadPercent(
                person,
                WorkHarderPrefix)
            - ReadPercent(
                person,
                RecoverPrefix);

        adjustmentPercent =
            Math.Clamp(
                adjustmentPercent,
                -50,
                50);

        if (adjustmentPercent == 0)
            return income;

        return income
            * (1m + adjustmentPercent / 100m);
    }

    private static decimal ReadPercent(
        IPerson person,
        string prefix)
    {
        foreach (var tag in person.Tags.All)
        {
            if (!tag.StartsWith(
                    prefix,
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var value =
                tag[prefix.Length..];

            if (decimal.TryParse(
                value,
                out var percent))
            {
                return Math.Clamp(
                    percent,
                    0,
                    50);
            }
        }

        return 0;
    }
}
