using Dynastia.Contracts;
using Dynastia.Core.Events;
using Dynastia.Core.Simulation;
using Dynastia.Mechanics.GameScore;

namespace Dynastia.Core.Tests;

public sealed class GameScoreTests
{
    [Fact]
    public void PermanentAwardsAndReversibleClaimsAreIdempotent()
    {
        var state = new GameState { Year = 1800 };
        var person = state.CreatePerson("Jan", "Test", 20, Guid.Parse("00000000-0000-0000-0000-000000000001"));
        person.Tags.Add("state.alive");
        var spouse = state.CreatePerson("Anna", "Test", 20, Guid.Parse("00000000-0000-0000-0000-000000000002"));
        spouse.Tags.Add("state.alive");
        var events = new GameEventBus();
        var service = new StandardGameScoreService(state, events, new FamilyStub(), new HouseholdStub(), state, null, null, null, null);

        events.Publish(Event("life.birth", person.Id));
        events.Publish(Event("life.birth", person.Id));
        Assert.Equal(100, service.TotalScore);

        events.Publish(Event("career.employment", person.Id));
        events.Publish(Event("career.promotion", person.Id, data: new() { ["jobLevel"] = "3" }));
        Assert.Equal(125, service.TotalScore);
        events.Publish(Event("career.fired", person.Id));
        Assert.Equal(100, service.TotalScore);

        events.Publish(Event("relationship.married", person.Id, [spouse.Id]));
        Assert.Equal(125, service.TotalScore);
        events.Publish(Event("relationship.divorce", person.Id, [spouse.Id]));
        Assert.Equal(100, service.TotalScore);

        events.Publish(Event("life.death", person.Id, data: new() { ["age"] = "80" }));
        Assert.Equal(180, service.TotalScore);

        service.ReconcileAfterLoad();
        Assert.Equal(180, service.TotalScore);
    }


    [Fact]
    public void NestedPublishedEventsKeepTheirOwnEventDeltas()
    {
        var state = new GameState { Year = 1800 };
        var person = state.CreatePerson("Jan", "Test", 18);
        person.Tags.Add("state.alive");
        var events = new GameEventBus();
        var nestedPublished = false;
        events.EventPublished += (_, gameEvent) =>
        {
            if (!nestedPublished && gameEvent.Type == "life.adult")
            {
                nestedPublished = true;
                events.Publish(Event("life.birth", person.Id));
            }
        };
        var service = new StandardGameScoreService(state, events, new FamilyStub(), new HouseholdStub(), state, null, null, null, null);

        var adulthood = Event("life.adult", person.Id);
        events.Publish(adulthood);

        Assert.Equal(150, service.TotalScore);
        Assert.Equal(50, service.GetEventDelta(adulthood));
        Assert.Equal(100, service.GetEventDelta(events.AllEvents[1]));
    }

    [Fact]
    public void PropertySaleOnlyReversesPointsActuallyAwarded()
    {
        var state = new GameState { Year = 1800 };
        var person = state.CreatePerson("Jan", "Test", 20);
        person.Tags.Add("state.alive");
        var events = new GameEventBus();
        var service = new StandardGameScoreService(state, events, new FamilyStub(), new HouseholdStub(), state, null, null, null, null);
        var property = Guid.Parse("00000000-0000-0000-0000-000000000099");

        events.Publish(Event("household.house_sold", person.Id, data: new() { ["propertyId"] = property.ToString() }));
        Assert.Equal(0, service.TotalScore);
        events.Publish(Event("household.house_bought", person.Id, data: new() { ["propertyId"] = property.ToString() }));
        events.Publish(Event("household.house_extended", person.Id, data: new() { ["propertyId"] = property.ToString() }));
        Assert.Equal(75, service.TotalScore);
        events.Publish(Event("household.house_sold", person.Id, data: new() { ["propertyId"] = property.ToString() }));
        Assert.Equal(0, service.TotalScore);
    }

    private static GameEvent Event(string type, Guid subject, IReadOnlyList<Guid>? related = null, Dictionary<string, string>? data = null) =>
        new() { Type = type, Year = 1800, SubjectId = subject, RelatedPersonIds = related ?? [], Data = data ?? new() };

    private sealed class FamilyStub : IFamilyService
    {
        public void InitializePerson(IPerson person, Sex sex, int? generation = null) { }
        public Sex GetSex(IPerson person) => Sex.Male;
        public int? GetGeneration(IPerson person) => 1;
        public IPerson? GetFather(IPerson person) => null;
        public IPerson? GetMother(IPerson person) => null;
        public IPerson? GetSpouse(IPerson person) => null;
        public IReadOnlyList<IPerson> GetChildren(IPerson person) => [];
        public void SetParents(IPerson child, IPerson? father, IPerson? mother) { }
        public void SetSpouses(IPerson first, IPerson second, int startYear) { }
        public void EndRelationship(IPerson first, IPerson second, int endYear, string endReason, bool clearFirst = true, bool clearSecond = true) { }
        public IReadOnlyList<RelationshipHistoryInfo> GetRelationshipHistory(IPerson person) => [];
        public void SetGeneratedFamilyBackground(IPerson person, GeneratedFamilyBackgroundInfo background) { }
        public GeneratedFamilyBackgroundInfo? GetGeneratedFamilyBackground(IPerson person) => null;
        public string FormatSurname(string surname, Sex sex) => surname;
        public string GetDisplayName(IPerson person) => $"{person.Name} {person.Surname}";
        public bool IsBloodline(IPerson person) => true;
        public bool IsMaleLineage(IPerson person) => true;
    }

    private sealed class HouseholdStub : IHouseholdService
    {
        public IPerson? ResolveHouseholdHead(IPerson person) => person;
        public HouseholdStatusSnapshot? GetStatus(IPerson head) => null;
        public IPerson? GetNanny(IPerson head) => null;
        public IReadOnlyList<HouseholdInfo> GetActiveHouseholds() => [];
        public HouseholdInfo? GetHouseholdInfo(IPerson person) => null;
        public bool IsAutonomousHousehold(IPerson person) => false;
        public MoveResidentBranchResult EstablishIndependentResidentBranch(IPerson sourceHead, IPerson newHead, Guid? propertyId = null) => throw new NotSupportedException();
        public void ReconcileHouseholds() { }
        public bool ShouldShowFamilyNews(GameEvent gameEvent) => true;
    }
}
