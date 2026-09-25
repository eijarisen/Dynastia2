namespace Dynastia.Contracts;

public enum HouseholdKinshipRole
{
    None = 0,
    Self = 1,
    Spouse = 2,
    ParentLike = 3,
    SiblingLike = 4,
    ChildLike = 5
}

public static class HouseholdKinshipRules
{
    public static HouseholdKinshipRole Resolve(
        IPerson actor,
        IPerson target,
        IFamilyService family)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(family);

        if (actor.Id == target.Id)
            return HouseholdKinshipRole.Self;

        if (family.GetSpouse(actor)?.Id == target.Id)
            return HouseholdKinshipRole.Spouse;

        if (IsParent(actor, target, family)
            || IsGrandparent(actor, target, family)
            || IsUncleOrAunt(actor, target, family))
        {
            return HouseholdKinshipRole.ParentLike;
        }

        if (IsSibling(actor, target, family)
            || IsFirstCousin(actor, target, family))
        {
            return HouseholdKinshipRole.SiblingLike;
        }

        if (IsChild(actor, target, family)
            || IsGrandchild(actor, target, family)
            || IsNephewOrNiece(actor, target, family))
        {
            return HouseholdKinshipRole.ChildLike;
        }

        return HouseholdKinshipRole.None;
    }

    public static bool IsSupportedRelative(
        IPerson actor,
        IPerson target,
        IFamilyService family)
    {
        var role = Resolve(actor, target, family);
        return role is not HouseholdKinshipRole.None
            and not HouseholdKinshipRole.Self;
    }

    public static bool IsSupportedResidentRelative(
        IPerson actor,
        IPerson target,
        IFamilyService family,
        IEconomyService economy,
        bool requireAdult = false)
    {
        if (target.Id == actor.Id
            || !IsSupportedRelative(actor, target, family))
        {
            return false;
        }

        return IsResidentHouseholdMember(
            actor,
            target,
            economy,
            requireAdult);
    }

    public static bool IsResidentHouseholdMember(
        IPerson actor,
        IPerson target,
        IEconomyService economy,
        bool requireAdult = false)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(economy);

        if (!actor.Tags.Has("state.alive")
            || !target.Tags.Has("state.alive")
            || requireAdult && target.Age < 18)
        {
            return false;
        }

        var householdId = economy.GetHouseholdId(actor);
        return householdId is not null
            && economy.GetHouseholdId(target) == householdId;
    }

    private static bool IsParent(
        IPerson person,
        IPerson candidate,
        IFamilyService family) =>
        family.GetFather(person)?.Id == candidate.Id
        || family.GetMother(person)?.Id == candidate.Id;

    private static bool IsChild(
        IPerson person,
        IPerson candidate,
        IFamilyService family) =>
        family.GetChildren(person).Any(child => child.Id == candidate.Id);

    private static bool IsGrandparent(
        IPerson person,
        IPerson candidate,
        IFamilyService family)
    {
        foreach (var parent in Parents(person, family))
        {
            if (IsParent(parent, candidate, family))
                return true;
        }

        return false;
    }

    private static bool IsGrandchild(
        IPerson person,
        IPerson candidate,
        IFamilyService family) =>
        family.GetChildren(person)
            .Any(child => IsChild(child, candidate, family));

    private static bool IsSibling(
        IPerson first,
        IPerson second,
        IFamilyService family)
    {
        var firstFather = family.GetFather(first)?.Id;
        var firstMother = family.GetMother(first)?.Id;

        return (firstFather is not null
                && family.GetFather(second)?.Id == firstFather)
            || (firstMother is not null
                && family.GetMother(second)?.Id == firstMother);
    }

    private static bool IsUncleOrAunt(
        IPerson person,
        IPerson candidate,
        IFamilyService family) =>
        Parents(person, family)
            .Any(parent => IsSibling(parent, candidate, family));

    private static bool IsNephewOrNiece(
        IPerson person,
        IPerson candidate,
        IFamilyService family) =>
        Siblings(person, family)
            .Any(sibling => IsChild(sibling, candidate, family));

    private static bool IsFirstCousin(
        IPerson first,
        IPerson second,
        IFamilyService family)
    {
        foreach (var parent in Parents(first, family))
        {
            foreach (var parentSibling in Siblings(parent, family))
            {
                if (IsChild(parentSibling, second, family))
                    return true;
            }
        }

        return false;
    }

    private static IEnumerable<IPerson> Parents(
        IPerson person,
        IFamilyService family)
    {
        var father = family.GetFather(person);
        if (father is not null)
            yield return father;

        var mother = family.GetMother(person);
        if (mother is not null)
            yield return mother;
    }

    private static IEnumerable<IPerson> Siblings(
        IPerson person,
        IFamilyService family)
    {
        var seen = new HashSet<Guid>();

        foreach (var parent in Parents(person, family))
        {
            foreach (var child in family.GetChildren(parent))
            {
                if (child.Id != person.Id && seen.Add(child.Id))
                    yield return child;
            }
        }
    }
}
