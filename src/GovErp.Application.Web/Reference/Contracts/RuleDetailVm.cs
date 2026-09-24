namespace GovErp.Application.Web.Reference.Contracts;

/// <summary>Описание правила вместе с действующей версией и историей версий тенанта.</summary>
public sealed record RuleDetailVm(RuleDescriptionVm Description, RuleVm? Current, IReadOnlyList<RuleVm> Versions);
