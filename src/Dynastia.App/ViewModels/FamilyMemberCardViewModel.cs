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
                stats);

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

        var occupationTooltip =
            "Unknown";

        var satisfactionTooltip =
            "Unknown";

        if (career is not null)
        {
            var careerSnapshot =
                career.GetCareer(
                    person);

            OccupationText =
                careerSnapshot.JobTitle;

            occupationTooltip =
                careerSnapshot.IsRetired
                    ? careerSnapshot.AnnualIncome > 0
                        ? $"{careerSnapshot.JobTitle} — " +
                          $"{careerSnapshot.AnnualIncome:N0} zł/year pension"
                        : careerSnapshot.JobTitle
                    : careerSnapshot.JobLevel > 0
                        ? $"{careerSnapshot.JobTitle} — " +
                          $"{careerSnapshot.AnnualIncome:N0} zł/year"
                        : careerSnapshot.JobTitle;

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

                occupationTooltip =
                    OccupationText;
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
            marriageTooltip is null
                ? "N/A"
                : $"{marriageTooltip.Label} " +
                  $"({marriageTooltip.Value:0}%)";

        InfoTooltipText =
            IsLiving
                ? string.Join(
                    Environment.NewLine,
                    new[]
                    {
                        $"Health: {healthTooltip}",
                        $"Education: {educationTooltip}",
                        $"Occupation: {occupationTooltip}",
                        $"Job Satisfaction: {satisfactionTooltip}",
                        $"Spouse: {spouseTooltip}",
                        $"Marriage Satisfaction: {marriageText}"
                    })
                : string.Join(
                    Environment.NewLine,
                    new[]
                    {
                        $"Education: {educationTooltip}",
                        $"Spouse: {spouseTooltip}"
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

    public string InfoTooltipText { get; }

    public bool IsSelected { get; }

    public bool IsActiveHouseholdHead { get; }

    public RelayCommand SelectCommand { get; }
}
