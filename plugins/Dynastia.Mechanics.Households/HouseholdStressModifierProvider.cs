using Dynastia.Contracts;

namespace Dynastia.Mechanics.Households;

internal sealed class HouseholdStressModifierProvider : IStressModifierProvider
{
    private readonly IHouseholdService _households;

    public HouseholdStressModifierProvider(IHouseholdService households)
    {
        _households = households;
    }

    public string Id => "households.stress";

    public IEnumerable<StressContribution> GetStressContributions(IPerson person, int year)
    {
        var head = _households.ResolveHouseholdHead(person);
        if (head is null)
            yield break;

        var status = _households.GetStatus(head);
        if (status?.IsLargeFamilyStrained == true)
            yield return new StressContribution("household.large_family_strain", 1);

        if (status?.IsOvercrowded == true)
            yield return new StressContribution(
                "household.overcrowded",
                HouseholdCrowdingRules.AnnualStressPenalty);
    }
}
