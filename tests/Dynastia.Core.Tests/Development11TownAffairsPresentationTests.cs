namespace Dynastia.Core.Tests;

public sealed class Development11TownAffairsPresentationTests
{
    [Fact]
    public void TownAffairsUsesCurrentNineTabLayoutAndLargerTownDescription()
    {
        var window = RepositoryFiles.ReadText("src", "Dynastia.App", "Views", "TownLifeWindow.axaml");
        var hub = RepositoryFiles.ReadText("src", "Dynastia.App", "ViewModels", "TownAffairsViewModel.cs");

        Assert.DoesNotContain("Header=\"Instructions\"", window);
        Assert.DoesNotContain("Instructions =", hub);
        Assert.Contains("Institutions = 0", hub);
        Assert.Contains("Community = 1", hub);
        Assert.Contains("Housing = 2", hub);
        Assert.Contains("Jobs = 3", hub);
        Assert.Contains("Health = 4", hub);
        Assert.Contains("Church = 5", hub);
        Assert.Contains("Education = 6", hub);
        Assert.Contains("Bank = 7", hub);
        Assert.Contains("Court = 8", hub);
        Assert.Contains("FontSize=\"14.5\" Text=\"Polity\"", window);
        Assert.Contains("FontSize=\"14.5\" Text=\"Population\"", window);
    }

    [Fact]
    public void HousingTabShowsPurchaseOffersOnlyWithEmphasizedPriceBesideBuy()
    {
        var window = RepositoryFiles.ReadText("src", "Dynastia.App", "Views", "TownLifeWindow.axaml");
        var hub = RepositoryFiles.ReadText("src", "Dynastia.App", "ViewModels", "TownAffairsViewModel.cs");

        Assert.DoesNotContain("Housing Market", window);
        Assert.DoesNotContain("Standard 6-person market", window);
        Assert.DoesNotContain("Local market:", window);
        Assert.DoesNotContain("Current appraisal:", window);
        Assert.DoesNotContain("Owned Houses in This Town", window);
        Assert.DoesNotContain("BaseHouseMarketValue", hub);
        Assert.DoesNotContain("OwnedHouses", hub);
        Assert.Contains("ColumnDefinitions=\"44,*,Auto,Auto\"", window);
        Assert.Contains("FontSize=\"16\" FontWeight=\"Bold\" Foreground=\"#5A3A18\" Text=\"{Binding AskingPriceText}\"", window);
        Assert.Contains("Grid.Column=\"3\" Classes=\"paperAction\"", window);
    }

    [Fact]
    public void JobSalaryAppearsImmediatelyToLeftOfChance()
    {
        var window = RepositoryFiles.ReadText("src", "Dynastia.App", "Views", "TownLifeWindow.axaml");

        var salary = window.IndexOf("Text=\"{Binding SalaryText}\"", StringComparison.Ordinal);
        var chance = window.IndexOf("Text=\"{Binding ChanceText}\"", salary, StringComparison.Ordinal);
        var apply = window.IndexOf("Content=\"Apply\" Click=\"OnApplyJobClick\"", chance, StringComparison.Ordinal);

        Assert.True(salary >= 0);
        Assert.True(chance > salary);
        Assert.True(apply > chance);
        Assert.Contains("<StackPanel Orientation=\"Horizontal\" Spacing=\"10\">", window);
    }

    [Fact]
    public void HealthTabKeepsInsufficientFundsActionVisibleButDisabled()
    {
        var window = RepositoryFiles.ReadText("src", "Dynastia.App", "Views", "TownLifeWindow.axaml");
        var townLife = RepositoryFiles.ReadText("src", "Dynastia.App", "ViewModels", "MainWindowViewModel.TownLife.cs");
        var hub = RepositoryFiles.ReadText("src", "Dynastia.App", "ViewModels", "TownAffairsViewModel.cs");
        var treatment = RepositoryFiles.ReadText("plugins", "Dynastia.Mechanics.Wellbeing", "WellbeingPlugin.TreatmentActions.cs");

        Assert.Contains(".GetCandidateActions(actor, subject)", townLife);
        Assert.Contains("ActionReasonCodes.InsufficientFunds", townLife);
        Assert.Contains("TownAffairsHealthActionIds.Contains(action.Id)", townLife);
        Assert.Contains("bool IsAvailable", hub);
        Assert.Contains("DisplayOpacity", hub);
        Assert.Contains("IsEnabled=\"{Binding IsAvailable}\"", window);
        Assert.Contains("Opacity=\"{Binding DisplayOpacity}\"", window);
        Assert.Contains("ActionReasonCodes.InsufficientFunds", treatment);
        Assert.Contains("No health actions are currently available.", hub);
    }

