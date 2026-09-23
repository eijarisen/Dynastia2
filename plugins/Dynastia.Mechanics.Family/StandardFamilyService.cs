using Dynastia.Contracts;

namespace Dynastia.Mechanics.Family;

public sealed class StandardFamilyService : IFamilyService
{
    private const string SurnamesPath =
        "Names/polish_surnames.csv";

    private readonly IGameState _gameState;
    private readonly IPersonLookup? _personLookup;
    private readonly IGameDataService _data;
    private readonly IHistoricalNameService _historicalNames;
    private readonly StandardNationalityService _nationalities;

    private IPerson? _reconciledFounderReference;
    private bool _reconcilingFounderParents;

    public StandardFamilyService(
        IGameState gameState,
        IGameDataService data,
        IHistoricalNameService historicalNames,
        StandardNationalityService nationalities)
        : this(
            gameState,
            gameState as IPersonLookup,
            data,
            historicalNames,
            nationalities)
    {
    }

    public StandardFamilyService(
        IGameState gameState,
        IPersonLookup? personLookup,
        IGameDataService data,
        IHistoricalNameService historicalNames,
        StandardNationalityService nationalities)
    {
        _gameState = gameState;
        _personLookup = personLookup;
        _data = data;
        _historicalNames = historicalNames;
        _nationalities = nationalities;
    }

    public void InitializePerson(
        IPerson person,
        Sex sex,
        int? generation = null)
    {
        _nationalities.InitializeDefault(person);

        if (person.Components.Has<FamilyComponent>())
            return;

        person.Components.Set(
            new FamilyComponent
            {
                Sex = sex,
                Generation = generation
            });

        person.Tags.Add(
            sex == Sex.Male
                ? "sex.male"
                : "sex.female");
    }

    public Sex GetSex(IPerson person)
    {
        return GetRequired(person).Sex;
    }

    public int? GetGeneration(IPerson person)
    {
        return GetRequired(person).Generation;
    }

    public IPerson? GetFather(IPerson person)
    {
        return FindPerson(GetRequired(person).FatherId);
    }

    public IPerson? GetMother(IPerson person)
    {
        return FindPerson(GetRequired(person).MotherId);
    }

    public IPerson? GetSpouse(IPerson person)
    {
        return FindPerson(GetRequired(person).SpouseId);
    }

    public IReadOnlyList<IPerson> GetChildren(IPerson person)
    {
        var component = GetRequired(person);

        return component.ChildrenIds
            .Select(id => FindPerson(id))
            .Where(x => x is not null)
            .Cast<IPerson>()
            .ToList();
    }

    public void SetParents(
        IPerson child,
        IPerson? father,
        IPerson? mother)
    {
        var childFamily = GetRequired(child);

        childFamily.FatherId = father?.Id;
        childFamily.MotherId = mother?.Id;

        if (father is not null)
            AddChild(father, child);

        if (mother is not null)
            AddChild(mother, child);
    }

    public void SetSpouses(
        IPerson first,
        IPerson second,
        int startYear)
    {
        var firstFamily = GetRequired(first);
        var secondFamily = GetRequired(second);

        firstFamily.SpouseId = second.Id;
        secondFamily.SpouseId = first.Id;

        first.Tags.Remove("relationship.single");
        second.Tags.Remove("relationship.single");

        first.Tags.Add("relationship.married");
        second.Tags.Add("relationship.married");

        AddMarriageRecord(
            firstFamily,
            second.Id,
            startYear);

        AddMarriageRecord(
            secondFamily,
            first.Id,
            startYear);
    }

    public void EndRelationship(
        IPerson first,
        IPerson second,
        int endYear,
        string endReason,
        bool clearFirst = true,
        bool clearSecond = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(endReason);

        EndMarriageRecord(
            GetRequired(first),
            second.Id,
            endYear,
            endReason);

        EndMarriageRecord(
            GetRequired(second),
            first.Id,
            endYear,
            endReason);

        first.Tags.Remove("relationship.married");
        second.Tags.Remove("relationship.married");

        if (clearFirst)
        {
            GetRequired(first).SpouseId = null;

            if (!first.Tags.Has("state.dead"))
                first.Tags.Add("relationship.single");
        }

        if (clearSecond)
        {
            GetRequired(second).SpouseId = null;

            if (!second.Tags.Has("state.dead"))
                second.Tags.Add("relationship.single");
        }
    }

    public IReadOnlyList<RelationshipHistoryInfo> GetRelationshipHistory(
        IPerson person)
    {
        return GetRequired(person)
            .MarriageHistory
            .Select(x => new RelationshipHistoryInfo(
                x.SpouseId,
                x.StartYear,
                x.EndYear,
                x.EndReason))
            .ToList();
    }

    public void SetGeneratedFamilyBackground(
        IPerson person,
        GeneratedFamilyBackgroundInfo background)
    {
        ArgumentNullException.ThrowIfNull(background);

        GetRequired(person).GeneratedBackground =
            background;
    }

