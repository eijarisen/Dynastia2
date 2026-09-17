using Dynastia.Contracts;

namespace Dynastia.Mechanics.Career;

public sealed partial class StandardCareerService
{
    internal double GetCareerAbility(
        IPerson person,
        CareerDefinition definition)
    {
        var stats = _stats.GetStats(person)
            .ToDictionary(stat => stat.Id, stat => stat.Value, StringComparer.OrdinalIgnoreCase);

        return CareerAptitude.GetComposite(
            definition,
            statId => stats.TryGetValue(statId, out var value) ? value : 3);
    }

    internal static double GetCareerAbility(
        CareerDefinition definition,
        int strength,
        int intellect,
        int appeal)
    {
        return CareerAptitude.GetComposite(
            definition,
            statId => statId.ToLowerInvariant() switch
            {
                "strength" => strength,
                "intellect" => intellect,
                "appeal" => appeal,
                _ => 3
            });
    }

    internal int GetExpectedEducation(
        CareerDefinition definition,
        int jobLevel) =>
        _educationProfiles.GetExpectedEducation(
            definition.EducationProfile,
            jobLevel);


    internal double GetAutomaticCareerFitMultiplier(
        CareerDefinition definition,
        IPerson person,
        int desiredLevel)
    {
        var ability = GetCareerAbility(person, definition);
        var education = _education.GetEducationLevel(person);
        var expectedEducation = GetExpectedEducation(
            definition,
            Math.Clamp(desiredLevel, 1, 5));
        return GetCareerFitMultiplier(ability, education, expectedEducation);
    }

    internal static double GetCareerFitMultiplier(
        double ability,
        int education,
        int expectedEducation)
    {
        var abilityMultiplier = Math.Clamp(
            0.25 + Math.Clamp(ability, 1, 5) * 0.25,
            0.35,
            1.50);
        var gap = expectedEducation - Math.Clamp(education, 0, 5);
        var educationMultiplier = gap switch
        {
            <= 0 => 1.10,
            1 => 0.60,
            2 => 0.25,
            _ => 0.10
        };

        return abilityMultiplier * educationMultiplier;
    }

    internal double GetSelectionContextMultiplier(
        CareerDefinition definition,
        IPerson person)
    {
        var snapshot = _localOpportunities.GetOpportunitySnapshot(person);
        var context = new ContextWeightContext(
            _gameState.Year,
            person.Age,
            _family.GetSex(person),
            GetTemperament(person),
            SettlementClass: snapshot.Town.SettlementClass);
        return _careerContext.GetMultiplier(definition.Id, context);
    }

    internal double GetSelectionContextMultiplier(
        CareerDefinition definition,
        GeneratedCareerContext candidate)
    {
        var context = new ContextWeightContext(
            candidate.Year,
            candidate.Age,
            candidate.Sex,
            candidate.Temperament,
            SettlementClass: candidate.Town.SettlementClass);
        return _careerContext.GetMultiplier(definition.Id, context);
    }

    internal double GetTemperamentMultiplier(
        CareerDefinition definition,
        IPerson person)
    {
        var context = new ContextWeightContext(
            _gameState.Year,
            person.Age,
            _family.GetSex(person),
            GetTemperament(person));
        return _careerContext.GetDimensionMultiplier(
            definition.Id,
            context,
            "Temperament");
    }

    internal static string? GetTemperament(IPerson person)
    {
        if (person.Tags.Has("personality.melancholic")) return "Melancholic";
        if (person.Tags.Has("personality.phlegmatic")) return "Phlegmatic";
        if (person.Tags.Has("personality.sanguine")) return "Sanguine";
        if (person.Tags.Has("personality.choleric")) return "Choleric";
        return null;
    }
}