    [Fact]
    public void InstitutionCardsRouteSchoolBankAndMedicalToTheirTabs()
    {
        var window = RepositoryFiles.ReadText("src", "Dynastia.App", "Views", "TownLifeWindow.axaml");
        var codeBehind = RepositoryFiles.ReadText("src", "Dynastia.App", "Views", "TownLifeWindow.axaml.cs");
        var hub = RepositoryFiles.ReadText("src", "Dynastia.App", "ViewModels", "TownAffairsViewModel.cs");

        Assert.Contains("PointerPressed=\"OnInstitutionPointerPressed\"", window);
        Assert.Contains("model.OpenInstitution(institution.InstitutionId)", codeBehind);
        Assert.Contains("\"school\" => TownAffairsTab.Education", hub);
        Assert.Contains("\"bank\" => TownAffairsTab.Bank", hub);
        Assert.Contains("\"medical\" => TownAffairsTab.Health", hub);
    }

    [Fact]
    public void BankTabOwnsLoanOfferEntryPointsAndInventoryOnlyVisitsBank()
    {
        var townWindow = RepositoryFiles.ReadText("src", "Dynastia.App", "Views", "TownLifeWindow.axaml");
        var inventory = RepositoryFiles.ReadText("src", "Dynastia.App", "Views", "FamilyInventoryWindow.axaml");
        var inventoryCode = RepositoryFiles.ReadText("src", "Dynastia.App", "Views", "FamilyInventoryWindow.axaml.cs");
        var hub = RepositoryFiles.ReadText("src", "Dynastia.App", "ViewModels", "TownAffairsViewModel.cs");

        Assert.Contains("Header=\"Bank\"", townWindow);
        Assert.Contains("Text=\"Take a Loan\"", townWindow);
        Assert.Contains("Text=\"Give a Loan\"", townWindow);
        Assert.Contains("OnTakeLoanClick", townWindow);
        Assert.Contains("OnGiveLoanClick", townWindow);
        Assert.Contains("GetLoanOffers(bool isGivingLoan)", hub);
        Assert.Contains("QueueLoan(", hub);

        Assert.Contains("Content=\"Visit a Bank\"", inventory);
        Assert.Contains("IsEnabled=\"{Binding CanVisitBank}\"", inventory);
        Assert.DoesNotContain("Content=\"Take Loan\"", inventory);
        Assert.DoesNotContain("Content=\"Give Loan\"", inventory);
        Assert.Contains("TownAffairsTab.Bank", inventoryCode);
        Assert.Contains(".Bank", inventoryCode);
        Assert.Contains(".IsAvailable", inventoryCode);
    }

    [Fact]
    public void SelectTownUsesCompactThreeLineInformationHierarchy()
    {
        var mainWindow = RepositoryFiles.ReadText("src", "Dynastia.App", "Views", "MainWindow.axaml.cs");
        var inventoryCode = RepositoryFiles.ReadText("src", "Dynastia.App", "Views", "FamilyInventoryWindow.axaml.cs");

        Assert.Contains("compact: isBuy", mainWindow);
        Assert.Contains("compact: true", inventoryCode);
    }

    [Fact]
    public void HeirloomDescriptionWrapsBeforeInheritanceControls()
    {
        var inventory = RepositoryFiles.ReadText("src", "Dynastia.App", "Views", "FamilyInventoryWindow.axaml");
        var heirlooms = inventory.IndexOf("Header=\"Heirlooms\"", StringComparison.Ordinal);

        Assert.True(heirlooms >= 0);

        var origin = inventory.IndexOf(
            "Text=\"{Binding OriginText}\"",
            heirlooms,
            StringComparison.Ordinal);
        Assert.True(origin > heirlooms);

        var inheritance = inventory.IndexOf(
            "Text=\"Inheritance:\"",
            origin,
            StringComparison.Ordinal);
        Assert.True(inheritance > origin);

        Assert.Contains(
            "Classes=\"inventoryAssetDetail\" MaxHeight=\"44\" Text=\"{Binding OriginText}\"",
            inventory);
        Assert.Contains("<Style Selector=\"TextBlock.inventoryAssetDetail\">", inventory);
        Assert.Contains("<Setter Property=\"TextWrapping\" Value=\"Wrap\" />", inventory);
    }

