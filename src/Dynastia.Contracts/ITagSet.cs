namespace Dynastia.Contracts;

public interface ITagSet
{
    IReadOnlyCollection<string> All { get; }
    bool Has(string tag);
    void Add(string tag);
    void Remove(string tag);
}
