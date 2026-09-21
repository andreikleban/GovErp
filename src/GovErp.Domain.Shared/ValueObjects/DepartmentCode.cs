namespace GovErp.Domain.Shared.ValueObjects;

public sealed record DepartmentCode : SegmentCode
{
    public DepartmentCode(string value) : base(value, @"\A[0-9]{4}\z", "Department") { }
}
