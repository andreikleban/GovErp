namespace GovErp.Application.Web.Reference.Contracts;

public sealed record CombinationVm(string Code, string Status, DateOnly EffectiveFrom, DateOnly? EffectiveTo, string Source);
