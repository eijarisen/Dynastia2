using Dynastia.App.Persistence;
using Dynastia.Contracts;
using Dynastia.Core.Actions;
using Dynastia.Core.Events;
using Dynastia.Core.Simulation;
using Dynastia.Mechanics.Family;

namespace Dynastia.App.Tests;

public sealed class SaveReliabilityTests
{
    [Fact]
    public void LoadRepairsOneSidedSpouseReferenceInsteadOfRejectingSave()
    {
        using var fixture = new SaveFixture();

        var husband = fixture.CreatePerson("Jan", Sex.Male);
        var wife = fixture.CreatePerson("Anna", Sex.Female);

        husband.Components.Get<FamilyComponent>()!.SpouseId = wife.Id;
        wife.Components.Get<FamilyComponent>()!.SpouseId = null;

        husband.Tags.Remove("relationship.single");
        husband.Tags.Add("relationship.married");
        wife.Tags.Add("relationship.single");

        using var stream = new MemoryStream();
        fixture.Saves.Save(stream, fixture.UiState);
        stream.Position = 0;

        fixture.Saves.Load(stream, fixture.UiState);

        var restoredHusband = fixture.State.FindPerson(husband.Id)!;
        var restoredWife = fixture.State.FindPerson(wife.Id)!;

        Assert.Equal(
            restoredWife.Id,
            restoredHusband.Components.Get<FamilyComponent>()!.SpouseId);

        Assert.Equal(
            restoredHusband.Id,
            restoredWife.Components.Get<FamilyComponent>()!.SpouseId);

        Assert.True(restoredWife.Tags.Has("relationship.married"));
        Assert.False(restoredWife.Tags.Has("relationship.single"));
    }

    [Fact]
    public void LoadPreservesReciprocalCoupleAndClearsConflictingThirdPartySpouseLink()
    {
        using var fixture = new SaveFixture();

        var first = fixture.CreatePerson("Jan", Sex.Male);
        var second = fixture.CreatePerson("Anna", Sex.Female);
        var third = fixture.CreatePerson("Maria", Sex.Female);

        first.Components.Get<FamilyComponent>()!.SpouseId = second.Id;
        second.Components.Get<FamilyComponent>()!.SpouseId = third.Id;
        third.Components.Get<FamilyComponent>()!.SpouseId = second.Id;

        foreach (var person in new[] { first, second, third })
        {
            person.Tags.Remove("relationship.single");
            person.Tags.Add("relationship.married");
        }

        using var stream = new MemoryStream();
        fixture.Saves.Save(stream, fixture.UiState);
        stream.Position = 0;

        fixture.Saves.Load(stream, fixture.UiState);

        var restoredFirst = fixture.State.FindPerson(first.Id)!;
        var restoredSecond = fixture.State.FindPerson(second.Id)!;
        var restoredThird = fixture.State.FindPerson(third.Id)!;

        Assert.Null(restoredFirst.Components.Get<FamilyComponent>()!.SpouseId);
        Assert.Equal(
            restoredThird.Id,
            restoredSecond.Components.Get<FamilyComponent>()!.SpouseId);
        Assert.Equal(
            restoredSecond.Id,
            restoredThird.Components.Get<FamilyComponent>()!.SpouseId);
        Assert.True(restoredFirst.Tags.Has("relationship.single"));
        Assert.False(restoredFirst.Tags.Has("relationship.married"));
    }

    [Fact]
    public void AutosaveOverwritesSingleRootFileAndRestoresPreYearState()
    {
        using var fixture = new SaveFixture();
        fixture.CreatePerson("Jan", Sex.Male);

        fixture.State.Year = 1900;
        fixture.Saves.SaveAutosave(fixture.UiState);

        var path = fixture.Saves.AutosavePath;
        var firstContents = File.ReadAllText(path);

        fixture.State.Year = 1901;
        fixture.Saves.SaveAutosave(fixture.UiState with { AlbumYear = 1901 });

        var secondContents = File.ReadAllText(path);

        Assert.NotEqual(firstContents, secondContents);
        Assert.Equal(
            Path.Combine(fixture.Root, GameSaveService.AutosaveFileName),
            path);
        Assert.Single(Directory.GetFiles(fixture.Root, "*.txt"));
        Assert.False(File.Exists(path + ".tmp"));

        fixture.State.Year = 1910;

        using var stream = File.OpenRead(path);
        fixture.Saves.Load(
            stream,
            fixture.UiState with { AlbumYear = 1910 });

        Assert.Equal(1901, fixture.State.Year);
    }

    private sealed class SaveFixture : IDisposable
    {
        private readonly GameRandom _random = new(1234);
        private readonly TestSuccessionService _succession;

        public SaveFixture()
        {
            Root = Path.Combine(
                Path.GetTempPath(),
                "Dynastia.App.Tests",
                Guid.NewGuid().ToString("N"));

            Directory.CreateDirectory(Root);

            State = new GameState(_random)
            {
                DynastySurname = "Nowak",
                Year = 1900,
                StartYear = 1900
            };

            var selection = new SelectionService();
            var events = new GameEventBus();
            var actions = new ActionRegistry(
                State,
                events,
                _random,
                new ActionGuardRegistry());

            _succession = new TestSuccessionService(State);

            Saves = new GameSaveService(
                State,
                selection,
                _succession,
                events,
                actions,
                biography: null,
                random: _random,
                gameRootDirectory: Root);
        }

        public string Root { get; }
        public GameState State { get; }
        public GameSaveService Saves { get; }

        public GameUiSaveState UiState =>
            new(
                State.People.FirstOrDefault()?.Id,
                _succession.ActiveControllerId,
                State.Year,
                true,
                0);

        public IPerson CreatePerson(string name, Sex sex)
        {
            var person = State.CreatePerson(name, "Nowak", 30);
            person.Tags.Add("state.alive");
            person.Tags.Add("relationship.single");
            person.Components.Set(
                new FamilyComponent
                {
                    Sex = sex,
                    Generation = 1
                });

            _succession.SetActiveController(person);
            return person;
        }

        public void Dispose()
        {
            if (Directory.Exists(Root))
                Directory.Delete(Root, recursive: true);
        }
    }

    private sealed class TestSuccessionService(IGameState state) : ISuccessionService
    {
        private Guid? _activeId;

        public bool IsGameOver => false;
        public int? MaleLineEndedYear => null;
        public bool DynastyLeftPoland => false;
        public int? DynastyLeftPolandYear => null;
        public Guid? ActiveControllerId => _activeId;
        public IPerson? ActiveController =>
            _activeId is Guid id
                ? state.People.FirstOrDefault(person => person.Id == id)
                : null;
        public bool HasLivingMaleLineage => true;
        public event EventHandler? StateChanged;

        public bool IsControllable(IPerson person) => true;

        public bool SetActiveController(IPerson person)
        {
            _activeId = person.Id;
            StateChanged?.Invoke(this, EventArgs.Empty);
            return true;
        }

        public void Refresh() =>
            StateChanged?.Invoke(this, EventArgs.Empty);
    }
}
