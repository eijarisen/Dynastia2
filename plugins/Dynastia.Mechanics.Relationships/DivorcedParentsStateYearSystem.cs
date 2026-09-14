using Dynastia.Contracts;

namespace Dynastia.Mechanics.Relationships;

internal sealed class DivorcedParentsStateYearSystem :
    IYearSystem
{
    private readonly IFamilyService _family;
    private readonly IHealthService _health;

    public DivorcedParentsStateYearSystem(
        IFamilyService family,
        IHealthService health)
    {
        _family = family;
        _health = health;
    }

    public string Id =>
        "relationships.parents_divorced_state";

    public YearPhase Phase =>
        YearPhase.Status;

    public IReadOnlyCollection<string> Before =>
        Array.Empty<string>();

    public IReadOnlyCollection<string> After =>
        Array.Empty<string>();

    public void Execute(
        IGameState gameState)
    {
        foreach (var person in
            gameState.People)
        {
            if (person.Age >= 18
                || person.Tags.Has(
                    "state.dead"))
            {
                person.Tags.Remove(
                    DivorcedParentsTracker.Tag);

                _health.RemoveCondition(
                    person,
                    "parents_divorced");

                continue;
            }

            if (person.Tags.Has(
                    DivorcedParentsTracker.Tag)
                || ParentsHaveDivorced(
                    person))
            {
                person.Tags.Add(
                    DivorcedParentsTracker.Tag);

                _health.AddCondition(
                    person,
                    "parents_divorced",
                    gameState.Year);
            }
        }
    }

    private bool ParentsHaveDivorced(
        IPerson child)
    {
        var father =
            _family.GetFather(
                child);

        var mother =
            _family.GetMother(
                child);

        if (father is null
            || mother is null)
        {
            return false;
        }

        return _family
            .GetRelationshipHistory(
                father)
            .Any(relationship =>
                relationship.SpouseId == mother.Id
                && relationship.EndYear.HasValue
                && (child.BirthDate is null
                    || child.BirthDate.Value.Year
                        <= relationship.EndYear.Value)
                && relationship.EndReason?.Contains(
                    "divorce",
                    StringComparison.OrdinalIgnoreCase)
                    == true);
    }
}
