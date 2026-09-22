using System.Text.Json;

namespace Dynastia.Core.Tests;

public sealed class LocalSocietyCivicOfficeBatch4Tests
{
    [Fact]
    public void CivicProfilesChangeByPolityAndPost1918PolandAllowsBothSexes()
    {
        var rows = Read("data", "LocalSociety", "civic_office_profiles.csv")
            .TrimStart('\uFEFF')
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Skip(1)
            .Select(line => line.Split(','))
            .ToArray();

        Assert.Contains(rows, row => row[0] == "PLC" && row[3] == "Town Councillor" && row[4] == "Burmistrz");
        Assert.Contains(rows, row => row[0] == "PRUSSIA" && row[4] == "Bürgermeister");
        Assert.Contains(rows, row => row[0] == "PL" && row[1] == "1918" && row[7] == "Any");
        Assert.Contains(rows, row => row[0] == "PL" && row[1] == "1990" && row[5] == "City President" && row[7] == "Any");
    }

    [Fact]
    public void EligiblePlayerStillCompetesWithThreeNpcElitesAndHighStatusHelpsMaterially()
    {
        using var rules = JsonDocument.Parse(Read("data", "LocalSociety", "civic_office_rules.json"));
        var appointment = rules.RootElement.GetProperty("appointment");
        Assert.Equal(3, appointment.GetProperty("npcCandidateCount").GetInt32());
        var npcRange = appointment.GetProperty("npcCandidateWeightRange")
            .EnumerateArray()
            .Select(value => value.GetInt32())
            .ToArray();
        Assert.Equal(new[] { 120, 190 }, npcRange);

        static double Weight(double renown, double reputation, int education, int appeal, int participation) =>
            Math.Max(1, 1.5 * renown + 0.5 * Math.Max(reputation, 0) + 4 * education + 3 * appeal + 2 * Math.Min(participation, 10));

        var threshold = Weight(65, 0, 3, 3, 5);
        var eminent = Weight(100, 50, 5, 5, 10);
        var thresholdVsWeakestNpcField = threshold / (threshold + 3 * 120d);
        var eminentVsWeakestNpcField = eminent / (eminent + 3 * 120d);

        Assert.True(thresholdVsWeakestNpcField < 0.30);
        Assert.True(eminent > threshold * 1.7);
        Assert.True(eminentVsWeakestNpcField > thresholdVsWeakestNpcField + 0.10);

        var service = Read("plugins", "Dynastia.Mechanics.Community", "CivicOfficeService.cs");
        Assert.Contains("1.5 * social.LocalRenown", service);
        Assert.Contains("_rules.NpcCandidateCount", service);
        Assert.Contains("AppointSimulated", service);
        Assert.Contains("AppointNpc", service);
    }

    [Fact]
    public void AppointmentEndsOtherLivelihoodAndOfficeBecomesSyntheticLevelFiveCareer()
    {
        var civic = Read("plugins", "Dynastia.Mechanics.Community", "CivicOfficeService.cs");
        var career = Read("plugins", "Dynastia.Mechanics.Career", "StandardCareerService.cs");
        var farming = Read("plugins", "Dynastia.Mechanics.Farming", "StandardFarmingService.cs");

        Assert.Contains("_career.AssignCareer(person, null, 0", civic);
        Assert.Contains("_crafts.EndOccupation(person, \"civic office\")", civic);
        Assert.Contains("if (civicOffice?.IsTownHead(person) == true)", career);
        Assert.Contains("new CareerSnapshot(", career);
        Assert.Contains("TownHeadStatusId", career);
        Assert.Contains("return !_career.IsEmployed(person);", farming);
    }

    [Fact]
    public void SalaryUsesPublicAdministrationLevelFiveSettlementAndProsperityAndCountsAsCareerIncome()
    {
        using var rules = JsonDocument.Parse(Read("data", "LocalSociety", "civic_office_rules.json"));
        var multipliers = rules.RootElement.GetProperty("employment").GetProperty("salary").GetProperty("settlementMultipliers");
        Assert.Equal(0.9m, multipliers.GetProperty("SmallTown").GetDecimal());
        Assert.Equal(1.0m, multipliers.GetProperty("Town").GetDecimal());
        Assert.Equal(1.1m, multipliers.GetProperty("City").GetDecimal());
        Assert.Equal(1.2m, multipliers.GetProperty("MajorCity").GetDecimal());

        var civic = Read("plugins", "Dynastia.Mechanics.Community", "CivicOfficeService.cs");
        var compensation = Read("plugins", "Dynastia.Mechanics.Career", "StandardCareerService.Compensation.cs");
        var lifetime = Read("plugins", "Dynastia.Mechanics.Career", "CareerLifetimeEarningsYearSystem.cs");
        Assert.Contains("GetLevelOneSalary(PublicAdministrationCareerId) * 10m", civic);
        Assert.Contains("_prosperity.GetIncomeMultiplier", civic);
        Assert.Contains("return civicOffice.GetAnnualSalary(person);", compensation);
        Assert.Contains("RecordLifetimeCareerEarnings", lifetime);
    }

    [Fact]
    public void DutiesPreventNeglectIncreaseParticipationAndApprovalWhileImprisonmentRemovesOfficeSameYear()
    {
        using var rules = JsonDocument.Parse(Read("data", "LocalSociety", "civic_office_rules.json"));
        var approval = rules.RootElement.GetProperty("approval");
        Assert.Equal(-8, approval.GetProperty("noCommunityOrOfficeActionPenalty").GetInt32());
        Assert.Equal(4, approval.GetProperty("performOfficeDutiesBonus").GetInt32());
        Assert.True(approval.GetProperty("imprisonmentCausesImmediateLoss").GetBoolean());

        var civic = Read("plugins", "Dynastia.Mechanics.Community", "CivicOfficeService.cs");
        var system = Read("plugins", "Dynastia.Mechanics.Community", "CivicOfficeYearSystem.cs");
        Assert.Contains("state.LastOfficeActionYear != _gameState.Year", civic);
        Assert.Contains("participation.Count += 1", civic);
        Assert.Contains("PendingApprovalAdjustment += _rules.DutiesBonus", civic);
        Assert.Contains("outgoing.Tags.Has(\"state.imprisoned\")", civic);
        Assert.Contains("\"justice.crime\"", system);
    }

    [Fact]
    public void ReplacementLeavesOutgoingHeadUnoccupiedAndTownAffairsShowsOfficeAndDuties()
    {
        var civic = Read("plugins", "Dynastia.Mechanics.Community", "CivicOfficeService.cs");
        var careerActions = Read("plugins", "Dynastia.Mechanics.Career", "CareerPlugin.Actions.cs");
        var townLife = Read("plugins", "Dynastia.Mechanics.TownLife", "StandardTownLifeService.cs");
        var window = Read("src", "Dynastia.App", "Views", "TownLifeWindow.axaml");
        var viewModel = Read("src", "Dynastia.App", "ViewModels", "TownAffairsViewModel.cs");

        Assert.Contains("LoseSimulatedOffice(outgoing", civic);
        Assert.Contains("person.Tags.Remove(TownHeadTag)", civic);
        Assert.Contains("community.civic_office_lost", civic);
        Assert.Contains("!actionContext.Actor.Tags.Has(\"civic.office.town_head\")", careerActions);
        Assert.Contains("_civicOfficeResolver()?.GetTownHead", townLife);
        Assert.Contains("CivicOffice.OfficeTitle", window);
        Assert.Contains("Approval", viewModel);
        Assert.Contains("OnOfficeDutiesClick", window);
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
