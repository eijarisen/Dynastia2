using Dynastia.Contracts;

namespace Dynastia.Mechanics.Family;

public sealed class StandardFamilyService : IFamilyService
{
    private readonly IGameState _gameState;

    public StandardFamilyService(IGameState gameState)
    {
        _gameState = gameState;
    }

    public void InitializePerson(
        IPerson person,
        Sex sex,
        int? generation = null)
    {
        if (person.Components.Has<FamilyComponent>())
            return;

        person.Components.Set(
            new FamilyComponent
            {
                Sex = sex,
                Generation = generation
            });

        person.Tags.Add(
            sex == Sex.Male
                ? "sex.male"
                : "sex.female");
    }

    public Sex GetSex(IPerson person)
    {
        return GetRequired(person).Sex;
    }

    public int? GetGeneration(IPerson person)
    {
        return GetRequired(person).Generation;
    }

    public IPerson? GetFather(IPerson person)
    {
        return FindPerson(GetRequired(person).FatherId);
    }

    public IPerson? GetMother(IPerson person)
    {
        return FindPerson(GetRequired(person).MotherId);
    }

    public IPerson? GetSpouse(IPerson person)
    {
        return FindPerson(GetRequired(person).SpouseId);
    }

    public IReadOnlyList<IPerson> GetChildren(IPerson person)
    {
        var component = GetRequired(person);

    return component.ChildrenIds
        .Select(id => FindPerson(id))
        .Where(p => p is not null)
        .Cast<IPerson>()
        .ToList();
    }

    public void SetParents(
        IPerson child,
        IPerson? father,
        IPerson? mother)
    {
        var childFamily = GetRequired(child);

        childFamily.FatherId = father?.Id;
        childFamily.MotherId = mother?.Id;

        if (father is not null)
        {
            AddChild(father, child);
        }

        if (mother is not null)
        {
            AddChild(mother, child);
        }
    }

    public void SetSpouses(
        IPerson first,
        IPerson second)
    {
        GetRequired(first).SpouseId = second.Id;
        GetRequired(second).SpouseId = first.Id;

        first.Tags.Remove("relationship.single");
        second.Tags.Remove("relationship.single");

        first.Tags.Add("relationship.married");
        second.Tags.Add("relationship.married");
    }

    public bool IsBloodline(IPerson person)
    {
        return person.Tags.Has("family.bloodline");
    }

    public bool IsMaleLineage(IPerson person)
    {
        return person.Tags.Has("lineage.male");
    }

    private void AddChild(IPerson parent, IPerson child)
    {
        var family = GetRequired(parent);

        if (!family.ChildrenIds.Contains(child.Id))
        {
            family.ChildrenIds.Add(child.Id);
        }
    }

    private IPerson? FindPerson(Guid? id)
    {
        if (id is null)
            return null;

        return _gameState.People.FirstOrDefault(
            person => person.Id == id.Value);
    }

    private static FamilyComponent GetRequired(IPerson person)
    {
        return person.Components.Get<FamilyComponent>()
            ?? throw new InvalidOperationException(
                $"Person '{person.Name} {person.Surname}' has no family component.");
    }
}
