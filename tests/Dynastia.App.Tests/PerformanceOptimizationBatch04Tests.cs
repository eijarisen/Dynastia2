using Dynastia.App.Persistence;
using Dynastia.Contracts;
using Dynastia.Core.Actions;
using Dynastia.Core.Events;
using Dynastia.Core.Simulation;

namespace Dynastia.App.Tests;

public sealed class PerformanceOptimizationBatch04Tests
{
    [Fact]
    public void YearCheckpointPreparesComponentsOnlyWhenRestoredAndReusesTypeRegistry()
    {
        var state = new GameState(new GameRandom(17))
        {
            DynastySurname = "Nowak"
        };
        var person = state.CreatePerson("Jan", "Nowak", 30);
        person.Tags.Add("state.alive");

        var selection = new SelectionService
        {
            SelectedPersonId = person.Id
        };
        var events = new GameEventBus();
        var random = new GameRandom(17);
        var actions = new ActionRegistry(
            state,
            events,
            random,
            new ActionGuardRegistry());
        var succession = new SuccessionStub(person);
        var saves = new GameSaveService(
            state,
            selection,
            succession,
            events,
            actions,
            biography: null,
            random: random);

        var first = saves.Capture();

        Assert.Equal(0, saves.ComponentPreparationCount);
        Assert.Equal(0, saves.ComponentTypeRegistryBuildCount);

        first.Restore();

        Assert.Equal(1, saves.ComponentPreparationCount);
        Assert.Equal(1, saves.ComponentTypeRegistryBuildCount);

        var second = saves.Capture();
        Assert.Equal(1, saves.ComponentPreparationCount);

        second.Restore();

        Assert.Equal(2, saves.ComponentPreparationCount);
        Assert.Equal(1, saves.ComponentTypeRegistryBuildCount);
    }

    private sealed class SuccessionStub(IPerson active) : ISuccessionService
    {
        public bool IsGameOver => false;
        public int? MaleLineEndedYear => null;
        public bool DynastyLeftPoland => false;
        public int? DynastyLeftPolandYear => null;
        public Guid? ActiveControllerId => active.Id;
        public IPerson? ActiveController => active;
        public bool HasLivingMaleLineage => true;
        public event EventHandler? StateChanged;

        public bool IsControllable(IPerson person) => person.Id == active.Id;
        public bool SetActiveController(IPerson person) => person.Id == active.Id;
        public void Refresh() => StateChanged?.Invoke(this, EventArgs.Empty);
    }
}
