using System.Collections.ObjectModel;
using GovErp.Domain.Validation.Entities;

namespace GovErp.Domain.Validation.ValueObjects;

public sealed record RuleOutcome
{
    public Guid OutcomeRef { get; private init; } = Guid.NewGuid();
    public string RuleId { get; }
    public int RuleVersion { get; }
    public ValidationStep Step { get; }
    public RuleLayer Layer { get; }
    public int? DistributionLine { get; }
    public Severity Severity { get; }
    public IReadOnlyDictionary<string, string> Inputs { get; }
    public IReadOnlyDictionary<string, string> Computed { get; }
    public string Message { get; }
    public string Resolution { get; }
    public IReadOnlyList<ApproverRole> OverridableBy { get; }
    public OverrideSnapshot? OverriddenBy { get; internal init; }
    public bool IsOverridden => OverriddenBy is not null;

    private RuleOutcome(RuleDefinition rule, Severity severity, int? line,
        IReadOnlyDictionary<string, string> inputs, IReadOnlyDictionary<string, string> computed, string? message)
    {
        RuleId = rule.RuleId;
        RuleVersion = rule.Version;
        Step = rule.Step;
        Layer = rule.Layer;
        DistributionLine = line;
        Severity = severity;
        Inputs = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(inputs));
        Computed = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(computed));
        Message = message ?? rule.Message;
        Resolution = rule.Resolution;
        OverridableBy = Array.AsReadOnly(severity == Severity.SoftStop ? rule.OverridableBy.ToArray() : []);
    }

    private RuleOutcome(Guid outcomeRef, string ruleId, int ruleVersion, ValidationStep step, RuleLayer layer, int? line,
        Severity severity, IReadOnlyDictionary<string, string> inputs, IReadOnlyDictionary<string, string> computed,
        string message, string resolution, IReadOnlyList<ApproverRole> overridableBy)
    {
        OutcomeRef = outcomeRef;
        RuleId = ruleId;
        RuleVersion = ruleVersion;
        Step = step;
        Layer = layer;
        DistributionLine = line;
        Severity = severity;
        Inputs = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(inputs));
        Computed = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(computed));
        Message = message;
        Resolution = resolution;
        OverridableBy = Array.AsReadOnly(overridableBy.ToArray());
    }

    public static RuleOutcome From(RuleDefinition rule, Severity severity, int? line,
        IReadOnlyDictionary<string, string> inputs, IReadOnlyDictionary<string, string> computed, string? message = null) =>
        new(rule, severity, line, inputs, computed, message);

    /// <summary>Восстановление сохранённого outcome (JSON-колонки оценки). Не вычисляет ничего заново.</summary>
    public static RuleOutcome Restore(Guid outcomeRef, string ruleId, int ruleVersion, ValidationStep step, RuleLayer layer,
        int? distributionLine, Severity severity, IReadOnlyDictionary<string, string> inputs, IReadOnlyDictionary<string, string> computed,
        string message, string resolution, IReadOnlyList<ApproverRole> overridableBy, OverrideSnapshot? overriddenBy) =>
        new(outcomeRef, ruleId, ruleVersion, step, layer, distributionLine, severity, inputs, computed, message, resolution, overridableBy)
        { OverriddenBy = overriddenBy };
}
