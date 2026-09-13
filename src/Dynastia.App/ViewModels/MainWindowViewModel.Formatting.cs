using System.Collections.ObjectModel;
using Avalonia.Threading;
using Dynastia.App.Persistence;
using Dynastia.Contracts;
using Dynastia.Core.Simulation;

namespace Dynastia.App.ViewModels;

public sealed partial class MainWindowViewModel
{
    private string PersonName(
        IPerson? person)
    {
        if (person is null)
            return "None";

        return _familyService is null
            ? $"{person.Name} " +
              $"{person.Surname}"
            : _familyService
                .GetDisplayName(
                    person);
    }

    private string FormatPeople(
        IReadOnlyList<IPerson> people)
    {
        return people.Count == 0
            ? "None"
            : string.Join(
                ", ",
                people.Select(
                    PersonName));
    }

    private string PersonNameWithLifeYears(
        IPerson? person)
    {
        if (person is null)
            return "None";

        var name =
            PersonName(
                person);

        var birthYear =
            person.BirthDate?.Year;

        var deathYear =
            person.DeathDate?.Year;

        if (birthYear is int born
            && deathYear is int died)
        {
            return $"{name} ({born}–{died})";
        }

        if (birthYear is int livingBorn)
        {
            return person.Tags.Has(
                "state.dead")
                    ? $"{name} ({livingBorn}–?)"
                    : $"{name} ({livingBorn}–)";
        }

        if (deathYear is int knownDeath)
        {
            return $"{name} (?–{knownDeath})";
        }

        return name;
    }

    private string FormatPeopleWithLifeYears(
        IReadOnlyList<IPerson> people)
    {
        return people.Count == 0
            ? "None"
            : string.Join(
                ", ",
                people.Select(
                    PersonNameWithLifeYears));
    }

    private static string FormatNames(
        IReadOnlyList<string> names)
    {
        return names.Count == 0
            ? "None"
            : string.Join(
                ", ",
                names);
    }

    private string FormatRelationshipHistory(
        IReadOnlyList<
            RelationshipHistoryInfo>
            history)
    {
        if (history.Count == 0)
            return "None";

        var lines =
            new List<string>();

        foreach (var relationship in
            history.OrderBy(
                x => x.StartYear))
        {
            var spouse =
                _gameState.People
                    .FirstOrDefault(
                        x =>
                            x.Id
                            == relationship.SpouseId);

            var spouseName =
                PersonName(
                    spouse);

            var end =
                relationship.EndYear
                    is int endYear
                        ? endYear.ToString()
                        : "present";

            var reason =
                string.IsNullOrWhiteSpace(
                    relationship.EndReason)
                    ? string.Empty
                    : $" ({relationship.EndReason})";

            lines.Add(
                $"{relationship.StartYear}–{end}: " +
                $"{spouseName}{reason}");
        }

        return string.Join(
            Environment.NewLine,
            lines);
    }

    private enum HouseholdViewMode
    {
        Lineage = 0,
        Bloodline = 1,
        Deceased = 2
    }

}
