using System.Collections.ObjectModel;
using Avalonia.Threading;
using Dynastia.App.Persistence;
using Dynastia.Contracts;
using Dynastia.Core.Simulation;

namespace Dynastia.App.ViewModels;

public sealed partial class MainWindowViewModel
{
    private void RefreshSelectedStats()
    {
        SelectedStats.Clear();

        if (_statsService is null)
            return;

        var person =
            FindSelectedPerson();

        if (person is null)
            return;

        foreach (var stat in
            _statsService.GetStats(
                person))
        {
            SelectedStats.Add(
                stat);
        }
    }

    private void RefreshHealth()
    {
        var person =
            FindSelectedPerson();

        if (person is null
            || _healthService is null)
        {
            SelectedHealth = null;
            return;
        }

        SelectedHealth =
            new HealthViewModel(
                _healthService.GetHealth(
                    person));
    }

    private void RefreshEconomy()
    {
        var person =
            FindSelectedPerson();

        if (person is null
            || _economyService is null)
        {
            SelectedEconomy = null;
            return;
        }

        var snapshot =
            _economyService.GetHousehold(
                person);

        var status =
            snapshot is null
                ? null
                : _householdService?.GetStatus(
                    person);

        SelectedEconomy =
            snapshot is null
                ? null
                : new EconomyViewModel(
                    snapshot,
                    status,
                    _economyService.GetProjectedAnnualIncome(person));
    }

    private void RefreshEducation()
    {
        var person =
            FindSelectedPerson();

        if (person is null
            || _educationService is null)
        {
            SelectedEducation = null;
            return;
        }

        SelectedEducation =
            new EducationViewModel(
                person.Age,
                _educationService
                    .GetEducationLevel(person));
    }

    private void RefreshHobbies()
    {
        OnPropertyChanged(
            nameof(SelectedHobbiesText));
    }

    private void RefreshCareer()
    {
        var person =
            FindSelectedPerson();

        if (person is null
            || _careerService is null)
        {
            SelectedCareer = null;
            return;
        }

        SelectedCareer =
            new CareerViewModel(
                _careerService.GetCareer(person),
                person.Tags.Has("state.alive"));
    }

    private void RefreshMarriageSatisfaction()
    {
        OnPropertyChanged(
            nameof(
                SelectedMarriageSatisfactionText));

        OnPropertyChanged(
            nameof(
                SelectedMarriageSatisfactionLabel));

        OnPropertyChanged(
            nameof(
                SelectedMarriageSatisfactionDetailsText));

        OnPropertyChanged(
            nameof(
                HasSelectedMarriageSatisfaction));
    }

    private void RefreshChildHappiness()
    {
        OnPropertyChanged(nameof(SelectedChildHappinessText));
        OnPropertyChanged(nameof(SelectedChildHappinessLabel));
        OnPropertyChanged(nameof(HasSelectedChildHappiness));
    }

    private void RefreshJustice()
    {
        var person =
            FindSelectedPerson();

        if (person is null
            || _justiceService is null)
        {
            SelectedJustice = null;
            return;
        }

        SelectedJustice =
            new JusticeViewModel(
                _justiceService.GetStatus(
                    person));
    }

    private void RefreshNarrative()
    {
        SelectedBiography.Clear();

        var person =
            FindSelectedPerson();

        if (person is null
            || _biographyService is null)
        {
            SelectedAboutText =
                "No information available.";

            OnPropertyChanged(
                nameof(BiographyEmptyText));

            return;
        }

        SelectedAboutText =
            _biographyService.GetAbout(
                person);

        foreach (var entry in
            _biographyService.GetBiography(
                person))
        {
            SelectedBiography.Add(
                new BiographyEntryViewModel(
                    entry));
        }

        OnPropertyChanged(
            nameof(BiographyEmptyText));
    }

