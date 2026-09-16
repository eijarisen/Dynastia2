namespace Dynastia.Contracts;

public interface ILocalCareerOpportunityService
{
    CareerLocationEvaluation Evaluate(
        IPerson person,
        CareerLocationRequirement requirement);

    CareerLocationEvaluation Evaluate(
        TownInfo town,
        CareerLocationRequirement requirement);

    LocationOpportunitySnapshot GetOpportunitySnapshot(
        IPerson person);

    LocationOpportunitySnapshot GetOpportunitySnapshot(
        TownInfo town);
}
