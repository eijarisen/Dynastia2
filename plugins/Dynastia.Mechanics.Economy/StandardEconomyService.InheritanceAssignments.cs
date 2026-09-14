namespace Dynastia.Mechanics.Economy;

public sealed partial class StandardEconomyService
{
    internal void ClearHouseInheritanceAssignments(
        Guid heirId)
    {
        foreach (var person in _gameState.People)
        {
            var household =
                person.Components.Get<
                    HouseholdEconomyComponent>();

            if (household is not null)
            {
                foreach (var house in household.Houses)
                {
                    if (house.AssignedHeirId == heirId)
                        house.AssignedHeirId = null;
                }
            }

            var estate =
                person.Components.Get<
                    PersonalEstateComponent>();

            if (estate is null)
                continue;

            foreach (var house in
                estate.PendingHouseProperties)
            {
                if (house.AssignedHeirId == heirId)
                    house.AssignedHeirId = null;
            }
        }
    }
}
