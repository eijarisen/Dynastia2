using System.Globalization;
using Dynastia.Contracts;

namespace Dynastia.Mechanics.Heirlooms;

internal sealed class HeirloomAchievementGenerator
{
    private readonly IGameState _gameState;
    private readonly IEconomyService _economy;
    private readonly IFamilyService _family;
    private readonly IEducationService _education;
    private readonly ICareerService _career;
    private readonly IHeirloomService _heirlooms;
    private readonly IGameRandom _random;
    private readonly HeirloomAchievementCatalog _catalog;

    public HeirloomAchievementGenerator(
        IGameState gameState,
        IEconomyService economy,
        IFamilyService family,
        IEducationService education,
        ICareerService career,
        IHeirloomService heirlooms,
        IGameRandom random,
        HeirloomAchievementCatalog catalog)
    {
        _gameState = gameState;
        _economy = economy;
        _family = family;
        _education = education;
        _career = career;
        _heirlooms = heirlooms;
        _random = random;
        _catalog = catalog;
    }

    public void Handle(GameEvent gameEvent)
    {
        if (gameEvent.SubjectId is not Guid subjectId)
            return;

        var person = _gameState.People.FirstOrDefault(candidate => candidate.Id == subjectId);
        if (person is null)
            return;

        if (gameEvent.Type.Equals("education.success", StringComparison.OrdinalIgnoreCase))
            HandleEducation(person);
        else if (gameEvent.Type.Equals("career.promotion", StringComparison.OrdinalIgnoreCase))
            HandleCareer(person, gameEvent);
        else if (gameEvent.Type.Equals("craft.became_master", StringComparison.OrdinalIgnoreCase))
            HandleCraft(person, gameEvent);
        else if (gameEvent.Type.Equals("life.death", StringComparison.OrdinalIgnoreCase))
            HandleDeath(person);
    }

    private void HandleEducation(IPerson person)
    {
        if (_education.GetEducationLevel(person) != 5 || !CanCreateFor(person))
            return;

        var triggerId = $"education5:{person.Id}";
        if (HasCompleted(person, triggerId))
            return;

        var pool = _catalog.GetAcademicPool(_gameState.Year);
        if (pool is null)
            return;

        var templateId = Choose(pool.TemplateIds);
        _heirlooms.Create(
            person,
            new HeirloomCreationRequest(
                templateId,
                person.Id,
                "education5",
                triggerId,
                $"Earned when {_family.GetDisplayName(person)} reached Education 5."));
        MarkCompleted(person, triggerId);
    }

    private void HandleCareer(IPerson person, GameEvent sourceEvent)
    {
        var snapshot = _career.GetCareer(person);
        if (snapshot.JobLevel != 5 || !CanCreateFor(person))
            return;

        var triggerId = $"career5:{person.Id}";
        if (HasCompleted(person, triggerId))
            return;

        var family = _career.GetCareerFamily(person);
        if (string.IsNullOrWhiteSpace(family)
            || !_catalog.CareerTemplates.TryGetValue(family, out var templateId))
        {
            return;
        }

        var careerName = snapshot.CareerName;
        if (string.IsNullOrWhiteSpace(careerName)
            && sourceEvent.Data.TryGetValue("careerName", out var eventCareerName))
        {
            careerName = eventCareerName;
        }
        careerName ??= family;

        _heirlooms.Create(
            person,
            new HeirloomCreationRequest(
                templateId,
                person.Id,
                "career5",
                triggerId,
                $"Earned when {_family.GetDisplayName(person)} reached Career Level 5 in {careerName}.",
                NewsTokens: new Dictionary<string, string>
                {
                    ["Career"] = careerName
                }));
        MarkCompleted(person, triggerId);
    }

    private void HandleCraft(IPerson person, GameEvent sourceEvent)
    {
        if (!sourceEvent.Data.TryGetValue("craftId", out var craftId)
            || string.IsNullOrWhiteSpace(craftId)
            || !_catalog.CraftTemplates.TryGetValue(craftId, out var templateId)
            || !CanCreateFor(person))
        {
            return;
        }

        var triggerId = $"craft5:{person.Id}:{craftId}";
        if (HasCompleted(person, triggerId))
            return;

        var craftName = sourceEvent.Data.TryGetValue("craftName", out var sourceName)
            && !string.IsNullOrWhiteSpace(sourceName)
                ? sourceName
                : craftId;

        _heirlooms.Create(
            person,
            new HeirloomCreationRequest(
                templateId,
                person.Id,
                "craft5",
                triggerId,
                $"Created when {_family.GetDisplayName(person)} became a Master of {craftName}.",
                NewsTokens: new Dictionary<string, string>
                {
                    ["Craft"] = craftName
                }));
        MarkCompleted(person, triggerId);
    }

    private void HandleDeath(IPerson person)
    {
        if (!CanCreateFor(person))
            return;

        var generatedLargeFamily = false;
        var livingChildren = _family.GetChildren(person)
            .Count(child => child.Tags.Has("state.alive"));
        var largeFamily = _catalog.DeathRules.LargeFamily;
        if (largeFamily.Guaranteed
            && livingChildren >= largeFamily.MinimumLivingChildren)
        {
            var triggerId = $"large_family_death:{person.Id}";
            if (!HasCompleted(person, triggerId))
            {
                _heirlooms.Create(
                    person,
                    new HeirloomCreationRequest(
                        Choose(largeFamily.TemplateIds),
                        person.Id,
                        "large_family_death",
                        triggerId,
                        $"Preserved after {_family.GetDisplayName(person)} died leaving {livingChildren} living children.",
                        NewsTokens: new Dictionary<string, string>
                        {
                            ["LivingChildren"] = livingChildren.ToString(CultureInfo.InvariantCulture)
                        }));
                MarkCompleted(person, triggerId);
                generatedLargeFamily = true;
            }
        }

        if (generatedLargeFamily && !_catalog.DeathRules.MayGenerateBothOnSameDeath)
            return;

        var longevity = _catalog.DeathRules.Longevity;
        if (!longevity.Guaranteed || person.Age < longevity.MinimumAgeAtDeath)
            return;

        var longevityTrigger = $"longevity100:{person.Id}";
        if (HasCompleted(person, longevityTrigger))
            return;

        _heirlooms.Create(
            person,
            new HeirloomCreationRequest(
                Choose(longevity.TemplateIds),
                person.Id,
                "longevity100",
                longevityTrigger,
                $"Preserved after {_family.GetDisplayName(person)} died at age {person.Age}.",
                NewsTokens: new Dictionary<string, string>
                {
                    ["Age"] = person.Age.ToString(CultureInfo.InvariantCulture)
                }));
        MarkCompleted(person, longevityTrigger);
    }

    private bool CanCreateFor(IPerson person) =>
        _economy.GetHouseholdId(person) is not null;

    private string Choose(IReadOnlyList<string> values) =>
        values[_random.NextInt(0, values.Count - 1)];

    private static bool HasCompleted(IPerson person, string triggerId) =>
        person.Components.Get<HeirloomPersonTriggerComponent>()?.CompletedTriggerIds.Contains(triggerId)
        ?? false;

    private static void MarkCompleted(IPerson person, string triggerId)
    {
        var component = person.Components.Get<HeirloomPersonTriggerComponent>();
        if (component is null)
        {
            component = new HeirloomPersonTriggerComponent();
            person.Components.Set(component);
        }
        component.CompletedTriggerIds.Add(triggerId);
    }
}
