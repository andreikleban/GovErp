using System.Collections.ObjectModel;
using System.Globalization;
using GovErp.Domain.Validation.Exceptions;
using GovErp.Domain.Validation.ValueObjects;

namespace GovErp.Domain.Validation.Entities;

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
