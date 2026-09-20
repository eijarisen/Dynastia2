using Dynastia.Contracts;

namespace Dynastia.Mechanics.FamilyRelations;

internal sealed class FamilyRelationEventBridge
{
    private readonly IGameState _gameState;
    private readonly IFamilyService _family;
    private readonly IEconomyService _economy;
    private readonly IHouseholdService _households;
    private readonly StandardFamilyRelationService _relations;
    private readonly IChildHappinessService _childHappiness;

    public FamilyRelationEventBridge(
        IGameState gameState,
        IFamilyService family,
        IEconomyService economy,
        IHouseholdService households,
        StandardFamilyRelationService relations,
        IChildHappinessService childHappiness,
        IGameEventBus events)
    {
        _gameState = gameState;
        _family = family;
        _economy = economy;
        _households = households;
        _relations = relations;
        _childHappiness = childHappiness;
        events.EventPublished += OnEvent;
    }

    private void OnEvent(object? sender, GameEvent e)
    {
        if (e.Type.Equals("life.birth", StringComparison.OrdinalIgnoreCase))
        {
            var child = FindOptional(e.SubjectId);
            if (child is null)
                return;
            foreach (var id in e.RelatedPersonIds.Take(2))
            {
                var parent = Find(id);
                if (parent is not null)
                    _relations.EnsureRelation(parent, child, FamilyRelationshipType.ParentChild, 72);
            }
            return;
        }

        if (e.Type.Equals("education.help_learning_success", StringComparison.OrdinalIgnoreCase)
            || e.Type.Equals("education.help_learning_failure", StringComparison.OrdinalIgnoreCase)
            || e.Type.Equals("childhood.raised", StringComparison.OrdinalIgnoreCase))
        {
            var child = FindOptional(e.SubjectId);
            var parent = e.RelatedPersonIds.Select(Find).FirstOrDefault(p => p is not null);
            if (child is not null && parent is not null && _relations.GetRelation(child, parent) is not null)
            {
                var bonus = e.Type.Equals("childhood.raised", StringComparison.OrdinalIgnoreCase) ? 3 : 2;
                _relations.ModifyRelation(child, parent, bonus);
            }
            return;
        }

        if (e.Type.Equals("relationship.affair", StringComparison.OrdinalIgnoreCase))
        {
            var offender = FindOptional(e.SubjectId);
            var spouse = e.RelatedPersonIds.Select(Find).FirstOrDefault(p => p is not null);
            if (offender is not null)
            {
                ApplyHouseholdFamilyFallout(
                    offender,
                    24,
                    GetCurrentHouseholdMemberIds(offender),
                    spouse is null ? [] : [spouse.Id]);
            }

            return;
        }

        if (IsDivorce(e.Type))
        {
            var first = FindOptional(e.SubjectId);
            var second = e.RelatedPersonIds.Select(Find).FirstOrDefault(p => p is not null);
            if (first is null || second is null)
                return;

            // Even a previously Thriving marriage must not remain Warm after
            // an actual divorce. Affairs are handled separately above because
            // they can now damage a marriage without automatically ending it.
            _relations.ConvertDivorceToExSpouse(
                first,
                second,
                55);

            ApplyParentalDivorceDamage(
                first,
                second,
                isAffair: false);

            var formerHouseholdIds = GetFormerHouseholdMemberIds(e);
            ApplyHouseholdFamilyFallout(
                first,
                18,
                formerHouseholdIds,
                [second.Id]);

            ApplyHouseholdFamilyFallout(
                second,
                12,
                formerHouseholdIds,
                [first.Id]);

            return;
        }

        if (e.Type.Equals("justice.crime", StringComparison.OrdinalIgnoreCase)
            || e.Type.Equals("justice.crime_uncaught", StringComparison.OrdinalIgnoreCase))
        {
            var offender = FindOptional(e.SubjectId);
            if (offender is not null)
            {
                ApplyHouseholdFamilyFallout(
                    offender,
                    e.Type.Equals("justice.crime", StringComparison.OrdinalIgnoreCase) ? 24 : 12,
                    GetCurrentHouseholdMemberIds(offender),
                    []);
            }
            return;
        }

        if (e.Type.Equals("household.member_moved_out", StringComparison.OrdinalIgnoreCase))
        {
            ApplyChildhoodHappinessHandoff(e);
            return;
        }

        if (e.Type.Equals("inheritance.disadvantaged", StringComparison.OrdinalIgnoreCase))
        {
            ApplyInheritanceDisadvantage(e);
            return;
        }

        if (e.Type.Equals("life.death", StringComparison.OrdinalIgnoreCase)
            && e.SubjectId is Guid deceasedId)
            ApplySharedBereavement(deceasedId);
    }

