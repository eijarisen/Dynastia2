namespace Dynastia.Contracts;

public interface ILocalCareerOpportunityService
{
    CareerLocationEvaluation Evaluate(
        IPerson person,
        CareerLocationRequirement requirement);

    LocationOpportunitySnapshot GetOpportunitySnapshot(
        IPerson person);
}
