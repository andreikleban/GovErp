using System.Collections.ObjectModel;
using GovErp.Domain.Validation.Entities;
using GovErp.Domain.Validation.Exceptions;
using GovErp.Domain.Validation.ValueObjects;

namespace GovErp.Domain.Validation.DomainServices;

/// <summary>
/// Восемь шагов над снимками. Сначала проверяются вход и конфигурация правил (fail-closed); затем шаги 1–6 —
/// правила каталога по их scope (фонд, грант), Hard Stop прерывает; шаг 7 — маршрут; шаг 8 — только при Post.
/// Чистая функция: без портов и часов; единственная случайность — идентификаторы записи и outcome'ов.
/// </summary>
public sealed class ValidationPipeline(RuleCatalog catalog)
{
    public const string ValidationInputRuleId = "VALIDATION_INPUT";
    public const string RuleConfigurationRuleId = "RULE_CONFIGURATION";

    private static readonly ValidationStep[] RuleSteps =
    [
        ValidationStep.RequiredSegments, ValidationStep.ValidCombination, ValidationStep.FundAndGrantRestrictions,
        ValidationStep.TransactionPurpose, ValidationStep.BudgetAvailability, ValidationStep.EncumbranceImpact,
    ];

    private static readonly string[] KnownStatuses = ["Draft", "Submitted", "Approved", "Rejected", "Posted"];

