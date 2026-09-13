using Dynastia.Contracts;

namespace Dynastia.Mechanics.Thoughts;

internal sealed partial class ThoughtPhraseRenderer
{
    private static IReadOnlyList<string> MarriageIssueVariants(
        ThoughtVoice voice,
        string issue,
        string satisfactionLabel)
    {
        if (string.IsNullOrWhiteSpace(
            issue))
        {
            if (satisfactionLabel.Equals(
                "Thriving",
                StringComparison.OrdinalIgnoreCase))
            {
                return voice switch
                {
                    ThoughtVoice.AdultRough =>
                        ["Marriage is great.", "We're really good together."],

                    ThoughtVoice.AdultElaborate =>
                        ["Our marriage feels unusually strong; there is a genuine sense that we are thriving together.",
                         "The marriage has become one of the most stable and rewarding parts of my life."],

                    _ =>
                        ["Our marriage is going wonderfully.", "Things between us are genuinely very good."]
                };
            }

            if (satisfactionLabel.Equals(
                "Satisfied",
                StringComparison.OrdinalIgnoreCase))
            {
                return
                    [
                        "Things between us are going well.",
                        "I'm happy with how our marriage is going."
                    ];
            }

            if (satisfactionLabel.Equals(
                "Unhappy",
                StringComparison.OrdinalIgnoreCase))
            {
                return
                    [
                        "Our marriage hasn't been going well.",
                        "Things between us have become difficult."
                    ];
            }

            if (satisfactionLabel.Equals(
                "Miserable",
                StringComparison.OrdinalIgnoreCase))
            {
                return voice switch
                {
                    ThoughtVoice.AdultRough =>
                        ["Marriage is awful right now.", "We're barely getting along."],

                    ThoughtVoice.AdultElaborate =>
                        ["The marriage has deteriorated to the point that it is difficult to imagine continuing like this.",
                         "Our relationship has become deeply unhappy and increasingly difficult to sustain."],

                    _ =>
                        ["Our marriage is in a terrible place.", "Things between us feel close to breaking point."]
                };
            }
        }

        if (issue.Equals(
            "being broke",
            StringComparison.OrdinalIgnoreCase))
        {
            return voice switch
            {
                ThoughtVoice.AdultRough =>
                    ["No money. We keep fighting over it.", "We're broke. It's hurting us."],

                ThoughtVoice.AdultElaborate =>
                    ["Our financial difficulties are steadily eroding the patience we have for one another.",
                     "Money problems are turning ordinary disagreements into much larger strains on the marriage."],

                _ =>
                    ["Money problems are putting a strain on our marriage.",
                     "Our lack of money is making the marriage harder."]
            };
        }

        if (issue.Equals(
            "household strain",
            StringComparison.OrdinalIgnoreCase))
        {
            return voice switch
            {
                ThoughtVoice.AdultRough =>
                    ["Too much to do here. We're both fed up.", "House is too much. We're worn out."],

                ThoughtVoice.AdultElaborate =>
                    ["The constant demands of the household leave remarkably little room for us as a couple.",
                     "The household consumes so much energy that our relationship is increasingly neglected."],

                _ =>
                    ["The pressure of this household is hurting our marriage.",
                     "There is so much to manage at home that our marriage is suffering."]
            };
        }

        if (issue.Contains(
            "fertility",
            StringComparison.OrdinalIgnoreCase))
        {
            return voice switch
            {
                ThoughtVoice.AdultRough =>
                    ["We want kids. It isn't happening.", "Want children. Worried it won't happen."],

                ThoughtVoice.AdultElaborate =>
                    ["The uncertainty surrounding whether we'll be able to have children has become difficult for us both.",
                     "Questions about whether we can have children are creating a strain neither of us can easily resolve."],

                _ =>
                    ["I'm worried we may not have the children we hoped for.",
                     "Not knowing whether we'll be able to have children is hard on us."]
            };
        }

        if (issue.Contains(
            "health",
            StringComparison.OrdinalIgnoreCase))
        {
            return
                [
                    "Health problems are putting pressure on our marriage.",
                    "Illness has made things harder between us lately."
                ];
        }

        if (issue.Contains(
            "intellect",
            StringComparison.OrdinalIgnoreCase)
            || issue.Contains(
                "appeal",
                StringComparison.OrdinalIgnoreCase))
        {
            return
                [
                    "We haven't been connecting as well as we used to.",
                    "There is a distance between us that has been hard to ignore."
                ];
        }

        if (issue.Contains(
            "unemployed",
            StringComparison.OrdinalIgnoreCase))
        {
            return
                [
                    "Not having steady work is putting pressure on our marriage.",
                    "My employment problems are starting to affect us both."
                ];
        }

        return
            [
                "The marriage has been difficult lately.",
                "Things between us have not been going well."
            ];
    }

}
