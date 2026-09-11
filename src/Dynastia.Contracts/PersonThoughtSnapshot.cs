namespace Dynastia.Contracts;

public sealed record PersonThoughtSnapshot(
    int Year,
    string ThoughtId,
    string Topic,
    string Text,
    string Emoji,
    int Salience,
    string? SourceId);
