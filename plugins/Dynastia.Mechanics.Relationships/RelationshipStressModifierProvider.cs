using Dynastia.Contracts;

namespace Dynastia.Mechanics.Relationships;

internal sealed class RelationshipStressModifierProvider : IStressModifierProvider
{
    private readonly IMarriageSatisfactionService _satisfaction;

    public RelationshipStressModifierProvider(IMarriageSatisfactionService satisfaction)
    {
        _satisfaction = satisfaction;
    }

    public string Id => "relationships.stress";

    public IEnumerable<StressContribution> GetStressContributions(IPerson person, int year)
    {
        var snapshot = _satisfaction.GetSatisfaction(person);
        if (snapshot is null)
            yield break;

        if (snapshot.Value < 10)
            yield return new StressContribution("relationship.very_low_satisfaction", 2);
        else if (snapshot.Value < 20)
            yield return new StressContribution("relationship.very_low_satisfaction", 1);
    }
}
