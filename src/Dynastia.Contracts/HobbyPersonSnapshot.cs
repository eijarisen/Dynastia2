namespace Dynastia.Contracts;

public sealed record HobbyPersonSnapshot(
    int HobbyCapacity,
    IReadOnlyList<HobbyInfo> Hobbies);
