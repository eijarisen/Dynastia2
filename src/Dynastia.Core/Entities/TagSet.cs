using Dynastia.Contracts;

namespace Dynastia.Core.Entities;

public sealed class TagSet : ITagSet
{
    private readonly HashSet<string> _tags =
        new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyCollection<string> All => _tags;

    public bool Has(string tag) => _tags.Contains(tag);

    public void Add(string tag)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tag);
        _tags.Add(tag);
    }

    public void Remove(string tag) => _tags.Remove(tag);
}
