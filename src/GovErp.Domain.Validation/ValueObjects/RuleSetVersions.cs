namespace GovErp.Domain.Validation.ValueObjects;

/// <summary>
/// Which version of which layer went into the rule-set fingerprint.
/// </summary>
public sealed record AppliedRuleVersion(string RuleId, RuleLayer Layer, int Version, string? ScopeFund, string? ScopeGrant);

/// <summary>
/// Layer versions and the fingerprint of the rule set that was applied.
/// </summary>
public sealed record RuleSetVersions(string Engine, int Core, int Federal, int State, int Tenant)
{
    public string Fingerprint { get; init; } = string.Empty;
    private readonly IReadOnlyList<AppliedRuleVersion> _appliedRules = Array.AsReadOnly(Array.Empty<AppliedRuleVersion>());
    public IReadOnlyList<AppliedRuleVersion> AppliedRules
    {
        get => _appliedRules;
        init => _appliedRules = Array.AsReadOnly(value.ToArray());
    }

    public bool Equals(RuleSetVersions? other) => other is not null
        && Engine == other.Engine && Core == other.Core && Federal == other.Federal
        && State == other.State && Tenant == other.Tenant && Fingerprint == other.Fingerprint
        && AppliedRules.SequenceEqual(other.AppliedRules);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Engine);
        hash.Add(Core);
        hash.Add(Federal);
        hash.Add(State);
        hash.Add(Tenant);
        hash.Add(Fingerprint);
        foreach (var rule in AppliedRules) hash.Add(rule);
        return hash.ToHashCode();
    }
}
