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
            .Where(x => x is not null)
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
            AddChild(father, child);

        if (mother is not null)
            AddChild(mother, child);
    }

    public void SetSpouses(
        IPerson first,
        IPerson second,
        int startYear)
    {
        var firstFamily = GetRequired(first);
        var secondFamily = GetRequired(second);

        firstFamily.SpouseId = second.Id;
        secondFamily.SpouseId = first.Id;

        first.Tags.Remove("relationship.single");
        second.Tags.Remove("relationship.single");

        first.Tags.Add("relationship.married");
        second.Tags.Add("relationship.married");

        AddMarriageRecord(
            firstFamily,
            second.Id,
            startYear);

        AddMarriageRecord(
            secondFamily,
            first.Id,
            startYear);
    }

    public void EndRelationship(
        IPerson first,
        IPerson second,
        int endYear,
        string endReason,
        bool clearFirst = true,
        bool clearSecond = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(endReason);

        EndMarriageRecord(
            GetRequired(first),
            second.Id,
            endYear,
            endReason);

        EndMarriageRecord(
            GetRequired(second),
            first.Id,
            endYear,
            endReason);

        first.Tags.Remove("relationship.married");
        second.Tags.Remove("relationship.married");

        if (clearFirst)
        {
            GetRequired(first).SpouseId = null;

            if (!first.Tags.Has("state.dead"))
                first.Tags.Add("relationship.single");
        }

        if (clearSecond)
        {
            GetRequired(second).SpouseId = null;

            if (!second.Tags.Has("state.dead"))
                second.Tags.Add("relationship.single");
        }
    }

    public IReadOnlyList<RelationshipHistoryInfo> GetRelationshipHistory(
        IPerson person)
    {
        return GetRequired(person)
            .MarriageHistory
            .Select(x => new RelationshipHistoryInfo(
                x.SpouseId,
                x.StartYear,
                x.EndYear,
                x.EndReason))
            .ToList();
    }

    public void SetGeneratedFamilyBackground(
        IPerson person,
        GeneratedFamilyBackgroundInfo background)
    {
        ArgumentNullException.ThrowIfNull(background);

        GetRequired(person).GeneratedBackground =
            background;
    }

    public GeneratedFamilyBackgroundInfo? GetGeneratedFamilyBackground(
        IPerson person)
    {
        return GetRequired(person).GeneratedBackground;
    }

    public string FormatSurname(
        string surname,
        Sex sex)
    {
        if (sex != Sex.Female)
            return surname;

        if (surname.EndsWith(
            "ski",
            StringComparison.OrdinalIgnoreCase))
        {
            return surname[..^3] + "ska";
        }

        if (surname.EndsWith(
            "cki",
            StringComparison.OrdinalIgnoreCase))
        {
            return surname[..^3] + "cka";
        }

        return surname;
    }

    public string GetDisplayName(
        IPerson person)
    {
        return $"{person.Name} " +
            FormatSurname(
                person.Surname,
                GetSex(person));
    }

    public bool IsBloodline(IPerson person)
    {
        return person.Tags.Has("family.bloodline");
    }

    public bool IsMaleLineage(IPerson person)
    {
        return person.Tags.Has("lineage.male");
    }

    private static void AddMarriageRecord(
        FamilyComponent family,
        Guid spouseId,
        int startYear)
    {
        if (family.MarriageHistory.Any(
            x => x.SpouseId == spouseId
                && x.EndYear is null))
        {
            return;
        }

        family.MarriageHistory.Add(
            new MarriageRecord
            {
                SpouseId = spouseId,
                StartYear = startYear
            });
    }

    private static void EndMarriageRecord(
        FamilyComponent family,
        Guid spouseId,
        int endYear,
        string endReason)
    {
        var record =
            family.MarriageHistory
                .LastOrDefault(
                    x => x.SpouseId == spouseId
                        && x.EndYear is null);

        if (record is null)
            return;

        record.EndYear = endYear;
        record.EndReason = endReason;
    }

    private void AddChild(
        IPerson parent,
        IPerson child)
    {
        var family = GetRequired(parent);

        if (!family.ChildrenIds.Contains(child.Id))
            family.ChildrenIds.Add(child.Id);
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
                $"Person '{person.Name} {person.Surname}' " +
                "has no family component.");
    }
}
