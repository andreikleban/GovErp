namespace GovErp.Application.Web.Reference.Contracts;

/// <summary>A rule description together with the tenant's current version and version history.</summary>
public sealed record RuleDetailVm(RuleDescriptionVm Description, RuleVm? Current, IReadOnlyList<RuleVm> Versions);
