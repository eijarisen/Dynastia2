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
        // Preserve Dynasty 4's exact lookup order:
        // self -> father -> current husband.
        if (_family.GetSex(person) == Sex.Male
            && _family.IsMaleLineage(person)
            && person.Age >= 18)
        {
            return person;
        }

        var father =
            _family.GetFather(person);

        if (father is not null
            && _family.IsMaleLineage(father))
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

        var underageChildren =
            _family.GetChildren(head)
                .Count(child =>
                    child.Tags.Has("state.alive")
                    && child.Age < 18);

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
