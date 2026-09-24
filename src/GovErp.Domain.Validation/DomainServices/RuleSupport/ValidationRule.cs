using System.Globalization;
using GovErp.Domain.Validation.Entities;
using GovErp.Domain.Validation.ValueObjects;

namespace GovErp.Domain.Validation.DomainServices.RuleSupport;

/// <summary>
/// Base of the catalog rules. Check is laid out in three marked sections, in this order:
/// <list type="bullet">
/// <item><c>// Scope:</c> what is checked (every line, every budget line, every PO line, the invoice);</item>
/// <item><c>// Decide:</c> the verdict for one item, strictest case first;</item>
/// <item><c>// Evidence</c>: the facts stored with the outcome for the validation log, audit and explanation.</item>
/// </list>
/// A rule reads only the snapshot and its parameters (no storage, no clock). The pipeline has already checked that
/// the snapshot carries every fact (fund, grant, combination, vendor), so a rule never decides on missing data.
/// </summary>
public abstract class ValidationRule : IValidationRule
{
    public abstract string RuleId { get; }

    public virtual IReadOnlyList<ParameterSpec> Parameters => [];

    /// <summary>The severity of Fail when the rule definition does not configure one.</summary>
    protected virtual Severity DefaultSeverity => Severity.HardStop;

    public IReadOnlyList<RuleOutcome> Evaluate(ValidationSubject subject, RuleDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(subject);
        ArgumentNullException.ThrowIfNull(definition);
        var configured = definition.Severity ?? DefaultSeverity;
        return Check(subject, new RuleParameters(definition))
            .Select(finding => finding.ToOutcome(definition, configured))
            .ToList();
    }

    protected abstract IEnumerable<Finding> Check(ValidationSubject subject, RuleParameters parameters);

    /// <summary>The rule is broken with the configured severity (or DefaultSeverity).</summary>
    protected static Verdict Fail(string reason) => new(null, reason);

    protected static Verdict HardStop(string reason) => new(Severity.HardStop, reason);

    protected static Verdict SoftStop(string reason) => new(Severity.SoftStop, reason);

    protected static Verdict Warning(string reason) => new(Severity.Warning, reason);

    /// <summary>Recorded for transparency; nothing is blocked.</summary>
    protected static Verdict Allowed(string reason) => new(Severity.Allowed, reason);

    /// <summary>A share (0.0325) as a percentage with two decimals ("3.25").</summary>
    protected static string Percent(decimal share) => (share * 100).ToString("0.00", CultureInfo.InvariantCulture);
}