    private void ApplyChildhoodHappinessHandoff(
        GameEvent gameEvent)
    {
        if (!gameEvent.Data.TryGetValue("targetId", out var targetValue)
            || !Guid.TryParse(targetValue, out var targetId))
        {
            return;
        }

        var adultChild = Find(targetId);
        if (adultChild is null
            || adultChild.Age < 18
            || adultChild.Tags.Has("family_relations.childhood_handoff_applied"))
        {
            return;
        }

        var happiness = _childHappiness.GetFinalChildhoodHappiness(adultChild);
        if (happiness is null)
            return;

        var parentDelta = (happiness.Value - 3) * 6.0;
        var siblingDelta = (happiness.Value - 3) * 4.0;

        foreach (var parent in new[]
                 {
                     _family.GetFather(adultChild),
                     _family.GetMother(adultChild)
                 }
                 .Where(person => person is not null)
                 .Cast<IPerson>())
        {
            if (_relations.GetRelation(adultChild, parent) is not null
                && parentDelta != 0)
            {
                _relations.ModifyRelation(
                    adultChild,
                    parent,
                    parentDelta,
                    majorInteraction: false);
            }
        }

        if (siblingDelta != 0)
        {
            foreach (var sibling in GetSiblings(adultChild))
            {
                if (_relations.GetRelation(adultChild, sibling) is null)
                    continue;

                _relations.ModifyRelation(
                    adultChild,
                    sibling,
                    siblingDelta,
                    majorInteraction: false);
            }
        }

        adultChild.Tags.Add("family_relations.childhood_handoff_applied");
    }

    private void ApplyInheritanceDisadvantage(
        GameEvent gameEvent)
    {
        var disadvantaged = FindOptional(gameEvent.SubjectId);
        if (disadvantaged is null)
            return;

        IPerson? source = null;
        if (gameEvent.Data.TryGetValue("sourceId", out var sourceValue)
            && Guid.TryParse(sourceValue, out var sourceId))
        {
            source = Find(sourceId);
        }

        var severity = gameEvent.Data.GetValueOrDefault("severity") ?? "heavy";
        var sourceDamage = severity.Equals("skipped", StringComparison.OrdinalIgnoreCase)
            ? 10.0
            : 6.0;
        var siblingDamage = severity.Equals("skipped", StringComparison.OrdinalIgnoreCase)
            ? 8.0
            : 4.0;

        if (source is not null
            && _relations.GetRelation(disadvantaged, source) is not null)
        {
            _relations.ModifyRelation(
                disadvantaged,
                source,
                -sourceDamage,
                majorInteraction: true);
        }

        if (!gameEvent.Data.TryGetValue("favoredHeirIds", out var favoredRaw))
            return;

        foreach (var id in favoredRaw
                     .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                     .Select(value => Guid.TryParse(value, out var parsed) ? parsed : Guid.Empty)
                     .Where(id => id != Guid.Empty && id != disadvantaged.Id))
        {
            var favored = Find(id);
            if (favored is null
                || _relations.GetRelation(disadvantaged, favored) is null)
            {
                continue;
            }

            _relations.ModifyRelation(
                disadvantaged,
                favored,
                -siblingDamage,
                majorInteraction: true);
        }
    }

    private IReadOnlyList<IPerson> GetSiblings(
        IPerson person)
    {
        var fatherId = _family.GetFather(person)?.Id;
        var motherId = _family.GetMother(person)?.Id;

        return _gameState.People
            .Where(candidate => candidate.Id != person.Id)
            .Where(candidate =>
                (fatherId is Guid father
                    && _family.GetFather(candidate)?.Id == father)
                || (motherId is Guid mother
                    && _family.GetMother(candidate)?.Id == mother))
            .DistinctBy(candidate => candidate.Id)
            .ToList();
    }

