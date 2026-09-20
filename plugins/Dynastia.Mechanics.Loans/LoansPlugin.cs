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

        var locations =
            Require<ILocationService>(context, "Location service");

        var facilityQuality =
            Require<ITownFacilityQualityService>(
                context,
                "Town facility quality service");

        var outsiderIdentities =
            Require<IOutsiderIdentityService>(
                context,
                "Outsider identity service");

        var historical =
            Require<IHistoricalActionVariantService>(
                context,
                "Historical action variant service");

        var appearance =
            Require<IAppearanceService>(
                context,
                "Appearance service");

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
                random,
                historicalNames,
                locations,
                outsiderIdentities,
                appearance,
                facilityQuality);

        context.AddService<ILoanService>(
            loans);

        context.GetService<IHouseholdFinanceProjectionProviderRegistry>()
            ?.Register(new LoanFinanceProjectionProvider(loans));

        RegisterActions(
            actions,
            loans,
            family,
            economy,
            events,
            gameState,
            historical,
            loanEras,
            locations,
            facilityQuality);

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
        LoanEraCatalog loanEras,
        ILocationService locations,
        ITownFacilityQualityService facilityQuality)
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
                    && HasLocalBank(
                        actionContext.Actor,
                        actionContext.GameState.Year,
                        locations,
                        facilityQuality)
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
                        || !HasLocalBank(
                            actor,
                            actionContext.GameState.Year,
                            locations,
                            facilityQuality)
                        || loans.HasActiveSelfOriginatedBankLoan(actor)
                        || !TryReadTerms(
                            actionContext.Parameters,
                            loans,
                            out var terms))
                    {
                        return new GameActionResult(false);
                    }

                    var contract =
                        loans.CreateBankLoan(
                            actor,
                            terms.Principal,
                            terms.DurationYears,
                            actionContext.GameState.Year,
                            terms.InterestMultiplier);

                    var creditor =
                        ResolveCounterparty(
                            actionContext.Parameters,
                            loans,
                            actor,
                            actionContext.GameState.Year,
                            contract.ContractId);

                    var creditorName = creditor.Name;
                    contract.ExternalCreditorName = creditorName;
                    contract.ExternalCounterpartyTownId = creditor.TownId;
                    contract.ExternalCounterpartyNationalityId = creditor.NationalityId;

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
                                    ["creditorName"] = creditorName ?? string.Empty,
                                    ["text"] =
                                        string.IsNullOrWhiteSpace(creditorName)
                                            ? $"{family.GetDisplayName(actor)} {era.TakeLoanEventPhrase}: " +
                                              $"{terms.Principal:N0} zł for {terms.DurationYears} years."
                                            : $"{family.GetDisplayName(actor)} arranged a loan with {creditorName}: " +
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
                        return economy.CanAfford(actionContext.Actor, queuedPrincipal);
                    }

                    return economy.CanAfford(actionContext.Actor, 1000m);
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

                    if (!economy.CanAfford(
                            lender,
                            terms.Principal))
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

                    var borrower =
                        ResolveCounterparty(
                            actionContext.Parameters,
                            loans,
                            lender,
                            actionContext.GameState.Year,
                            contract.ContractId);
                    var borrowerName = borrower.Name;

                    contract.ExternalBorrowerName =
                        borrowerName;
                    contract.ExternalCounterpartyTownId = borrower.TownId;
                    contract.ExternalCounterpartyNationalityId = borrower.NationalityId;

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

    private static bool HasLocalBank(
        IPerson person,
        int year,
        ILocationService locations,
        ITownFacilityQualityService facilityQuality)
    {
        var town = locations.GetLocation(person).HomeTown;
        return facilityQuality.GetBankQuality(town, year).IsAvailable;
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

        var interestMultiplier = 1m;
        if (parameters.TryGetValue(
                "interestMultiplier",
                out var interestMultiplierText)
            && (!decimal.TryParse(
                    interestMultiplierText,
                    NumberStyles.Number,
                    CultureInfo.InvariantCulture,
                    out interestMultiplier)
                || interestMultiplier <= 0))
        {
            return false;
        }

        try
        {
            terms =
                loans.CalculateTerms(
                    principal,
                    duration,
                    interestMultiplier);

            return true;
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }
    }

    private static ExternalCounterpartySelection ResolveCounterparty(
        IReadOnlyDictionary<string, string> parameters,
        StandardLoanService loans,
        IPerson householdRepresentative,
        int year,
        Guid seed)
    {
        if (parameters.TryGetValue(
                "counterpartyName",
                out var selectedName)
            && !string.IsNullOrWhiteSpace(selectedName))
        {
            parameters.TryGetValue(
                "counterpartyTownId",
                out var selectedTownId);
            parameters.TryGetValue(
                "counterpartyNationalityId",
                out var selectedNationalityId);

            return new ExternalCounterpartySelection(
                selectedName,
                selectedTownId ?? string.Empty,
                selectedNationalityId ?? string.Empty);
        }

        var generated =
            loans.GenerateExternalCounterparty(
                householdRepresentative,
                year,
                seed);

        return new ExternalCounterpartySelection(
            generated.Name,
            generated.OriginTown.Id,
            generated.NationalityId);
    }

    private sealed record ExternalCounterpartySelection(
        string Name,
        string TownId,
        string NationalityId);

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
