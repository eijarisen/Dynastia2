using Dynastia.Contracts;

namespace Dynastia.Mechanics.Inheritance;

internal sealed class EstateRecipientRules(IFamilyService family, IEconomyService economy)
{
    private readonly IFamilyService _family = family;
    private readonly IEconomyService _economy = economy;

    public bool HasEstablishedHouseholdOutsideEstate(
        IPerson person,
        Guid? estateHouseholdId)
    {
        if (person.Age < 18)
            return false;

        var householdId =
            _economy.GetHouseholdId(
                person);

        if (householdId is null
            || householdId
                == estateHouseholdId)
        {
            return false;
        }

        if (_economy.HasHousehold(
            person))
        {
            return true;
        }

        if (_economy
            .GetHouseholdDynastyAnchorId(
                person)
            == person.Id)
        {
            return true;
        }

        var spouse =
            _family.GetSpouse(
                person);

        return spouse is not null
            && _economy.GetHouseholdId(
                spouse)
                == householdId;
    }

    // Living-anchor transfers deliberately have the less restrictive historic test.
    public bool HasLivingAnchorHousehold(IPerson anchor, Guid? estateHouseholdId)
    {
        var householdId = _economy.GetHouseholdId(anchor);
        return anchor.Age >= 18 && householdId is not null && householdId != estateHouseholdId;
    }
}
