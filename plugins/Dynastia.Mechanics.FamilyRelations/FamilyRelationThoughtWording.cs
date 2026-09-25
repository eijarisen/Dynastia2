using Dynastia.Contracts;

namespace Dynastia.Mechanics.FamilyRelations;

internal static class FamilyRelationThoughtWording
{
    public static void Register(IGamePluginContext context)
    {
        var registry = context.GetService<IThoughtWordingRegistry>()
            ?? throw new InvalidOperationException("Thought wording registry is unavailable.");

        registry.RegisterCatalogue(
            "dynastia.family_relations",
            new Dictionary<string, ThoughtWordingDefinition>(StringComparer.OrdinalIgnoreCase)
            {
                ["family_relation.close"] = new()
                {
                    Child = ["I really like being with my {relation}.", "My {relation} and I get along really well."],
                    Adolescent = ["My {relation} and I have always been close.", "I can usually count on my {relation}."],
                    AdultRough = ["Me and my {relation} are close.", "I can count on my {relation}."],
                    AdultElaborate = ["My {relation} and I have maintained a genuinely close bond.", "I value how dependable my relationship with my {relation} has become."],
                    AdultNormal = ["My {relation} and I have always been close.", "I'm glad my {relation} and I can rely on each other."]
                },
                ["family_relation.strained"] = new()
                {
                    Child = ["I don't really get along with my {relation}.", "Things feel bad between me and my {relation}."],
                    Adolescent = ["My {relation} and I never seem to get along.", "Things are still tense with my {relation}."],
                    AdultRough = ["Me and my {relation} don't get along.", "Still can't stand dealing with my {relation}."],
                    AdultElaborate = ["My relationship with my {relation} remains painfully strained.", "There is still too much hostility between my {relation} and me."],
                    AdultNormal = ["My {relation} and I never seem to get along.", "Things are still strained between me and my {relation}."]
                },
                ["family_relation.improved"] = new()
                {
                    Child = ["It was nice spending time with my {relation} again."],
                    Adolescent = ["It was actually good spending time with my {relation} again."],
                    AdultRough = ["Good to spend some time with my {relation} again."],
                    AdultElaborate = ["It was genuinely good to spend time with my {relation} again and mend things a little."],
                    AdultNormal = ["It was good spending time with my {relation} again."]
                }
            });
    }
}
