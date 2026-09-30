using System.Reflection;
using Dynastia.Contracts;
using Dynastia.Core.Simulation;
using Dynastia.Mechanics.Church;
using Dynastia.TestSupport;

namespace Dynastia.Core.Tests;

public sealed class TownPolicySocietyBatches56Tests
{
    [Fact]
    public void PoorFamilyProspectIsStableAndDoesNotConsumeGlobalRandom()
    {
        var state = new GameState
        {
            Year = 1912,
            StartYear = 1900
        };
        var actor = state.CreatePerson(
            "Jan",
            "Kowalski",
            35,
            Guid.Parse("9116802a-9cf4-4de8-a6ae-56cbfd2672d1"));
        var householdId = Guid.Parse("38b5dc79-9b0d-4bea-a6c8-4772f554e37f");
        var town = new TownInfo("Test Town", "Test County", 19, 52, 12_000)
        {
            Id = "test-town",
            RegionId = "test-region"
        };
        var economy = Proxy<IEconomyService>((method, _) => method.Name switch
        {
            nameof(IEconomyService.GetResidenceTown) => town,
            nameof(IEconomyService.GetHouseholdId) => householdId,
            _ => Default(method.ReturnType)
        });
        var nationalities = Proxy<INationalityService>((method, _) => method.Name switch
        {
            nameof(INationalityService.GetNationality) => "polish",
            nameof(INationalityService.GenerateNationality) => "polish",
            nameof(INationalityService.GetNameCultureId) => "polish",
            _ => Default(method.ReturnType)
        });
        var names = Proxy<IHistoricalNameService>((method, args) => method.Name switch
        {
            nameof(IHistoricalNameService.GetRandomSurname) => "Kowalski",
            nameof(IHistoricalNameService.GetRandomFirstName) =>
                args is { Length: > 0 } && Equals(args[0], Sex.Female) ? "Anna" : "Jan",
            nameof(IHistoricalNameService.FormatSurname) =>
                args is { Length: > 1 } && Equals(args[1], Sex.Female) ? "Kowalska" : "Kowalski",
            _ => Default(method.ReturnType)
        });
        using var random = new SequenceGameRandom();
        var events = Proxy<IGameEventBus>((method, _) => Default(method.ReturnType));
        var context = new GameActionContext(
            state,
            actor,
            actor,
            events,
            random);

        var first = ChurchPlugin.BuildPoorFamilyProspect(
            context,
            economy,
            nationalities,
            names);
        var second = ChurchPlugin.BuildPoorFamilyProspect(
            context,
            economy,
            nationalities,
            names);

        Assert.Equal(first, second);
        Assert.NotEqual(Guid.Empty, first.ContactId);
        Assert.Equal("test-town", first.TownId);
        Assert.InRange(first.ContactAge, 22, 55);
        Assert.False(string.IsNullOrWhiteSpace(first.FamilyName));
        Assert.False(string.IsNullOrWhiteSpace(first.ContactName));
        Assert.False(string.IsNullOrWhiteSpace(first.HouseholdSummary));
        Assert.Equal(0, random.ConsumedCount);
    }

    [Fact]
    public void PoorFamilyAidPersistsDisplayedContactAndCreatesControlledConnectionOnlyOnExecution()
    {
        var church = RepositoryFiles.ReadText(
            "plugins", "Dynastia.Mechanics.Church", "ChurchPlugin.cs");
        var contracts = RepositoryFiles.ReadText(
            "src", "Dynastia.Contracts", "HouseholdConnectionContracts.cs");
        var connections = RepositoryFiles.ReadText(
            "plugins", "Dynastia.Mechanics.Community", "CommunityConnectionService.cs");
        var archetypes = RepositoryFiles.ReadText(
            "data", "LocalSociety", "connection_archetypes.csv");
        var presentation = RepositoryFiles.ReadText(
            "src", "Dynastia.App", "ViewModels", "MainWindowViewModel.TownLife.cs");

        Assert.Contains("poorFamilyContactId", church);
        Assert.Contains("poorFamilyContactBirthYear", church);
        Assert.Contains("poorFamilySummary", church);
        Assert.Contains("WithPoorFamilyMetadata", church);
        Assert.Contains("connections.EnsureLocalConnection", church);
        Assert.Contains("Familiarity", connections);
        Assert.Contains("MarkMeaningfulInteraction(connection)", connections);
        Assert.Contains("FirstOrDefault(item =>", connections);
        Assert.Contains("HouseholdConnectionSeed", contracts);
        Assert.Contains("EnsureLocalConnection(HouseholdConnectionSeed seed)", contracts);
        Assert.Contains("poor_family,Local Resident,Poor,Modest,10,10,poor_relief", archetypes);
        Assert.Contains("StartsWith(\n                    \"poorFamily\"", presentation);
        Assert.Contains("PoorFamilyProspect: poorFamilyProspect", presentation);
    }

