namespace Dynastia.Core.Tests;

public sealed class Development13ContinuationTests
{
    [Fact]
    public void TownAffairsUsesRequestedTabOrderAndShowsMayorSummaryUnderPopulation()
    {
        var hub = Read("src", "Dynastia.App", "ViewModels", "TownAffairsViewModel.cs");
        var window = Read("src", "Dynastia.App", "Views", "TownLifeWindow.axaml");

        Assert.Contains("Institutions = 0", hub);
        Assert.Contains("Community = 1", hub);
        Assert.Contains("Housing = 2", hub);
        Assert.Contains("Jobs = 3", hub);
        Assert.Contains("Health = 4", hub);
        Assert.Contains("Church = 5", hub);
        Assert.Contains("Education = 6", hub);
        Assert.Contains("Bank = 7", hub);
        Assert.Contains("Court = 8", hub);
        Assert.Contains("(int)TownAffairsTab.Court", hub);

        var headers = new[]
        {
            "Institutions", "Community", "Housing", "Jobs", "Health",
            "Church", "Education", "Bank", "Court"
        };
        var previous = -1;
        foreach (var header in headers)
        {
            var current = window.IndexOf($"<TabItem Header=\"{header}\"", StringComparison.Ordinal);
            Assert.True(current > previous, $"Town Affairs tab '{header}' is out of order.");
            previous = current;
        }

        var population = window.IndexOf("Text=\"Population\"", StringComparison.Ordinal);
        var mayor = window.IndexOf("Text=\"Mayor\"", StringComparison.Ordinal);
        Assert.True(population >= 0 && mayor > population);
        Assert.Contains("Text=\"{Binding MayorSummaryText}\"", window);
        Assert.Contains("Approval {CivicOffice.Approval:0.#}%", hub);
    }

    [Fact]
    public void ChurchVisitAndReligiousStudyStayInTownAffairsButNotStandardActions()
    {
        var actions = Read("src", "Dynastia.App", "ViewModels", "MainWindowViewModel.Actions.cs");
        var townLife = Read("src", "Dynastia.App", "ViewModels", "MainWindowViewModel.TownLife.cs");

        Assert.Contains("\"church.attend\"", actions);
        Assert.Contains("\"personality.religious_study\"", actions);
        Assert.Contains("continue;", actions);
        Assert.Contains("AddChurchAction(\"church.attend\"", townLife);
        Assert.Contains("AddChurchAction(\"personality.religious_study\"", townLife);
    }

    [Fact]
    public void HealthTabHidesUnavailableFacilityImprovementsAndEmptyOptionalSections()
    {
        var townLife = Read("src", "Dynastia.App", "ViewModels", "MainWindowViewModel.TownLife.cs");
        var hub = Read("src", "Dynastia.App", "ViewModels", "TownAffairsViewModel.cs");
        var window = Read("src", "Dynastia.App", "Views", "TownLifeWindow.axaml");

        var methodStart = townLife.IndexOf("GetTownAffairsHealthActions(IPerson subject)", StringComparison.Ordinal);
        var methodEnd = townLife.IndexOf("GetTownAffairsChurchActions()", methodStart, StringComparison.Ordinal);
        Assert.True(methodStart >= 0 && methodEnd > methodStart);
        var healthMethod = townLife[methodStart..methodEnd];

        Assert.Contains("ActionReasonCodes.InsufficientFunds", healthMethod);
        Assert.DoesNotContain("ActionReasonCodes.ResourceUnavailable", healthMethod);
        Assert.Contains("HasTherapyHealthActions", hub);
        Assert.Contains("HasMedicalImprovementActions", hub);
        Assert.Contains("IsVisible=\"{Binding HasTherapyHealthActions}\"", window);
        Assert.Contains("IsVisible=\"{Binding HasMedicalImprovementActions}\"", window);
        Assert.DoesNotContain("Text=\"Current Health / Conditions\"", window);
        Assert.DoesNotContain("Cost ×", hub);
    }