    public GeneratedFamilyBackgroundInfo? GetGeneratedFamilyBackground(
        IPerson person)
    {
        var component =
            GetRequired(person);

        if (component.GeneratedBackground is not null)
            return component.GeneratedBackground;

        if (component.FatherId is not null
            || component.MotherId is not null
            || component.MarriageHistory.Count == 0
            || (component.Generation == 0
                && person.Tags.Has(
                    "family.bloodline")))
        {
            return null;
        }

        return BuildDeterministicExternalBackground(
            person,
            component);
    }

    public string FormatSurname(
        string surname,
        Sex sex)
    {
        return _historicalNames.FormatSurname(
            surname,
            sex,
            "polish");
    }

    public string FormatSurname(
        IPerson person,
        string surname,
        Sex sex)
    {
        return _nationalities.FormatSurname(
            person,
            surname,
            sex);
    }

    public string GetDisplayName(
        IPerson person)
    {
        return $"{person.Name} " +
            _nationalities.FormatSurname(
                person,
                person.Surname,
                GetSex(person));
    }

    public bool IsBloodline(IPerson person)
    {
        return person.Tags.Has("family.bloodline");
    }

    public bool IsMaleLineage(IPerson person)
    {
        return person.Tags.Has("lineage.male");
    }

    internal void ReconcileLegacyState() =>
        ReconcileFoundingParents();

    private static void AddMarriageRecord(
        FamilyComponent family,
        Guid spouseId,
        int startYear)
    {
        if (family.MarriageHistory.Any(
            x => x.SpouseId == spouseId
                && x.EndYear is null))
        {
            return;
        }

        family.MarriageHistory.Add(
            new MarriageRecord
            {
                SpouseId = spouseId,
                StartYear = startYear
            });
    }

    private static void EndMarriageRecord(
        FamilyComponent family,
        Guid spouseId,
        int endYear,
        string endReason)
    {
        var record =
            family.MarriageHistory
                .LastOrDefault(
                    x => x.SpouseId == spouseId
                        && x.EndYear is null);

        if (record is null)
            return;

        record.EndYear = endYear;
        record.EndReason = endReason;
    }

    private void AddChild(
        IPerson parent,
        IPerson child)
    {
        var family = GetRequired(parent);

        if (!family.ChildrenIds.Contains(child.Id))
            family.ChildrenIds.Add(child.Id);
    }

    private IPerson? FindPerson(Guid? id)
    {
        if (id is null)
            return null;

        return _personLookup?.FindPerson(id)
            ?? _gameState.People.FirstOrDefault(
                person => person.Id == id.Value);
    }

    private static FamilyComponent GetRequired(IPerson person)
    {
        return person.Components.Get<FamilyComponent>()
            ?? throw new InvalidOperationException(
                $"Person '{person.Name} {person.Surname}' " +
                "has no family component. Run state reconciliation before reading family data.");
    }

    private void ReconcileFoundingParents()
    {
        if (_reconcilingFounderParents)
            return;

        _reconcilingFounderParents =
            true;

        try
        {
            var founder =
                _gameState.People
                    .FirstOrDefault(
                        candidate =>
                        {
                            var component =
                                candidate.Components.Get<
                                    FamilyComponent>();

                            return component is not null
                                && component.Sex == Sex.Male
                                && component.Generation == 1
                                && component.FatherId is not null
                                && component.MotherId is not null
                                && candidate.Tags.Has(
                                    "family.bloodline");
                        });

            if (founder is null)
                return;

            if (ReferenceEquals(
                    _reconciledFounderReference,
                    founder))
            {
                return;
            }

            var founderFamily =
                founder.Components.Get<
                    FamilyComponent>();

            if (founderFamily is null)
                return;

            var father =
                FindPerson(
                    founderFamily.FatherId);

            var mother =
                FindPerson(
                    founderFamily.MotherId);

            if (father is null
                || mother is null)
            {
                return;
            }

            father.Tags.Add(
                "family.bloodline");

            father.Tags.Add(
                "lineage.male");

            mother.Tags.Add(
                "family.bloodline");

            var motherFamily =
                mother.Components.Get<
                    FamilyComponent>();

            if (motherFamily is not null
                && motherFamily.Generation is null)
            {
                motherFamily.Generation =
                    0;
            }

            if (string.IsNullOrWhiteSpace(
                    mother.MaidenName)
                || mother.MaidenName.Equals(
                    mother.Surname,
                    StringComparison.OrdinalIgnoreCase)
                || mother.MaidenName.Equals(
                    _gameState.DynastySurname,
                    StringComparison.OrdinalIgnoreCase))
            {
                mother.MaidenName =
                    SelectDeterministicMaidenName(
                        mother);
            }

            ReconcileParentDeathDates(
                founder,
                father,
                mother);

            _reconciledFounderReference =
                founder;
        }
        finally
        {
            _reconcilingFounderParents =
                false;
        }
    }

