using Dynastia.Contracts;

namespace Dynastia.Mechanics.Thoughts;

internal sealed partial class ThoughtPhraseRenderer
{
    private static IReadOnlyList<string> GetVariants(
        ThoughtCandidate candidate,
        ThoughtVoice voice)
    {
        string C(
            string key,
            string fallback = "")
        {
            return candidate.Context.TryGetValue(
                key,
                out var value)
                    ? value
                    : fallback;
        }

        var relation =
            C(
                "relation",
                "them");

        var relationPossessive =
            C(
                "relationPossessive",
                "their");

        var condition =
            C(
                "condition",
                "this illness");

        var issue =
            C(
                "issue");

        var satisfactionLabel =
            C(
                "satisfactionLabel");

        var activity =
            C(
                "activity",
                "Taking some time to recover");

        var statId =
            C(
                "statId");

        var fromTown =
            C(
                "fromTown",
                "our old town");

        var toTown =
            C(
                "toTown",
                "our new town");

        return candidate.WordingKey switch
        {
            "fallback" =>
                voice switch
                {
                    ThoughtVoice.Child =>
                        [
                            "I wonder what we'll do today.",
                            "I hope something nice happens today.",
                            "I wonder what everyone is doing."
                        ],

                    ThoughtVoice.Adolescent =>
                        [
                            "I wonder what this year will be like.",
                            "Things feel pretty normal right now.",
                            "I wonder what's going to happen next."
                        ],

                    ThoughtVoice.AdultRough =>
                        [
                            "Things are fine.",
                            "Nothing much going on.",
                            "Life's normal enough."
                        ],

                    ThoughtVoice.AdultElaborate =>
                        [
                            "Life has been relatively uneventful lately.",
                            "For once, there is remarkably little demanding my attention.",
                            "Things have settled into a fairly ordinary rhythm."
                        ],

                    _ =>
                        [
                            "Things are pretty ordinary right now.",
                            "Life feels fairly normal at the moment.",
                            "Nothing especially unusual is happening right now."
                        ]
                },

            "family_relation.close" =>
                voice switch
                {
                    ThoughtVoice.Child =>
                        [$"I really like being with my {relation}.", $"My {relation} and I get along really well."],
                    ThoughtVoice.Adolescent =>
                        [$"My {relation} and I have always been close.", $"I can usually count on my {relation}."],
                    ThoughtVoice.AdultRough =>
                        [$"Me and my {relation} are close.", $"I can count on my {relation}."],
                    ThoughtVoice.AdultElaborate =>
                        [$"My {relation} and I have maintained a genuinely close bond.", $"I value how dependable my relationship with my {relation} has become."],
                    _ =>
                        [$"My {relation} and I have always been close.", $"I'm glad my {relation} and I can rely on each other."]
                },

            "family_relation.strained" =>
                voice switch
                {
                    ThoughtVoice.Child =>
                        [$"I don't really get along with my {relation}.", $"Things feel bad between me and my {relation}."],
                    ThoughtVoice.Adolescent =>
                        [$"My {relation} and I never seem to get along.", $"Things are still tense with my {relation}."],
                    ThoughtVoice.AdultRough =>
                        [$"Me and my {relation} don't get along.", $"Still can't stand dealing with my {relation}."],
                    ThoughtVoice.AdultElaborate =>
                        [$"My relationship with my {relation} remains painfully strained.", $"There is still too much hostility between my {relation} and me."],
                    _ =>
                        [$"My {relation} and I never seem to get along.", $"Things are still strained between me and my {relation}."]
                },

            "family_relation.improved" =>
                voice switch
                {
                    ThoughtVoice.Child =>
                        [$"It was nice spending time with my {relation} again."],
                    ThoughtVoice.Adolescent =>
                        [$"It was actually good spending time with my {relation} again."],
                    ThoughtVoice.AdultRough =>
                        [$"Good to spend some time with my {relation} again."],
                    ThoughtVoice.AdultElaborate =>
                        [$"It was genuinely good to spend time with my {relation} again and mend things a little."],
                    _ =>
                        [$"It was good spending time with my {relation} again."]
                },

            "family.loss.current" =>
                voice switch
                {
                    ThoughtVoice.Child =>
                        [
                            $"{relation}'s gone. I miss {relationPossessive}.",
                            $"I keep thinking about {relation}. I miss {relationPossessive}.",
                            $"I wish {relation} was still here."
                        ],

                    ThoughtVoice.Adolescent =>
                        [
                            $"I still can't believe {relation} is really gone.",
                            $"It doesn't feel real that {relation} is gone.",
                            $"I keep thinking about losing {relation}."
                        ],

                    ThoughtVoice.AdultRough =>
                        [
                            $"{relation}'s gone. I miss {relationPossessive}.",
                            $"Still can't believe {relation}'s gone.",
                            $"I miss {relation}. Badly."
                        ],

                    ThoughtVoice.AdultElaborate =>
                        [
                            $"Losing {relation} has left an absence I still don't know how to live with.",
                            $"The death of {relation} has changed the shape of my life in ways I am only beginning to understand.",
                            $"I keep returning to the fact that {relation} is gone; the loss still feels painfully immediate."
                        ],

                    _ =>
                        [
                            $"I still can't believe {relation} is gone.",
                            $"I can't stop thinking about losing {relation}.",
                            $"I miss {relation} more than I expected."
                        ]
                },

            "family.bereavement.recent" =>
                voice switch
                {
                    ThoughtVoice.Child =>
                        [
                            "I still miss them.",
                            "I still think about them a lot.",
                            "I wish they were still here."
                        ],

                    ThoughtVoice.Adolescent =>
                        [
                            "I still think about them all the time.",
                            "The loss still hurts more than I want to admit.",
                            "I keep missing them when I least expect it."
                        ],

                    ThoughtVoice.AdultRough =>
                        [
                            "Still miss them.",
                            "Still thinking about the loss.",
                            "It still hurts."
                        ],

                    ThoughtVoice.AdultElaborate =>
                        [
                            "The loss is no longer new, but it still weighs heavily on me.",
                            "Time has passed, yet the grief still surfaces with surprising force.",
                            "I am learning to live around the loss, though it has not truly left me."
                        ],

                    _ =>
                        [
                            "I'm still grieving.",
                            "The loss still weighs on me.",
                            "I still find myself thinking about them."
                        ]
                },

            "orphan.new" =>
                voice switch
                {
                    ThoughtVoice.Child =>
                        [
                            "Mum and Dad are gone. Who's going to look after me?",
                            "I don't have Mum or Dad anymore. What happens now?",
                            "I just want Mum and Dad back."
                        ],

                    _ =>
                        [
                            "Both my parents are gone. I don't know what happens to me now.",
                            "I have no parents left. Everything feels uncertain.",
                            "Losing both my parents has left me wondering where I belong."
                        ]
                },

            "orphan.ongoing" =>
                voice switch
                {
                    ThoughtVoice.Child =>
                        [
                            "I wish my parents were still here.",
                            "I still miss Mum and Dad.",
                            "I wish I could have my parents back."
                        ],

                    _ =>
                        [
                            "I still wish I could have my parents back.",
                            "I still feel the absence of my parents.",
                            "Not having my parents here still hurts."
                        ]
                },

            "orphanage" =>
                voice switch
                {
                    ThoughtVoice.Child =>
                        [
                            "I don't like it here. I want a home.",
                            "I wish I lived with a family.",
                            "I want somewhere that feels like home."
                        ],

                    _ =>
                        [
                            "I hate living in the orphanage. I just want somewhere that feels like home.",
                            "I want a real home, not another year in the orphanage.",
                            "Living here still doesn't feel like belonging anywhere."
                        ]
                },

            "placement.new" =>
                voice switch
                {
                    ThoughtVoice.Child =>
                        [
                            "I'm living with a new family now.",
                            "They took me into their home.",
                            "Maybe this can be my home now."
                        ],

                    _ =>
                        [
                            "They took me into their household. Maybe things will finally settle down.",
                            "I'm living with a new family now. I hope this can become home.",
                            "Being taken in has made things feel a little less uncertain."
                        ]
                },

            "placement.ongoing" =>
                voice switch
                {
                    ThoughtVoice.Child =>
                        [
                            "This is my home now.",
                            "I'm getting used to this family.",
                            "I think this feels more like home now."
                        ],

                    _ =>
                        [
                            "I'm getting used to living with this family.",
                            "This household is starting to feel familiar.",
                            "I'm slowly settling into life with this family."
                        ]
                },

            "orphan.independent" =>
                [
                    "I'm having to look after myself now.",
                    "I have to manage on my own more than I should.",
                    "Looking after myself this young is harder than I expected."
                ],

            "parents.divorced" =>
                voice switch
                {
                    ThoughtVoice.Child =>
                        [
                            "I wish Mum and Dad were still together.",
                            "I don't like that Mum and Dad split up.",
                            "Things felt better when Mum and Dad were together."
                        ],

                    _ =>
                        [
                            "Things haven't felt the same since my parents split up.",
                            "I still think about my parents breaking up.",
                            "My parents' divorce changed more than I expected."
                        ]
                },

            "marriage.new" =>
                voice switch
                {
                    ThoughtVoice.AdultRough =>
                        [
                            "I'm married. Feels good.",
                            "Got married. I'm happy.",
                            "We're married now. Good."
                        ],

                    ThoughtVoice.AdultElaborate =>
                        [
                            "Beginning this marriage feels like the start of an important new part of my life.",
                            "Marriage has given this year a sense of genuine new beginning.",
                            "Starting married life feels both momentous and deeply hopeful."
                        ],

                    _ =>
                        [
                            "Getting married has made this a wonderful year.",
                            "I'm happy to be starting married life.",
                            "Being married feels like the beginning of something important."
                        ]
                },

            "divorce.current" =>
                voice switch
                {
                    ThoughtVoice.AdultRough =>
                        [
                            "Marriage is over. Still hurts.",
                            "It's over. Hate thinking about it.",
                            "We're done. It still stings."
                        ],

                    ThoughtVoice.AdultElaborate =>
                        [
                            "The end of the marriage still occupies my thoughts more than I would like.",
                            "The marriage is over, but I am still trying to understand what that ending means for my life.",
                            "Separation has left a difficult mixture of relief, grief and uncertainty."
                        ],

                    _ =>
                        [
                            "I can't stop thinking about how our marriage ended.",
                            "The divorce still hurts.",
                            "I'm still trying to come to terms with the marriage ending."
                        ]
                },

            "divorce.recent" =>
                voice switch
                {
                    ThoughtVoice.AdultRough =>
                        [
                            "Still thinking about the divorce.",
                            "Marriage is over. Still hurts.",
                            "Haven't really moved on yet."
                        ],

                    ThoughtVoice.AdultElaborate =>
                        [
                            "The divorce is no longer new, yet it continues to shape how I think about the future.",
                            "I am moving forward, though the end of the marriage still occupies a great deal of my mind.",
                            "The marriage has ended, but emotionally I have not entirely left it behind."
                        ],

                    _ =>
                        [
                            "I'm still thinking about the divorce.",
                            "I haven't completely moved on from the marriage ending.",
                            "The divorce is still difficult to put behind me."
                        ]
                },

            "affair.victim" =>
                [
                    "I can't believe they betrayed me.",
                    "I keep thinking about the betrayal.",
                    "I don't know how to trust them after this."
                ],

            "affair.actor" =>
                [
                    "My affair destroyed the marriage.",
                    "I made a mess of the marriage.",
                    "I keep thinking about what my affair cost us."
                ],

            "marriage.repaired" =>
                voice switch
                {
                    ThoughtVoice.AdultRough =>
                        [
                            "We're doing better now.",
                            "Things are better between us.",
                            "Maybe we can fix this."
                        ],

                    ThoughtVoice.AdultElaborate =>
                        [
                            "For the first time in a while, it feels as though we're finding our way back to each other.",
                            "There is finally a sense that the marriage may be recovering rather than merely surviving.",
                            "The effort we have put into the marriage is beginning to feel worthwhile."
                        ],

                    _ =>
                        [
                            "Things between us finally seem to be improving.",
                            "Our marriage feels better than it did.",
                            "It feels like we're finding our way back to each other."
                        ]
                },

            "marriage.satisfaction" =>
                MarriageIssueVariants(
                    voice,
                    issue,
                    satisfactionLabel),

            "birth.parent" =>
                voice switch
                {
                    ThoughtVoice.AdultRough =>
                        [
                            "The baby's here. I'm happy.",
                            "We had a baby. Feels good.",
                            "New baby. I'm glad."
                        ],

                    ThoughtVoice.AdultElaborate =>
                        [
                            "Welcoming a new child into the family has made this an extraordinary year.",
                            "The arrival of our child has given this year an entirely new significance.",
                            "Having a new child has changed the emotional center of our household overnight."
                        ],

                    _ =>
                        [
                            "I'm so happy about the new baby.",
                            "Having a new baby has made this a wonderful year.",
                            "I can't stop thinking about the new baby."
                        ]
                },

            "birth.sibling" =>
                voice switch
                {
                    ThoughtVoice.Child =>
                        [
                            "I have a new baby in the family!",
                            "There's a new baby at home!",
                            "I wonder what the new baby will be like."
                        ],

                    _ =>
                        [
                            "There's a new baby in the family now.",
                            "Having a new sibling is going to change things at home.",
                            "The family feels different with a new baby here."
                        ]
                },

            "depression" =>
                voice switch
                {
                    ThoughtVoice.Child =>
                        [
                            $"This {condition} makes everything feel sad lately.",
                            $"I don't feel happy when this {condition} gets bad."
                        ],

                    ThoughtVoice.Adolescent =>
                        [
                            $"This {condition} makes everything feel heavier than it should.",
                            $"I can't seem to get out from under this {condition}."
                        ],

                    ThoughtVoice.AdultRough =>
                        [
                            $"This {condition} is bad. Don't want to do much.",
                            $"Can't shake this {condition}."
                        ],

                    ThoughtVoice.AdultElaborate =>
                        [
                            $"The {condition} colors even ordinary days with an exhausting sense of weight.",
                            $"Living with {condition} has made it increasingly difficult to summon interest in ordinary things."
                        ],

                    _ =>
                        [
                            $"I can't seem to shake this {condition}.",
                            $"This {condition} has made everything feel heavy lately."
                        ]
                },

            "anxiety" =>
                voice switch
                {
                    ThoughtVoice.Child =>
                        [
                            $"This {condition} keeps making me worry something bad will happen.",
                            $"I feel worried a lot because of this {condition}."
                        ],

                    ThoughtVoice.Adolescent =>
                        [
                            $"This {condition} keeps my mind finding new things to worry about.",
                            $"I feel tense all the time with this {condition}."
                        ],

                    ThoughtVoice.AdultRough =>
                        [
                            $"This {condition} won't let my head settle down.",
                            $"Can't stop worrying with this {condition}."
                        ],

                    ThoughtVoice.AdultElaborate =>
                        [
                            $"The {condition} keeps turning ordinary uncertainties into persistent concerns.",
                            $"Living with {condition} leaves me anticipating problems long before there is reason to expect them."
                        ],

                    _ =>
                        [
                            $"This {condition} keeps me worrying about everything that could go wrong.",
                            $"I can't seem to stop worrying while this {condition} persists."
                        ]
                },

            "alcoholism" =>
                voice switch
                {
                    ThoughtVoice.AdultRough =>
                        [
                            $"This {condition} is hard to stop.",
                            $"The {condition} keeps getting worse."
                        ],

                    ThoughtVoice.AdultElaborate =>
                        [
                            $"The {condition} is becoming increasingly difficult to ignore or control.",
                            $"I understand what this {condition} is doing to me, but changing it remains painfully difficult."
                        ],

                    _ =>
                        [
                            $"This {condition} is getting harder to control.",
                            $"I know I need to get this {condition} under control."
                        ]
                },

            "drug_dependence" =>
                voice switch
                {
                    ThoughtVoice.AdultRough =>
                        [
                            $"This {condition} is hard to stop.",
                            $"The {condition} keeps pulling me back."
                        ],

                    ThoughtVoice.AdultElaborate =>
                        [
                            $"The {condition} is becoming increasingly difficult to control.",
                            $"I understand what this {condition} is doing to me, but breaking the dependence remains painfully difficult."
                        ],

                    _ =>
                        [
                            $"This {condition} is getting harder to control.",
                            $"I know I need to get this {condition} under control."
                        ]
                },

            "health.terminal" =>
                HealthVariants(
                    voice,
                    condition,
                    terminal:
                        true),

            "health.permanent" =>
                ConditionVariants(
                    voice,
                    condition,
                    "has been bothering me a lot lately"),

            "health.curable" =>
                HealthVariants(
                    voice,
                    condition,
                    terminal:
                        false),

            "health.minor" =>
                voice switch
                {
                    ThoughtVoice.Child =>
                        [
                            "I don't feel good.",
                            "I wish I wasn't sick.",
                            "I feel yucky today."
                        ],

                    ThoughtVoice.Adolescent =>
                        [
                            "I'm tired of feeling sick.",
                            "Being ill is getting really annoying.",
                            "I wish I'd just feel normal again."
                        ],

                    ThoughtVoice.AdultRough =>
                        [
                            "Feel awful today.",
                            "Sick again. Hate it.",
                            "Don't feel good at all."
                        ],

                    ThoughtVoice.AdultElaborate =>
                        [
                            "Being unwell for this long is becoming increasingly exhausting.",
                            "Even this comparatively minor illness is wearing down my patience.",
                            "I had underestimated how tiring it would be to feel unwell for so long."
                        ],

                    _ =>
                        [
                            "This illness is really wearing me down.",
                            "I'm tired of feeling sick.",
                            "I wish I could just feel well again."
                        ]
                },

            "health.poor" =>
                voice switch
                {
                    ThoughtVoice.Child =>
                        ["I feel really bad.", "I don't feel well at all."],

                    ThoughtVoice.Adolescent =>
                        ["My health has been really bad lately.", "I feel awful most of the time."],

                    ThoughtVoice.AdultRough =>
                        ["Health's bad. Really bad.", "I feel awful lately."],

                    ThoughtVoice.AdultElaborate =>
                        ["My health has deteriorated enough that it is becoming difficult to think about much else.",
                         "Feeling this physically unwell is beginning to dominate my daily life."],

                    _ =>
                        ["My health has been worrying me.", "I feel seriously unwell lately."]
                },

            "therapy.success" =>
                voice switch
                {
                    ThoughtVoice.AdultRough =>
                        ["Therapy helped. Feel better.", "That actually helped."],

                    ThoughtVoice.AdultElaborate =>
                        ["The therapy seems to have helped me regain some control over how I've been feeling.",
                         "The therapy has given me a clearer sense that things can improve."],

                    _ =>
                        ["I feel like therapy really helped me.", "Therapy seems to have made a real difference."]
                },

            "therapy.failure" =>
                ["Therapy didn't seem to help much.", "I hoped therapy would help more than it did."],

            "recover" =>
                voice switch
                {
                    ThoughtVoice.Child =>
                        ["Resting has helped me feel a little better."],

                    ThoughtVoice.Adolescent =>
                        ["Taking time to recover was probably a good idea."],

                    ThoughtVoice.AdultRough =>
                        [$"{activity} helped.", "Needed that rest."],

                    ThoughtVoice.AdultElaborate =>
                        [$"{activity} was precisely the pause I needed.",
                         "Taking deliberate time to recover has done more for me than I expected."],

                    _ =>
                        [$"{activity} was exactly what I needed.",
                         "Taking some time to recover has helped."]
                },

            "heal" =>
                ["I'm finally starting to feel better.", "I think my health is finally improving."],

            "justice.new" =>
                voice switch
                {
                    ThoughtVoice.AdultRough =>
                        ["I'm stuck in prison now.", "Prison. This is bad."],

                    ThoughtVoice.AdultElaborate =>
                        ["The reality of losing so much of my life to imprisonment is finally sinking in.",
                         "The consequences of imprisonment now feel painfully concrete."],

                    _ =>
                        ["I can't believe I'm going to spend this time in prison.",
                         "Being sent to prison is difficult to comprehend."]
                },

            "justice.imprisoned" =>
                voice switch
                {
                    ThoughtVoice.AdultRough =>
                        ["I want out of here.", "Hate being stuck here."],

                    ThoughtVoice.AdultElaborate =>
                        ["The confinement is becoming unbearable; every year spent here feels painfully wasted.",
                         "Imprisonment has reduced life to an exhausting repetition of confinement."],

                    _ =>
                        ["I can't stand being stuck in prison.", "I just want to be free again."]
                },

            "justice.released" =>
                voice switch
                {
                    ThoughtVoice.AdultRough =>
                        ["Free at last.", "Finally out."],

                    ThoughtVoice.AdultElaborate =>
                        ["Being free again feels almost unfamiliar after so much time in confinement.",
                         "Freedom feels unexpectedly strange after living under confinement."],

                    _ =>
                        ["It's good to finally be free.", "I can't believe I'm finally out."]
                },

            "wrongful.arrest" =>
                ["I was arrested for something I didn't do.",
                 "I still can't believe they detained me when I was innocent."],

            "career.miserable" =>
                voice switch
                {
                    ThoughtVoice.AdultRough =>
                        ["Work's awful. Hate it.", "Hate this job.", "Work is miserable."],

                    ThoughtVoice.AdultElaborate =>
                        ["My work has become deeply frustrating, and I increasingly dread having to return to it.",
                         "The job has become so unpleasant that it now colors the rest of my week.",
                         "I find myself resenting the work more strongly with every passing month."],

                    _ =>
                        ["I hate going to work.", "I'm miserable at work.", "Work has become awful lately."]
                },

            "career.unhappy" =>
                voice switch
                {
                    ThoughtVoice.AdultRough =>
                        ["Don't like this job much.", "Work's getting on my nerves."],

                    ThoughtVoice.AdultElaborate =>
                        ["My work is increasingly unsatisfying, even if it has not become unbearable.",
                         "I am finding less and less satisfaction in my work."],

                    _ =>
                        ["I'm really not enjoying this job.", "Work has been frustrating lately."]
                },

            "career.satisfied" =>
                ["Work is going pretty well.", "I'm fairly happy with how work is going."],

            "career.thriving" =>
                voice switch
                {
                    ThoughtVoice.AdultRough =>
                        ["Work's good. I like it.", "Job's going great."],

                    ThoughtVoice.AdultElaborate =>
                        ["I find my work genuinely rewarding; it gives me a welcome sense of progress.",
                         "My work has become one of the more satisfying parts of my life."],

                    _ =>
                        ["Work is going really well.", "I'm genuinely enjoying my work."]
                },

            "career.fired" =>
                voice switch
                {
                    ThoughtVoice.AdultRough =>
                        ["Got fired. Great.", "Lost my job. Bad timing."],

                    ThoughtVoice.AdultElaborate =>
                        ["Losing my job so abruptly has forced me to reconsider what I do next.",
                         "Being dismissed has unsettled both my finances and my sense of direction."],

                    _ =>
                        ["Getting fired has really set me back.", "I can't believe I lost my job."]
                },

            "career.jobloss" =>
                ["I'm still trying to recover from losing my job.",
                 "Losing my job is still causing problems."],

            "career.employment" =>
                voice switch
                {
                    ThoughtVoice.AdultRough =>
                        ["Got a job. Good.", "Found work. Finally."],

                    ThoughtVoice.AdultElaborate =>
                        ["Finding work has given me some welcome stability and a clearer sense of direction.",
                         "Securing employment has restored a useful sense of stability."],

                    _ =>
                        ["I'm glad I finally found work.", "Finding a job is a huge relief."]
                },

            "career.promotion" =>
                voice switch
                {
                    ThoughtVoice.AdultRough =>
                        ["Got promoted. Finally.", "Promotion. Good."],

                    ThoughtVoice.AdultElaborate =>
                        ["The promotion feels like welcome recognition for the effort I've put into my career.",
                         "Advancing at work feels like meaningful recognition of the effort I have invested."],

                    _ =>
                        ["I got promoted. Things are going well at work.", "I'm really pleased about the promotion."]
                },

            "career.quit" =>
                ["Leaving that job was probably the right decision.",
                 "I'm relieved I finally left that job."],

            "career.retirement.new" =>
                ["Retirement is going to take some getting used to.",
                 "It's strange knowing I don't have to go back to work."],

            "career.retirement" =>
                ["Retirement has become part of my routine.",
                 "I'm getting used to retired life."],

            "career.unemployed" =>
                voice switch
                {
                    ThoughtVoice.AdultRough =>
                        ["Need a job.", "Need work soon.", "Can't stay unemployed."],

                    ThoughtVoice.AdultElaborate =>
                        ["Being without work is becoming increasingly difficult; I need to find something soon.",
                         "Unemployment is becoming a growing source of uncertainty and pressure."],

                    _ =>
                        ["I really need to find work.", "Being unemployed is getting difficult."]
                },

            "education.success" =>
                voice switch
                {
                    ThoughtVoice.Child =>
                        ["I learned something new!", "I did really well with my lessons!"],

                    ThoughtVoice.Adolescent =>
                        ["I'm actually doing pretty well with my studies.", "My studying is finally paying off."],

                    _ =>
                        ["The studying paid off.", "I'm pleased that the studying went well."]
                },

            "education.failure" =>
                voice switch
                {
                    ThoughtVoice.Child =>
                        ["That was too hard.", "I tried, but I still didn't get it."],

                    ThoughtVoice.Adolescent =>
                        ["I worked on it, but I still couldn't get it right.", "My studies didn't go as well as I wanted."],

                    _ =>
                        ["The course didn't go as well as I'd hoped.", "I put in the effort, but it didn't work out."]
                },

            "economy.broke" =>
                voice switch
                {
                    ThoughtVoice.Child =>
                        ["Mum says we don't have much money.", "Everyone keeps saying money is tight."],

                    ThoughtVoice.Adolescent =>
                        ["Money is always tight at home. Everyone worries about it.", "The money problems at home are hard to ignore."],

                    ThoughtVoice.AdultRough =>
                        ["We're broke. Need money.", "No money. This is bad."],

                    ThoughtVoice.AdultElaborate =>
                        ["Our finances are becoming precarious; every expense now feels consequential.",
                         "Our financial position has become fragile enough that every unexpected cost matters."],

                    _ =>
                        ["We're running out of money, and it's getting hard.", "Money problems are becoming serious."]
                },

            "loan.negative_balance" =>
                voice switch
                {
                    ThoughtVoice.AdultRough =>
                        ["Don't know how we'll pay all this back.", "This debt keeps getting worse."],

                    ThoughtVoice.AdultElaborate =>
                        ["I do not know how we are going to repay all of this; the debt now hangs over every financial decision.",
                         "Our debts have pushed the household below zero, and recovering from that position is going to take time."],

                    _ =>
                        ["I don't know how we're going to pay all this back.",
                         "The debt is getting hard to ignore."]
                },

            "loan.large_payment" =>
                voice switch
                {
                    ThoughtVoice.AdultRough =>
                        ["That loan eats too much of our money.", "The loan payment hurts every year."],

                    ThoughtVoice.AdultElaborate =>
                        ["That loan is taking a painful share of our household income every year.",
                         "The annual repayment is large enough to constrain nearly every other financial choice we make."],

                    _ =>
                        ["That loan is taking a painful chunk of our income every year.",
                         "The yearly loan payment is becoming a real burden."]
                },

            "loan.final_repayment" =>
                voice switch
                {
                    ThoughtVoice.AdultRough =>
                        ["Finally. That debt's gone.", "Paid it off at last."],

                    ThoughtVoice.AdultElaborate =>
                        ["At last, the debt is fully repaid; it is a relief to have that obligation behind us.",
                         "The final payment is made. Our finances can finally move forward without that debt hanging over them."],

                    _ =>
                        ["At last, we're free of that debt.",
                         "The loan is finally paid off. What a relief."]
                },

            "loan.receivable_income" =>
                voice switch
                {
                    ThoughtVoice.AdultRough =>
                        ["That money I lent is paying back nicely.", "The loan income helps."],

                    ThoughtVoice.AdultElaborate =>
                        ["The money I lent out is proving worthwhile; the repayments provide a useful stream of income.",
                         "The loan I made continues to return money to the household, which was precisely the point of lending it."],

                    _ =>
                        ["The money I lent out is proving worthwhile.",
                         "Those loan repayments are a useful bit of income."]
                },

            "household.strain" =>
                voice switch
                {
                    ThoughtVoice.Child =>
                        ["It's always noisy here.", "There's never any quiet at home."],

                    ThoughtVoice.Adolescent =>
                        ["There's never any peace in this house.", "This house is always chaotic."],

                    ThoughtVoice.AdultRough =>
                        ["Too much going on in this house.", "This house is exhausting."],

                    ThoughtVoice.AdultElaborate =>
                        ["The constant demands of such a crowded household are becoming difficult to manage.",
                         "The household requires so much constant attention that it is becoming genuinely exhausting."],

                    _ =>
                        ["This household is exhausting.", "There's too much to manage at home lately."]
                },

            "household.overcrowded" =>
                voice switch
                {
                    ThoughtVoice.Child =>
                        ["There never seems to be enough room or quiet at home.",
                         "It's hard to find any space for myself here."],

                    ThoughtVoice.Adolescent =>
                        ["The house feels crowded all the time.",
                         "There are too many people under one roof."],

                    ThoughtVoice.AdultRough =>
                        ["Too many of us are packed into this house.",
                         "We need more room in this place."],

                    ThoughtVoice.AdultElaborate =>
                        ["There are simply too many of us sharing this home comfortably.",
                         "The lack of space and privacy in this household is becoming difficult to bear."],

                    _ =>
                        ["There are too many of us sharing this home.",
                         "This household has become far too crowded."]
                },

            "role.family_nanny.started" =>
                voice switch
                {
                    ThoughtVoice.AdultRough =>
                        ["I'm looking after the kids now.", "I'll be caring for the younger kids."],

                    ThoughtVoice.AdultElaborate =>
                        ["I have taken on the responsibility of caring for the younger children in the household.",
                         "For now, I will be devoting much of my time to helping care for the younger children."],

                    _ =>
                        ["I'm going to help look after the younger children.",
                         "I've agreed to take care of the younger children for now."]
                },

            "role.family_nanny.ended" =>
                voice switch
                {
                    ThoughtVoice.AdultRough =>
                        ["I'm done looking after the kids now.", "That childcare work is over now."],

                    ThoughtVoice.AdultElaborate =>
                        ["My period of looking after the younger children has come to an end.",
                         "The responsibility of caring for the younger children is no longer mine."],

                    _ =>
                        ["I'm no longer the one looking after the younger children.",
                         "My time helping as the family caregiver has come to an end."]
                },

            "role.family_nanny" =>
                voice switch
                {
                    ThoughtVoice.AdultRough =>
                        ["I'm looking after the kids now.", "Kids need watching. That's my job now."],

                    ThoughtVoice.AdultElaborate =>
                        ["For now, much of my time is devoted to caring for the younger children in the household.",
                         "Caring for the younger children has become the central responsibility of my days."],

                    _ =>
                        ["I'm spending my days looking after the children.",
                         "Looking after the younger children is taking most of my time."]
                },

            "role.nanny" =>
                ["I'm spending my days caring for the children.",
                 "Looking after the children keeps me busy."],

            "role.housewife" =>
                ["There's always something that needs doing at home.",
                 "Keeping the household running takes most of my attention."],

            "household.moved" =>
                voice switch
                {
                    ThoughtVoice.Child =>
                        [
                            $"We moved from {fromTown} to {toTown}. Everything feels different here.",
                            $"I still keep thinking about {fromTown}. Now we live in {toTown}.",
                            $"Our new home is in {toTown}. I wonder how long it will take to feel normal."
                        ],

                    ThoughtVoice.Adolescent =>
                        [
                            $"Moving from {fromTown} to {toTown} has changed almost everything around me.",
                            $"I am still getting used to life in {toTown} after leaving {fromTown}.",
                            $"A new town, a new routine. {toTown} still feels unfamiliar."
                        ],

                    ThoughtVoice.AdultRough =>
                        [
                            $"We moved to {toTown}. Big change.",
                            $"Left {fromTown}. Now we have to settle in {toTown}.",
                            $"New town, new life. Still getting used to {toTown}."
                        ],

                    ThoughtVoice.AdultElaborate =>
                        [
                            $"Leaving {fromTown} for {toTown} has unsettled the familiar rhythm of our household more than I expected.",
                            $"The move to {toTown} marks a substantial change in our lives; I am still learning the shape of this new place.",
                            $"We have exchanged {fromTown} for {toTown}, and the practical consequences of that decision are only beginning to settle."
                        ],

                    _ =>
                        [
                            $"Moving from {fromTown} to {toTown} was a major change. I am still settling in.",
                            $"Life feels different now that we live in {toTown} instead of {fromTown}.",
                            $"The move to {toTown} is still on my mind. There is a lot to get used to."
                        ]
                },

            "craft.learned" =>
                [$"I've been learning {C("craftName", "a craft")}."],

            "craft.self_employment_started" =>
                [$"I've started earning a living through {C("craftName", "my craft")}."],

            "craft.work" =>
                C("performance", "ordinary").ToLowerInvariant() switch
                {
                    "strong" =>
                        ["Business has been unusually good this year."],
                    "poor" =>
                        ["Work for my trade has been painfully scarce."],
                    _ => voice switch
                    {
                        ThoughtVoice.AdultRough =>
                            ["Work's been coming in."],
                        ThoughtVoice.AdultElaborate =>
                            ["I've been fortunate enough to maintain a steady stream of work in my trade."],
                        _ =>
                            ["I've had a steady amount of work for my craft lately."]
                    }
                },

            "farming.work" =>
                C("performance", "ordinary").ToLowerInvariant() switch
                {
                    "strong" =>
                        ["The land produced unusually well this year."],
                    "poor" =>
                        ["The farm hardly produced anything this year."],
                    _ => voice switch
                    {
                        ThoughtVoice.Child =>
                            ["I've been helping with the farm."],
                        ThoughtVoice.Adolescent =>
                            ["There's always work to do on the land."],
                        ThoughtVoice.AdultRough =>
                            ["Farm work keeps me busy."],
                        ThoughtVoice.AdultElaborate =>
                            ["A good portion of my time lately has gone into keeping the family's land productive."],
                        _ =>
                            ["I've been spending plenty of time working the family's land."]
                    }
                },

            "property.bought" =>
                ["I'm pleased we were able to buy another house.", "Buying the house feels like a real step forward."],

            "property.sold" =>
                ["Selling the house changed our finances quite a bit.", "I'm still thinking about selling that house."],

            "property.given" =>
                ["My father gave me a house.", "Receiving that house gives me a much stronger start."],

            "property.promised" =>
                ["I've been promised a house when the time comes.", "Knowing a house is waiting for me changes how I think about the future."],

            "property.received" =>
                ["The house that was promised to me is finally mine.", "I finally received the house that had been promised to me."],

            "inheritance" =>
                voice switch
                {
                    ThoughtVoice.AdultRough =>
                        ["Got an inheritance. That helps.", "Inheritance helps. A lot."],

                    ThoughtVoice.AdultElaborate =>
                        ["The inheritance has changed our financial position far more than I expected.",
                         "Receiving the inheritance has materially altered what is possible for us."],

                    _ =>
                        ["That inheritance has made life considerably easier.", "The inheritance has changed our finances a lot."]
                },

            "support.success" =>
                ["I'm relieved they agreed to help me with money.", "Their financial help is a real relief."],

            "support.failure" =>
                ["I asked for help, but they refused.", "I needed help with money, but they said no."],

            "improvement" =>
                ImprovementVariants(
                    voice,
                    statId,
                    C(
                        "previousValue"),
                    C(
                        "newValue")),

            "rare.lottery" =>
                voice switch
                {
                    ThoughtVoice.AdultRough =>
                        ["I won! I'm rich!", "Won the lottery. Can't believe it."],

                    ThoughtVoice.AdultElaborate =>
                        ["Winning the lottery has changed our circumstances so suddenly that it still hardly feels real.",
                         "The lottery win has transformed our finances with almost absurd suddenness."],

                    _ =>
                        ["I still can't believe I won the lottery.", "Winning the lottery still doesn't feel real."]
                },

            "rare.fire" =>
                voice switch
                {
                    ThoughtVoice.Child =>
                        ["The house caught fire. I was really scared.", "I keep thinking about the fire."],

                    ThoughtVoice.Adolescent =>
                        ["I keep thinking about the fire. It could have been much worse.", "That fire still scares me when I think about it."],

                    _ =>
                        ["That fire could have destroyed everything.", "I keep thinking about how bad the fire could have been."]
                },

            "rare.assault" =>
                voice switch
                {
                    ThoughtVoice.Adolescent =>
                        ["I keep thinking about being attacked.", "I'm still angry about the attack."],

                    ThoughtVoice.AdultRough =>
                        ["Got attacked. Still angry about it.", "Still thinking about that attack."],

                    ThoughtVoice.AdultElaborate =>
                        ["I keep replaying the attack in my mind, even though it's already over.",
                         "The attack is over, but the experience still intrudes on my thoughts."],

                    _ =>
                        ["I keep thinking about the assault.", "I'm still shaken by being attacked."]
                },

            "rare.accident" =>
                voice switch
                {
                    ThoughtVoice.Child =>
                        ["That accident really scared me.", "I keep thinking about getting hurt."],

                    ThoughtVoice.Adolescent =>
                        ["I keep thinking about the accident.", "That accident could have been much worse."],

                    _ =>
                        ["I keep thinking about the accident.", "I'm still shaken by what happened."]
                },

            "rare.burglary" =>
                ["I still feel uneasy after the burglary.", "Knowing someone broke into our home is hard to forget."],

            "rare.fraud" =>
                ["I'm furious that I was tricked out of that money.", "I can't believe I fell for that fraud."],

            "rare.inheritance" =>
                ["That unexpected inheritance changed things quickly.", "I never expected money from such a distant relative."],

            "rare.found" =>
                ["Finding something that valuable was an incredible stroke of luck.", "I still can't believe what I found."],

            "recent.assault" =>
                voice switch
                {
                    ThoughtVoice.Adolescent =>
                        ["I'm still nervous after what happened.", "I'm still uneasy after the attack."],

                    ThoughtVoice.AdultRough =>
                        ["Still thinking about that attack.", "Still on edge after what happened."],

                    ThoughtVoice.AdultElaborate =>
                        ["The immediate danger has passed, but the experience still leaves me uneasy.",
                         "The event is over, yet I remain more watchful than I was before."],

                    _ =>
                        ["I'm still shaken by being attacked.", "I'm still uneasy after what happened."]
                },

            "life.adult" =>
                voice switch
                {
                    ThoughtVoice.AdultRough =>
                        ["I'm an adult now.", "Adult now. Things change."],

                    ThoughtVoice.AdultElaborate =>
                        ["Reaching adulthood feels like the beginning of a much more consequential part of my life.",
                         "Adulthood suddenly makes the future feel far more immediate and consequential."],

                    _ =>
                        ["I'm an adult now. Things are going to change.", "Becoming an adult feels like a real turning point."]
                },

            _ =>
                ["Things are pretty ordinary right now."]
        };
    }

}
