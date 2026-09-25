using Dynastia.Contracts;

namespace Dynastia.Mechanics.Hobbies;

internal sealed class HobbyThoughtProvider :
    IThoughtProvider
{
    private readonly StandardHobbyService _hobbies;

    public HobbyThoughtProvider(
        StandardHobbyService hobbies)
    {
        _hobbies = hobbies;
    }

    public string Id =>
        "hobbies.pastimes";

    public IEnumerable<ThoughtCandidate> GetCandidates(
        IPerson person,
        ThoughtContext context)
    {
        var snapshot = _hobbies.GetHobbies(person);

        foreach (var hobby in snapshot.Hobbies)
        {
            var text = _hobbies.GetThoughtText(
                person,
                hobby.Id,
                context.Year);

            if (string.IsNullOrWhiteSpace(text))
                continue;

            yield return new ThoughtCandidate(
                             $"hobby.{hobby.Id}",
                             "hobby",
                             $"hobby.{hobby.Id}",
                             HobbyBalanceRules.ThoughtSalience,
                             ThoughtMoodIds.Pleased,
                             hobby.Emoji,
                             ThoughtSalienceTraits.None,
                             "hobby",
                             hobby.Id,
                             "literal",
                             new Dictionary<string, string>
                             {
                             ["literalText"] = text
                             }
                         );
        }
    }
}
