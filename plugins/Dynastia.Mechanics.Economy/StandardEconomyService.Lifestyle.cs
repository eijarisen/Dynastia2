using Dynastia.Contracts;

namespace Dynastia.Mechanics.Economy;

public sealed partial class StandardEconomyService
{
    public HouseholdLifestyleStance GetLifestyle(IPerson person) =>
        GetRequiredHousehold(person).Lifestyle;

    public void SetLifestyle(
        IPerson person,
        HouseholdLifestyleStance stance)
    {
        var household = GetRequiredHousehold(person);
        household.Lifestyle = stance;
        SynchronizeLifestyleTags(household);
    }

    internal void RecordBudgetHistory(
        HouseholdEconomyComponent household,
        int year)
    {
        var existing = household.BudgetHistory
            .FirstOrDefault(point => point.Year == year);

        var point = new HouseholdBudgetHistoryPoint(
            year,
            RoundCurrency(household.Wealth),
            RoundCurrency(household.LastIncome),
            RoundCurrency(household.LastExpenses));

        if (existing is not null)
        {
            var index = household.BudgetHistory.IndexOf(existing);
            household.BudgetHistory[index] = point;
        }
        else
        {
            household.BudgetHistory.Add(point);
        }

        household.BudgetHistory.Sort((left, right) => left.Year.CompareTo(right.Year));
    }

    private void SynchronizeLifestyleTags(
        HouseholdEconomyComponent household)
    {
        foreach (var memberId in household.MemberIds.Distinct())
        {
            var member = _gameState.People.FirstOrDefault(person => person.Id == memberId);
            if (member is null)
                continue;

            member.Tags.Remove(HouseholdLifestyleRules.LavishTag);
            member.Tags.Remove(HouseholdLifestyleRules.ThriftyTag);

            if (household.Lifestyle == HouseholdLifestyleStance.Lavish)
                member.Tags.Add(HouseholdLifestyleRules.LavishTag);
            else if (household.Lifestyle == HouseholdLifestyleStance.Thrifty)
                member.Tags.Add(HouseholdLifestyleRules.ThriftyTag);
        }
    }
}
