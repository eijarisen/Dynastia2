using Dynastia.Contracts;

namespace Dynastia.Mechanics.Career;

public sealed partial class CareerPlugin
{
    private const string JobCareerIdParameter =
        "jobCareerId";

    private const string JobLevelParameter =
        "jobLevel";

    private const string JobSuccessChanceParameter =
        "jobSuccessChance";

    private static bool TryGetSelectedJob(
        GameActionContext actionContext,
        out string careerId,
        out int jobLevel)
    {
        careerId = string.Empty;
        jobLevel = 0;

        if (!actionContext.Parameters.TryGetValue(
                JobCareerIdParameter,
                out var selectedCareerId)
            || string.IsNullOrWhiteSpace(selectedCareerId))
        {
            return false;
        }

        careerId = selectedCareerId;

        return actionContext.Parameters.TryGetValue(
                JobLevelParameter,
                out var levelText)
            && int.TryParse(levelText, out jobLevel)
            && jobLevel is >= 1 and <= 3;
    }

    private static GameActionResult ResolveSelectedJobApplication(
        GameActionContext actionContext,
        StandardCareerService career,
        IFamilyService family,
        IGameEventBus events,
        IPerson applicant,
        IPerson? helper = null)
    {
        if (!TryGetSelectedJob(
                actionContext,
                out var careerId,
                out var jobLevel))
        {
            return new GameActionResult(false);
        }

        var result = actionContext.Parameters.TryGetValue(
                JobSuccessChanceParameter,
                out var chanceText)
            && double.TryParse(
                chanceText,
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out var selectedChance)
            ? career.ApplyForJobWithChance(
                applicant,
                careerId,
                jobLevel,
                selectedChance)
            : career.ApplyForJob(
                applicant,
                careerId,
                jobLevel);

        if (!result.IsValid)
            return new GameActionResult(true);

        if (!result.WasAccepted)
            return new GameActionResult(true);

        var data = new Dictionary<string, string>
        {
            ["careerId"] = result.After.CareerId ?? string.Empty,
            ["careerName"] = result.After.CareerName ?? string.Empty,
            ["jobTitle"] = result.After.JobTitle,
            ["jobLevel"] = result.After.JobLevel.ToString(),
            ["chance"] = result.SuccessChance.ToString("0.00")
        };

        if (result.Before.JobLevel > 0)
        {
            data["oldCareerId"] = result.Before.CareerId ?? string.Empty;
            data["text"] =
                $"{family.GetDisplayName(applicant)} left {result.Before.JobTitle} work " +
                $"for a better-paying position as {result.After.JobTitle}.";

            events.Publish(new GameEvent
            {
                Type = "career.changed_job",
                Year = actionContext.GameState.Year,
                SubjectId = applicant.Id,
                RelatedPersonIds = helper is null ? [] : [helper.Id],
                Data = data
            });
        }
        else
        {
            data["text"] =
                $"{family.GetDisplayName(applicant)} found employment as {result.After.JobTitle}.";

            events.Publish(new GameEvent
            {
                Type = "career.employment",
                Year = actionContext.GameState.Year,
                SubjectId = applicant.Id,
                RelatedPersonIds = helper is null ? [] : [helper.Id],
                Data = data
            });
        }

        return new GameActionResult(true);
    }
}
