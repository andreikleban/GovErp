using System.Collections.ObjectModel;
using System.Globalization;
using GovErp.Domain.Validation.Exceptions;
using GovErp.Domain.Validation.ValueObjects;

namespace GovErp.Domain.Validation.Entities;

public sealed class RuleDefinition
{
    public Guid Id { get; }
    public string RuleId { get; }
    public int Version { get; }
    public ValidationStep Step { get; }
    public RuleLayer Layer { get; }
    public string? ScopeFund { get; }
    public string? ScopeGrant { get; }
    public Severity? Severity { get; }
    public IReadOnlyDictionary<string, string> Parameters { get; }
    public IReadOnlyList<ApproverRole> OverridableBy { get; }
    public DateOnly EffectiveFrom { get; }
    public DateOnly? EffectiveTo { get; }
    public string Message { get; }
    public string Resolution { get; }
    public bool IsEnabled { get; }

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
            throw new ValidationException($"Rule {ruleId}: invalid version or effective interval.");
        if (!Enum.IsDefined(step) || !Enum.IsDefined(layer) || (severity.HasValue && !Enum.IsDefined(severity.Value))
            || overridableBy.Any(role => !Enum.IsDefined(role)))
            throw new ValidationException($"Rule {ruleId}: invalid enum value.");
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

    public bool IsEffectiveOn(DateOnly date) => IsEnabled && date >= EffectiveFrom && (EffectiveTo is null || date <= EffectiveTo);

    public string Parameter(string name) => Parameters.TryGetValue(name, out var value)
        ? value : throw new ValidationException($"Rule {RuleId} v{Version} has no parameter '{name}'.");

    public decimal DecimalParameter(string name) =>
        decimal.TryParse(Parameter(name), NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture, out var value)
            ? value : throw new ValidationException($"Rule {RuleId} v{Version}: parameter '{name}' must be a decimal.");
}
