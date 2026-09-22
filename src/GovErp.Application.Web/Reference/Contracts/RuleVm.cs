namespace GovErp.Application.Web.Reference.Contracts;

public sealed record RuleVm(string RuleId, int Version, int Step, string Layer, string? ScopeFund, string? ScopeGrant, string? Severity,
    IReadOnlyDictionary<string, string> Parameters, IReadOnlyList<string> OverridableBy, DateOnly EffectiveFrom, DateOnly? EffectiveTo, bool IsEnabled, string Message);

/// <summary>CurrentFingerprint — отпечаток общего набора без scope; scoped-правила показаны с ScopeFund и ScopeGrant.</summary>
public sealed record RuleSetVm(IReadOnlyList<RuleVm> Rules, string CurrentFingerprint, string EngineVersion);
