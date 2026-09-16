using Dynastia.Contracts;

namespace Dynastia.App.ViewModels;

public sealed record JobOpportunityDialogViewModel(
    string Title,
    string Subtitle,
    string ApplicantName,
    IReadOnlyList<JobOpportunityCardViewModel> Opportunities)
{
    public string ApplicantText => $"Applicant: {ApplicantName}";
}

public sealed class JobOpportunityCardViewModel
{
    public JobOpportunityCardViewModel(
        JobOpportunityInfo opportunity,
        string careerEmoji)
    {
        Opportunity = opportunity;
        CareerEmoji = careerEmoji;
    }

    public JobOpportunityInfo Opportunity { get; }

    public string CareerEmoji { get; }

    public string Heading =>
        $"{Opportunity.JobTitle} ({Opportunity.JobLevel}) -- {Opportunity.CareerName}";

    public string RequirementsText =>
        $"Requires: {Opportunity.PrimaryAbility} {Opportunity.RequiredAbilityLevel} · " +
        $"Education {Opportunity.RequiredEducationLevel} · " +
        $"Experience {Opportunity.RequiredExperienceYears}y";

    public string ApplicantComparisonText =>
        $"Applicant: {Opportunity.PrimaryAbility} {Opportunity.ApplicantAbilityLevel} · " +
        $"Education {Opportunity.ApplicantEducationLevel} · " +
        $"Experience {Opportunity.ApplicantExperienceYears}y · " +
        $"Chance {Opportunity.SuccessChance:P0} · " +
        $"Salary {Opportunity.AnnualSalary:N0} zł/year";
}

public sealed record PotentialPartnerDialogViewModel(
    string SearcherName,
    IReadOnlyList<PotentialPartnerCardViewModel> Candidates,
    string SelectionPrompt = "Choose whom to approach.")
{
    public string IntroText =>
        $"Possible matches for {SearcherName}. {SelectionPrompt}";
}

public sealed class PotentialPartnerCardViewModel
{
    private static readonly (string Id, string Name)[] StatOrder =
    [
        ("immunity", "Immunity"),
        ("longevity", "Longevity"),
        ("fertility", "Fertility"),
        ("appeal", "Appeal"),
        ("strength", "Strength"),
        ("intellect", "Intellect")
    ];

    public PotentialPartnerCardViewModel(
        PartnerCandidateInfo candidate,
        string careerEmoji,
        string actionVerb = "Approach")
    {
        Candidate = candidate;
        CareerEmoji = careerEmoji;
        ActionVerb = actionVerb;

        Stats = StatOrder
            .Select(definition =>
                new StatValue(
                    definition.Id,
                    definition.Name,
                    Stat(definition.Id),
                    string.Empty))
            .ToList();
    }

    public PartnerCandidateInfo Candidate { get; }

    public IReadOnlyList<StatValue> Stats { get; }

    public string CareerEmoji { get; }

    public string ActionVerb { get; }

    public string PortraitEmoji =>
        Candidate.PortraitEmoji;

    public string Heading =>
        $"{Candidate.DisplayName}, {Candidate.Age}";

    public string PersonalityText =>
        Candidate.Personality.DisplayName;

    public string OccupationText =>
        Candidate.JobLevel <= 0
            ? "🔎 Unemployed"
            : $"{CareerEmoji} {Candidate.JobTitle} — {Candidate.CareerName}, Level {Candidate.JobLevel}";

    public string EducationText =>
        $"Education: {Candidate.EducationLevel}";

    public string HobbiesText =>
        Candidate.Hobbies.Count == 0
            ? "Hobbies: None"
            : "Hobbies: " + string.Join(
                ", ",
                Candidate.Hobbies.Select(hobby => hobby.Name));

    public bool HasFinancialEstimate =>
        Candidate.Sex == Sex.Male;

    public string FinancialText =>
        $"Estimated wealth: {Candidate.EstimatedWealth:N0} zł · Houses: {Candidate.EstimatedHouses}";

    public string SuccessChanceText =>
        $"Chance of success: {Candidate.AcceptanceChance:P0}";

    public string ApproachText =>
        $"{ActionVerb} {Candidate.Name}";

    private int Stat(string id) =>
        Candidate.Stats.TryGetValue(id, out var value)
            ? value
            : 0;
}
