using System.Globalization;
using Dynastia.Contracts;

namespace Dynastia.Mechanics.Households;

/// <summary>
/// Estimates immediate, attributable score only. Safety and family continuity must be
/// resolved before this value is used. Unknown outcomes receive no speculative points.
/// </summary>
internal sealed class AutonomousGameScoreEstimator(IGamePluginContext context)
{
    public double Estimate(AutonomousActionCandidate candidate, AutonomousHouseholdSnapshot snapshot)
    {
        var preview = context.GetService<IGameScorePreviewService>();
        if (preview is null)
            return 0;

        var id = candidate.Action.Id.ToLowerInvariant();
        var target = candidate.Target;
        var year = context.GetService<IGameState>()?.Year ?? 0;
        var outcomes = new List<GameEvent>();
        var chance = 1.0;

        void Add(string type, IPerson person, params (string Key, string Value)[] data) =>
            outcomes.Add(new GameEvent
            {
                Type = type,
                Year = year,
                SubjectId = person.Id,
                RelatedPersonIds = person.Id == snapshot.Head.Id ? [] : [snapshot.Head.Id],
                Data = data.ToDictionary(pair => pair.Key, pair => pair.Value)
            });

        if (id.StartsWith("stats.improve_", StringComparison.Ordinal))
        {
            var statId = id["stats.improve_".Length..];
            var member = snapshot.Members.FirstOrDefault(item => item.Person.Id == target.Id);
            var value = member?.Stats.TryGetValue(statId, out var captured) == true
                ? captured
                : context.GetService<IStatsService>()?.GetStats(target)
                    .FirstOrDefault(stat => stat.Id.Equals(statId, StringComparison.OrdinalIgnoreCase))?.Value;
            if (value is null || value >= 5)
                return 0;
            Add("stats.paid_improvement", target,
                ("statId", statId), ("previousValue", Number(value.Value)), ("newValue", Number(value.Value + 1)));
        }
        else if (id is "education.get_education" or "education.private_tutor")
        {
            var selected = candidate.Parameters.GetValueOrDefault("educationOption", "standard");
            if (id == "education.get_education" && selected.StartsWith("craft:", StringComparison.OrdinalIgnoreCase))
            {
                var craftId = selected["craft:".Length..];
                var option = context.GetService<ICraftService>()?.GetEducationOptions(target)
                    .FirstOrDefault(item => item.CraftId.Equals(craftId, StringComparison.OrdinalIgnoreCase));
                // A learned craft's course adds progress, which need not yield a mastery
                // event. Do not treat every successful course as a scored achievement.
                if (option is null || option.IsKnownCraft)
                    return 0;
                chance = option.SuccessChance;
                Add("craft.learned", target, ("craftId", option.CraftId));
            }
            else
            {
                if (!selected.Equals("standard", StringComparison.OrdinalIgnoreCase))
                    return 0;
                var education = context.GetService<IEducationService>();
                if (education is null)
                    return 0;
                var level = education.GetEducationLevel(target);
                var ceiling = id == "education.private_tutor"
                    ? Math.Min(5, education.GetHelpedEducationCeiling(year))
                    : education.GetLocalEducationCeiling(target, year);
                if (level >= ceiling)
                    return 0;
                chance = id == "education.private_tutor"
                    ? education.GetPrivateTutorSuccessChance(target)
                    : education.GetPaidEducationSuccessChance(target)
                        * HouseholdLifestyleRules.GetEducationChanceMultiplier(target);
                Add(id == "education.private_tutor" ? "education.private_tutor_success" : "education.success",
                    target, ("level", Number(level + 1)));
            }
        }
        else if (id is "career.seek_employment" or "career.help_seek_employment"
                 or "career.find_another_job" or "career.help_find_better_job")
        {
            var applicant = id.StartsWith("career.help_", StringComparison.Ordinal) ? target : snapshot.Head;
            if (!candidate.Parameters.TryGetValue("jobSuccessChance", out var rawChance)
                || !double.TryParse(rawChance, NumberStyles.Float, CultureInfo.InvariantCulture, out chance))
                return 0;
            var current = context.GetService<ICareerService>()?.GetCareer(applicant)
                ?? snapshot.Members.FirstOrDefault(member => member.Person.Id == applicant.Id)?.Career;
            if (current?.IsEmployed == true)
                return 0; // A better job emits career.changed_job, which carries no score.
            if (context.GetService<ICraftService>()?.IsSelfEmployed(applicant) == true)
                Add("craft.self_employment_ended", applicant);
            if (context.GetService<ICriminalOccupationService>()?.IsActive(applicant) == true)
                Add("justice.life_of_crime_ended", applicant);
            Add("career.employment", applicant);
        }
        else if (id.StartsWith("craft.start.", StringComparison.Ordinal))
        {
            var crafts = context.GetService<ICraftService>();
            if (crafts is null || !crafts.KnowsCraft(target, id["craft.start.".Length..]))
                return 0;
            if (crafts.IsSelfEmployed(target))
                Add("craft.self_employment_ended", target);
            if (context.GetService<ICareerService>()?.GetCareer(target).IsEmployed == true
                || snapshot.Members.FirstOrDefault(member => member.Person.Id == target.Id)?.Career?.IsEmployed == true)
                Add("career.quit", target); // The score reconciliation reverses this abandoned claim.
            if (context.GetService<ICriminalOccupationService>()?.IsActive(target) == true)
                Add("justice.life_of_crime_ended", target);
            Add("craft.self_employment_started", target);
        }
        else if (id == "career.quit_job")
            Add("career.quit", snapshot.Head);
        else if (id == "household.buy_house")
            Add("household.house_bought", snapshot.Head, ("propertyId", NewAssetKey(candidate, year)));
        else if (id == "farming.buy_farmland")
            Add("farmland.bought", snapshot.Head, ("farmlandId", NewAssetKey(candidate, year)));
        else if (id is "household.extend_house" or "household.sell_house")
        {
            if (!candidate.Parameters.TryGetValue("propertyId", out var propertyId))
                return 0;
            Add(id == "household.extend_house" ? "household.house_extended" : "household.house_sold",
                snapshot.Head, ("propertyId", propertyId));
        }
        else if (id is "farming.add_livestock" or "farming.sell_farmland")
        {
            if (!candidate.Parameters.TryGetValue("farmlandId", out var farmlandId))
                return 0;
            Add(id == "farming.add_livestock" ? "farming.livestock_added" : "farmland.sold",
                snapshot.Head, ("farmlandId", farmlandId));
        }

        return outcomes.Count == 0 || !double.IsFinite(chance)
            ? 0
            : Math.Clamp(chance, 0, 1) * preview.PreviewEventDelta(outcomes);
    }

    private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);

    // Real purchases create fresh GUIDs. A deterministic, non-GUID preview key cannot
    // collide with an existing property, and needs no random draw or generated asset.
    private static string NewAssetKey(AutonomousActionCandidate candidate, int year) =>
        $"autonomy-preview:{candidate.Target.Id:D}:{candidate.Action.Id}:{year}";
}
