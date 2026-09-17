namespace Dynastia.Contracts;

public sealed record GameActionResult(
    bool Success,
    string? Message = null,
    string? ReasonCode = null);
