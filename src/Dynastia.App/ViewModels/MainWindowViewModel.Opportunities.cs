using Dynastia.Contracts;

namespace Dynastia.App.ViewModels;

public sealed partial class MainWindowViewModel
{
    public JobOpportunityDialogViewModel? GetJobOpportunityDialog(
        string actionId)
    {
        var actor = _succession.ActiveController;
        if (actor is null)
            return null;

        var applicant = IsFamilyJobSearchAction(actionId)
            ? FindSelectedPerson()
            : actor;

        return applicant is null
            ? null
            : GetJobOpportunityDialog(actionId, applicant);
    }

    internal JobOpportunityDialogViewModel? GetJobOpportunityDialog(
        string actionId,
        IPerson applicant)
    {
        if (_careerService is null || _locationService is null)
            return null;

        var opportunities = _careerService
            .GetJobOpportunities(applicant)
            .OrderByDescending(opportunity => opportunity.AnnualSalary)
            .ThenByDescending(opportunity => opportunity.JobLevel)
            .ThenBy(opportunity => opportunity.JobTitle, StringComparer.OrdinalIgnoreCase)
            .Take(4)
            .Select(opportunity =>
                new JobOpportunityCardViewModel(
                    opportunity,
                    _careerPresentationService?
                        .GetCareerEmoji(opportunity.CareerId)
                        ?? CareerPresentationDefaults.DefaultCareerEmoji))
            .ToList();

        var town = _locationService
            .GetLocation(applicant)
            .HomeTown;
        var name = _familyService is null
            ? $"{applicant.Name} {applicant.Surname}"
            : _familyService.GetDisplayName(applicant);

        return new JobOpportunityDialogViewModel(
            $"Job Opportunities — {town.Town}",
            "Available work reflects local and regional opportunities.",
            name,
            opportunities);
    }

    public void QueueJobApplication(
        string actionId,
        JobOpportunityInfo opportunity)
    {
        var actor = _succession.ActiveController;
        if (actor is null || _succession.IsGameOver)
            return;

        var target = IsFamilyJobSearchAction(actionId)
            ? FindSelectedPerson()
            : actor;

        if (target is null)
            return;

        QueueJobApplication(actionId, opportunity, target);
    }

    internal void QueueJobApplication(
        string actionId,
        JobOpportunityInfo opportunity,
        IPerson target)
    {
        var actor = _succession.ActiveController;
        if (actor is null || _succession.IsGameOver)
            return;

        var result = _actionRegistry.Execute(
            actionId,
            actor,
            target,
            new Dictionary<string, string>
            {
                ["jobCareerId"] = opportunity.CareerId,
                ["jobLevel"] = opportunity.JobLevel.ToString(),
                ["jobRequiredAbility"] = opportunity.RequiredAbilityLevel.ToString(),
                ["jobRequiredEducation"] = opportunity.RequiredEducationLevel.ToString(),
                ["jobRequiredExperience"] = opportunity.RequiredExperienceYears.ToString(),
                ["jobSuccessChance"] = opportunity.SuccessChance.ToString(
                    "R",
                    System.Globalization.CultureInfo.InvariantCulture)
            });

        if (!result.Success
            && !string.IsNullOrWhiteSpace(result.Message))
        {
            PersistenceStatusText = result.Message;
        }

        RefreshAfterOpportunitySelection();
    }

    public PotentialPartnerDialogViewModel? GetPotentialPartnerDialog(
        string actionId = "relationship.find_spouse")
    {
        var actor = _succession.ActiveController;
        if (actor is null || _partnerSearchService is null)
            return null;

        var arrangedDaughter = actionId.Equals(
            "relationship.marry_off_daughter",
            StringComparison.OrdinalIgnoreCase);
        var arrangedSon = actionId.Equals(
            "relationship.marry_off_son",
            StringComparison.OrdinalIgnoreCase);
        var arrangedMarriage = arrangedDaughter || arrangedSon;
        var seeker = arrangedMarriage
            ? FindSelectedPerson()
            : actor;

        if (seeker is null)
            return null;

        var name = _familyService is null
            ? $"{seeker.Name} {seeker.Surname}"
            : _familyService.GetDisplayName(seeker);

        var candidateProfiles = arrangedMarriage
            ? _partnerSearchService.GetCandidatesFor(
                seeker,
                arrangedSon ? Sex.Female : Sex.Male,
                arrangedSon ? "arranged-marriage-son" : "arranged-marriage")
            : _partnerSearchService.GetCandidates(seeker);

        var candidates = candidateProfiles
            .Select(candidate =>
                new PotentialPartnerCardViewModel(
                    candidate,
                    candidate.JobLevel > 0
                        ? _careerPresentationService?
                            .GetCareerEmoji(candidate.CareerId)
                            ?? CareerPresentationDefaults.DefaultCareerEmoji
                        : "🔎",
                    arrangedMarriage ? "Choose" : "Approach"))
            .ToList();

        return new PotentialPartnerDialogViewModel(
            name,
            candidates,
            arrangedSon
                ? "Choose a proposed wife."
                : arrangedDaughter
                    ? "Choose a proposed husband."
                    : "Choose whom to approach.");
    }

    public void QueueCourtship(
        string actionId,
        PartnerCandidateInfo candidate)
    {
        var actor = _succession.ActiveController;
        if (actor is null
            || _partnerSearchService is null
            || _succession.IsGameOver)
        {
            return;
        }

        var arrangedMarriage = actionId.Equals(
                "relationship.marry_off_daughter",
                StringComparison.OrdinalIgnoreCase)
            || actionId.Equals(
                "relationship.marry_off_son",
                StringComparison.OrdinalIgnoreCase);

        var target = arrangedMarriage
            ? FindSelectedPerson()
            : actor;

        if (target is null)
            return;

        var result = _actionRegistry.Execute(
            actionId,
            actor,
            target,
            _partnerSearchService.BuildActionParameters(candidate));

        if (!result.Success
            && !string.IsNullOrWhiteSpace(result.Message))
        {
            PersistenceStatusText = result.Message;
        }

        RefreshAfterOpportunitySelection();
    }

    private static bool IsFamilyJobSearchAction(string actionId) =>
        actionId.Equals(
            "career.help_seek_employment",
            StringComparison.OrdinalIgnoreCase)
        || actionId.Equals(
            "career.help_find_better_job",
            StringComparison.OrdinalIgnoreCase);

    private void RefreshAfterOpportunitySelection()
    {
        RefreshPeople();
        RefreshAlbum();
        RefreshHealth();
        RefreshEconomy();
        RefreshEducation();
        RefreshCareer();
        RefreshJustice();
        RefreshNarrative();
        RefreshActions();
    }
}
