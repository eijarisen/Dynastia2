using Dynastia.Contracts;

namespace Dynastia.Mechanics.FamilyRelations;

internal sealed class FamilyRelationThoughtProvider : IThoughtProvider
{
    private readonly IFamilyRelationService _relations;

    public FamilyRelationThoughtProvider(IFamilyRelationService relations)
    {
        _relations = relations;
    }

    public string Id => "thoughts.family_relations";

    public IEnumerable<ThoughtCandidate> GetCandidates(IPerson person, ThoughtContext context)
    {
        if (!person.Tags.Has("state.alive") || person.Age < 12)
            yield break;

        var improved = context.Events.LastOrDefault(e =>
            e.Type.Equals("family_relations.improved", StringComparison.OrdinalIgnoreCase)
            && (e.SubjectId == person.Id || e.RelatedPersonIds.Contains(person.Id)));

        if (improved is not null)
        {
            var otherId = improved.SubjectId == person.Id
                ? improved.RelatedPersonIds.FirstOrDefault()
                : improved.SubjectId;
            var other = context.GameState.People.FirstOrDefault(p => p.Id == otherId);
            if (other is not null)
            {
                var kinship = ResolveKinship(person, other, context.Family);
                yield return new ThoughtCandidate(
                                 "family_relation.improved",
                                 "family.relations",
                                 $"family_relation.improved.{other.Id}",
                                 52,
                                 ThoughtMoodIds.Pleased,
                                 "🤝",
                                 ThoughtSalienceTraits.None,
                                 "event",
                                 improved.Type,
                                 "family_relation.improved",
                                 new Dictionary<string, string> { ["relation"] = kinship.ToLowerInvariant() }
                             );
            }
        }

        var strongest = _relations.GetRelatedHouseholds(person)
            .SelectMany(h => h.Relations)
            .OrderByDescending(r => Math.Abs(r.Sympathy - 50))
            .FirstOrDefault();
        if (strongest is null)
            yield break;

        var relationWord = strongest.Kinship.ToLowerInvariant();
        if (strongest.Sympathy >= 80)
        {
            yield return new ThoughtCandidate(
                             "family_relation.close",
                             "family.relations",
                             $"family_relation.close.{strongest.RelativeId}",
                             34,
                             ThoughtMoodIds.Pleased,
                             "❤️",
                             ThoughtSalienceTraits.None,
                             "state",
                             strongest.RelativeId.ToString(),
                             "family_relation.close",
                             new Dictionary<string, string> { ["relation"] = relationWord }
                         );
        }
        else if (strongest.Sympathy < 40)
        {
            yield return new ThoughtCandidate(
                             "family_relation.strained",
                             "family.relations",
                             $"family_relation.strained.{strongest.RelativeId}",
                             strongest.Type == FamilyRelationshipType.ExSpouse ? 44 : 38,
                             ThoughtMoodIds.Concerned,
                             "👪",
                             ThoughtSalienceTraits.None,
                             "state",
                             strongest.RelativeId.ToString(),
                             "family_relation.strained",
                             new Dictionary<string, string> { ["relation"] = relationWord }
                         );
        }
    }

    private static string ResolveKinship(IPerson person, IPerson other, IFamilyService family)
    {
        if (family.GetFather(person)?.Id == other.Id) return "Father";
        if (family.GetMother(person)?.Id == other.Id) return "Mother";
        if (family.GetChildren(person).Any(c => c.Id == other.Id))
            return family.GetSex(other) == Sex.Male ? "Son" : "Daughter";
        var ended = family.GetRelationshipHistory(person).Any(h => h.SpouseId == other.Id && h.EndYear is not null);
        if (ended) return family.GetSex(other) == Sex.Male ? "Ex-husband" : "Ex-wife";
        return family.GetSex(other) == Sex.Male ? "Brother" : "Sister";
    }
}