    [Fact]
    public void FamilyTreatmentLabelDoesNotEmbedPrice()
    {
        var treatment = RepositoryFiles.ReadText("plugins", "Dynastia.Mechanics.Wellbeing", "WellbeingPlugin.TreatmentActions.cs");

        Assert.Contains("Label = StripTreatmentCostLabel(", treatment);
        Assert.Contains("StripTreatmentCostLabel", treatment);
        Assert.Contains("\"Summon a Physician\"", treatment);
        Assert.Contains("DisplayCost = presentation.Cost", treatment);
    }

    [Fact]
    public void ChildEducationTabOffersExistingHelpInLearningAction()
    {
        var education = RepositoryFiles.ReadText("src", "Dynastia.App", "ViewModels", "MainWindowViewModel.CraftsEducation.cs");

        Assert.Contains("target.Age < 18", education);
        Assert.Contains("\"education.help_learning\"", education);
        Assert.Contains("\"help_learning\"", education);
        Assert.Contains("LeadingEmoji: \"📚\"", education);
        Assert.Contains("_actionRegistry.Evaluate(", education);
    }

    [Fact]
    public void FamilyInventorySplitsPropertyTabsIntoFourColumnTilesAndClosesForBuySellFlows()
    {
        var inventory = RepositoryFiles.ReadText("src", "Dynastia.App", "Views", "FamilyInventoryWindow.axaml");
        var codeBehind = RepositoryFiles.ReadText("src", "Dynastia.App", "Views", "FamilyInventoryWindow.axaml.cs");
        var viewModels = RepositoryFiles.ReadText("src", "Dynastia.App", "ViewModels", "FamilyInventoryViewModels.cs");

        Assert.DoesNotContain("Header=\"Properties\"", inventory);
        Assert.Contains("Header=\"Houses\"", inventory);
        Assert.Contains("Header=\"Farmland\"", inventory);
        Assert.Contains("Header=\"Heirlooms\"", inventory);
        Assert.DoesNotContain("Text=\"Houses\"", inventory);
        Assert.DoesNotContain("Text=\"Farmland\"", inventory);
        Assert.DoesNotContain("Text=\"Heirlooms\"", inventory);
        Assert.Contains("<Setter Property=\"FontSize\" Value=\"14.5\" />", inventory);
        Assert.True(inventory.Split("Classes=\"inventoryEmpty\"").Length - 1 >= 3);
        Assert.True(inventory.Split("primitives:UniformGrid Columns=\"4\"").Length - 1 >= 3);
        Assert.True(inventory.Split("Text=\"Inheritance:\"").Length - 1 >= 3);
        Assert.DoesNotContain("ToolTip.Tip=\"{Binding OriginText}\"", inventory);
        Assert.Contains("TownText = house.Town.DisplayName", viewModels);
        Assert.Contains("var normalized = Math.Clamp(value, 0, 3);", viewModels);
        Assert.Contains("OpenPropertyWindowAndClose", codeBehind);
        Assert.Contains("Close();", codeBehind);
    }

    [Fact]
    public void LocalServiceTabsHideWhenUnavailableAndRemoteTownsOnlyShowInstitutionsAndHousing()
    {
        var window = RepositoryFiles.ReadText("src", "Dynastia.App", "Views", "TownLifeWindow.axaml");
        var hub = RepositoryFiles.ReadText("src", "Dynastia.App", "ViewModels", "TownAffairsViewModel.cs");

        Assert.Contains("Header=\"Jobs\" IsVisible=\"{Binding ShowJobsTab}\"", window);
        Assert.Contains("Header=\"Health\" IsVisible=\"{Binding ShowHealthTab}\"", window);
        Assert.Contains("Header=\"Education\" IsVisible=\"{Binding ShowEducationTab}\"", window);
        Assert.Contains("Header=\"Bank\" IsVisible=\"{Binding ShowBankTab}\"", window);
        Assert.Contains("Snapshot.School.IsAvailable", hub);
        Assert.Contains("Subject is { Age: >= 6 and < 18 }", hub);
        Assert.Contains("!IsRemote && Snapshot.Bank.IsAvailable", hub);
        Assert.Contains("if (tab is null || !IsTabVisible(tab.Value))", hub);
        Assert.DoesNotContain("Move here before using local services.", hub);
    }