    private GeneratedFamilyBackgroundInfo
        BuildDeterministicExternalBackground(
            IPerson person,
            FamilyComponent component)
    {
        var familySurname =
            component.Sex == Sex.Female
            && !string.IsNullOrWhiteSpace(
                person.MaidenName)
                ? person.MaidenName!
                : person.Surname;

        var personBirthYear =
            person.BirthDate?.Year
            ?? (_gameState.Year - person.Age);

        var fatherBirthYear =
            personBirthYear
            - (20 + DeterministicByte(person.Id, 7) % 16);

        var motherBirthYear =
            personBirthYear
            - (20 + DeterministicByte(person.Id, 9) % 16);

        var nationalityId =
            _nationalities.GetNationality(person);

        var nameCultureId =
            _nationalities.GetNameCultureId(
                nationalityId);

        var fatherName =
            $"{SelectDeterministicFirstName(Sex.Male, fatherBirthYear, nameCultureId, person.Id, 11)} " +
            familySurname;

        var motherName =
            $"{SelectDeterministicFirstName(Sex.Female, motherBirthYear, nameCultureId, person.Id, 23)} " +
            _historicalNames.FormatSurname(
                familySurname,
                Sex.Female,
                nameCultureId);

        var siblingCount =
            DeterministicByte(
                person.Id,
                31)
            % 5;

        var siblings =
            new List<string>();

        for (var index = 0;
            index < siblingCount;
            index++)
        {
            var male =
                DeterministicByte(
                    person.Id,
                    41 + index)
                % 2 == 0;

            var sex =
                male
                    ? Sex.Male
                    : Sex.Female;

            var siblingBirthYear =
                personBirthYear
                + (DeterministicByte(
                        person.Id,
                        47 + index)
                    % 11)
                - 5;

            var name =
                SelectDeterministicFirstName(
                    sex,
                    siblingBirthYear,
                    nameCultureId,
                    person.Id,
                    53 + index);

            siblings.Add(
                $"{name} " +
                _historicalNames.FormatSurname(
                    familySurname,
                    sex,
                    nameCultureId));
        }

        return new GeneratedFamilyBackgroundInfo(
            fatherName,
            motherName,
            siblings);
    }

    private string SelectDeterministicFirstName(
        Sex sex,
        int birthYear,
        string nameCultureId,
        Guid id,
        int salt)
    {
        var sample =
            (DeterministicByte(id, salt)
                % 1_000_000)
            / 1_000_000d;

        return _historicalNames.GetRandomFirstName(
            sex,
            birthYear,
            nameCultureId,
            new FixedSampleGameRandom(sample));
    }

    private static int DeterministicByte(
        Guid id,
        int salt)
    {
        var bytes =
            id.ToByteArray();

        var first =
            bytes[salt % bytes.Length];

        var second =
            bytes[(salt * 7 + 3)
                % bytes.Length];

        return (first * 31
                + second
                + salt * 17)
            & 0x7FFFFFFF;
    }

    private string SelectDeterministicMaidenName(
        IPerson mother)
    {
        var entries =
            _data.GetWeightedStringList(
                SurnamesPath);

        if (entries.Count == 0)
            return mother.Surname;

        var bytes =
            mother.Id.ToByteArray();

        var seed =
            (uint)bytes[0]
            | ((uint)bytes[1] << 8)
            | ((uint)bytes[2] << 16)
            | ((uint)bytes[3] << 24);

        var start =
            (int)(seed
                % (uint)entries.Count);

        for (var offset = 0;
            offset < entries.Count;
            offset++)
        {
            var candidate =
                entries[
                    (start + offset)
                    % entries.Count]
                .Value;

            if (!candidate.Equals(
                    mother.Surname,
                    StringComparison.OrdinalIgnoreCase)
                && !candidate.Equals(
                    _gameState.DynastySurname,
                    StringComparison.OrdinalIgnoreCase))
            {
                return candidate;
            }
        }

        return mother.Surname;
    }

    private static void ReconcileParentDeathDates(
        IPerson founder,
        IPerson father,
        IPerson mother)
    {
        var fatherDate =
            father.DeathDate;

        var motherDate =
            mother.DeathDate;

        int month;
        int day;

        if (fatherDate is GameDate completeFather
            && completeFather.Month is int fatherMonth
            && completeFather.Day is int fatherDay)
        {
            month = fatherMonth;
            day = fatherDay;
        }
        else if (motherDate is GameDate completeMother
            && completeMother.Month is int motherMonth
            && completeMother.Day is int motherDay)
        {
            month = motherMonth;
            day = motherDay;
        }
        else
        {
            var bytes =
                founder.Id.ToByteArray();

            month =
                bytes[4] % 12
                + 1;

            day =
                bytes[5] % 28
                + 1;
        }

        if (fatherDate is GameDate fatherValue
            && (!fatherValue.Month.HasValue
                || !fatherValue.Day.HasValue))
        {
            father.DeathDate =
                new GameDate(
                    fatherValue.Year,
                    month,
                    day);
        }

        if (motherDate is GameDate motherValue
            && (!motherValue.Month.HasValue
                || !motherValue.Day.HasValue))
        {
            mother.DeathDate =
                new GameDate(
                    motherValue.Year,
                    month,
                    day);
        }
    }
}
