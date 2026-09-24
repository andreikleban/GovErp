namespace GovErp.Application.Web.Reference.Contracts;

/// <summary>
/// Chart-segment lists used to fill in forms.
/// </summary>
public sealed record SegmentsVm(IReadOnlyList<SegmentValueVm> Funds, IReadOnlyList<SegmentValueVm> Departments, IReadOnlyList<SegmentValueVm> Objects, IReadOnlyList<SegmentValueVm> Grants);
