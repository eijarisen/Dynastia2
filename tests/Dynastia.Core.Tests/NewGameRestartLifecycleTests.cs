using Dynastia.Contracts;
using Dynastia.Core.Data;
using Dynastia.Core.Events;
using Dynastia.Core.Simulation;
using Dynastia.Mechanics.Appearance;
using Dynastia.Mechanics.Family;

namespace Dynastia.Core.Tests;

public sealed class NewGameRestartLifecycleTests
{
    [Fact]
    public void RestartReconcilesAppearanceBeforeSelectingFounderAndClearsPriorEvents()
    {
        var data = CreateRepositoryData();
        var random = new GameRandom(seed: 4317);
        var gameState = new GameState(random);
        var selection = new SelectionService();
        var events = new GameEventBus();
        var calendar = new GameCalendar();
        var reconciliation = new StateReconciliationLifecycle();
        var historicalNames = StandardHistoricalNameService.Load(data);
        var nationalities = StandardNationalityService.Load(
            data,
            historicalNames);
        var family = new StandardFamilyService(
            gameState,
            data,
            historicalNames,
            nationalities);
        var appearance = new StandardAppearanceService(family);

        reconciliation.Register(
            "test.appearance",
            [ReconciliationLifecycleStage.AfterNewGame],
            _ =>
            {
                foreach (var person in gameState.People)
                    appearance.EnsureAppearance(person);
            });

        selection.SelectionChanged += (_, _) =>
        {
            if (selection.SelectedPersonId is not Guid selectedId)
                return;

            var selected = gameState.People.Single(
                person => person.Id == selectedId);

            // This is the same read used by the family-card UI. It must not
            // happen until AfterNewGame reconciliation has initialized portraits.
            _ = appearance.GetPortrait(selected);
        };

        var newGame = new StandardNewGameService(
            gameState,
            family,
            selection,
            data,
            historicalNames,
            random,
            calendar,
            events,
            reconciliation);

        newGame.StartNewGame("Kowalski", 1900);

        events.Publish(
            new GameEvent
            {
                Type = "test.previous_dynasty",
                Year = 1901
            });

        var founder = newGame.StartNewGame("Nowak", 1900);

        Assert.Equal(founder.Id, selection.SelectedPersonId);
        Assert.All(
            gameState.People,
            person => Assert.True(
                person.Components.Has<AppearanceComponent>()));

        Assert.All(
            gameState.People,
            person => Assert.Equal(
                "polish",
                nationalities.GetNationality(person)));

        var onlyEvent = Assert.Single(events.AllEvents);
        Assert.Equal("game.started", onlyEvent.Type);
        Assert.Equal(founder.Id, onlyEvent.SubjectId);
    }

    private static IGameDataService CreateRepositoryData()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            var dataPath = Path.Combine(directory.FullName, "data");

            if (File.Exists(
                Path.Combine(
                    dataPath,
                    "Names",
                    "name_eras.csv")))
            {
                return new JsonGameDataService(dataPath);
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException(
            "Could not locate repository data directory from test output.");
    }
}
