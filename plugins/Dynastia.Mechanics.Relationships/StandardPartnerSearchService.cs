using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Dynastia.Contracts;

namespace Dynastia.Mechanics.Relationships;

internal sealed class StandardPartnerSearchService :
    IPartnerSearchService
{
    private const string SurnamesPath =
        "Names/polish_surnames.csv";

    private static readonly string[] MainStatIds =
    [
        "immunity",
        "longevity",
        "fertility",
        "appeal",
        "strength",
        "intellect"
    ];

    private readonly IGameState _gameState;
    private readonly IFamilyService _family;
    private readonly IStatsService _stats;
    private readonly IEducationService _education;
    private readonly ICareerService _career;
    private readonly IEconomyService _economy;
    private readonly IPersonalityService _personality;
    private readonly IAppearanceService _appearance;
    private readonly Func<IHobbyService?> _hobbyResolver;
    private readonly ILocationService _locations;
    private readonly IGameDataService _data;
    private readonly IHistoricalNameService _historicalNames;
    private readonly IGameCalendar _calendar;
    private readonly IGameRandom _random;
    private readonly IGameEventBus _events;
    private readonly RelationshipEventVariantCatalog _eventVariants;

    public StandardPartnerSearchService(
        IGameState gameState,
        IFamilyService family,
        IStatsService stats,
        IEducationService education,
        ICareerService career,
        IEconomyService economy,
        IPersonalityService personality,
        IAppearanceService appearance,
        Func<IHobbyService?> hobbyResolver,
        ILocationService locations,
        IGameDataService data,
        IHistoricalNameService historicalNames,
        IGameCalendar calendar,
        IGameRandom random,
        IGameEventBus events,
        RelationshipEventVariantCatalog eventVariants)
    {
        _gameState = gameState;
        _family = family;
        _stats = stats;
        _education = education;
        _career = career;
        _economy = economy;
        _personality = personality;
        _appearance = appearance;
        _hobbyResolver = hobbyResolver;
        _locations = locations;
        _data = data;
        _historicalNames = historicalNames;
        _calendar = calendar;
        _random = random;
        _events = events;
        _eventVariants = eventVariants;
    }

    public IReadOnlyList<PartnerCandidateInfo> GetCandidates(
        IPerson seeker,
        int count = 3)
    {
        ArgumentNullException.ThrowIfNull(seeker);

        var partnerSex = seeker.Tags.Has("sexuality.homosexual")
            ? Sex.Male
            : Sex.Female;

        return GetCandidatesFor(
            seeker,
            partnerSex,
            "partner",
            count);
    }

    public IReadOnlyList<PartnerCandidateInfo> GetCandidatesFor(
        IPerson seeker,
        Sex partnerSex,
        string poolKey,
        int count = 3)
    {
        ArgumentNullException.ThrowIfNull(seeker);
        ArgumentException.ThrowIfNullOrWhiteSpace(poolKey);

        if (count <= 0
            || seeker.Age < 18
            || !seeker.Tags.Has("state.alive")
            || _family.GetSpouse(seeker) is not null)
        {
            return [];
        }

        if (!RelationshipPersonalityRules.CanFindPartner(
                seeker,
                partnerSex))
        {
            return [];
        }

        var result = new List<PartnerCandidateInfo>();
        var seekerValue = GetPartnerValue(seeker);
        var town = _locations.GetLocation(seeker).HomeTown;
        var educationRange = _education.GetGeneratedAdultRange(
            _gameState.Year);

        for (var slot = 0; slot < count; slot++)
        {
            var candidateId = CreateCandidateId(
                seeker,
                _gameState.Year,
                poolKey,
                slot);
            var key =
                $"{_gameState.DynastySurname}|{seeker.Id:N}|" +
                $"{_gameState.Year}|{poolKey}|{slot}";
            var candidateRandom =
                new DeterministicRelationshipRandom(key);

            if (!RelationshipPersonalityRules.TryChoosePartnerAge(
                    seeker,
                    partnerSex,
                    candidateRandom,
                    out var age))
            {
                continue;
            }

            var birthYear = _gameState.Year - age;
            var birthMonth = candidateRandom.NextInt(1, 12);
            var birthDay = candidateRandom.NextInt(
                1,
                _calendar.GetDaysInMonth(
                    birthYear,
                    birthMonth));
            var birthDate = new GameDate(
                birthYear,
                birthMonth,
                birthDay);

            var name = _historicalNames.GetRandomFirstName(
                partnerSex,
                birthYear,
                candidateRandom);
            var surname = RandomWeightedSurname(candidateRandom);
            var personality = _personality
                .GenerateCandidatePersonality(candidateId);

            var education = candidateRandom.NextInt(
                educationRange.MinimumLevel,
                educationRange.MaximumLevel);

            var stats = MainStatIds.ToDictionary(
                id => id,
                _ => candidateRandom.NextInt(1, 5),
                StringComparer.OrdinalIgnoreCase);

            var exceptional = GetStat(seeker, "appeal") == 5;
            if (exceptional)
            {
                var candidates = MainStatIds.ToList();
                for (var index = 0;
                    index < 2 && candidates.Count > 0;
                    index++)
                {
                    var selected = candidateRandom.NextInt(
                        0,
                        candidates.Count - 1);
                    var statId = candidates[selected];
                    candidates.RemoveAt(selected);
                    stats[statId] = Math.Min(5, stats[statId] + 1);
                }
            }

            var jobLevel = candidateRandom.NextInt(0, 3);
            if (exceptional
                && jobLevel > 0
                && candidateRandom.NextDouble() < 0.35)
            {
                jobLevel = Math.Min(4, jobLevel + 1);
            }

            var career = _career.GenerateCandidateCareer(
                partnerSex,
                town,
                _gameState.Year,
                jobLevel,
                candidateId.ToString("N"));

            var hobbies = _hobbyResolver()?
                .GenerateCandidateHobbies(
                    candidateId,
                    partnerSex,
                    age,
                    _gameState.Year,
                    personality.Temperament,
                    town.SettlementClass)
                ?? [];

            var (estimatedWealth, estimatedHouses) = partnerSex == Sex.Male
                ? EstimateMaleResources(career, education)
                : (0m, 0);

            var partnerValue = PartnerSearchRules.CalculatePartnerValue(
                partnerSex,
                age,
                stats.Values,
                education,
                career.JobLevel,
                estimatedWealth,
                estimatedHouses);
            var acceptanceChance = PartnerSearchRules.CalculateAcceptanceChance(
                seekerValue,
                partnerValue);
            var appearance = _appearance.GenerateCandidateAppearance(
                candidateId,
                partnerSex);
            var portrait = _appearance.GetPortrait(
                appearance,
                partnerSex,
                age,
                candidateId);

            result.Add(new PartnerCandidateInfo(
                candidateId.ToString("N"),
                name,
                surname,
                partnerSex,
                birthDate,
                age,
                personality,
                education,
                stats,
                hobbies,
                career.CareerId,
                career.CareerName,
                career.JobTitle,
                career.JobLevel,
                career.JobSatisfaction,
                career.AnnualIncome,
                estimatedWealth,
                estimatedHouses,
                appearance,
                portrait,
                partnerValue,
                acceptanceChance,
                town.Id,
                _gameState.Year));
        }

        return result;
    }

    public double GetPartnerValue(IPerson person)
    {
        ArgumentNullException.ThrowIfNull(person);

        var stats = _stats.GetStats(person)
            .ToDictionary(
                stat => stat.Id,
                stat => stat.Value,
                StringComparer.OrdinalIgnoreCase);
        var education = _education.GetEducationLevel(person);
        var career = _career.GetCareer(person);
        var sex = _family.GetSex(person);
        var household = sex == Sex.Male
            ? _economy.GetHousehold(person)
            : null;

        return PartnerSearchRules.CalculatePartnerValue(
            sex,
            person.Age,
            stats.Values,
            education,
            career.JobLevel,
            household?.Wealth ?? 0m,
            household?.HousesOwned ?? 0);
    }

    public IReadOnlyDictionary<string, string> BuildActionParameters(
        PartnerCandidateInfo candidate)
    {
        var birthMonth = candidate.BirthDate.Month
            ?? throw new InvalidOperationException(
                "Generated partner candidate is missing a birth month.");
        var birthDay = candidate.BirthDate.Day
            ?? throw new InvalidOperationException(
                "Generated partner candidate is missing a birth day.");

        var parameters = new Dictionary<string, string>(
            StringComparer.OrdinalIgnoreCase)
        {
            ["partner.candidateKey"] = candidate.CandidateKey,
            ["partner.name"] = candidate.Name,
            ["partner.surname"] = candidate.Surname,
            ["partner.sex"] = candidate.Sex.ToString(),
            ["partner.birthYear"] = candidate.BirthDate.Year.ToString(CultureInfo.InvariantCulture),
            ["partner.birthMonth"] = birthMonth.ToString(CultureInfo.InvariantCulture),
            ["partner.birthDay"] = birthDay.ToString(CultureInfo.InvariantCulture),
            ["partner.temperament"] = candidate.Personality.Temperament,
            ["partner.morals"] = candidate.Personality.Morals,
            ["partner.education"] = candidate.EducationLevel.ToString(CultureInfo.InvariantCulture),
            ["partner.careerId"] = candidate.CareerId ?? string.Empty,
            ["partner.jobLevel"] = candidate.JobLevel.ToString(CultureInfo.InvariantCulture),
            ["partner.jobSatisfaction"] = candidate.JobSatisfaction.ToString(CultureInfo.InvariantCulture),
            ["partner.estimatedWealth"] = candidate.EstimatedWealth.ToString(CultureInfo.InvariantCulture),
            ["partner.estimatedHouses"] = candidate.EstimatedHouses.ToString(CultureInfo.InvariantCulture),
            ["partner.hobbies"] = string.Join('|', candidate.Hobbies.Select(hobby => hobby.Id)),
            ["partner.value"] = candidate.PartnerValue.ToString("R", CultureInfo.InvariantCulture),
            ["partner.acceptance"] = candidate.AcceptanceChance.ToString("R", CultureInfo.InvariantCulture),
            ["partner.searchYear"] = candidate.SearchYear.ToString(CultureInfo.InvariantCulture),
            ["partner.appearance.hairGeneA"] = candidate.Appearance.HairGeneA.ToString(),
            ["partner.appearance.hairGeneB"] = candidate.Appearance.HairGeneB.ToString(),
            ["partner.appearance.hairColor"] = candidate.Appearance.HairColor.ToString(),
            ["partner.appearance.hairTexture"] = candidate.Appearance.HairTexture.ToString(),
            ["partner.appearance.greyingStartAge"] = candidate.Appearance.GreyingStartAge.ToString(CultureInfo.InvariantCulture),
            ["partner.appearance.balding"] = candidate.Appearance.BaldingTendency.ToString(),
            ["partner.appearance.beard"] = candidate.Appearance.BeardDensity.ToString()
        };

        foreach (var stat in candidate.Stats)
        {
            parameters[$"partner.stat.{stat.Key}"] =
                stat.Value.ToString(CultureInfo.InvariantCulture);
        }

        return parameters;
    }

    public GameActionResult ResolveCourtship(
        GameActionContext actionContext)
    {
        var seeker = actionContext.Actor;
        if (_family.GetSpouse(seeker) is not null)
            return new GameActionResult(false);

        if (!TryReadCandidate(
                actionContext.Parameters,
                out var candidate))
        {
            return new GameActionResult(false);
        }

        var candidateId = Guid.ParseExact(
            candidate.CandidateKey,
            "N");
        if (_gameState.People.Any(person => person.Id == candidateId))
            return new GameActionResult(false);

        var seekerName = _family.GetDisplayName(seeker);
        var candidateName = candidate.DisplayName;
        var currentCandidateAge = Math.Max(
            18,
            actionContext.GameState.Year - candidate.BirthDate.Year);
        // The chance shown when the player selects this concrete candidate
        // is the chance used when the queued courtship resolves.
        var acceptanceChance = candidate.AcceptanceChance;

        if (_random.NextDouble() >= acceptanceChance)
        {
            _events.Publish(new GameEvent
            {
                Type = "relationship.courtship",
                Year = actionContext.GameState.Year,
                SubjectId = seeker.Id,
                Data = new Dictionary<string, string>
                {
                    ["accepted"] = "false",
                    ["candidateName"] = candidateName,
                    ["text"] =
                        $"{seekerName} courted {candidateName}, but {GetSubjectPronoun(candidate.Sex)} rejected {GetObjectPronoun(_family.GetSex(seeker))}."
                }
            });

            return new GameActionResult(true);
        }

        var partner = CreatePersonFromCandidate(
            candidate,
            currentCandidateAge);

        if (seeker.Tags.Has("simulation.peripheral_ex"))
            partner.Tags.Add("simulation.peripheral_partner");

        var eventPartnerName =
            _family.GetDisplayName(partner);
        var sameSex = candidate.Sex == Sex.Male;

        if (!sameSex)
        {
            partner.MaidenName = candidate.Surname;
            partner.Surname = seeker.Surname;
        }

        _events.Publish(new GameEvent
        {
            Type = "relationship.courtship",
            Year = actionContext.GameState.Year,
            SubjectId = seeker.Id,
            RelatedPersonIds = [partner.Id],
            Data = new Dictionary<string, string>
            {
                ["accepted"] = "true",
                ["candidateName"] = eventPartnerName,
                ["text"] =
                    $"{seekerName} courted {eventPartnerName}, and {GetSubjectPronoun(candidate.Sex)} accepted."
            }
        });

        _family.SetSpouses(
            seeker,
            partner,
            actionContext.GameState.Year);

        var sameSexVariant = sameSex
            ? _eventVariants.GetVariant(
                "relationship.same_sex_union",
                actionContext.GameState.Year)
            : null;
        var eventType = sameSexVariant?.EventType
            ?? "relationship.married";
        var unionText = sameSexVariant is null
            ? $"{seekerName} married {eventPartnerName}."
            : sameSexVariant.FormatText(
                seekerName,
                eventPartnerName);

        _events.Publish(new GameEvent
        {
            Type = eventType,
            Year = actionContext.GameState.Year,
            SubjectId = seeker.Id,
            RelatedPersonIds = [partner.Id],
            Data = new Dictionary<string, string>
            {
                ["spouseId"] = partner.Id.ToString(),
                ["text"] = unionText,
                ["biographyVerb"] =
                    sameSexVariant?.FormatBiographyVerb(eventPartnerName)
                    ?? string.Empty,
                ["preserveGeneratedProfile"] = "true"
            }
        });

        return new GameActionResult(true);
    }

    public GameActionResult ResolveArrangedMarriage(
        GameActionContext actionContext,
        HistoricalActionVariant variant)
    {
        var father = actionContext.Actor;
        var daughter = actionContext.Target;

        if (_family.GetSpouse(daughter) is not null
            || !TryReadCandidate(
                actionContext.Parameters,
                out var candidate)
            || candidate.Sex != Sex.Male)
        {
            return new GameActionResult(false);
        }

        var candidateId = Guid.ParseExact(
            candidate.CandidateKey,
            "N");
        if (_gameState.People.Any(person => person.Id == candidateId))
            return new GameActionResult(false);

        var currentCandidateAge = Math.Max(
            18,
            actionContext.GameState.Year - candidate.BirthDate.Year);
        // Preserve the exact proposal odds presented in the chooser.
        var acceptanceChance = candidate.AcceptanceChance;

        var fatherName = _family.GetDisplayName(father);
        var daughterName = _family.GetDisplayName(daughter);

        if (_random.NextDouble() >= acceptanceChance)
        {
            _events.Publish(new GameEvent
            {
                Type = "relationship.marry_off_failed",
                Year = actionContext.GameState.Year,
                SubjectId = father.Id,
                RelatedPersonIds = [daughter.Id],
                Data = new Dictionary<string, string>
                {
                    ["candidateName"] = candidate.DisplayName,
                    ["chance"] = acceptanceChance.ToString(
                        "0.00",
                        CultureInfo.InvariantCulture),
                    ["text"] =
                        $"{fatherName} {variant.Narrative}, but {candidate.DisplayName} declined the match with {daughterName}."
                }
            });

            return new GameActionResult(true);
        }

        var husband = CreatePersonFromCandidate(
            candidate,
            currentCandidateAge);
        var husbandName = _family.GetDisplayName(husband);

        daughter.MaidenName ??= daughter.Surname;
        _family.SetSpouses(
            daughter,
            husband,
            actionContext.GameState.Year);
        daughter.Surname = husband.Surname;

        SeedArrangedHusbandResources(
            husband,
            daughter,
            candidate);

        _events.Publish(new GameEvent
        {
            Type = "relationship.married",
            Year = actionContext.GameState.Year,
            SubjectId = daughter.Id,
            RelatedPersonIds = [husband.Id, father.Id],
            Data = new Dictionary<string, string>
            {
                ["spouseId"] = husband.Id.ToString(),
                ["arrangedByFatherId"] = father.Id.ToString(),
                ["chance"] = acceptanceChance.ToString(
                    "0.00",
                    CultureInfo.InvariantCulture),
                ["preserveGeneratedProfile"] = "true",
                ["text"] =
                    $"{fatherName} {variant.Narrative}. {daughterName} married {husbandName}."
            }
        });

        return new GameActionResult(true);
    }

    private IPerson CreatePersonFromCandidate(
        PartnerCandidateInfo candidate,
        int age)
    {
        var candidateId = Guid.ParseExact(
            candidate.CandidateKey,
            "N");
        var person = _gameState.CreatePerson(
            candidate.Name,
            candidate.Surname,
            age,
            candidateId);
        person.BirthDate = candidate.BirthDate;

        _family.InitializePerson(person, candidate.Sex);
        person.Tags.Add("state.alive");
        person.Tags.Add("age.adult");
        person.Tags.Add("relationship.single");
        person.Tags.Add("sexuality.heterosexual");

        _appearance.SetAppearance(
            person,
            candidate.Appearance);
        _stats.SetStats(person, candidate.Stats);
        _education.SetEducationLevel(
            person,
            candidate.EducationLevel);
        _personality.SetPersonality(
            person,
            candidate.Personality);
        _hobbyResolver()?.SetHobbies(
            person,
            candidate.Hobbies.Select(hobby => hobby.Id).ToList());
        _career.AssignCareer(
            person,
            candidate.CareerId,
            candidate.JobLevel,
            candidate.JobSatisfaction);

        GeneratedFamilyBackgroundGenerator.Assign(
            person,
            candidate.Surname,
            _family,
            _historicalNames,
            _random);

        return person;
    }

    private PartnerCandidateInfo CreateCandidateFromParameters(
        IReadOnlyDictionary<string, string> parameters)
    {
        var key = Required(parameters, "partner.candidateKey");
        var name = Required(parameters, "partner.name");
        var surname = Required(parameters, "partner.surname");
        var sex = Enum.Parse<Sex>(Required(parameters, "partner.sex"), true);
        var birthDate = new GameDate(
            RequiredInt(parameters, "partner.birthYear"),
            RequiredInt(parameters, "partner.birthMonth"),
            RequiredInt(parameters, "partner.birthDay"));
        var age = Math.Max(18, _gameState.Year - birthDate.Year);
        var personality = new PersonalitySnapshot(
            Required(parameters, "partner.temperament"),
            Required(parameters, "partner.morals"));
        var education = RequiredInt(parameters, "partner.education");
        var stats = MainStatIds.ToDictionary(
            id => id,
            id => RequiredInt(parameters, $"partner.stat.{id}"),
            StringComparer.OrdinalIgnoreCase);
        var careerId = Required(parameters, "partner.careerId");
        if (string.IsNullOrWhiteSpace(careerId))
            careerId = null;
        var jobLevel = RequiredInt(parameters, "partner.jobLevel");
        var jobSatisfaction = RequiredInt(parameters, "partner.jobSatisfaction");
        var hobbyIds = Required(parameters, "partner.hobbies")
            .Split('|', StringSplitOptions.RemoveEmptyEntries);
        var hobbies = hobbyIds
            .Select(id => new HobbyInfo(id, id, string.Empty))
            .ToList();
        var estimatedWealth = OptionalDecimal(
            parameters,
            "partner.estimatedWealth");
        var estimatedHouses = OptionalInt(
            parameters,
            "partner.estimatedHouses");
        var value = RequiredDouble(parameters, "partner.value");
        var acceptance = RequiredDouble(parameters, "partner.acceptance");
        var searchYear = RequiredInt(parameters, "partner.searchYear");
        var candidateId = Guid.ParseExact(key, "N");
        var appearance = TryReadAppearance(parameters, out var storedAppearance)
            ? storedAppearance
            : _appearance.GenerateCandidateAppearance(
                candidateId,
                sex);
        var portrait = _appearance.GetPortrait(
            appearance,
            sex,
            age,
            candidateId);

        return new PartnerCandidateInfo(
            key,
            name,
            surname,
            sex,
            birthDate,
            age,
            personality,
            education,
            stats,
            hobbies,
            careerId,
            string.Empty,
            string.Empty,
            jobLevel,
            jobSatisfaction,
            0,
            estimatedWealth,
            estimatedHouses,
            appearance,
            portrait,
            value,
            acceptance,
            string.Empty,
            searchYear);
    }

    private bool TryReadCandidate(
        IReadOnlyDictionary<string, string> parameters,
        out PartnerCandidateInfo candidate)
    {
        try
        {
            candidate = CreateCandidateFromParameters(parameters);
            return true;
        }
        catch
        {
            candidate = null!;
            return false;
        }
    }

    private static (decimal Wealth, int Houses) EstimateMaleResources(
        GeneratedCareerProfile career,
        int educationLevel)
    {
        var education = Math.Clamp(educationLevel, 0, 5);
        var level = Math.Clamp(career.JobLevel, 0, 5);

        var savingsYears =
            1.0m
            + level * 0.75m
            + education * 0.25m;
        var wealth = career.AnnualIncome * savingsYears
            + education * 250m;
        wealth = Math.Max(0m, Math.Round(wealth / 500m) * 500m);

        var standing = level * 2 + education;
        var houses = standing >= 12
            ? 2
            : standing >= 7
                ? 1
                : 0;

        return (wealth, houses);
    }

    private void SeedArrangedHusbandResources(
        IPerson husband,
        IPerson daughter,
        PartnerCandidateInfo candidate)
    {
        _economy.EnsureIndependentHousehold(
            husband,
            daughter);
        _economy.SetWealth(
            husband,
            Math.Max(0m, candidate.EstimatedWealth));

        var town = _locations.FindTown(candidate.TownId)
            ?? _locations.GetLocation(daughter).HomeTown;

        for (var index = 0;
            index < Math.Max(0, candidate.EstimatedHouses);
            index++)
        {
            _economy.AddHouse(
                husband,
                town);
        }
    }

    private static int OptionalInt(
        IReadOnlyDictionary<string, string> parameters,
        string key)
    {
        return parameters.TryGetValue(key, out var value)
            && int.TryParse(
                value,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var parsed)
            ? Math.Max(0, parsed)
            : 0;
    }

    private static decimal OptionalDecimal(
        IReadOnlyDictionary<string, string> parameters,
        string key)
    {
        return parameters.TryGetValue(key, out var value)
            && decimal.TryParse(
                value,
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out var parsed)
            ? Math.Max(0m, parsed)
            : 0m;
    }

    private string RandomWeightedSurname(
        IGameRandom random)
    {
        var entries = _data.GetWeightedStringList(SurnamesPath);
        var total = entries.Sum(entry => (double)entry.Weight);
        var roll = random.NextDouble() * total;

        foreach (var entry in entries)
        {
            if (roll < entry.Weight)
                return entry.Value;
            roll -= entry.Weight;
        }

        return entries[^1].Value;
    }

    private int GetStat(IPerson person, string statId) =>
        _stats.GetStats(person)
            .First(stat => stat.Id.Equals(
                statId,
                StringComparison.OrdinalIgnoreCase))
            .Value;

    private static Guid CreateCandidateId(
        IPerson seeker,
        int year,
        string poolKey,
        int slot)
    {
        var bytes = SHA256.HashData(
            Encoding.UTF8.GetBytes(
                $"{seeker.Id:N}|{year}|{poolKey}|{slot}"));
        return new Guid(bytes.AsSpan(0, 16));
    }

    private static bool TryReadAppearance(
        IReadOnlyDictionary<string, string> parameters,
        out AppearanceSnapshot appearance)
    {
        try
        {
            appearance = new AppearanceSnapshot(
                Enum.Parse<HairColor>(
                    Required(parameters, "partner.appearance.hairGeneA"),
                    true),
                Enum.Parse<HairColor>(
                    Required(parameters, "partner.appearance.hairGeneB"),
                    true),
                Enum.Parse<HairColor>(
                    Required(parameters, "partner.appearance.hairColor"),
                    true),
                Enum.Parse<HairTexture>(
                    Required(parameters, "partner.appearance.hairTexture"),
                    true),
                RequiredInt(parameters, "partner.appearance.greyingStartAge"),
                Enum.Parse<BaldingTendency>(
                    Required(parameters, "partner.appearance.balding"),
                    true),
                Enum.Parse<BeardDensity>(
                    Required(parameters, "partner.appearance.beard"),
                    true));

            return true;
        }
        catch
        {
            appearance = null!;
            return false;
        }
    }

    private static string GetSubjectPronoun(Sex sex) =>
        sex == Sex.Female ? "she" : "he";

    private static string GetObjectPronoun(Sex sex) =>
        sex == Sex.Female ? "her" : "him";

    private static string Required(
        IReadOnlyDictionary<string, string> parameters,
        string key) =>
        parameters.TryGetValue(key, out var value)
            ? value
            : throw new InvalidOperationException(
                $"Missing courtship parameter '{key}'.");

    private static int RequiredInt(
        IReadOnlyDictionary<string, string> parameters,
        string key) =>
        int.Parse(
            Required(parameters, key),
            CultureInfo.InvariantCulture);

    private static double RequiredDouble(
        IReadOnlyDictionary<string, string> parameters,
        string key) =>
        double.Parse(
            Required(parameters, key),
            CultureInfo.InvariantCulture);
}
