using Dynastia.Contracts;

namespace Dynastia.App.ViewModels;

public sealed class FamilyMemberCardViewModel
{
    public FamilyMemberCardViewModel(
        IPerson person,
        IFamilyService? family,
        IHealthService? health,
        IEducationService? education,
        ICareerService? career,
        IJusticeService? justice,
        IStatsService? stats,
        ILocationService? locations,
        IMarriageSatisfactionService? marriageSatisfaction,
        IThoughtService? thoughts,
        bool isSelected,
        bool isActiveHouseholdHead,
        Action<Guid> selectPerson)
    {
        PersonId =
            person.Id;

        FirstName =
            person.Name;

        Surname =
            family is null
                ? person.Surname
                : family.FormatSurname(
                    person.Surname,
                    family.GetSex(person));

        FullName =
            family is null
                ? $"{FirstName} {Surname}"
                : family.GetDisplayName(
                    person);

        IsLiving =
            !person.Tags.Has(
                "state.dead");

        IsSelected =
            isSelected;

        IsActiveHouseholdHead =
            isActiveHouseholdHead;

        AvatarText =
            PersonEmojiResolver.GetPersonEmoji(
                person,
                family,
                health,
                career,
                justice,
                stats,
                thoughts);

        var birthYear =
            person.BirthDate?.Year
            ?? 0;

        LifeSpanText =
            IsLiving
                ? birthYear > 0
                    ? $"{birthYear} • Age {person.Age}"
                    : $"Age {person.Age}"
                : person.DeathDate is GameDate deathDate
                    ? birthYear > 0
                        ? $"{birthYear}-{deathDate.Year} • Age {person.Age}"
                        : $"{deathDate.Year} • Age {person.Age}"
                    : birthYear > 0
                        ? $"{birthYear} • Age {person.Age}"
                        : $"Age {person.Age}";

        var healthTooltip =
            "Unknown";

        if (health is not null)
        {
            if (IsLiving)
            {
                var snapshot =
                    health.GetHealth(
                        person);

                ShowHealth =
                    true;

                HealthValue =
                    snapshot.Percentage;

                HealthText =
                    $"{Math.Round(snapshot.Current)}/" +
                    $"{Math.Round(snapshot.Maximum)}";

                healthTooltip =
                    HealthText;
            }
            else
            {
                healthTooltip =
                    "Deceased";
            }
        }

        var educationTooltip =
            education is null
                ? "Unknown"
                : $"Level " +
                  $"{education.GetEducationLevel(person)}";

        var satisfactionTooltip =
            "N/A";

        var lastOccupationTooltip =
            "None";

        if (career is not null)
        {
            var careerSnapshot =
                career.GetCareer(
                    person);

            OccupationText =
                FormatOccupation(
                    careerSnapshot.JobTitle,
                    careerSnapshot.JobLevel);

            lastOccupationTooltip =
                ResolveLastOccupation(
                    careerSnapshot);

            satisfactionTooltip =
                careerSnapshot.JobLevel > 0
                && !careerSnapshot.IsRetired
                    ? careerSnapshot.JobSatisfactionText
                    : "N/A";
        }

        if (justice is not null)
        {
            var justiceStatus =
                justice.GetStatus(
                    person);

            if (justiceStatus.IsImprisoned)
            {
                OccupationText =
                    justiceStatus.IsLifeSentence
                        ? "Imprisoned · Life"
                        : justiceStatus.RemainingYears == 1
                            ? "Imprisoned · 1 year left"
                            : $"Imprisoned · " +
                              $"{justiceStatus.RemainingYears} " +
                              "years left";
            }
        }

        ShowOccupation =
            IsLiving
            && !string.IsNullOrWhiteSpace(
                OccupationText);

        if (locations is not null)
        {
            var location =
                locations.GetLocation(
                    person);

            TownText =
                IsLiving
                    ? location.HomeTown.Town
                    : (
                        location.DeathTown
                        ?? location.HomeTown
                    ).Town;
        }

        var spouse =
            family?.GetSpouse(
                person);

        var spouseTooltip =
            spouse is null
                ? "None"
                : family is null
                    ? $"{spouse.Name} {spouse.Surname}"
                    : family.GetDisplayName(
                        spouse);

        var marriageTooltip =
            marriageSatisfaction?
                .GetSatisfaction(
                    person);

        var marriageText =
            marriageTooltip?.Label
            ?? "N/A";

        var thought =
            IsLiving
                ? thoughts?.GetCurrentThought(
                    person)
                : null;

        ThoughtText =
            thought?.Text
            ?? string.Empty;

        var father =
            family?.GetFather(
                person);

        var mother =
            family?.GetMother(
                person);

        var parentNames =
            new[] { father, mother }
                .Where(parent => parent is not null)
                .Cast<IPerson>()
                .Select(parent =>
                    family is null
                        ? $"{parent.Name} {parent.Surname}"
                        : family.GetDisplayName(parent))
                .ToList();

        var children =
            family?.GetChildren(
                person)
            ?? Array.Empty<IPerson>();

        var childNames =
            children
                .Select(child =>
                    family is null
                        ? $"{child.Name} {child.Surname}"
                        : family.GetDisplayName(child))
                .ToList();

        InfoTooltipText =
            IsLiving
                ? BuildLivingTooltip(
                    ThoughtText,
                    healthTooltip,
                    satisfactionTooltip,
                    marriageText)
                : string.Join(
                    Environment.NewLine,
                    new[]
                    {
                        $"Education: {educationTooltip}",
                        $"Parents: " +
                        (parentNames.Count == 0
                            ? "None"
                            : string.Join(", ", parentNames)),
                        $"Spouse: {spouseTooltip}",
                        $"Children: " +
                        (childNames.Count == 0
                            ? "None"
                            : string.Join(", ", childNames)),
                        $"Last occupation: {lastOccupationTooltip}"
                    });

        SelectCommand =
            new RelayCommand(
                () =>
                    selectPerson(
                        PersonId));
    }

