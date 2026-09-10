using Dynastia.Contracts;

namespace Dynastia.Mechanics.Inheritance;

public sealed class AdulthoodInheritanceSystem :
    IYearSystem
{
    private readonly IFamilyService _family;
    private readonly IEconomyService _economy;
    private readonly IGameEventBus _events;

    public AdulthoodInheritanceSystem(
        IFamilyService family,
        IEconomyService economy,
        IGameEventBus events)
    {
        _family = family;
        _economy = economy;
        _events = events;
    }

    public string Id =>
        "inheritance.adulthood_claims";

    public YearPhase Phase =>
        YearPhase.Status;

    public IReadOnlyCollection<string> Before =>
        Array.Empty<string>();

    public IReadOnlyCollection<string> After =>
        ["aging.increment_age"];

    public void Execute(
        IGameState gameState)
    {
        foreach (var person in
            gameState.People)
        {
            if (!person.Tags.Has(
                    "state.alive")
                || person.Age != 18
                || _family.GetSex(person)
                    != Sex.Male
                || !_family.IsMaleLineage(
                    person))
            {
                continue;
            }

            _economy.EnsureHousehold(
                person);

            TransferPendingCash(
                gameState,
                person);

            TransferPendingHouses(
                gameState,
                person);
        }
    }

    private void TransferPendingCash(
        IGameState gameState,
        IPerson person)
    {
        var amount =
            _economy.GetPendingInheritance(
                person);

        if (amount <= 0)
            return;

        _economy.ChangeWealth(
            person,
            amount);

        _economy.SetPendingInheritance(
            person,
            0);

        _events.Publish(
            new GameEvent
            {
                Type =
                    "inheritance.received_at_adulthood",

                Year =
                    gameState.Year,

                SubjectId =
                    person.Id,

                Data =
                    new Dictionary<string, string>
                    {
                        ["amount"] =
                            amount.ToString(),

                        ["text"] =
                            $"{_family.GetDisplayName(person)} " +
                            $"received an inheritance of " +
                            $"${amount:N0} upon adulthood."
                    }
            });
    }

    private void TransferPendingHouses(
        IGameState gameState,
        IPerson person)
    {
        var houses =
            _economy.TakePendingHouses(
                person);

        if (houses.Count == 0)
            return;

        _economy.EnsureHousehold(
            person);

        foreach (var house in
            houses)
        {
            _economy.AddExistingHouse(
                person,
                house);
        }

        var father =
            _family.GetFather(
                person);

        _events.Publish(
            new GameEvent
            {
                Type =
                    "inheritance.promised_houses_received",

                Year =
                    gameState.Year,

                SubjectId =
                    person.Id,

                RelatedPersonIds =
                    father is null
                        ? []
                        : [father.Id],

                Data =
                    new Dictionary<string, string>
                    {
                        ["count"] =
                            houses.Count.ToString(),

                        ["towns"] =
                            string.Join(
                                ", ",
                                houses.Select(
                                    house =>
                                        house.Town.Town)),

                        ["text"] =
                            $"{_family.GetDisplayName(person)} " +
                            $"received {houses.Count} promised " +
                            $"house{(houses.Count == 1 ? "" : "s")}."
                    }
            });
    }

}
