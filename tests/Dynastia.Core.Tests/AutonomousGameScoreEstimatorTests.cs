using System.Text.Json;
using Dynastia.Contracts;
using Dynastia.Mechanics.GameScore;
using Dynastia.Mechanics.Households;

namespace Dynastia.Core.Tests;

public sealed partial class AutonomousStrategyCharacterizationTests
{
    [Fact]
    public void ScorePreviewUsesExistingAwardsAndClaimsWithoutChangingTheLedger()
    {
        using var f = new Fixture();
        var score = InstallScorePreview(f);
        var property = f.World.NextId().ToString();
        var initial = new[]
        {
            ScoreEvent(f, "education.success", ("level", "2")),
            ScoreEvent(f, "stats.paid_improvement", ("statId", "immunity"), ("previousValue", "2"), ("newValue", "3")),
            ScoreEvent(f, "craft.learned", ("craftId", "carpentry")),
            ScoreEvent(f, "career.employment"),
            ScoreEvent(f, "career.promotion", ("jobLevel", "3")),
            ScoreEvent(f, "household.house_bought", ("propertyId", property)),
            ScoreEvent(f, "household.house_extended", ("propertyId", property))
        };
        foreach (var item in initial)
            f.World.Events.Publish(item);
        var component = f.Head.Components.Get<GameScoreComponent>()!;
        var before = JsonSerializer.Serialize(component);
        var eventCount = f.World.Events.AllEvents.Count;

        Assert.Equal(0, score.PreviewEventDelta(initial[..6]));
        Assert.Equal(-100, score.PreviewEventDelta([
            ScoreEvent(f, "career.quit"),
            ScoreEvent(f, "household.house_sold", ("propertyId", property))]));
        Assert.Equal(-15, score.PreviewEventDelta([
            ScoreEvent(f, "career.quit"), ScoreEvent(f, "career.employment")]));
        Assert.Equal(25, score.PreviewEventDelta([
            ScoreEvent(f, "household.house_extended", ("propertyId", property))]));

        Assert.Equal(before, JsonSerializer.Serialize(component));
        Assert.Equal(eventCount, f.World.Events.AllEvents.Count);
        Assert.Equal(0, f.World.Random.ConsumedCount);
    }

    [Fact]
    public void ScorePreviewHonorsFamilyNewsAndLockedClaimsWithoutCreatingState()
    {
        using var f = new Fixture();
        var visible = false;
        var households = Probe<IHouseholdService>((method, _) => method.Name == "ShouldShowFamilyNews"
            ? visible : throw new InvalidOperationException(method.Name));
        var score = new StandardGameScoreService(f.World.State, f.World.Events, f.World.Family,
            households, f.World.State, null, null, null, null);
        Assert.Equal(0, score.PreviewEventDelta([ScoreEvent(f, "career.employment")]));
        visible = true;
        Assert.Equal(10, score.PreviewEventDelta([ScoreEvent(f, "career.employment")]));
        Assert.Null(f.Head.Components.Get<GameScoreComponent>());
        Assert.Empty(f.World.Events.AllEvents);

        f.World.Events.Publish(ScoreEvent(f, "career.employment"));
        f.World.Events.Publish(ScoreEvent(f, "career.retirement"));
        Assert.Equal(0, score.PreviewEventDelta([
            ScoreEvent(f, "career.quit"), ScoreEvent(f, "career.employment")]));
        Assert.Equal(10, score.TotalScore);
    }

    [Fact]
    public void ScoreEstimatorCountsOnlyNewStatAndEducationTiersAtTheirSuccessChance()
    {
        using var f = new Fixture();
        InstallScorePreview(f);
        f.Context.AddService<IEducationService>(Probe<IEducationService>((method, _) => method.Name switch
        {
            "GetEducationLevel" => (object)1,
            "GetLocalEducationCeiling" or "GetHelpedEducationCeiling" => (object)5,
            "GetPaidEducationSuccessChance" => 0.5,
            "GetPrivateTutorSuccessChance" => 0.9,
            _ => throw new InvalidOperationException(method.Name)
        }));
        var snapshot = f.Snapshot with
        {
            Members = [Member(f.Head) with { Stats = new Dictionary<string, int> { ["immunity"] = 3 } }]
        };
        var estimator = new AutonomousGameScoreEstimator(f.Context);
        Assert.Equal(25, estimator.Estimate(Candidate("stats.improve_immunity", f.Head), snapshot));
        Assert.Equal(7.5, estimator.Estimate(Candidate("education.get_education", f.Head), snapshot));
        Assert.Equal(13.5, estimator.Estimate(Candidate("education.private_tutor", f.Head), snapshot));
        f.World.Events.Publish(ScoreEvent(f, "stats.paid_improvement", ("statId", "immunity"), ("previousValue", "3"), ("newValue", "4")));
        f.World.Events.Publish(ScoreEvent(f, "education.success", ("level", "2")));
        Assert.Equal(0, estimator.Estimate(Candidate("stats.improve_immunity", f.Head), snapshot));
        Assert.Equal(0, estimator.Estimate(Candidate("education.get_education", f.Head), snapshot));
        Assert.Equal(0, f.World.Random.ConsumedCount);
    }

