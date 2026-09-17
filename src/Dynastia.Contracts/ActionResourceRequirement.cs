namespace Dynastia.Contracts;

public sealed record ActionResourceRequirement(
    string ResourceId,
    decimal RequiredAmount,
    decimal AvailableAmount,
    string? DisplayName = null,
    string? Unit = null)
{
    public bool IsSatisfied =>
        AvailableAmount >= RequiredAmount;
}
