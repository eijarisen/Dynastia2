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
        IChildHappinessService? childHappiness,
        IThoughtService? thoughts,
        IAppearanceService? appearance,
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
            IsLiving
                ? PersonEmojiResolver.GetPersonEmoji(
                    person,
                    family,
                    health,
                    career,
                    justice,
                    stats,
                    thoughts,
                    appearance)
                : appearance is not null
                    ? appearance.GetPortrait(
                        person,
                        useDeadOverride: false)
                    : ResolveLastPortraitFallback(
                        person,
                        family);

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

        var educationLevel =
            education?.GetEducationLevel(person)
            ?? 0;

        var educationTooltip =
            education is null
                ? "Unknown"
                : $"Level {educationLevel}";

        ShowChildEducation =
            IsLiving
            && person.Age < 18
            && education is not null;

        ChildEducationLevel =
            educationLevel;

        ChildEducationTooltipText =
            educationLevel <= 0
                ? "Education: None"
                : $"Education: Level {educationLevel}";

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
                careerSnapshot.IsEmployed
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

        var generatedBackground =
            family?.GetGeneratedFamilyBackground(
                person);

        var fatherName =
            father is not null
                ? family is null
                    ? $"{father.Name} {father.Surname}"
                    : family.GetDisplayName(father)
                : generatedBackground?.FatherName
                  ?? "Unknown";

        var motherName =
            mother is not null
                ? family is null
                    ? $"{mother.Name} {mother.Surname}"
                    : family.GetDisplayName(mother)
                : generatedBackground?.MotherName
                  ?? "Unknown";

        var parentsTooltip =
            fatherName == "Unknown"
            && motherName == "Unknown"
                ? "Unknown"
                : $"{fatherName}, {motherName}";

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

        TooltipThoughtText =
            ThoughtUiFormatter.QuoteAndWrap(
                ThoughtText);

        HasTooltipThought =
            !string.IsNullOrWhiteSpace(
                TooltipThoughtText);

        HealthTooltipText =
            $"Health: {healthTooltip}";

        var childHappinessSnapshot =
            IsLiving && person.Age < 18
                ? childHappiness?.GetHappiness(person)
                : null;

        ChildHappinessLabel =
            childHappinessSnapshot?.Label ?? string.Empty;

        ChildHappinessTooltipText =
            childHappinessSnapshot is null
                ? string.Empty
                : $"Happiness: {childHappinessSnapshot.Label}";

        ShowChildHappiness =
            childHappinessSnapshot is not null;

        CareerSatisfactionLabel =
            satisfactionTooltip;

        CareerTooltipText =
            $"Career: {CareerSatisfactionLabel}";

        MarriageSatisfactionLabel =
            marriageText;

        MarriageTooltipText =
            $"Marriage: {MarriageSatisfactionLabel}";

        ShowAdultSatisfaction =
            IsLiving
            && person.Age >= 18;

        InfoTooltipText =
            IsLiving
                ? string.Empty
                : string.Join(
                    Environment.NewLine,
                    new[]
                    {
                        $"Education: {educationTooltip}",
                        $"Parents: {parentsTooltip}",
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

    public string TooltipThoughtText { get; } =
        string.Empty;

    public bool HasTooltipThought { get; }

    public string HealthTooltipText { get; } =
        string.Empty;

    public int ChildEducationLevel { get; }

    public string ChildEducationTooltipText { get; } =
        string.Empty;

    public bool ShowChildEducation { get; }

    public string ChildHappinessLabel { get; } = string.Empty;
    public string ChildHappinessTooltipText { get; } = string.Empty;
    public bool ShowChildHappiness { get; }

    public string CareerSatisfactionLabel { get; } =
        string.Empty;

    public string CareerTooltipText { get; } =
        string.Empty;

    public string MarriageSatisfactionLabel { get; } =
        string.Empty;

    public string MarriageTooltipText { get; } =
        string.Empty;

    public bool ShowAdultSatisfaction { get; }

    public bool IsDeceased =>
        !IsLiving;

    public string InfoTooltipText { get; }

    public bool IsSelected { get; }

    public bool IsActiveHouseholdHead { get; }

    public RelayCommand SelectCommand { get; }

    private static string ResolveLastPortraitFallback(
        IPerson person,
        IFamilyService? family)
    {
        var sex = family?.GetSex(person)
            ?? (person.Tags.Has("sex.female")
                ? Sex.Female
                : Sex.Male);

        if (person.Age <= 4)
            return "👶🏻";

        if (person.Age <= 11)
            return sex == Sex.Male ? "👦🏻" : "👧🏻";

        if (person.Age <= 17)
            return "🧑🏻";

        if (person.Age >= 70)
            return sex == Sex.Male ? "👴🏻" : "👵🏻";

        return sex == Sex.Male ? "👨🏻" : "👩🏻";
    }

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
        if (career.IsEmployed)
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

        if (career.StatusId?.Equals(
                "status.housewife",
                StringComparison.OrdinalIgnoreCase) == true)
        {
            return career.JobTitle;
        }

        return "None";
    }


}