    [Fact]
    public void TownAffairsUsesCompactInstitutionsAndUpdatedCommunityAndChurchComposition()
    {
        var xaml = RepositoryFiles.ReadText(
            "src", "Dynastia.App", "Views", "TownLifeWindow.axaml");
        var service = RepositoryFiles.ReadText(
            "plugins", "Dynastia.Mechanics.TownLife", "StandardTownLifeService.cs");
        var viewModel = RepositoryFiles.ReadText(
            "src", "Dynastia.App", "ViewModels", "TownAffairsViewModel.cs");

        var institutionsStart = xaml.IndexOf("<TabItem Header=\"Institutions\">", StringComparison.Ordinal);
        var communityStart = xaml.IndexOf("<TabItem Header=\"Community\"", institutionsStart, StringComparison.Ordinal);
        Assert.True(institutionsStart >= 0 && communityStart > institutionsStart);
        var institutions = xaml[institutionsStart..communityStart];

        Assert.Contains("<primitives:UniformGrid Columns=\"3\" Rows=\"3\" />", institutions);
        Assert.DoesNotContain("<ScrollViewer", institutions);
        Assert.Contains("Width=\"42\" Height=\"42\"", institutions);
        Assert.Contains("MinHeight=\"112\"", institutions);
        Assert.DoesNotContain("Careers:", service);
        Assert.Contains("? \"—\"", service);

        Assert.Contains(
            "Lobbying builds civic standing and introduces the proposer, even when the proposal itself is mostly ceremonial.",
            xaml);
        Assert.Contains("Text=\"{Binding EffectSummary}\"", xaml);
        Assert.DoesNotContain("base support", viewModel);
        Assert.Contains("\"No direct effect.\"", viewModel);

        Assert.Contains("ColumnDefinitions=\"*,*\"", xaml);
        Assert.Contains("Content=\"{Binding AttendChurchAction}\"", xaml);
        Assert.Contains("Content=\"{Binding AidPoorFamilyAction}\"", xaml);
        Assert.Contains("Content=\"{Binding DonateChurchAction}\"", xaml);
        Assert.Contains("Content=\"{Binding WelfareChurchAction}\"", xaml);
        Assert.Contains("Content=\"{Binding ReligiousStudyChurchAction}\"", xaml);
        Assert.Contains("Helping them will introduce this family as an Acquaintance.", xaml);
        Assert.Contains("PoorFamilyPortrait", xaml);
        Assert.Contains("PoorFamilyContactText", xaml);
        Assert.Contains("PoorFamilySummaryText", xaml);
    }

    private static T Proxy<T>(Func<MethodInfo, object?[]?, object?> handler)
        where T : class
    {
        var proxy = DispatchProxy.Create<T, DelegateProxy>();
        ((DelegateProxy)(object)proxy).Handler = handler;
        return proxy;
    }

    private static object? Default(Type type)
    {
        if (type == typeof(void))
            return null;
        if (!type.IsValueType || Nullable.GetUnderlyingType(type) is not null)
            return null;
        return Activator.CreateInstance(type);
    }

    public sealed class DelegateProxy : DispatchProxy
    {
        public Func<MethodInfo, object?[]?, object?>? Handler { get; set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) =>
            Handler?.Invoke(
                targetMethod ?? throw new InvalidOperationException("Proxy method is unavailable."),
                args);
    }
}
