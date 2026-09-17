using System.Globalization;
using Dynastia.Contracts;

namespace Dynastia.Mechanics.Loans;

public sealed class LoansPlugin :
    IGamePlugin
{
    private const string SurnamesPath =
        "Names/polish_surnames.csv";

    public void Initialize(
        IGamePluginContext context)
    {
        var gameState =
            Require<IGameState>(context, "Game state");

        var random =
            Require<IGameRandom>(context, "Random service");

        var family =
            Require<IFamilyService>(context, "Family service");

        var historicalNames =
            Require<IHistoricalNameService>(
                context,
                "Historical name service");

        var economy =
            Require<IEconomyService>(context, "Economy service");

        var succession =
            Require<ISuccessionService>(context, "Succession service");

        var data =
            Require<IGameDataService>(context, "Game data service");

        var externalBorrowerSurnames =
            data.GetWeightedStringList(
                SurnamesPath);

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
                loanEras,
                random);

        context.AddService<ILoanService>(
            loans);

        context.GetService<IHouseholdFinanceProjectionProviderRegistry>()
            ?.Register(new LoanFinanceProjectionProvider(loans));

        RegisterActions(
            actions,
            loans,
            family,
            historicalNames,
            externalBorrowerSurnames,
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
        IHistoricalNameService historicalNames,
        IReadOnlyList<WeightedStringEntry> externalBorrowerSurnames,
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
                        actionContext.ActorHasControl,
                        family)
                    && !loans.HasActiveSelfOriginatedBankLoan(
                        actionContext.Actor),
                Execute = actionContext =>
                {
                    var actor = actionContext.Actor;

                    if (!CanInitiateLoan(
                            actor,
                            actionContext.Target,
                            actionContext.ActorHasControl,
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
                            actionContext.ActorHasControl,
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
                            actionContext.ActorHasControl,
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

                    var contract =
                        loans.CreateExternalReceivable(
                            lender,
                            terms.Principal,
                            terms.DurationYears,
                            actionContext.GameState.Year);

                    var borrowerName =
                        GenerateExternalBorrowerName(
                            contract.ContractId,
                            actionContext.GameState.Year,
                            family,
                            historicalNames,
                            externalBorrowerSurnames);

                    contract.ExternalBorrowerName =
                        borrowerName;

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
                                    ["borrowerName"] = borrowerName,
                                    ["text"] =
                                        $"{family.GetDisplayName(lender)} " +
                                        $"{FormatExternalLoanNarrative(wording.Narrative, borrowerName)}: " +
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
            EvaluateAvailability = action.EvaluateAvailability,
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
        bool actorHasControl,
        IFamilyService family)
    {
        return actor.Id == target.Id
            && actor.Tags.Has("state.alive")
            && actorHasControl
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

    private static string GenerateExternalBorrowerName(
        Guid contractId,
        int year,
        IFamilyService family,
        IHistoricalNameService historicalNames,
        IReadOnlyList<WeightedStringEntry> surnames)
    {
        var random =
            new LoanFlavorRandom(
                contractId);

        var sex =
            year < 1918
                ? Sex.Male
                : random.Chance(0.5)
                    ? Sex.Male
                    : Sex.Female;

        var firstName =
            historicalNames.GetRandomFirstName(
                sex,
                year - 30,
                random);

        var surname =
            SelectWeighted(
                surnames,
                random);

        return
            $"{firstName} {family.FormatSurname(surname, sex)}";
    }

    private static string SelectWeighted(
        IReadOnlyList<WeightedStringEntry> entries,
        IGameRandom random)
    {
        var totalWeight =
            entries.Sum(
                entry => (double)entry.Weight);

        var roll =
            random.NextDouble()
            * totalWeight;

        foreach (var entry in entries)
        {
            if (roll < entry.Weight)
                return entry.Value;

            roll -= entry.Weight;
        }

        return entries[^1].Value;
    }

    private static string FormatExternalLoanNarrative(
        string narrative,
        string borrowerName)
    {
        const string genericBorrower =
            "an outside borrower";

        if (narrative.Contains(
                genericBorrower,
                StringComparison.OrdinalIgnoreCase))
        {
            return narrative.Replace(
                genericBorrower,
                borrowerName,
                StringComparison.OrdinalIgnoreCase);
        }

        return $"{narrative} ({borrowerName})";
    }

    private sealed class LoanFlavorRandom :
        IGameRandom
    {
        private ulong _state;

        public LoanFlavorRandom(
            Guid seed)
        {
            const ulong offsetBasis =
                14695981039346656037UL;

            const ulong prime =
                1099511628211UL;

            var state =
                offsetBasis;

            foreach (var value in seed.ToByteArray())
            {
                state ^= value;
                state *= prime;
            }

            _state =
                state == 0
                    ? offsetBasis
                    : state;
        }

        public int NextInt(
            int minInclusive,
            int maxInclusive)
        {
            if (maxInclusive < minInclusive)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maxInclusive));
            }

            var range =
                (ulong)((long)maxInclusive - minInclusive + 1L);

            return minInclusive
                + (int)(NextUInt64() % range);
        }

        public double NextDouble()
        {
            return (NextUInt64() >> 11)
                * (1.0 / 9007199254740992.0);
        }

        public bool Chance(
            double probability)
        {
            return probability switch
            {
                <= 0 => false,
                >= 1 => true,
                _ => NextDouble() < probability
            };
        }

        private ulong NextUInt64()
        {
            _state =
                unchecked(
                    _state
                    * 6364136223846793005UL
                    + 1442695040888963407UL);

            return _state;
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
