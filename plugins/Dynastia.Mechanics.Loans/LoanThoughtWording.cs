using Dynastia.Contracts;

namespace Dynastia.Mechanics.Loans;

internal static class LoanThoughtWording
{
    public static void Register(IGamePluginContext context)
    {
        var registry = context.GetService<IThoughtWordingRegistry>()
            ?? throw new InvalidOperationException("Thought wording registry is unavailable.");

        registry.RegisterCatalogue(
            "dynastia.loans",
            new Dictionary<string, ThoughtWordingDefinition>(StringComparer.OrdinalIgnoreCase)
            {
                ["loan.negative_balance"] = new()
                {
                    AdultRough = ["Don't know how we'll pay all this back.", "This debt keeps getting worse."],
                    AdultElaborate = ["I do not know how we are going to repay all of this; the debt now hangs over every financial decision.", "Our debts have pushed the household below zero, and recovering from that position is going to take time."],
                    AdultNormal = ["I don't know how we're going to pay all this back.", "The debt is getting hard to ignore."]
                },
                ["loan.large_payment"] = new()
                {
                    AdultRough = ["That loan eats too much of our money.", "The loan payment hurts every year."],
                    AdultElaborate = ["That loan is taking a painful share of our household income every year.", "The annual repayment is large enough to constrain nearly every other financial choice we make."],
                    AdultNormal = ["That loan is taking a painful chunk of our income every year.", "The yearly loan payment is becoming a real burden."]
                },
                ["loan.final_repayment"] = new()
                {
                    AdultRough = ["Finally. That debt's gone.", "Paid it off at last."],
                    AdultElaborate = ["At last, the debt is fully repaid; it is a relief to have that obligation behind us.", "The final payment is made. Our finances can finally move forward without that debt hanging over them."],
                    AdultNormal = ["At last, we're free of that debt.", "The loan is finally paid off. What a relief."]
                },
                ["loan.receivable_income"] = new()
                {
                    AdultRough = ["That money I lent is paying back nicely.", "The loan income helps."],
                    AdultElaborate = ["The money I lent out is proving worthwhile; the repayments provide a useful stream of income.", "The loan I made continues to return money to the household, which was precisely the point of lending it."],
                    AdultNormal = ["The money I lent out is proving worthwhile.", "Those loan repayments are a useful bit of income."]
                }
            });
    }
}
