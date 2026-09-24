using System.Collections.ObjectModel;
using System.Globalization;
using GovErp.Domain.Validation.Exceptions;
using GovErp.Domain.Validation.ValueObjects;

namespace GovErp.Domain.Validation.Entities;

/// <summary>
/// One version of a rule: layer, parameters, severity and effective dates.
/// </summary>
public sealed class RuleDefinition
{
    public Guid Id { get; private set; }
    public string RuleId { get; private set; }
    public int Version { get; private set; }
    public ValidationStep Step { get; private set; }
    public RuleLayer Layer { get; private set; }
    public string? ScopeFund { get; private set; }
    public string? ScopeGrant { get; private set; }
    public Severity? Severity { get; private set; }
    public IReadOnlyDictionary<string, string> Parameters { get; private set; }
    public IReadOnlyList<ApproverRole> OverridableBy { get; private set; }
    public DateOnly EffectiveFrom { get; private set; }
    public DateOnly? EffectiveTo { get; private set; }
    public string Message { get; private set; }
    public string Resolution { get; private set; }
    public bool IsEnabled { get; private set; }

    private RuleDefinition()
    {
        RuleId = null!;
        Parameters = null!;
        OverridableBy = null!;
        Message = null!;
        Resolution = null!;
    }

    public RuleDefinition(string ruleId, int version, ValidationStep step, RuleLayer layer, Severity? severity,
        IReadOnlyDictionary<string, string> parameters, IReadOnlyList<ApproverRole> overridableBy,
        DateOnly effectiveFrom, DateOnly? effectiveTo, string message, string resolution, bool enabled = true,
        string? scopeFund = null, string? scopeGrant = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ruleId);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        ArgumentNullException.ThrowIfNull(resolution);
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentNullException.ThrowIfNull(overridableBy);
        if (version < 1 || effectiveTo < effectiveFrom)
            throw new ValidationException(ValidationErrors.DefinitionInvalid, ("rule", ruleId));
        if (!Enum.IsDefined(step) || !Enum.IsDefined(layer) || (severity.HasValue && !Enum.IsDefined(severity.Value))
            || overridableBy.Any(role => !Enum.IsDefined(role)))
            throw new ValidationException(ValidationErrors.DefinitionEnumInvalid, ("rule", ruleId));
        Id = Guid.NewGuid();
        RuleId = ruleId;
        Version = version;
        Step = step;
        Layer = layer;
        Severity = severity;
        ScopeFund = scopeFund;
        ScopeGrant = scopeGrant;
        Parameters = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(parameters, StringComparer.Ordinal));
        OverridableBy = Array.AsReadOnly(overridableBy.Distinct().Order().ToArray());
        EffectiveFrom = effectiveFrom;
        EffectiveTo = effectiveTo;
        Message = message;
        Resolution = resolution;
        IsEnabled = enabled;
    }

    public bool IsEffectiveOn(DateOnly date) => IsEnabled && IsDatedFor(date);

    /// <summary>
    /// The date is within the effective period, whether or not the definition is enabled.
    /// </summary>
    public bool IsDatedFor(DateOnly date) => date >= EffectiveFrom && (EffectiveTo is null || date <= EffectiveTo);

    /// <summary>
    /// A definition without a scope covers every fund and grant; a scoped one only its own.
    /// </summary>
    public bool Covers(string? fund, string? grant) =>
        (ScopeFund is null || ScopeFund == fund) && (ScopeGrant is null || ScopeGrant == grant);

    /// <summary>
    /// Softer than the definition it would replace. A fixed severity may only rise; a dynamic one (null, decided by the rule per outcome) must stay dynamic.
    /// </summary>
    public bool IsMilderThan(RuleDefinition upper) => (upper.Severity, Severity) switch
    {
        (null, null) => false,
        (null, _) or (_, null) => true,
        var (fixedUpper, fixedOwn) => fixedOwn < fixedUpper,
    };

    /// <summary>
    /// Roles that may release this definition's soft stop but not the one it would replace.
    /// </summary>
    public bool AllowsMoreOverridersThan(RuleDefinition upper) => OverridableBy.Except(upper.OverridableBy).Any();

    public string Parameter(string name) => Parameters.TryGetValue(name, out var value)
        ? value : throw new ValidationException(ValidationErrors.ParameterMissing, ("rule", RuleId), ("version", Version), ("parameter", name));

    public decimal DecimalParameter(string name) =>
        decimal.TryParse(Parameter(name), NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture, out var value)
            ? value : throw new ValidationException(ValidationErrors.ParameterNotDecimal, ("rule", RuleId), ("version", Version), ("parameter", name));
}