    private void ApplyHouseholdFamilyFallout(
        IPerson subject,
        double damage,
        IReadOnlyCollection<Guid> memberIds,
        IReadOnlyCollection<Guid> excludedPersonIds)
    {
        if (damage <= 0)
            return;

        foreach (var memberId in memberIds)
        {
            if (memberId == subject.Id
                || excludedPersonIds.Contains(memberId))
            {
                continue;
            }

            var member = Find(memberId);
            if (member is null
                || !member.Tags.Has("state.alive")
                || _relations.GetRelation(subject, member) is null)
            {
                continue;
            }

            _relations.ModifyRelation(
                subject,
                member,
                -damage);
        }
    }

    private void ApplyParentalDivorceDamage(
        IPerson first,
        IPerson second,
        bool isAffair)
    {
        foreach (var child in _gameState.People.Where(child =>
            child.Tags.Has("state.alive")
            && child.Age < 18
            && ((_family.GetFather(child)?.Id == first.Id && _family.GetMother(child)?.Id == second.Id)
                || (_family.GetFather(child)?.Id == second.Id && _family.GetMother(child)?.Id == first.Id))))
        {
            foreach (var parent in new[] { first, second })
            {
                if (_relations.GetRelation(parent, child) is null)
                    continue;

                var childHead = _households.ResolveHouseholdHead(child);
                var parentHead = _households.ResolveHouseholdHead(parent);
                var sameHousehold = childHead is not null && parentHead is not null
                    && _economy.GetHouseholdId(childHead) == _economy.GetHouseholdId(parentHead);

                var damage = isAffair
                    ? sameHousehold ? 18 : 30
                    : sameHousehold ? 12 : 25;

                _relations.ModifyRelation(parent, child, -damage);
            }
        }
    }

    private IReadOnlyCollection<Guid> GetFormerHouseholdMemberIds(
        GameEvent gameEvent)
    {
        if (gameEvent.Data.TryGetValue(
                "formerHouseholdMemberIds",
                out var serialized))
        {
            var parsed = serialized
                .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(value => Guid.TryParseExact(value, "N", out var id) ? id : Guid.Empty)
                .Where(id => id != Guid.Empty)
                .Distinct()
                .ToList();

            if (parsed.Count > 0)
                return parsed;
        }

        var fallback = new HashSet<Guid>();
        var subject = FindOptional(gameEvent.SubjectId);
        if (subject is not null)
        {
            foreach (var id in _economy.GetHouseholdMemberIds(subject))
                fallback.Add(id);
        }

        foreach (var relatedId in gameEvent.RelatedPersonIds)
        {
            var related = Find(relatedId);
            if (related is null)
                continue;

            foreach (var id in _economy.GetHouseholdMemberIds(related))
                fallback.Add(id);
        }

        return fallback;
    }

    private IReadOnlyCollection<Guid> GetCurrentHouseholdMemberIds(
        IPerson person) =>
        _economy.GetHouseholdMemberIds(person)
            .Distinct()
            .ToList();

    private void ApplySharedBereavement(Guid deceasedId)
    {
        var deceased = Find(deceasedId);
        if (deceased is null)
            return;

        var survivors = new List<IPerson>();
        var father = _family.GetFather(deceased);
        var mother = _family.GetMother(deceased);
        if (father?.Tags.Has("state.alive") == true) survivors.Add(father);
        if (mother?.Tags.Has("state.alive") == true) survivors.Add(mother);
        survivors.AddRange(_family.GetChildren(deceased).Where(p => p.Tags.Has("state.alive")));
        survivors.AddRange(_relations.GetSiblings(deceased).Where(p => p.Tags.Has("state.alive")));
        survivors = survivors.DistinctBy(p => p.Id).ToList();

        for (var i = 0; i < survivors.Count; i++)
        for (var j = i + 1; j < survivors.Count; j++)
        {
            var relation = _relations.GetRelation(survivors[i], survivors[j]);
            if (relation?.Score >= 60)
                _relations.ModifyRelation(survivors[i], survivors[j], 1, majorInteraction: false);
        }
    }

    private static bool IsDivorce(string type) =>
        type.Equals("relationship.divorce", StringComparison.OrdinalIgnoreCase)
        || type.Equals("relationship.low_satisfaction_divorce", StringComparison.OrdinalIgnoreCase)
        || type.Equals("relationship.prison_divorce", StringComparison.OrdinalIgnoreCase);

    private IPerson? FindOptional(Guid? id) => id is Guid value ? Find(value) : null;

    private IPerson? Find(Guid id) => _gameState.People.FirstOrDefault(p => p.Id == id);
}
