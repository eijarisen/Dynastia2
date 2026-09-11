using Dynastia.Contracts;

namespace Dynastia.Mechanics.Career;

internal sealed record FamilyConnectionOpportunity(
    IPerson Parent,
    CareerDefinition Career,
    int ParentPeakLevel,
    int JobLevel,
    double AcceptanceChance);
