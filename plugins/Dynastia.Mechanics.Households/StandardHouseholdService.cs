using Dynastia.Contracts;

namespace Dynastia.Mechanics.Households;

public sealed class StandardHouseholdService :
    IHouseholdService
{
    private readonly IGameState _gameState;
    private readonly IFamilyService _family;
    private readonly IEconomyService _economy;
    private readonly ICareerService _career;

    public StandardHouseholdService(
        IGameState gameState,
        IFamilyService family,
        IEconomyService economy,
        ICareerService career)
    {
        _gameState = gameState;
        _family = family;
        _economy = economy;
        _career = career;
    }

    public IPerson? ResolveHouseholdHead(
        IPerson person)
    {
        // An adult male-lineage person owns his own dynasty household.
        if (person.Age >= 18
            && !person.Tags.Has(
                "residence.orphanage")
            && (
                (
                    _family.GetSex(person)
                        == Sex.Male
                    && _family.IsMaleLineage(
                        person)
                )
                || person.Tags.Has(
                    "household.independent_orphan"))
            && _economy.HasHousehold(
                person))
        {
            return person;
        }

        // Adopted/hosted wards belong to the host household for
        // expenses, poverty and large-family strain.
        foreach (var candidate in
            _gameState.People)
        {
            if (!candidate.Tags.Has(
                    "state.alive")
                || !_economy.HasHousehold(
                    candidate))
            {
                continue;
            }

            if (_economy
                .GetHostedDependentIds(
                    candidate)
                .Contains(
                    person.Id))
            {
                return candidate;
            }
        }

        var father =
            _family.GetFather(person);

        // A child who has lost both parents must not keep inheriting
        // the dead father's household-health state.
        if (father is not null
            && _family.IsMaleLineage(father)
            && (!person.Tags.Has(
                    "trait.orphan")
                || father.Tags.Has(
                    "state.alive")))
        {
            return father;
        }

        var husband =
            _family.GetSpouse(person);

        if (husband is not null
            && _family.GetSex(husband) == Sex.Male
            && _family.IsMaleLineage(husband))
        {
            return husband;
        }

        return null;
    }

    public HouseholdStatusSnapshot? GetStatus(
        IPerson head)
    {
        var finance =
            _economy.GetHousehold(head);

        if (finance is null)
            return null;

        var spouse =
            _family.GetSpouse(head);

        if (spouse is not null
            && !spouse.Tags.Has("state.alive"))
        {
            spouse = null;
        }

        var biologicalMinorIds =
            _family.GetChildren(head)
                .Where(
                    child =>
                        child.Tags.Has(
                            "state.alive")
                        && !child.Tags.Has(
                            "role.nanny")
                        && child.Age < 18)
                .Select(
                    child =>
                        child.Id)
                .ToHashSet();

        foreach (var dependentId in
            _economy.GetHostedDependentIds(
                head))
        {
            var dependent =
                _gameState.People
                    .FirstOrDefault(
                        person =>
                            person.Id
                            == dependentId);

            if (dependent is not null
                && dependent.Tags.Has(
                    "state.alive")
                && !dependent.Tags.Has(
                    "role.nanny")
                && dependent.Age < 18)
            {
                biologicalMinorIds.Add(
                    dependent.Id);
            }
        }

        var underageChildren =
            biologicalMinorIds.Count;

        var isHousewife =
            spouse is not null
            && _career.GetCareer(spouse)
                .JobTitle.Equals(
                    "Housewife",
                    StringComparison.OrdinalIgnoreCase);

        var baseCapacity =
            isHousewife ? 4 : 3;

        var hasNannyReference =
            finance.NannyId.HasValue;

        var effectiveCapacity =
            baseCapacity
            + (hasNannyReference ? 2 : 0);

        var strained =
            underageChildren > effectiveCapacity
            && !hasNannyReference;

        var atCapacity =
            head.Tags.Has("state.alive")
            && underageChildren == effectiveCapacity
            && !hasNannyReference;

        var broke =
            head.Tags.Has("state.alive")
                ? finance.Wealth <= 0
                : _economy.GetPendingInheritance(head) <= 0;

        var nanny =
            GetNanny(head);

        var warnings =
            new List<string>();

        if (strained)
        {
            warnings.Add(
                "The large family size is putting a strain on everyone.");
        }
        else if (atCapacity)
        {
            warnings.Add(
                "Having more kids will strain the family.");
        }

        if (broke)
        {
            warnings.Add(
                "Being broke is negatively impacting the family's health.");
        }

        return new HouseholdStatusSnapshot(
            head.Id,
            underageChildren,
            baseCapacity,
            effectiveCapacity,
            finance.NannyId,
            nanny is null
                ? null
                : _family.GetDisplayName(nanny),
            hasNannyReference,
            strained,
            atCapacity,
            broke,
            warnings);
    }

    public IPerson? GetNanny(
        IPerson head)
    {
        var finance =
            _economy.GetHousehold(head);

        if (finance?.NannyId is not Guid id)
            return null;

        return _gameState.People.FirstOrDefault(
            person => person.Id == id);
    }
}