    [Fact]
    public void ScoreEstimatorUsesNetClaimsForEmploymentAndPreservesSaleReversals()
    {
        using var f = new Fixture();
        InstallScorePreview(f);
        var estimator = new AutonomousGameScoreEstimator(f.Context);
        var career = EmployedCareer() with { IsEmployed = false };
        f.Context.AddService<ICareerService>(Probe<ICareerService>((method, _) =>
            method.Name == "GetCareer" ? career : throw new InvalidOperationException(method.Name)));
        var job = Candidate("career.seek_employment", f.Head) with
        {
            Parameters = new Dictionary<string, string> { ["jobSuccessChance"] = "0.25" }
        };
        Assert.Equal(2.5, estimator.Estimate(job, f.Snapshot));
        f.World.Events.Publish(ScoreEvent(f, "career.employment"));
        Assert.Equal(0, estimator.Estimate(job, f.Snapshot));
        career = career with { IsEmployed = true };
        Assert.Equal(0, estimator.Estimate(job with { Action = Definition("career.find_another_job") }, f.Snapshot));
        Assert.Equal(-10, estimator.Estimate(Candidate("career.quit_job", f.Head), f.Snapshot));

        var property = f.World.NextId().ToString();
        f.World.Events.Publish(ScoreEvent(f, "household.house_bought", ("propertyId", property)));
        f.World.Events.Publish(ScoreEvent(f, "household.house_extended", ("propertyId", property)));
        var sale = Candidate("household.sell_house", f.Head) with
        {
            Parameters = new Dictionary<string, string> { ["propertyId"] = property }
        };
        Assert.Equal(-75, estimator.Estimate(sale, f.Snapshot));
        Assert.Equal(50, estimator.Estimate(Candidate("household.buy_house", f.Head), f.Snapshot));
        Assert.Equal(35, estimator.Estimate(Candidate("farming.buy_farmland", f.Head), f.Snapshot));
        Assert.Equal(0, estimator.Estimate(Candidate("relationship.divorce", f.Head), f.Snapshot));
        Assert.Equal(0, f.World.Random.ConsumedCount);
    }

    [Fact]
    public void ScoreEstimatorDoesNotFarmCraftLearningOrDiscardCareerClaimLosses()
    {
        using var f = new Fixture();
        InstallScorePreview(f);
        var estimator = new AutonomousGameScoreEstimator(f.Context);
        var study = Candidate("education.get_education", f.Head) with
        {
            Parameters = new Dictionary<string, string> { ["educationOption"] = "craft:carpentry" }
        };
        var option = new CraftEducationOption("carpentry", "Carpentry", false, 0, "Unknown", 0, 0,
            "strength", 3, 0.6);
        f.Context.AddService<ICraftService>(Probe<ICraftService>((method, _) => method.Name switch
        {
            "GetEducationOptions" => new[] { option },
            "KnowsCraft" => true,
            "IsSelfEmployed" => false,
            _ => throw new InvalidOperationException(method.Name)
        }));
        f.Context.AddService<ICareerService>(Probe<ICareerService>((method, _) => method.Name == "GetCareer"
            ? EmployedCareer() : throw new InvalidOperationException(method.Name)));
        Assert.Equal(9, estimator.Estimate(study, f.Snapshot));
        f.World.Events.Publish(ScoreEvent(f, "craft.learned", ("craftId", "carpentry")));
        Assert.Equal(0, estimator.Estimate(study, f.Snapshot));
        option = option with { IsKnownCraft = true, CurrentMasteryLevel = 2 };
        Assert.Equal(0, estimator.Estimate(study, f.Snapshot));

        f.World.Events.Publish(ScoreEvent(f, "career.employment"));
        f.World.Events.Publish(ScoreEvent(f, "career.promotion", ("jobLevel", "3")));
        Assert.Equal(-15, estimator.Estimate(Candidate("craft.start.carpentry", f.Head), f.Snapshot));
        Assert.Equal(40, f.Head.Components.Get<GameScoreComponent>()!.TotalScore);
        Assert.Equal(0, f.World.Random.ConsumedCount);
    }

    private static StandardGameScoreService InstallScorePreview(Fixture f)
    {
        var households = Probe<IHouseholdService>((method, _) => method.Name == "ShouldShowFamilyNews"
            ? true : throw new InvalidOperationException(method.Name));
        var score = new StandardGameScoreService(f.World.State, f.World.Events, f.World.Family,
            households, f.World.State, null, null, null, null);
        f.Context.AddService<IGameScorePreviewService>(score);
        f.Context.AddService<IGameState>(f.World.State);
        return score;
    }

    private static GameEvent ScoreEvent(Fixture f, string type, params (string Key, string Value)[] data) =>
        new()
        {
            Type = type, Year = f.World.State.Year, SubjectId = f.Head.Id,
            Data = data.ToDictionary(pair => pair.Key, pair => pair.Value)
        };
}