    [Fact]
    public void InventoryRemovesDuplicatePropertyHeadersAndEnlargesEmptyStates()
    {
        var inventory = Read("src", "Dynastia.App", "Views", "FamilyInventoryWindow.axaml");

        Assert.Contains("Header=\"Houses\"", inventory);
        Assert.Contains("Header=\"Farmland\"", inventory);
        Assert.Contains("Header=\"Heirlooms\"", inventory);
        Assert.DoesNotContain("Text=\"Houses\"", inventory);
        Assert.DoesNotContain("Text=\"Farmland\"", inventory);
        Assert.DoesNotContain("Text=\"Heirlooms\"", inventory);
        Assert.Contains("TextBlock.inventoryEmpty", inventory);
        Assert.True(inventory.Split("Classes=\"inventoryEmpty\"").Length - 1 >= 3);
    }

    [Fact]
    public void LoanOffersShowBoldColorCodedFavorability()
    {
        var model = Read("src", "Dynastia.App", "ViewModels", "LoanSelectionModels.cs");
        var window = Read("src", "Dynastia.App", "Views", "LoanSelectionWindow.axaml");

        Assert.Contains("FavorabilityText", model);
        Assert.Contains("FavorabilityBrush", model);
        Assert.Contains("multiplier <= 0.95m", model);
        Assert.Contains("multiplier >= 1.05m", model);
        Assert.Contains("Text=\"{Binding FavorabilityText}\"", window);
        Assert.Contains("Foreground=\"{Binding FavorabilityBrush}\"", window);
        Assert.Contains("FontSize=\"15.5\"", window);
        Assert.True(window.Split("FontWeight=\"Bold\"").Length - 1 >= 5);
    }

    [Fact]
    public void PersonalDetailsUseWordOnlyStatusWithoutBoldStyling()
    {
        var selection = Read("src", "Dynastia.App", "ViewModels", "MainWindowViewModel.Selection.cs");
        var window = Read("src", "Dynastia.App", "Views", "MainWindow.axaml");

        Assert.Contains("$\"Renown: {socialStatus.RenownLabel}\"", selection);
        Assert.Contains("$\"Reputation: {socialStatus.ReputationLabel}\"", selection);
        Assert.DoesNotContain("socialStatus?.RenownText", selection);
        Assert.DoesNotContain("socialStatus?.ReputationText", selection);

        var renownLine = window.Split('\n').Single(line => line.Contains("SelectedFamily.Renown", StringComparison.Ordinal));
        var reputationLine = window.Split('\n').Single(line => line.Contains("SelectedFamily.Reputation", StringComparison.Ordinal));
        Assert.DoesNotContain("FontWeight=", renownLine);
        Assert.DoesNotContain("FontWeight=", reputationLine);
    }

    [Fact]
    public void PotentialPartnerCardsShowNationalitySimpleOccupationAndStatusPrefix()
    {
        var model = Read("src", "Dynastia.App", "ViewModels", "OpportunitySelectionViewModels.cs");
        var window = Read("src", "Dynastia.App", "Views", "PotentialPartnersWindow.axaml");

        Assert.Contains("$\"Nationality: {Candidate.DisplayNationality}\"", model);
        Assert.DoesNotContain("Birthplace:", model);
        Assert.Contains("$\"{CareerEmoji} {Candidate.JobTitle}, Level {Candidate.JobLevel}\"", model);
        Assert.DoesNotContain("Candidate.CareerName", model);
        Assert.Contains("Text=\"Status:\"", window);
    }

    [Fact]
    public void TownAffairsAdministrationRoutesToCommunityAndDirectActionIsHiddenInPrison()
    {
        var hub = Read("src", "Dynastia.App", "ViewModels", "TownAffairsViewModel.cs");
        var owner = Read("src", "Dynastia.App", "ViewModels", "MainWindowViewModel.TownLife.cs");
        var window = Read("src", "Dynastia.App", "Views", "TownLifeWindow.axaml");

        Assert.Contains("\"administration\" => TownAffairsTab.Community", hub);
        Assert.Contains("_justiceService?.IsImprisoned(actor) == true", owner);
        Assert.Contains("_justiceService?.IsImprisoned(target) == true", owner);
        Assert.Contains("<Setter Property=\"FontSize\" Value=\"14.5\" />", window);
    }

