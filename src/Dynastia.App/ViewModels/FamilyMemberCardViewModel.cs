using Dynastia.Contracts;

namespace Dynastia.App.ViewModels;

public sealed class FamilyMemberCardViewModel
{
    public FamilyMemberCardViewModel(
        IPerson person,
        IFamilyService? family,
        IHealthService? health,
        ICareerService? career,
        IJusticeService? justice,
        IStatsService? stats,
        IAdoptionService? adoption,
        bool isSelected,
        bool isActiveHouseholdHead,
        Action<Guid> selectPerson)
    {
        PersonId = person.Id;

        FullName =
            family is null
                ? $"{person.Name} {person.Surname}"
                : family.GetDisplayName(person);

        var generation =
            family?.GetGeneration(person);

        SubtitleText =
            person.Tags.Has("state.dead")
                ? person.DeathDate is GameDate deathDate
                    ? $"Died {deathDate.Year} · Age {person.Age}"
                    : $"Deceased · Age {person.Age}"
                : generation is int number
                    ? $"G{number} Age: {person.Age}"
                    : $"Age: {person.Age}";

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

        if (health is not null
            && !person.Tags.Has("state.dead"))
        {
            var snapshot =
                health.GetHealth(person);

            ShowHealth = true;
            HealthValue = snapshot.Percentage;
            HealthText =
                $"{Math.Round(snapshot.Current)}/" +
                $"{Math.Round(snapshot.Maximum)}";
        }

        if (career is not null)
        {
            OccupationText =
                career.GetCareer(person).JobTitle;
        }

        if (justice is not null)
        {
            var justiceStatus =
                justice.GetStatus(person);

            if (justiceStatus.IsImprisoned)
            {
                OccupationText =
                    justiceStatus.IsLifeSentence
                        ? "Imprisoned · Life"
                        : justiceStatus.RemainingYears == 1
                            ? "Imprisoned · 1 year left"
                            : $"Imprisoned · {justiceStatus.RemainingYears} years left";
            }
        }

        if (adoption is not null
            && (
                person.Tags.Has(
                    "trait.orphan")
                || person.Tags.Has(
                    "residence.with_mother")
                || person.Tags.Has(
                    "residence.adopted")
                || person.Tags.Has(
                    "residence.orphanage")))
        {
            ResidenceText =
                adoption
                    .GetPlacement(person)
                    .Description;
        }

        SelectCommand =
            new RelayCommand(
                () => selectPerson(PersonId));
    }

    public Guid PersonId { get; }

    public string FullName { get; }

    public string SubtitleText { get; }

    public string AvatarText { get; }

    public string OccupationText { get; } =
        string.Empty;

    public string ResidenceText { get; } =
        string.Empty;

    public bool ShowHealth { get; }

    public double HealthValue { get; }

    public string HealthText { get; } =
        string.Empty;

    public bool IsSelected { get; }

    public bool IsActiveHouseholdHead { get; }

    public RelayCommand SelectCommand { get; }
}
