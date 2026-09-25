using Dynastia.Contracts;

namespace Dynastia.Mechanics.Thoughts;

internal static class ThoughtProviderUtilities
{
    public static bool IsEventInvolving(
        GameEvent gameEvent,
        IPerson person)
    {
        return gameEvent.SubjectId
                == person.Id
            || gameEvent.RelatedPersonIds.Contains(
                person.Id);
    }

    public static IReadOnlyDictionary<string, string>
        Context(
            params (string Key, string Value)[] values)
    {
        return values
            .Where(
                pair =>
                    !string.IsNullOrWhiteSpace(
                        pair.Value))
            .ToDictionary(
                pair =>
                    pair.Key,
                pair =>
                    pair.Value);
    }

    public static int ApplyAgePriority(
        IPerson person,
        ThoughtCandidate candidate)
    {
        var salience =
            candidate.Salience;

        var traits =
            candidate.SalienceTraits;

        if (person.Age is >= 5 and <= 11)
        {
            if ((traits & (
                    ThoughtSalienceTraits.FamilyLoss
                    | ThoughtSalienceTraits.Orphanhood
                    | ThoughtSalienceTraits.Placement
                    | ThoughtSalienceTraits.ParentsDivorced)) != 0)
            {
                salience += 10;
            }

            if (traits.HasFlag(
                ThoughtSalienceTraits.Health))
            {
                salience += 5;
            }

            if ((traits & (
                    ThoughtSalienceTraits.FamilyBirth
                    | ThoughtSalienceTraits.Education)) != 0)
            {
                salience += 5;
            }

            if (traits.HasFlag(
                ThoughtSalienceTraits.Poverty))
            {
                salience -= 20;
            }

            if (traits.HasFlag(
                ThoughtSalienceTraits.HouseholdStrain))
            {
                salience -= 10;
            }
        }
        else if (person.Age is >= 12 and <= 17)
        {
            if ((traits & (
                    ThoughtSalienceTraits.FamilyLoss
                    | ThoughtSalienceTraits.Orphanhood
                    | ThoughtSalienceTraits.Placement
                    | ThoughtSalienceTraits.ParentsDivorced
                    | ThoughtSalienceTraits.Health
                    | ThoughtSalienceTraits.Education)) != 0)
            {
                salience += 5;
            }

            if (traits.HasFlag(
                ThoughtSalienceTraits.RareTrauma))
            {
                salience += 5;
            }

            if (traits.HasFlag(
                ThoughtSalienceTraits.Poverty))
            {
                salience -= 10;
            }

            if (traits.HasFlag(
                ThoughtSalienceTraits.HouseholdStrain))
            {
                salience -= 5;
            }
        }

        return Math.Clamp(
            salience,
            1,
            100);
    }

    public static bool IsCloseRelative(
        IPerson person,
        IPerson other,
        IFamilyService family)
    {
        if (person.Id
            == other.Id)
        {
            return false;
        }

        if (family.GetFather(
                person)?.Id
                == other.Id
            || family.GetMother(
                person)?.Id
                == other.Id
            || family.GetFather(
                other)?.Id
                == person.Id
            || family.GetMother(
                other)?.Id
                == person.Id)
        {
            return true;
        }

        if (family.GetSpouse(
                person)?.Id
                == other.Id
            || family.GetRelationshipHistory(
                    person)
                .Any(
                    history =>
                        history.SpouseId
                            == other.Id
                        && (
                            history.EndYear is null
                            || string.Equals(
                                history.EndReason,
                                "death",
                                StringComparison.OrdinalIgnoreCase)
                        )))
        {
            return true;
        }

        var personFather =
            family.GetFather(
                person)?.Id;

        var personMother =
            family.GetMother(
                person)?.Id;

        var otherFather =
            family.GetFather(
                other)?.Id;

        var otherMother =
            family.GetMother(
                other)?.Id;

        return (
                personFather is not null
                && personFather
                    == otherFather
            )
            || (
                personMother is not null
                && personMother
                    == otherMother
            );
    }

    public static (
        string Relation,
        string RelationPossessive,
        int Salience)?
        LossRelation(
            IPerson survivor,
            IPerson deceased,
            IFamilyService family)
    {
        if (family.GetMother(
                survivor)?.Id
                == deceased.Id)
        {
            return survivor.Age < 18
                ? ("Mum", "her", 100)
                : ("Mum", "her", 94);
        }

        if (family.GetFather(
                survivor)?.Id
                == deceased.Id)
        {
            return survivor.Age < 18
                ? ("Dad", "him", 100)
                : ("Dad", "him", 94);
        }

        if (family.GetFather(
                deceased)?.Id
                == survivor.Id
            || family.GetMother(
                deceased)?.Id
                == survivor.Id)
        {
            return (
                family.GetSex(
                    deceased)
                    == Sex.Male
                        ? "my son"
                        : "my daughter",
                family.GetSex(
                    deceased)
                    == Sex.Male
                        ? "him"
                        : "her",
                96);
        }

        if (family.GetRelationshipHistory(
                survivor)
            .Any(
                history =>
                    history.SpouseId
                        == deceased.Id
                    && (
                        history.EndYear is null
                        || string.Equals(
                            history.EndReason,
                            "death",
                            StringComparison.OrdinalIgnoreCase)
                    )))
        {
            return (
                family.GetSex(
                    deceased)
                    == Sex.Male
                        ? "my husband"
                        : "my wife",
                family.GetSex(
                    deceased)
                    == Sex.Male
                        ? "him"
                        : "her",
                100);
        }

        var survivorFather =
            family.GetFather(
                survivor)?.Id;

        var survivorMother =
            family.GetMother(
                survivor)?.Id;

        var deceasedFather =
            family.GetFather(
                deceased)?.Id;

        var deceasedMother =
            family.GetMother(
                deceased)?.Id;

        if (
            (
                survivorFather is not null
                && survivorFather
                    == deceasedFather
            )
            || (
                survivorMother is not null
                && survivorMother
                    == deceasedMother
            )
        )
        {
            return (
                family.GetSex(
                    deceased)
                    == Sex.Male
                        ? "my brother"
                        : "my sister",
                family.GetSex(
                    deceased)
                    == Sex.Male
                        ? "him"
                        : "her",
                90);
        }

        return null;
    }

    public static string FallbackEmoji(
        IPerson person,
        IFamilyService family)
    {
        var sex =
            family.GetSex(
                person);

        if (person.Age <= 11)
        {
            return sex == Sex.Male
                ? "👦"
                : "👧";
        }

        if (person.Age <= 17)
            return "🧑";

        var retirementAge =
            sex == Sex.Male
                ? 65
                : 60;

        if (person.Age >= retirementAge)
        {
            return sex == Sex.Male
                ? "👴"
                : "👵";
        }

        return sex == Sex.Male
            ? "👨"
            : "👩";
    }
}