    [Fact]
    public void TownAffairsServiceTabsSwitchBetweenHouseholdMembersAndShowActionEmojis()
    {
        var window = RepositoryFiles.ReadText("src", "Dynastia.App", "Views", "TownLifeWindow.axaml");
        var hub = RepositoryFiles.ReadText("src", "Dynastia.App", "ViewModels", "TownAffairsViewModel.cs");
        var townLife = RepositoryFiles.ReadText("src", "Dynastia.App", "ViewModels", "MainWindowViewModel.TownLife.cs");

        Assert.DoesNotContain("StringFormat='For: {0}'", window);
        Assert.Contains("ItemsSource=\"{Binding HouseholdMembers}\"", window);
        Assert.Contains("Click=\"OnMemberTabClick\"", window);
        Assert.Contains("SelectSubject(Guid personId)", hub);
        Assert.Contains("GetTownAffairsHouseholdMembers()", townLife);
        Assert.Contains("QueueJobApplication(JobActionId, opportunity, Subject)", hub);
        Assert.Contains("QueueEducationAction(optionId, Subject)", hub);
        Assert.Contains("QueueTownAffairsHealthAction(actionId, Subject)", hub);
        Assert.Contains("Text=\"{Binding LeadingEmoji}\"", window);
        Assert.Contains("Text=\"{Binding Emoji}\"", window);
        Assert.Contains("Text=\"🏦\"", window);
        Assert.Contains("Text=\"🤝\"", window);
    }

    [Fact]
    public void InstitutionOrderAndEmptyStatePlacementMatchTownAffairsLayout()
    {
        var service = RepositoryFiles.ReadText("plugins", "Dynastia.Mechanics.TownLife", "StandardTownLifeService.cs");
        var window = RepositoryFiles.ReadText("src", "Dynastia.App", "Views", "TownLifeWindow.axaml");

        var administration = service.IndexOf("\"administration\" => 0", StringComparison.Ordinal);
        var medical = service.IndexOf("\"medical\" => 1", StringComparison.Ordinal);
        var school = service.IndexOf("\"school\" => 2", StringComparison.Ordinal);
        var bank = service.IndexOf("\"bank\" => 3", StringComparison.Ordinal);
        Assert.True(administration >= 0 && medical > administration && school > medical && bank > school);
        Assert.Contains("Margin=\"0,18,0,0\" HorizontalAlignment=\"Center\" TextAlignment=\"Center\"", window);
    }

    [Fact]
    public void CraftProfessionChoicesUseFramedCraftEmojisAndLifestyleButtonsFitPanel()
    {
        var craftEducation = RepositoryFiles.ReadText("src", "Dynastia.App", "ViewModels", "MainWindowViewModel.CraftsEducation.cs");
        var selector = RepositoryFiles.ReadText("src", "Dynastia.App", "Views", "PropertySelectionWindow.axaml");
        var inventory = RepositoryFiles.ReadText("src", "Dynastia.App", "Views", "FamilyInventoryWindow.axaml");

        Assert.Contains("LeadingEmoji: craft.Emoji", craftEducation);
        Assert.Contains("HasLeadingEmoji", selector);
        Assert.Contains("Text=\"{Binding LeadingEmoji}\"", selector);
        Assert.Contains("Width=\"104\"", inventory);
        Assert.Contains("MinWidth=\"0\"", inventory);
    }


    [Fact]
    public void TownAffairsHeaderEconomyAndInstitutionAvailabilityUseRevisedPresentation()
    {
        var window = RepositoryFiles.ReadText("src", "Dynastia.App", "Views", "TownLifeWindow.axaml");
        var codeBehind = RepositoryFiles.ReadText("src", "Dynastia.App", "Views", "TownLifeWindow.axaml.cs");
        var hub = RepositoryFiles.ReadText("src", "Dynastia.App", "ViewModels", "TownAffairsViewModel.cs");
        var institutionService = RepositoryFiles.ReadText("plugins", "Dynastia.Mechanics.TownLife", "StandardTownInstitutionService.cs");
        var townLife = RepositoryFiles.ReadText("plugins", "Dynastia.Mechanics.TownLife", "StandardTownLifeService.cs");

        Assert.Contains("Text=\"{Binding Snapshot.NavigationLabel}\"", window);
        Assert.DoesNotContain("Text=\"{Binding Snapshot.WindowTitle}\"", window);
        Assert.Contains("Title = model.Snapshot.NavigationLabel;", codeBehind);
        Assert.Contains("<StackPanel Spacing=\"12\">", window);
        Assert.Contains("Foreground=\"{Binding ProsperityBrush}\" Text=\"{Binding Snapshot.Prosperity.Index}\"", window);
        Assert.Contains("Text=\"Current Shocks\"", window);
        Assert.Contains("FontSize=\"32\" Text=\"{Binding Emoji}\"", window);
        Assert.Contains("tiers[\"administration\"] = Math.Max(1, tiers[\"administration\"]);", institutionService);
        Assert.Contains("institution.Tier > 0", townLife);
        Assert.Contains("institution.InstitutionId.Equals(\"medical\"", townLife);
        Assert.Contains("institution.InstitutionId.Equals(\"school\"", townLife);
        Assert.Contains("institution.InstitutionId.Equals(\"bank\"", townLife);
        Assert.Contains("<= 84 => DepressedProsperityBrush", hub);
        Assert.Contains("_ => BoomingProsperityBrush", hub);
    }

