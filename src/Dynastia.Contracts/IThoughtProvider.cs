namespace Dynastia.Contracts;

public interface IThoughtProvider
{
    string Id { get; }

    IEnumerable<ThoughtCandidate> GetCandidates(
        IPerson person,
        ThoughtContext context);
}
