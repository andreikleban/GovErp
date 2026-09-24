namespace GovErp.Application.Web.Reference.Contracts;

/// <summary>
/// An account combination as shown on screen.
/// </summary>
public sealed record CombinationVm(string Code, string Status, DateOnly EffectiveFrom, DateOnly? EffectiveTo, string Source);