    public Guid PersonId { get; }

    public string FirstName { get; }

    public string Surname { get; }

    public string FullName { get; }

    public string AvatarText { get; }

    public bool IsLiving { get; }

    public string LifeSpanText { get; }

    public string TownText { get; } =
        string.Empty;

    public string OccupationText { get; } =
        string.Empty;

    public bool ShowOccupation { get; }

    public bool ShowHealth { get; }

    public double HealthValue { get; }

    public string HealthText { get; } =
        string.Empty;

    public string ThoughtText { get; }

    public string InfoTooltipText { get; }

    public bool IsSelected { get; }

    public bool IsActiveHouseholdHead { get; }

    public RelayCommand SelectCommand { get; }

    private static string FormatOccupation(
        string title,
        int level)
    {
        return level > 0
            ? $"{title} ({level})"
            : title;
    }

    private static string ResolveLastOccupation(
        CareerSnapshot career)
    {
        if (career.JobLevel > 0)
        {
            return FormatOccupation(
                career.JobTitle,
                career.JobLevel);
        }

        if (!string.IsNullOrWhiteSpace(
                career.PeakJobTitle))
        {
            return career.PeakJobLevel > 0
                ? FormatOccupation(
                    career.PeakJobTitle,
                    career.PeakJobLevel)
                : career.PeakJobTitle;
        }

        if (career.JobTitle.Equals(
                "Housewife",
                StringComparison.OrdinalIgnoreCase))
        {
            return "Housewife";
        }

        return "None";
    }

    private static string BuildLivingTooltip(
        string thought,
        string health,
        string jobSatisfaction,
        string marriageSatisfaction)
    {
        var sections =
            new List<string>();

        var quoted =
            ThoughtUiFormatter.QuoteAndWrap(
                thought);

        if (!string.IsNullOrWhiteSpace(
            quoted))
        {
            sections.Add(
                quoted
                + Environment.NewLine);
        }

        sections.Add(
            $"Health: {health}");

        sections.Add(
            $"Job Satisfaction: {jobSatisfaction}");

        sections.Add(
            $"Marriage Satisfaction: {marriageSatisfaction}");

        return string.Join(
            Environment.NewLine,
            sections);
    }
}
