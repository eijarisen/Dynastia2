using Avalonia.Media;
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
        $"{Opportunity.JobTitle} ({Opportunity.JobLevel}) – {Opportunity.CareerName}";

    public string RequirementsText =>
        $"Requires: {Opportunity.PrimaryAbility} {Opportunity.RequiredAbilityLevel} · " +
        $"Education {Opportunity.RequiredEducationLevel} · " +
        $"Experience {Opportunity.RequiredExperienceYears}y";

    public string ApplicantComparisonText =>
        $"Applicant: {Opportunity.PrimaryAbility} {Opportunity.ApplicantAbilityLevel} · " +
        $"Education {Opportunity.ApplicantEducationLevel} · " +
        $"Experience {Opportunity.ApplicantExperienceYears}y" +
        (Opportunity.CraftBonus > 0
            ? $" · Craft bonus +{Opportunity.CraftBonus:P0}"
            : string.Empty);

    public string ChanceText =>
        $"Chance {Opportunity.SuccessChance:P0}";

    public IBrush ChanceBrush =>
        ChancePresentation.ForProbability(
            Opportunity.SuccessChance);

    public string SalaryText =>
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
        string careerEmoji)
    {
        Candidate = candidate;
        CareerEmoji = careerEmoji;

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


    public string PortraitEmoji =>
        Candidate.PortraitEmoji;

    public string Heading =>
        $"{Candidate.DisplayName}, {Candidate.Age}";

    public string PersonalityText =>
        Candidate.Personality.DisplayName;

    public string OriginNationalityText =>
        $"Nationality: {Candidate.DisplayNationality}";

    public string OccupationText =>
        Candidate.JobLevel <= 0
            ? "🔎 Unemployed"
            : $"{CareerEmoji} {Candidate.JobTitle}, Level {Candidate.JobLevel}";

    public string EducationText =>
        $"Education: {Candidate.EducationLevel}";

    public string CraftsText =>
        Candidate.Crafts.Count == 0
            ? "Crafts: None"
            : "Crafts: " + string.Join(
                ", ",
                Candidate.Crafts.Select(craft => craft.Name));

    public bool HasFinancialEstimate =>
        Candidate.Sex == Sex.Male;

    public string FinancialText =>
        $"Estimated wealth: {Candidate.EstimatedWealth:N0} zł · Houses: {Candidate.EstimatedHouses} · Farmland: {Candidate.EstimatedFarmland}";

    public string RenownStatusText =>
        Candidate.Status?.RenownLabel ?? string.Empty;

    public string ReputationStatusText =>
        Candidate.Status?.ReputationLabel ?? string.Empty;

    public IBrush RenownStatusBrush =>
        StatusBrush(RenownStatusText);

    public IBrush ReputationStatusBrush =>
        StatusBrush(ReputationStatusText);

    public bool HasStatus => Candidate.Status is not null;

    public string SuccessChanceText =>
        $"Chance of success: {Candidate.AcceptanceChance:P0}";

    public IBrush SuccessChanceBrush =>
        ChancePresentation.ForProbability(
            Candidate.AcceptanceChance);


    private static IBrush StatusBrush(string label) => label switch
    {
        "Disgraced" => new SolidColorBrush(Color.Parse("#A33636")),
        "Bad" => new SolidColorBrush(Color.Parse("#B95736")),
        "Questionable" => new SolidColorBrush(Color.Parse("#B57A33")),
        "Neutral" => new SolidColorBrush(Color.Parse("#806633")),
        "Good" => new SolidColorBrush(Color.Parse("#5F7C3B")),
        "Respected" => new SolidColorBrush(Color.Parse("#3E713A")),
        "Esteemed" => new SolidColorBrush(Color.Parse("#2F6938")),
        "Obscure" => new SolidColorBrush(Color.Parse("#7A7062")),
        "Known" => new SolidColorBrush(Color.Parse("#8A7041")),
        "Established" => new SolidColorBrush(Color.Parse("#806633")),
        "Prominent" => new SolidColorBrush(Color.Parse("#587640")),
        "Notable" => new SolidColorBrush(Color.Parse("#46703A")),
        "Eminent" => new SolidColorBrush(Color.Parse("#8A5D18")),
        _ => new SolidColorBrush(Color.Parse("#806633"))
    };

    private int Stat(string id) =>
        Candidate.Stats.TryGetValue(id, out var value)
            ? value
            : 0;
}
