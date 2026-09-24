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
public sealed class ValidationPipeline
{
    public const string ValidationInputRuleId = "VALIDATION_INPUT";
    public const string RuleConfigurationRuleId = "RULE_CONFIGURATION";

    private readonly RuleResolver _resolver;
    private readonly ConfigurationCheck _configuration;
    private readonly RuleStepRunner _steps;

    public ValidationPipeline(RuleCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        _resolver = new RuleResolver(catalog);
        _configuration = new ConfigurationCheck(catalog);
        _steps = new RuleStepRunner(catalog);
    }

    /// <summary>
    /// Resolves the definitions by the scope of the subject's lines and evaluates. An unresolvable configuration is a refusal, not an exception.
    /// </summary>
    public EvaluationRecord Evaluate(ValidationSubject subject, IReadOnlyList<RuleDefinition> definitions, EvaluationTrigger trigger,
        UserId evaluatedBy, DateTimeOffset evaluatedAt)
    {
        ArgumentNullException.ThrowIfNull(subject);
        ArgumentNullException.ThrowIfNull(definitions);
        EffectiveRuleSet rules;
        try
        {
            rules = _resolver.ResolveForSubject(definitions, subject);
        }
        catch (ValidationException ex)
        {
            var none = new EffectiveRuleSet([], new RuleSetVersions(RuleResolver.EngineVersion, 0, 0, 0, 0));
            return Refuse(subject, none, trigger, evaluatedBy, evaluatedAt, RuleConfigurationRuleId, ex.Problem);
        }

        return Evaluate(subject, rules, trigger, evaluatedBy, evaluatedAt);
    }

    public EvaluationRecord Evaluate(ValidationSubject subject, EffectiveRuleSet rules, EvaluationTrigger trigger,
        UserId evaluatedBy, DateTimeOffset evaluatedAt)
    {
        ArgumentNullException.ThrowIfNull(subject);
        ArgumentNullException.ThrowIfNull(rules);

        // Fail closed: an incomplete input or configuration is refused, never evaluated in part.
        if (InputCheck.FirstProblem(subject, evaluatedBy) is { } input)
            return Refuse(subject, rules, trigger, evaluatedBy, evaluatedAt, ValidationInputRuleId, input);
        if (_configuration.FirstProblem(subject, rules) is { } configuration)
            return Refuse(subject, rules, trigger, evaluatedBy, evaluatedAt, RuleConfigurationRuleId, configuration);

        // Steps 1–6: the catalog rules.
        var (raw, ruleSteps) = _steps.Run(subject, rules);

        // Step 7: overrides applied to the outcomes, then the approval route.
        var (outcomes, overall) = OutcomeAggregation.Apply(raw, subject, rules.Versions);
        var route = ApprovalRouteResolver.Build(subject, outcomes, rules);

        // Step 8: posting eligibility, only when the document is being posted.
        var preview = overall <= Severity.SoftStop ? PostingPreviewBuilder.Build(subject) : null;
        var check = trigger == EvaluationTrigger.Post ? PostingEligibility.Check(subject, overall, route, preview, rules) : null;

        IReadOnlyList<StepExecution> steps =
        [
            .. ruleSteps,
            new(ValidationStep.ApprovalRequirements, StepExecutionStatus.Executed),
            new(ValidationStep.PostingEligibility, check is null ? StepExecutionStatus.Skipped : StepExecutionStatus.Executed),
        ];
        return new EvaluationRecord(subject, trigger, evaluatedAt, evaluatedBy, rules.Versions, outcomes, overall,
            Capabilities.ForDocument(subject.Transaction.Status, overall, check?.Passed), steps, route,
            preview?.Lines ?? [], check, ReadyForPaymentHandoff(subject));
    }

    private static bool ReadyForPaymentHandoff(ValidationSubject subject) =>
        subject.BusinessDate is { } businessDate && PostingEligibility.ReadyForPaymentHandoff(subject, businessDate);

    /// <summary>
    /// A refusal is recorded like any evaluation: one Hard Stop outcome carrying the problem (code and arguments), every rule step skipped.
    /// </summary>
    private static EvaluationRecord Refuse(ValidationSubject subject, EffectiveRuleSet rules, EvaluationTrigger trigger,
        UserId evaluatedBy, DateTimeOffset evaluatedAt, string ruleId, Problem problem)
    {
        var definition = new RuleDefinition(ruleId, 1, ValidationStep.RequiredSegments, RuleLayer.Core, Severity.HardStop,
            new ReadOnlyDictionary<string, string>(new Dictionary<string, string>()), [], DateOnly.MinValue, null, problem.Code, "");
        var outcome = RuleOutcome.From(definition, Severity.HardStop, null, problem.Args, new Dictionary<string, string>(), problem.Code);
        var steps = Enum.GetValues<ValidationStep>()
            .Select(step => new StepExecution(step, step == ValidationStep.RequiredSegments ? StepExecutionStatus.Executed : StepExecutionStatus.Skipped))
            .ToArray();
        var check = trigger == EvaluationTrigger.Post ? new PostingCheck(false, [problem]) : null;
        return new EvaluationRecord(subject, trigger, evaluatedAt, evaluatedBy, rules.Versions, [outcome], Severity.HardStop,
            Capabilities.ForDocument(subject.Transaction.Status, Severity.HardStop, false), steps, [], [], check, false);
    }
}