    private void RefreshFamilyDetails()
    {
        var person =
            FindSelectedPerson();

        if (person is null
            || _familyService is null)
        {
            SelectedFamily = null;
            return;
        }

        var generatedBackground =
            _familyService
                .GetGeneratedFamilyBackground(
                    person);

        var father =
            _familyService.GetFather(
                person);

        var mother =
            _familyService.GetMother(
                person);

        var spouse =
            _familyService.GetSpouse(
                person);

        var children =
            _familyService.GetChildren(
                person);

        var siblings =
            GetSimulatedSiblings(
                person,
                father);

        var relationshipHistory =
            _familyService
                .GetRelationshipHistory(
                    person);

        var relationshipPeople =
            BuildRelationshipPeople(
                person,
                father,
                mother,
                siblings,
                spouse,
                children,
                generatedBackground);

        var relationshipHistoryItems =
            BuildRelationshipHistoryItems(
                relationshipHistory);

        SelectedFamily =
            new FamilyDetailsViewModel
            {
                Sex =
                    _familyService
                        .GetSex(person)
                        .ToString(),

                Generation =
                    _familyService
                        .GetGeneration(person)
                        is int generation
                            ? $"G{generation}"
                            : "N/A",

                Father =
                    father is not null
                        ? PersonNameWithLifeYears(
                            father)
                        : generatedBackground?.FatherName
                          ?? "Unknown",

                Mother =
                    mother is not null
                        ? PersonNameWithLifeYears(
                            mother)
                        : generatedBackground?.MotherName
                          ?? "Unknown",

                Siblings =
                    siblings.Count > 0
                        ? FormatPeopleWithLifeYears(
                            siblings)
                        : generatedBackground is not null
                            ? FormatNames(
                                generatedBackground.Siblings)
                            : father is null
                              && mother is null
                                ? "Unknown"
                                : "None",

                Spouse =
                    PersonNameWithLifeYears(
                        spouse),

                Children =
                    FormatPeopleWithLifeYears(
                        children),

                RelationshipHistory =
                    FormatRelationshipHistory(
                        relationshipHistory),

                RelationshipPeople =
                    relationshipPeople,

                RelationshipHistoryItems =
                    relationshipHistoryItems,

                ShowAdultRelationships =
                    person.Age >= 18,

                Bloodline =
                    _familyService
                        .IsBloodline(person)
                            ? "Yes"
                            : "No",

                MaleLineage =
                    _familyService
                        .IsMaleLineage(person)
                            ? "Yes"
                            : "No"
            };
    }


    private IReadOnlyList<RelationshipPersonLineViewModel>
        BuildRelationshipPeople(
            IPerson person,
            IPerson? father,
            IPerson? mother,
            IReadOnlyList<IPerson> siblings,
            IPerson? spouse,
            IReadOnlyList<IPerson> children,
            GeneratedFamilyBackgroundInfo? generatedBackground)
    {
        var result = new List<RelationshipPersonLineViewModel>();

        result.Add(new(
            "Father",
            father?.Id,
            father is not null
                ? PersonNameWithLifeYears(father)
                : generatedBackground?.FatherName ?? "Unknown"));

        result.Add(new(
            "Mother",
            mother?.Id,
            mother is not null
                ? PersonNameWithLifeYears(mother)
                : generatedBackground?.MotherName ?? "Unknown"));

        if (siblings.Count > 0)
        {
            foreach (var sibling in siblings)
            {
                result.Add(new(
                    _familyService?.GetSex(sibling) == Sex.Male
                        ? "Brother"
                        : "Sister",
                    sibling.Id,
                    PersonNameWithLifeYears(sibling)));
            }
        }
        else if (generatedBackground?.Siblings.Count > 0)
        {
            foreach (var siblingName in generatedBackground.Siblings)
                result.Add(new("Sibling", null, siblingName));
        }
        else
        {
            result.Add(new("Siblings", null, "None"));
        }

        if (person.Age >= 18)
        {
            result.Add(new(
                "Current spouse",
                spouse?.Id,
                spouse is null
                    ? "None"
                    : PersonNameWithLifeYears(spouse)));

            if (children.Count == 0)
            {
                result.Add(new("Children", null, "None"));
            }
            else
            {
                foreach (var child in children)
                {
                    result.Add(new(
                        _familyService?.GetSex(child) == Sex.Male
                            ? "Son"
                            : "Daughter",
                        child.Id,
                        PersonNameWithLifeYears(child)));
                }
            }
        }

        return result;
    }

    private IReadOnlyList<RelationshipHistoryLineViewModel>
        BuildRelationshipHistoryItems(
            IReadOnlyList<RelationshipHistoryInfo> history)
    {
        return history
            .OrderBy(item => item.StartYear)
            .Select(item =>
            {
                var spouse = _gameState.People
                    .FirstOrDefault(person => person.Id == item.SpouseId);
                var end = item.EndYear is int endYear
                    ? endYear.ToString()
                    : "present";
                var reason = string.IsNullOrWhiteSpace(item.EndReason)
                    ? string.Empty
                    : $" ({item.EndReason})";

                return new RelationshipHistoryLineViewModel(
                    $"{item.StartYear}–{end}: ",
                    spouse?.Id,
                    spouse is null ? "Unknown" : PersonName(spouse),
                    reason);
            })
            .ToList();
    }

    private IReadOnlyList<IPerson>
        GetSimulatedSiblings(
            IPerson person,
            IPerson? father)
    {
        if (father is null
            || _familyService is null)
        {
            return [];
        }

        return _familyService
            .GetChildren(father)
            .Where(
                sibling =>
                    sibling.Id
                    != person.Id)
            .ToList();
    }

}