    private readonly RuleCatalog _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));

    /// <summary>Разрешает определения по scope строк субъекта и оценивает. Неразрешимая конфигурация — отказ, а не исключение.</summary>
    public EvaluationRecord Evaluate(ValidationSubject subject, IReadOnlyList<RuleDefinition> definitions, EvaluationTrigger trigger,
        UserId evaluatedBy, DateTimeOffset evaluatedAt)
    {
        ArgumentNullException.ThrowIfNull(subject);
        ArgumentNullException.ThrowIfNull(definitions);
        EffectiveRuleSet rules;
        try
        {
            rules = RuleResolution.ResolveForSubject(definitions, subject);
        }
        catch (ValidationException ex)
        {
            var empty = new EffectiveRuleSet([], new RuleSetVersions(RuleResolution.EngineVersion, 0, 0, 0, 0));
            return Refuse(subject, empty, trigger, evaluatedBy, evaluatedAt, RuleConfigurationRuleId, ex.Message);
        }

        return Evaluate(subject, rules, trigger, evaluatedBy, evaluatedAt);
    }

    public EvaluationRecord Evaluate(ValidationSubject subject, EffectiveRuleSet rules, EvaluationTrigger trigger,
        UserId evaluatedBy, DateTimeOffset evaluatedAt)
    {
        ArgumentNullException.ThrowIfNull(subject);
        ArgumentNullException.ThrowIfNull(rules);

        if (InputProblem(subject, evaluatedBy) is { } inputProblem)
        {
            return Refuse(subject, rules, trigger, evaluatedBy, evaluatedAt, ValidationInputRuleId, inputProblem);
        }

        // Конфигурация проверяется до шагов: неизвестное правило не должно скрываться за ранним Hard Stop.
        if (ConfigurationProblem(subject, rules) is { } configurationProblem)
        {
            return Refuse(subject, rules, trigger, evaluatedBy, evaluatedAt, RuleConfigurationRuleId, configurationProblem);
        }

        var assignments = Assign(subject, rules);
        var raw = new List<RuleOutcome>();
        var steps = new List<StepExecution>();
        var stopped = false;
        foreach (var step in RuleSteps)
        {
            if (stopped)
            {
                steps.Add(new(step, StepExecutionStatus.Skipped));
                continue;
            }

            foreach (var (definition, scoped) in assignments.Where(a => a.Definition.Step == step))
            {
                raw.AddRange(_catalog.Find(definition.RuleId)!.Evaluate(scoped, definition));
            }

            steps.Add(new(step, StepExecutionStatus.Executed));
            stopped = raw.Any(o => o.Severity == Severity.HardStop);
        }

        var (outcomes, overall) = OutcomeAggregation.Apply(raw, subject, rules.Versions);
        var route = ApprovalRouteResolver.Build(subject, outcomes, rules);
        steps.Add(new(ValidationStep.ApprovalRequirements, StepExecutionStatus.Executed));

        var preview = overall <= Severity.SoftStop ? PostingPreviewBuilder.Build(subject) : null;
        PostingCheck? check = null;
        if (trigger == EvaluationTrigger.Post)
        {
            check = PostingEligibility.Check(subject, overall, route, preview, rules);
            steps.Add(new(ValidationStep.PostingEligibility, StepExecutionStatus.Executed));
        }
        else
        {
            steps.Add(new(ValidationStep.PostingEligibility, StepExecutionStatus.Skipped));
        }

        return new EvaluationRecord(subject, trigger, evaluatedAt, evaluatedBy, rules.Versions, outcomes, overall,
            CapabilitiesFor(subject.Transaction.Status, overall, check?.Passed), steps, route,
            preview?.Lines ?? [], check, ReadyForPaymentHandoff(subject));
    }

    /// <summary>Возможности зависят и от результата оценки, и от статуса документа.</summary>
    private static Capabilities CapabilitiesFor(string status, Severity overall, bool? postingPassed)
    {
        var byResult = Capabilities.For(overall, postingPassed);
        return status switch
        {
            "Draft" => new(true, byResult.CanSubmit, false, false, false),
            "Submitted" => new(false, false, byResult.CanApprove, false, false),
            "Approved" => new(false, false, false, byResult.CanPost, false),
            _ => new(false, false, false, false, false),
        };
    }

    private static bool ReadyForPaymentHandoff(ValidationSubject subject) =>
        subject.BusinessDate is { } businessDate && PostingEligibility.ReadyForPaymentHandoff(subject, businessDate);

    /// <summary>Каждому определению — строки тех scope, где оно действует; правило видит только свои строки.</summary>
    private static IReadOnlyList<(RuleDefinition Definition, ValidationSubject Scoped)> Assign(ValidationSubject subject, EffectiveRuleSet rules)
    {
        var lines = new Dictionary<RuleDefinition, List<DistributionSnapshot>>(ReferenceEqualityComparer.Instance);
        foreach (var scope in subject.Distributions.GroupBy(d => (Fund: (string?)d.Account.Fund.Value, Grant: d.Account.Grant?.Value)))
        {
            foreach (var definition in rules.ForScope(scope.Key.Fund, scope.Key.Grant).Rules)
            {
                if (!lines.TryGetValue(definition, out var list))
                {
                    lines[definition] = list = [];
                }

                list.AddRange(scope);
            }
        }

        return lines
            .OrderBy(pair => pair.Key.Step).ThenBy(pair => pair.Key.RuleId, StringComparer.Ordinal)
            .ThenBy(pair => pair.Key.Layer).ThenBy(pair => pair.Key.Version)
            .Select(pair => (pair.Key, subject with { Distributions = pair.Value.OrderBy(d => d.LineNo).ToArray() }))
            .ToArray();
    }

    private string? ConfigurationProblem(ValidationSubject subject, EffectiveRuleSet rules)
    {
        var unknown = rules.Rules
            .Where(r => r.Step <= ValidationStep.EncumbranceImpact && _catalog.Find(r.RuleId) is null)
            .Select(r => r.RuleId).Distinct().ToArray();
        if (unknown.Length > 0)
        {
            return $"Configured rules have no implementation: {string.Join(", ", unknown)}.";
        }

        foreach (var scope in subject.Distributions.Select(d => ((string?)d.Account.Fund.Value, d.Account.Grant?.Value)).Distinct())
        {
            var effective = rules.ForScope(scope.Item1, scope.Item2).Rules.Select(r => r.RuleId).ToHashSet(StringComparer.Ordinal);
            var missing = _catalog.MandatoryRuleIds.Where(id => !effective.Contains(id)).ToArray();
            if (missing.Length > 0)
            {
                return $"Mandatory rules are missing or disabled for fund {scope.Item1}: {string.Join(", ", missing)}.";
            }
        }

        return null;
    }

    private static string? InputProblem(ValidationSubject subject, UserId evaluatedBy)
    {
        var t = subject.Transaction;
        if (evaluatedBy.Value == Guid.Empty) return "The evaluating actor is required.";
        if (subject.Distributions.Count == 0) return "The invoice has no distributions.";
        if (subject.Distributions.Any(d => d.LineNo < 1)) return "Distribution line numbers must be positive.";
        if (subject.Distributions.GroupBy(d => d.LineNo).Any(g => g.Count() > 1)) return "Distribution line numbers must be unique.";
        if (subject.Distributions.Any(d => d.Amount <= Money.Zero)) return "Distribution amounts must be positive.";

        Money distributed;
        try
        {
            distributed = subject.Distributions.Aggregate(Money.Zero, (sum, d) => sum + d.Amount);
        }
        catch (ArgumentException)
        {
            return "Distribution amounts exceed decimal(18,2).";
        }

        if (distributed != t.Total) return $"Distributions {distributed} do not equal the invoice total {t.Total}.";
        if (t.ContentVersion < 1) return "Content version must be positive.";
        if (t.CreatedBy.Value == Guid.Empty) return "The invoice author is required.";
        if (!KnownStatuses.Contains(t.Status, StringComparer.Ordinal)) return $"Unknown document status '{t.Status}'.";
        if (t.Status is "Submitted" or "Approved" && t.ApprovalCycleId == Guid.Empty) return "An active document requires an approval cycle.";
        if (t.ServiceDate is not { } service || t.PostingDate is not { } posting) return "Service and posting dates are required.";
        if (t.InvoiceDate != service || t.InvoiceDate != posting)
            return "This demo supports only InvoiceDate = ServiceDate = PostingDate.";

        var fiscalYear = FiscalYear.FromDate(posting).Year;
        if (subject.Distributions.Any(d => d.Budget.Exists && d.Budget.FiscalYear != fiscalYear))
            return $"Budget snapshots must belong to FY{fiscalYear} of the posting date.";
        return null;
    }

    private static EvaluationRecord Refuse(ValidationSubject subject, EffectiveRuleSet rules, EvaluationTrigger trigger,
        UserId evaluatedBy, DateTimeOffset evaluatedAt, string ruleId, string message)
    {
        var definition = new RuleDefinition(ruleId, 1, ValidationStep.RequiredSegments, RuleLayer.Core, Severity.HardStop,
            new ReadOnlyDictionary<string, string>(new Dictionary<string, string>()), [], DateOnly.MinValue, null, message,
            "Correct the input or rule configuration and evaluate again.");
        var outcome = RuleOutcome.From(definition, Severity.HardStop, null, new Dictionary<string, string>(),
            new Dictionary<string, string>(), message);
        var steps = Enum.GetValues<ValidationStep>()
            .Select(step => new StepExecution(step, step == ValidationStep.RequiredSegments ? StepExecutionStatus.Executed : StepExecutionStatus.Skipped))
            .ToArray();
        var check = trigger == EvaluationTrigger.Post ? new PostingCheck(false, [message]) : null;
        return new EvaluationRecord(subject, trigger, evaluatedAt, evaluatedBy, rules.Versions, [outcome], Severity.HardStop,
            CapabilitiesFor(subject.Transaction.Status, Severity.HardStop, false), steps, [], [], check, false);
    }
}
