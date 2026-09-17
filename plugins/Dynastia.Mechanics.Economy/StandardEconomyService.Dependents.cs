using Dynastia.Contracts;

namespace Dynastia.Mechanics.Economy;

public sealed partial class StandardEconomyService
{
    public void SetNanny(
        IPerson person,
        Guid? nannyId)
    {
        GetRequiredHousehold(
            person)
            .NannyId =
                nannyId;
    }

    public IReadOnlyList<Guid> GetHostedDependentIds(
        IPerson householdHead)
    {
        var resolved = FindHousehold(householdHead);
        return resolved is null
            ? Array.Empty<Guid>()
            : resolved.Value.Household.HostedDependentIds.ToList();
    }

    public void AddHostedDependent(
        IPerson householdHead,
        IPerson dependent)
    {
        var household =
            GetRequiredHousehold(
                householdHead);

        if (!household.HostedDependentIds.Contains(
            dependent.Id))
        {
            household.HostedDependentIds.Add(
                dependent.Id);
        }

        AddHouseholdMember(
            householdHead,
            dependent);
    }

    public void RemoveHostedDependent(
        IPerson householdHead,
        IPerson dependent)
    {
        var resolved =
            FindHousehold(
                householdHead);

        if (resolved is null)
            return;

        resolved.Value
            .Household
            .HostedDependentIds
            .Remove(
                dependent.Id);

        resolved.Value
            .Household
            .MemberIds
            .Remove(
                dependent.Id);
    }

    internal HouseholdEconomyComponent
        GetRequiredHousehold(
            IPerson person)
    {
        var resolved =
            FindHousehold(
                person);

        if (resolved is not null)
            return resolved.Value.Household;

        EnsureHousehold(
            person);

        resolved =
            FindHousehold(
                person);

        if (resolved is not null)
        {
            return resolved.Value
                .Household;
        }

        throw new InvalidOperationException(
            $"{_family.GetDisplayName(person)} " +
            "does not belong to an active dynasty household.");
    }

}
