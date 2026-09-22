namespace GovErp.Application.Web.Reference.Contracts;

public sealed record SegmentsVm(IReadOnlyList<SegmentValueVm> Funds, IReadOnlyList<SegmentValueVm> Departments, IReadOnlyList<SegmentValueVm> Objects, IReadOnlyList<SegmentValueVm> Grants);
