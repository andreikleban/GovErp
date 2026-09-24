namespace GovErp.Application.Web.Reference.Contracts;

/// <summary>
/// A rule version. IsCurrent: this version applies on the business date (the newest effective one in its layer and scope);
/// earlier versions stay in history, past evaluations refer to them.
/// </summary>
public sealed record RuleVm(Guid Id, string RuleId, int Version, int Step, string Layer, string? ScopeFund, string? ScopeGrant, string? Severity,
    IReadOnlyDictionary<string, string> Parameters, IReadOnlyList<string> OverridableBy, DateOnly EffectiveFrom, DateOnly? EffectiveTo,
    bool IsEnabled, string Message, bool IsCurrent);

/// <summary>
/// CurrentFingerprint is the fingerprint of the general set without scope; scoped rules are shown with ScopeFund and ScopeGrant.
/// </summary>
public sealed record RuleSetVm(IReadOnlyList<RuleVm> Rules, string CurrentFingerprint, string EngineVersion);
