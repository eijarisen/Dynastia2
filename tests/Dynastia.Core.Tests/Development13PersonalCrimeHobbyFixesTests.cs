namespace Dynastia.Core.Tests;

public sealed class Development13PersonalCrimeHobbyFixesTests
{
    [Fact]
    public void PersonalDetailsPutDemographicsAtTopOfRightColumn()
    {
        var xaml = RepositoryFiles.ReadText("src", "Dynastia.App", "Views", "MainWindow.axaml");
        Assert.Equal(1, Count(xaml, "Text=\"{Binding SelectedPerson.AgeText}\""));
        Assert.Equal(1, Count(xaml, "StringFormat='Sex: {0}'"));
        Assert.Equal(1, Count(xaml, "Text=\"{Binding SelectedPerson.NationalityText}\""));

        var rightColumn = xaml.IndexOf("Grid.Column=\"1\"", StringComparison.Ordinal);
        var age = xaml.IndexOf("Text=\"{Binding SelectedPerson.AgeText}\"", rightColumn, StringComparison.Ordinal);
        var sex = xaml.IndexOf("StringFormat='Sex: {0}'", rightColumn, StringComparison.Ordinal);
        var nationality = xaml.IndexOf("Text=\"{Binding SelectedPerson.NationalityText}\"", rightColumn, StringComparison.Ordinal);
        var birthDate = xaml.IndexOf("Text=\"{Binding SelectedPerson.BirthDateText}\"", rightColumn, StringComparison.Ordinal);

        Assert.True(rightColumn >= 0 && age > rightColumn);
        Assert.True(sex > age);
        Assert.True(nationality > sex);
        Assert.True(birthDate > nationality);
    }

    [Fact]
    public void FractionalStatusValuesAlwaysResolveToAWordLabel()
    {
        var source = RepositoryFiles.ReadText("plugins", "Dynastia.Mechanics.Status", "StatusRules.cs");
        Assert.Contains("var exact = ordered.FirstOrDefault", source);
        Assert.Contains("ordered.LastOrDefault(band => value >= band.Minimum)?.Label", source);
        Assert.Contains("?? ordered[0].Label", source);
    }

    [Fact]
    public void UnavailablePrivateTutorChoiceIsNotAddedToPaperSelector()
    {
        var source = RepositoryFiles.ReadText(
            "src", "Dynastia.App", "ViewModels",
            "MainWindowViewModel.CraftsEducation.cs");

        Assert.Contains("if (evaluation.Available)", source);
        Assert.Contains("\"private_tutor\"", source);
        Assert.Contains("true,\n                        chance", source);
    }

    [Fact]
    public void SpouseCannotBeOrderedIntoCrimeAndCanBeAskedToQuit()
    {
        var source = RepositoryFiles.ReadText(
            "plugins", "Dynastia.Mechanics.Justice",
            "JusticePlugin.CriminalOccupation.cs");

        Assert.Contains("CanDirectOwnOccupation(context)", source);
        Assert.Contains("context.Actor.Id == context.Target.Id", source);
        Assert.Contains("Id = \"justice.ask_to_quit_crime\"", source);
        Assert.Contains("family.GetSpouse(context.Actor)?.Id != context.Target.Id", source);
        Assert.Contains("if (random.NextDouble() > 0.5)", source);
        Assert.Contains("crime.EndLifeOfCrime", source);
    }

    [Fact]
    public void HobbyReadsLazilyReconcileNewlyAgeEligiblePeople()
    {
        var service = RepositoryFiles.ReadText(
            "plugins", "Dynastia.Mechanics.Hobbies",
            "StandardHobbyService.cs");

        var getter = Slice(service, "public HobbyPersonSnapshot GetHobbies", "public IReadOnlyList<HobbyInfo> GenerateCandidateHobbies");
        Assert.Contains("if (component is null)", getter);
        Assert.Contains("EnsureCurrent(person);", getter);
        Assert.DoesNotContain("Hobby state is missing. Run state reconciliation before reading it.", getter);
    }

    private static int Count(string source, string value)
    {
        var count = 0;
        var offset = 0;
        while ((offset = source.IndexOf(value, offset, StringComparison.Ordinal)) >= 0)
        {
            count++;
            offset += value.Length;
        }
        return count;
    }

    private static string Slice(string source, string start, string end)
    {
        var startIndex = source.IndexOf(start, StringComparison.Ordinal);
        var endIndex = source.IndexOf(end, startIndex, StringComparison.Ordinal);
        Assert.True(startIndex >= 0 && endIndex > startIndex);
        return source[startIndex..endIndex];
    }

}
