using System.Collections.ObjectModel;
using GovErp.Domain.Validation.Entities;
using GovErp.Domain.Validation.Exceptions;
using GovErp.Domain.Validation.ValueObjects;

namespace GovErp.Domain.Validation.DomainServices;

/// <summary>
/// Eight steps over snapshots. The input and the rule configuration are checked first (fail-closed); then steps 1–6
/// run the catalog rules by their scope (fund, grant), a Hard Stop stops the run; step 7 is the route; step 8 runs only on Post.
/// A pure function: no ports and no clock; the only randomness is the ids of the record and the outcomes.
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

    /// <summary>Resolves the definitions by the scope of the subject's lines and evaluates. An unresolvable configuration is a refusal, not an exception.</summary>
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

        // Configuration is checked before the steps: an unknown rule must not hide behind an early Hard Stop.
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

    /// <summary>Capabilities depend on both the evaluation result and the document status.</summary>
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

    /// <summary>Each definition gets the lines of the scopes where it applies; a rule sees only its own lines.</summary>
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
        return MissingFacts(subject);
    }

    /// <summary>The snapshot carries every fact the rules decide on, so no rule has to guess about a missing fund, grant or vendor.</summary>
    private static string? MissingFacts(ValidationSubject subject)
    {
        var t = subject.Transaction;
        if (t.Vendor is null || t.Vendor.VendorId == Guid.Empty) return "The invoice vendor is not identified.";
        foreach (var line in subject.Distributions)
        {
            if (line.Combination is null) return $"Line {line.LineNo}: account combination facts are missing.";
            if (line.Fund is null) return $"Line {line.LineNo}: fund facts are missing for {line.Account}.";
            if (line.Fund.Code != line.Account.Fund.Value)
                return $"Line {line.LineNo}: fund facts {line.Fund.Code} do not match the account fund {line.Account.Fund}.";
            if (line.Account.Grant is { } grant && line.Grant is null) return $"Line {line.LineNo}: grant facts are missing for {grant}.";
            if (line.Account.Grant is { } coded && line.Grant is { } facts && facts.Code != coded.Value)
                return $"Line {line.LineNo}: grant facts {facts.Code} do not match the account grant {coded}.";
            if (t.IsPoBacked && line.Encumbrance is null) return $"Line {line.LineNo} of a PO-backed invoice is not linked to a PO line.";
        }

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
