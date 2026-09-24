namespace GovErp.Application.Web.Reference.Contracts;

/// <summary>
/// Версия правила. IsCurrent — эта версия применяется на бизнес-дату (самая свежая действующая в своём слое и scope);
/// прежние версии остаются в истории, на них ссылаются прошлые оценки.
/// </summary>
public sealed record RuleVm(Guid Id, string RuleId, int Version, int Step, string Layer, string? ScopeFund, string? ScopeGrant, string? Severity,
    IReadOnlyDictionary<string, string> Parameters, IReadOnlyList<string> OverridableBy, DateOnly EffectiveFrom, DateOnly? EffectiveTo,
    bool IsEnabled, string Message, bool IsCurrent);

/// <summary>CurrentFingerprint — отпечаток общего набора без scope; scoped-правила показаны с ScopeFund и ScopeGrant.</summary>
public sealed record RuleSetVm(IReadOnlyList<RuleVm> Rules, string CurrentFingerprint, string EngineVersion);
