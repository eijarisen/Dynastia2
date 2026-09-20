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
        {
            var excessChildren = Math.Max(
                0,
                status.UnderageChildren - status.EffectiveChildCapacity);

            if (excessChildren > 0)
            {
                yield return new StressContribution(
                    "household.large_family_strain",
                    excessChildren);
            }
        }

        if (status?.IsOvercrowded == true)
        {
            var penalty = HouseholdCrowdingRules.GetAnnualStressPenalty(
                status.ResidentCount);

            if (penalty > 0)
            {
                yield return new StressContribution(
                    "household.overcrowded",
                    penalty);
            }
        }
    }
}
