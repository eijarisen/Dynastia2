namespace Dynastia.Contracts;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class PersistedComponentIdAttribute : Attribute
{
    public PersistedComponentIdAttribute(
        string id,
        params string[] aliases)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("Persisted component ID is required.", nameof(id));

        Id = id.Trim();
        Aliases = aliases ?? Array.Empty<string>();
    }

    public string Id { get; }
    public IReadOnlyList<string> Aliases { get; }
}
