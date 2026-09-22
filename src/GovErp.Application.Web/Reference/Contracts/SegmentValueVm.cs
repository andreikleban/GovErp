namespace GovErp.Application.Web.Reference.Contracts;

public sealed record SegmentValueVm(string Code, string Name, bool IsActive, IReadOnlyDictionary<string, string> Attributes);