    [Fact]
    public void HealthTabHidesEmptyTreatmentAndOmitsPercentageText()
    {
        var hub = Read("src", "Dynastia.App", "ViewModels", "TownAffairsViewModel.cs");
        var owner = Read("src", "Dynastia.App", "ViewModels", "MainWindowViewModel.TownLife.cs");
        var window = Read("src", "Dynastia.App", "Views", "TownLifeWindow.axaml");

        Assert.Contains("HasTreatmentHealthActions => TreatmentHealthActions.Count > 0", hub);
        Assert.Contains("IsVisible=\"{Binding HasTreatmentHealthActions}\"", window);
        Assert.DoesNotContain("ShowTreatmentEmpty", hub);
        Assert.DoesNotContain("No treatment or healing action is currently available.", window);
        Assert.Contains("$\"Health: {health.Current:N0} / {health.Maximum:N0}\"", owner);
        Assert.DoesNotContain("health.Percentage", owner);
    }

    [Fact]
    public void LoanOffersAreMoreProminentColorCodedAndDoNotShowCounterpartyRoles()
    {
        var model = Read("src", "Dynastia.App", "ViewModels", "LoanSelectionModels.cs");
        var window = Read("src", "Dynastia.App", "Views", "LoanSelectionWindow.axaml");
        var codeBehind = Read("src", "Dynastia.App", "Views", "LoanSelectionWindow.axaml.cs");

        Assert.DoesNotContain("RoleText", model);
        Assert.DoesNotContain("RoleText", window);
        Assert.False(codeBehind.Contains("outside lender", StringComparison.OrdinalIgnoreCase));
        Assert.False(codeBehind.Contains("outside borrower", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("BorderBrush=\"{Binding FavorabilityBrush}\"", window);
        Assert.Contains("FontSize=\"15.5\"", window);
        Assert.Contains("FontSize=\"13.2\"", window);

        var townWindow = Read("src", "Dynastia.App", "Views", "TownLifeWindow.axaml");
        Assert.DoesNotContain("Text=\"{Binding Snapshot.Bank.DisplayName}\"", townWindow);
    }

    [Fact]
    public void CourtRecordUsesSameFramedSectionTreatmentAsProtection()
    {
        var window = Read("src", "Dynastia.App", "Views", "TownLifeWindow.axaml");
        var record = window.IndexOf("Text=\"Known Criminal Record\"", StringComparison.Ordinal);
        Assert.True(record >= 0);
        var frame = window.LastIndexOf("<Border Padding=\"10,8\" Background=\"#20FFF4D9\" BorderBrush=\"#A17A43\"", record, StringComparison.Ordinal);
        Assert.True(frame >= 0 && record - frame < 500);
    }

    [Fact]
    public void InventoryAssetTabsShareCardTypographySpacingAndGeometry()
    {
        var inventory = Read("src", "Dynastia.App", "Views", "FamilyInventoryWindow.axaml");

        Assert.Contains("Border.inventoryAssetCard", inventory);
        Assert.Contains("TextBlock.inventoryAssetTitle", inventory);
        Assert.Contains("TextBlock.inventoryAssetDetail", inventory);
        Assert.Contains("TextBlock.inventoryAssetValue", inventory);
        Assert.Contains("TextBlock.inventoryInheritanceLabel", inventory);
        Assert.Equal(3, inventory.Split("Classes=\"inventoryAssetCard\"").Length - 1);
        Assert.Equal(3, inventory.Split("RowDefinitions=\"108,Auto,Auto\"").Length - 1);
        Assert.Equal(3, inventory.Split("Classes=\"inventoryInheritanceLabel\"").Length - 1);
    }

    [Fact]
    public void TownAffairsUsesLargerTabsInstitutionsAndExpandedMayorPane()
    {
        var hub = Read("src", "Dynastia.App", "ViewModels", "TownAffairsViewModel.cs");
        var window = Read("src", "Dynastia.App", "Views", "TownLifeWindow.axaml");

        Assert.Contains("<Setter Property=\"FontSize\" Value=\"14.5\" />", window);
        Assert.Contains("Width=\"48\" Height=\"48\"", window);
        Assert.Contains("FontSize=\"32\" Text=\"{Binding Emoji}\"", window);
        Assert.Contains("ColumnDefinitions=\"3*,4*\"", window);
        Assert.Contains("Text=\"{Binding CivicOfficeNameText}\"", window);
        Assert.Contains("Text=\"{Binding CivicOfficeTitleText}\"", window);
        Assert.Contains("Text=\"{Binding CivicOfficeAgeText}\"", window);
        Assert.Contains("Text=\"{Binding CivicOfficeRenownText}\"", window);
        Assert.Contains("Text=\"{Binding CivicOfficeReputationText}\"", window);
        Assert.Contains("Text=\"{Binding CivicOfficeApprovalText}\"", window);
        Assert.Contains("$\"Age: {CivicOffice.Age(_owner.Year)}\"", hub);
    }

    [Fact]
    public void CivicOfficeIsRestrictedToPolishNationalityIncludingNpcMayors()
    {
        var civic = Read("plugins", "Dynastia.Mechanics.Community", "CivicOfficeService.cs");

        Assert.Contains("private const string PolishNationalityId = \"polish\";", civic);
        Assert.Contains("|| !IsPolish(person)", civic);
        Assert.Contains("var nationalityId = PolishNationalityId;", civic);
        Assert.Contains("NormalizeOfficeNationality(existing, town, year);", civic);
        Assert.Contains("NormalizeOfficeNationality(office, town, _gameState.Year);", civic);
        Assert.Contains("AppointNpc(state, town, year, \"polish-office-normalization\")", civic);
    }

    [Fact]
    public void FamilyRelationsWindowUsesTownAffairsTabSizeAndExplainsEmptyAcquaintances()
    {
        var model = Read("src", "Dynastia.App", "ViewModels", "FamilyRelationsViewModels.cs");
        var window = Read("src", "Dynastia.App", "Views", "FamilyRelationsWindow.axaml");

        Assert.Contains("Title=\"Family Relations\"", window);
        Assert.Contains("Text=\"Family Relations\"", window);
        Assert.Contains("<Style Selector=\"TabItem\">", window);
        Assert.Contains("<Setter Property=\"FontSize\" Value=\"14.5\" />", window);
        Assert.Contains("public bool HasConnections => Connections.Count > 0;", model);
        Assert.Contains("public bool ShowNoConnections => !HasConnections;", model);
        Assert.Contains("No acquaintances are currently known to this household.", window);
    }

    [Fact]
    public void CourtTabStaysVisibleForChildHealthSelectionAndSwitchesBackToAdultController()
    {
        var hub = Read("src", "Dynastia.App", "ViewModels", "TownAffairsViewModel.cs");

        Assert.Contains("!IsRemote && _householdPeople.Any(person => person.Age >= 18)", hub);
        Assert.Contains("tab == TownAffairsTab.Court", hub);
        Assert.Contains("_householdPeople.FirstOrDefault(person => person.Age >= 18)", hub);
        Assert.Contains("RefreshSubjectContent();", hub);
    }

    [Fact]
    public void CriminalOccupationSkipsPeopleOutsideActiveDynastyHouseholds()
    {
        var service = Read("plugins", "Dynastia.Mechanics.Justice", "CriminalOccupationService.cs");
        var annualStart = service.IndexOf("internal void ResolveAnnualHeist", StringComparison.Ordinal);
        var reconcileStart = service.IndexOf("internal void ReconcileAll", annualStart, StringComparison.Ordinal);
        Assert.True(annualStart >= 0 && reconcileStart > annualStart);
        var annual = service[annualStart..reconcileStart];

        Assert.Contains("_economy.GetHouseholdId(person) is null", annual);
        var guard = annual.IndexOf("_economy.GetHouseholdId(person) is null", StringComparison.Ordinal);
        var changeWealth = annual.IndexOf("_economy.ChangeWealth(person, proceeds)", StringComparison.Ordinal);
        Assert.True(guard >= 0 && changeWealth > guard);
    }

    private static string Read(params string[] parts) =>
        File.ReadAllText(Path.Combine(new[] { RepositoryRoot() }.Concat(parts).ToArray()));

    private static string RepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "Dynastia.slnx")))
                return current.FullName;
            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository root.");
    }
}
