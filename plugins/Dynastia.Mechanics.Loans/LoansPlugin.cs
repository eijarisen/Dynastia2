using System.Globalization;
using Dynastia.Contracts;

namespace Dynastia.Mechanics.Loans;

public sealed class LoansPlugin :
    IGamePlugin
{
    public void Initialize(
        IGamePluginContext context)
    {
        var gameState =
            Require<IGameState>(context, "Game state");

        var family =
            Require<IFamilyService>(context, "Family service");

        var economy =
            Require<IEconomyService>(context, "Economy service");

        var succession =
            Require<ISuccessionService>(context, "Succession service");

        var data =
            Require<IGameDataService>(context, "Game data service");

        var historical =
            Require<IHistoricalActionVariantService>(
                context,
                "Historical action variant service");

        var actions =
            Require<IActionRegistry>(context, "Action registry");

        var events =
            Require<IGameEventBus>(context, "Event bus");

        var systems =
            Require<IYearSystemRegistry>(context, "Year system registry");

        var thoughtProviders =
            Require<IThoughtProviderRegistry>(context, "Thought provider registry");

        var loanEras =
            LoanEraCatalog.Load(
                data);

        var loans =
            new StandardLoanService(
                gameState,
                family,
                economy,
                loanEras);

        context.AddService<ILoanService>(
            loans);

        RegisterActions(
            actions,
            loans,
            family,
            economy,
            events,
            gameState,
            historical,
            loanEras);

        systems.Register(
            new LoanPaymentYearSystem(
                loans,
                economy,
                family,
                succession,
                events));

        systems.Register(
            new LoanInheritanceYearSystem(
                loans,
                family,
                economy,
                events));

        thoughtProviders.Register(
            new LoanThoughtProvider(
                loans));

        context.Log(
            "Loans and debt mechanics registered.");
    }

    private static void RegisterActions(
        IActionRegistry actions,
        StandardLoanService loans,
        IFamilyService family,
        IEconomyService economy,
        IGameEventBus events,
        IGameState gameState,
        IHistoricalActionVariantService historical,
        LoanEraCatalog loanEras)
    {
        actions.RegisterDynamicProvider(
            (_, _) =>
                [
                    WithHistoricalPresentation(
                        new GameActionDefinition
            {
                Id = "loan.take",
                Label = "Take a Loan",
                Description =
                    "Borrow 1,000-10,000 zł from a bank for 1-50 years. " +
                    "Longer loans have smaller annual payments but more total interest. " +
                    "The principal arrives on the next Year Advance and the first repayment is due one year later.",
                Mode = ActionExecutionMode.Queued,
                QueuePhase = YearPhase.QueuedActionsEarly,
                IsAvailable = actionContext =>
                    CanInitiateLoan(
                        actionContext.Actor,
                        actionContext.Target,
                        family)
                    && !loans.HasActiveSelfOriginatedBankLoan(
                        actionContext.Actor),
                Execute = actionContext =>
                {
                    var actor = actionContext.Actor;

                    if (!CanInitiateLoan(
                            actor,
                            actionContext.Target,
                            family)
                        || loans.HasActiveSelfOriginatedBankLoan(actor)
                        || !TryReadTerms(
                            actionContext.Parameters,
                            loans,
                            out var terms))
                    {
                        return new GameActionResult(false);
                    }

                    loans.CreateBankLoan(
                        actor,
                        terms.Principal,
                        terms.DurationYears,
                        actionContext.GameState.Year);

                    economy.ChangeWealth(
                        actor,
                        terms.Principal);

                    var era =
                        loanEras.GetRule(
                            actionContext.GameState.Year);

                    events.Publish(
                        new GameEvent
                        {
                            Type = "loan.taken",
                            Year = actionContext.GameState.Year,
                            SubjectId = actor.Id,
                            Data =
                                new Dictionary<string, string>
                                {
                                    ["principal"] = terms.Principal.ToString(CultureInfo.InvariantCulture),
                                    ["duration"] = terms.DurationYears.ToString(CultureInfo.InvariantCulture),
                                    ["totalRepayment"] = terms.TotalRepayment.ToString(CultureInfo.InvariantCulture),
                                    ["annualPayment"] = terms.AnnualPayment.ToString(CultureInfo.InvariantCulture),
                                    ["text"] =
                                        $"{family.GetDisplayName(actor)} {era.TakeLoanEventPhrase}: " +
                                        $"{terms.Principal:N0} zł for {terms.DurationYears} years."
                                }
                        });

                    return new GameActionResult(true);
                }
                        },
                        historical,
                        gameState.Year)
                ]);

        actions.RegisterDynamicProvider(
            (_, _) =>
                [
                    WithHistoricalPresentation(
                        new GameActionDefinition
            {
                Id = "loan.give",
                Label = "Give a Loan",
                Description =
                    "Lend a whole-thousand amount to an outside customer for 1-50 years, up to 10,000 zł or the cash currently available to the household. " +
                    "The customer is not a simulated household. Repayments begin one year after the loan is issued and return to your household.",
                Mode = ActionExecutionMode.Queued,
                QueuePhase = YearPhase.QueuedActionsEarly,
                IsAvailable = actionContext =>
                {
                    if (!CanInitiateLoan(
                            actionContext.Actor,
                            actionContext.Target,
                            family))
                    {
                        return false;
                    }

                    var finance =
                        economy.GetHousehold(
                            actionContext.Actor);

                    if (finance is null)
                        return false;

                    if (actionContext.Parameters.TryGetValue(
                            "principal",
                            out var principalText)
                        && decimal.TryParse(
                            principalText,
                            NumberStyles.Number,
                            CultureInfo.InvariantCulture,
                            out var queuedPrincipal))
                    {
                        return finance.Wealth >= queuedPrincipal;
                    }

                    return finance.Wealth >= 1000m;
                },
                Execute = actionContext =>
                {
                    var lender = actionContext.Actor;

                    if (!CanInitiateLoan(
                            lender,
                            actionContext.Target,
                            family)
                        || !TryReadTerms(
                            actionContext.Parameters,
                            loans,
                            out var terms))
                    {
                        return new GameActionResult(false);
                    }

                    var lenderHousehold =
                        economy.GetHousehold(lender);

                    if (lenderHousehold is null
                        || lenderHousehold.Wealth < terms.Principal)
                    {
                        return new GameActionResult(false);
                    }

                    economy.ChangeWealth(
                        lender,
                        -terms.Principal);

                    loans.CreateExternalReceivable(
                        lender,
                        terms.Principal,
                        terms.DurationYears,
                        actionContext.GameState.Year);

                    var wording =
                        RequireHistoricalVariant(
                            historical,
                            "loan.give",
                            actionContext.GameState.Year);

                    events.Publish(
                        new GameEvent
                        {
                            Type = "loan.given",
                            Year = actionContext.GameState.Year,
                            SubjectId = lender.Id,
                            Data =
                                new Dictionary<string, string>
                                {
                                    ["principal"] = terms.Principal.ToString(CultureInfo.InvariantCulture),
                                    ["duration"] = terms.DurationYears.ToString(CultureInfo.InvariantCulture),
                                    ["totalRepayment"] = terms.TotalRepayment.ToString(CultureInfo.InvariantCulture),
                                    ["annualPayment"] = terms.AnnualPayment.ToString(CultureInfo.InvariantCulture),
                                    ["text"] =
                                        $"{family.GetDisplayName(lender)} {wording.Narrative}: " +
                                        $"{terms.Principal:N0} zł for {terms.DurationYears} years."
                                }
                        });

                    return new GameActionResult(true);
                }
                        },
                        historical,
                        gameState.Year)
                ]);
    }

    private static GameActionDefinition WithHistoricalPresentation(
        GameActionDefinition action,
        IHistoricalActionVariantService historical,
        int year)
    {
        var variant = RequireHistoricalVariant(
            historical,
            action.Id,
            year);

        return new GameActionDefinition
        {
            Id = action.Id,
            Label = variant.Label,
            Description = variant.Description,
            Mode = action.Mode,
            QueuePhase = action.QueuePhase,
            BypassGuards = action.BypassGuards,
            IsAvailable = action.IsAvailable,
            Execute = action.Execute
        };
    }

    private static HistoricalActionVariant RequireHistoricalVariant(
        IHistoricalActionVariantService historical,
        string actionId,
        int year)
    {
        return historical.GetVariant(actionId, year)
            ?? throw new InvalidDataException(
                $"Missing historical action data for '{actionId}' in {year}.");
    }

    private static bool CanInitiateLoan(
        IPerson actor,
        IPerson target,
        IFamilyService family)
    {
        return actor.Id == target.Id
            && actor.Tags.Has("state.alive")
            && actor.Tags.Has("control.playable")
            && !actor.Tags.Has("state.imprisoned")
            && actor.Age >= 18
            && family.GetSex(actor) == Sex.Male
            && family.IsMaleLineage(actor);
    }

    private static bool TryReadTerms(
        IReadOnlyDictionary<string, string> parameters,
        ILoanService loans,
        out LoanTermsInfo terms)
    {
        terms =
            new LoanTermsInfo(
                0,
                0,
                0,
                0,
                0);

        if (!parameters.TryGetValue("principal", out var principalText)
            || !decimal.TryParse(
                principalText,
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out var principal)
            || !parameters.TryGetValue("durationYears", out var durationText)
            || !int.TryParse(
                durationText,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var duration))
        {
            return false;
        }

        try
        {
            terms =
                loans.CalculateTerms(
                    principal,
                    duration);

            return true;
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }
    }

    private static T Require<T>(
        IGamePluginContext context,
        string name)
        where T : class
    {
        return context.GetService<T>()
            ?? throw new InvalidOperationException(
                $"{name} is unavailable.");
    }
}
