namespace Dynastia.Contracts;

public sealed record GeneratedFamilyBackgroundInfo(
    string FatherName,
    string MotherName,
    IReadOnlyList<string> Siblings);
