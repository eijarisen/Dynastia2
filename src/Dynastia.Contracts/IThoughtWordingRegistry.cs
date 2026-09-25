namespace Dynastia.Contracts;

public interface IThoughtWordingRegistry
{
    void RegisterCatalogue(
        string ownerId,
        IReadOnlyDictionary<string, ThoughtWordingDefinition> definitions);

    bool TryGet(
        string wordingKey,
        out ThoughtWordingDefinition definition);
}
