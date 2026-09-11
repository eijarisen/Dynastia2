using Dynastia.Contracts;

namespace Dynastia.Mechanics.Relationships;

public sealed class StandardMarriageSatisfactionService :
    IMarriageSatisfactionService
{
    private const double StartingSatisfaction =
        90;

    private const double ChildbirthBonus =
        10;

    private readonly IGameState _gameState;
    private readonly IFamilyService _family;

    public StandardMarriageSatisfactionService(
        IGameState gameState,
        IFamilyService family,
        IGameEventBus events)
    {
        _gameState = gameState;
        _family = family;

        events.EventPublished +=
            OnEventPublished;
    }

    public MarriageSatisfactionSnapshot?
        GetSatisfaction(
            IPerson person)
    {
        var spouse =
            _family.GetSpouse(
                person);

        if (spouse is null
            || !person.Tags.Has(
                "state.alive")
            || !spouse.Tags.Has(
                "state.alive"))
        {
            return null;
        }

        // Satisfaction currently models marriages between a man and wife.
        // Same-sex partnerships keep their existing relationship mechanics
        // without this additional score.
        if (!TryResolveHusbandAndWife(
            person,
            spouse,
            out _,
            out _))
        {
            return null;
        }

        var component =
            EnsurePair(
                person,
                spouse);

        return ToSnapshot(
            component);
    }

    public void InitializeMarriage(
        IPerson first,
        IPerson second,
        int startYear)
    {
        if (!TryResolveHusbandAndWife(
            first,
            second,
            out _,
            out _))
        {
            return;
        }

        SetPair(
            first,
            second,
            StartingSatisfaction,
            startYear,
            []);
    }

    public void ChangeSatisfaction(
        IPerson person,
        double amount)
    {
        var spouse =
            _family.GetSpouse(
                person);

        if (spouse is null
            || !TryResolveHusbandAndWife(
                person,
                spouse,
                out _,
                out _))
        {
            return;
        }

        var component =
            EnsurePair(
                person,
                spouse);

        SetPair(
            person,
            spouse,
            Math.Clamp(
                component.Satisfaction
                + amount,
                0,
                100),
            component.StartYear,
            component.CurrentIssues);
    }

    internal void ApplyAnnualEvaluation(
        IPerson first,
        IPerson second,
        double amount,
        IReadOnlyList<string> issues)
    {
        var component =
            EnsurePair(
                first,
                second);

        SetPair(
            first,
            second,
            Math.Clamp(
                component.Satisfaction
                + amount,
                0,
                100),
            component.StartYear,
            issues);
    }

    private void OnEventPublished(
        object? sender,
        GameEvent gameEvent)
    {
        if (gameEvent.Type.Equals(
                "relationship.married",
                StringComparison.OrdinalIgnoreCase)
            || gameEvent.Type.Equals(
                "relationship.remarried",
                StringComparison.OrdinalIgnoreCase))
        {
            var first =
                FindPerson(
                    gameEvent.SubjectId);

            var second =
                FindRelatedPerson(
                    gameEvent,
                    0);

            if (first is not null
                && second is not null)
            {
                InitializeMarriage(
                    first,
                    second,
                    gameEvent.Year);
            }

            return;
        }

        if (!gameEvent.Type.Equals(
            "life.birth",
            StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var father =
            FindRelatedPerson(
                gameEvent,
                0);

        var mother =
            FindRelatedPerson(
                gameEvent,
                1);

        if (father is null
            || mother is null
            || _family.GetSpouse(
                father)?.Id
                != mother.Id)
        {
            return;
        }

        ChangeSatisfaction(
            father,
            ChildbirthBonus);
    }

    private MarriageSatisfactionComponent
        EnsurePair(
            IPerson person,
            IPerson spouse)
    {
        var own =
            person.Components.Get<
                MarriageSatisfactionComponent>();

        if (own?.SpouseId
            == spouse.Id)
        {
            return own;
        }

        var spouseComponent =
            spouse.Components.Get<
                MarriageSatisfactionComponent>();

        if (spouseComponent?.SpouseId
            == person.Id)
        {
            SetPair(
                person,
                spouse,
                spouseComponent.Satisfaction,
                spouseComponent.StartYear,
                spouseComponent.CurrentIssues);

            return person.Components.Get<
                MarriageSatisfactionComponent>()
                ?? throw new InvalidOperationException(
                    "Marriage satisfaction could not be restored.");
        }

        var startYear =
            ResolveRelationshipStartYear(
                person,
                spouse);

        SetPair(
            person,
            spouse,
            StartingSatisfaction,
            startYear,
            []);

        return person.Components.Get<
            MarriageSatisfactionComponent>()
            ?? throw new InvalidOperationException(
                "Marriage satisfaction could not be initialized.");
    }

    private int ResolveRelationshipStartYear(
        IPerson person,
        IPerson spouse)
    {
        var current =
            _family
                .GetRelationshipHistory(
                    person)
                .LastOrDefault(
                    relationship =>
                        relationship.SpouseId
                            == spouse.Id
                        && relationship.EndYear
                            is null);

        return current?.StartYear
            ?? _gameState.Year;
    }

    private static void SetPair(
        IPerson first,
        IPerson second,
        double value,
        int startYear,
        IReadOnlyList<string> issues)
    {
        var issueCopy =
            issues.ToList();

        SetComponent(
            first,
            second.Id,
            value,
            startYear,
            issueCopy);

        SetComponent(
            second,
            first.Id,
            value,
            startYear,
            issueCopy);
    }

    private static void SetComponent(
        IPerson person,
        Guid spouseId,
        double value,
        int startYear,
        IReadOnlyList<string> issues)
    {
        var component =
            person.Components.Get<
                MarriageSatisfactionComponent>()
            ?? new MarriageSatisfactionComponent();

        component.SpouseId =
            spouseId;

        component.Satisfaction =
            Math.Clamp(
                value,
                0,
                100);

        component.StartYear =
            startYear;

        component.CurrentIssues.Clear();
        component.CurrentIssues.AddRange(
            issues);

        person.Components.Set(
            component);
    }

    private static MarriageSatisfactionSnapshot
        ToSnapshot(
            MarriageSatisfactionComponent component)
    {
        return new MarriageSatisfactionSnapshot(
            component.SpouseId,
            component.Satisfaction,
            ResolveLabel(
                component.Satisfaction),
            component.StartYear,
            component.CurrentIssues.ToList());
    }

    private static string ResolveLabel(
        double value)
    {
        return value switch
        {
            < 20 =>
                "Miserable",

            < 40 =>
                "Unhappy",

            < 60 =>
                "Content",

            < 80 =>
                "Satisfied",

            _ =>
                "Thriving"
        };
    }

    private bool TryResolveHusbandAndWife(
        IPerson first,
        IPerson second,
        out IPerson husband,
        out IPerson wife)
    {
        husband = null!;
        wife = null!;

        if (_family.GetSex(first)
                == Sex.Male
            && _family.GetSex(second)
                == Sex.Female)
        {
            husband = first;
            wife = second;
            return true;
        }

        if (_family.GetSex(second)
                == Sex.Male
            && _family.GetSex(first)
                == Sex.Female)
        {
            husband = second;
            wife = first;
            return true;
        }

        return false;
    }

    private IPerson? FindPerson(
        Guid? id)
    {
        if (id is null)
            return null;

        return _gameState.People
            .FirstOrDefault(
                person =>
                    person.Id
                    == id.Value);
    }

    private IPerson? FindRelatedPerson(
        GameEvent gameEvent,
        int index)
    {
        if (index < 0
            || index
                >= gameEvent
                    .RelatedPersonIds
                    .Count)
        {
            return null;
        }

        return FindPerson(
            gameEvent
                .RelatedPersonIds[index]);
    }
}
