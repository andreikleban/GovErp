namespace GovErp.Application.Web.Reference.Contracts;

/// <summary>
/// One chart-segment value: code, name and attributes.
/// </summary>
public sealed record SegmentValueVm(string Code, string Name, bool IsActive, IReadOnlyDictionary<string, string> Attributes);