    [Fact]
    public void JobsMemberTabsOnlyListAdults()
    {
        var window = RepositoryFiles.ReadText("src", "Dynastia.App", "Views", "TownLifeWindow.axaml");
        var hub = RepositoryFiles.ReadText("src", "Dynastia.App", "ViewModels", "TownAffairsViewModel.cs");

        Assert.Contains("ItemsSource=\"{Binding JobHouseholdMembers}\"", window);
        Assert.Contains("IsVisible=\"{Binding ShowJobHouseholdMemberTabs}\"", window);
        Assert.Contains(".Where(person => person.Age >= 18)", hub);
        Assert.Contains("tab == TownAffairsTab.Jobs", hub);
        Assert.Contains("Subject is { Age: < 18 }", hub);
    }

    [Fact]
    public void HouseholdTooltipsShowConditionsAndStressAndHidePreschoolEducation()
    {
        var card = RepositoryFiles.ReadText("src", "Dynastia.App", "ViewModels", "FamilyMemberCardViewModel.cs");
        var family = RepositoryFiles.ReadText("src", "Dynastia.App", "ViewModels", "MainWindowViewModel.Family.cs");
        var window = RepositoryFiles.ReadText("src", "Dynastia.App", "Views", "MainWindow.axaml");

        Assert.Contains("IStressService? stress", card);
        Assert.Contains("snapshot.Conditions.Select(condition => condition.Name)", card);
        Assert.Contains("$\"Health: {healthTooltip} • {conditionTooltip}\"", card);
        Assert.Contains("$\"Stress: {stressSnapshot.Total:0.#}/100\"", card);
        Assert.Contains("person.Age >= 6", card);
        Assert.Contains("_stressService", family);
        Assert.Contains("IsVisible=\"{Binding ShowStress}\"", window);
        Assert.Contains("Text=\"{Binding StressTooltipText}\"", window);
        Assert.Contains("IStatusService? status", card);
        Assert.Contains("ShowAdultStatus", card);
        Assert.Contains("RenownStatusText", card);
        Assert.Contains("ReputationStatusText", card);
        Assert.Contains("Text=\"{Binding RenownStatusText}\"", window);
        Assert.Contains("Text=\"{Binding ReputationStatusText}\"", window);

        var genealogy = RepositoryFiles.ReadText("src", "Dynastia.App", "Genealogy", "Host", "GameGenealogyDataSource.cs");
        Assert.Contains("$\"Stress: {stressSnapshot.Total:0.#}/100\"", genealogy);
        Assert.Contains("$\"Renown: {status.RenownLabel}\"", genealogy);
        Assert.Contains("$\"Reputation: {status.ReputationLabel}\"", genealogy);
    }

    [Fact]
    public void OverlapPickerDoubleClickActivatesTownAndClosesSelector()
    {
        var panel = RepositoryFiles.ReadText("src", "Dynastia.App", "Map", "Views", "TownMapPanel.cs");

        Assert.Contains("_lastOverlapClickTownId", panel);
        Assert.Contains("TimeSpan.FromMilliseconds(500)", panel);
        Assert.Contains("button.Click +=", panel);
        Assert.Contains("activateAfterSelection || isRapidSecondClick", panel);
        Assert.Contains("_overlapPicker.IsVisible = false;", panel);
        Assert.Contains("OnTownActivated(selectedTownId);", panel);
        Assert.Contains("await Task.Delay(500);", panel);
    }

}
