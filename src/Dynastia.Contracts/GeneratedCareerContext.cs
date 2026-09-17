namespace Dynastia.Contracts;

public sealed record GeneratedCareerContext(
    Sex Sex,
    TownInfo Town,
    int Year,
    int DesiredJobLevel,
    int Age,
    int Strength,
    int Intellect,
    int Appeal,
    int EducationLevel,
    string? Temperament,
    string DeterministicKey);
