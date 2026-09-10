using Dynastia.Contracts;

namespace Dynastia.Mechanics.Career;

public sealed class CareerIncomeProvider : IIncomeProvider
{
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

        return _career.GetAnnualIncome(person);
    }
}
