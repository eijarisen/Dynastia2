namespace Dynastia.Contracts;

public sealed record ActionGuardResult(
    bool Allowed,
    string? Reason = null);
