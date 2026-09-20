using Dynastia.Contracts;

namespace Dynastia.Mechanics.Career;

internal sealed record CareerInstitutionFailure(
    CareerInstitutionRequirement Requirement,
    TownInstitutionInfo Institution,
    TownInfo Town);

public sealed partial class StandardCareerService
{
    private bool MeetsInstitutionRequirement(
        IPerson person,
        CareerDefinition definition)
    {
        var town = _localOpportunities
            .GetOpportunitySnapshot(person)
            .Town;

        return MeetsInstitutionRequirement(
            town,
            definition,
            _gameState.Year);
    }

    private bool MeetsInstitutionRequirement(
        TownInfo town,
        CareerDefinition definition,
        int year)
    {
        var snapshot = _institutions.Resolve(town, year);
        return _institutionRequirements.IsSatisfied(
            definition.Id,
            snapshot);
    }

    internal CareerInstitutionFailure? GetCurrentInstitutionFailure(
        IPerson person)
    {
        ArgumentNullException.ThrowIfNull(person);

        var component = GetRequired(person);
        if (component.IsRetired
            || component.JobLevel <= 0
            || string.IsNullOrWhiteSpace(component.CareerId))
        {
            return null;
        }

        var definition = _catalog.Find(component.CareerId);
        if (definition is null)
            return null;

        var requirement = _institutionRequirements.Find(definition.Id);
        if (requirement is null)
            return null;

        var town = _localOpportunities
            .GetOpportunitySnapshot(person)
            .Town;
        var snapshot = _institutions.Resolve(town, _gameState.Year);
        var institution = snapshot.Find(requirement.InstitutionId)
            ?? new TownInstitutionInfo(
                requirement.InstitutionId,
                FormatInstitutionName(requirement.InstitutionId),
                0,
                "Unavailable");

        return institution.Tier < requirement.MinimumTier
            ? new CareerInstitutionFailure(requirement, institution, town)
            : null;
    }

    private static string FormatInstitutionName(string institutionId) =>
        string.Join(
            " ",
            institutionId.Split('_', StringSplitOptions.RemoveEmptyEntries)
                .Select(part => char.ToUpperInvariant(part[0]) + part[1..]));
}
