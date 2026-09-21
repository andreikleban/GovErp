namespace GovErp.Domain.Shared.ValueObjects;

public sealed record GrantCode : SegmentCode
{
    public GrantCode(string value) : base(value, @"\A[A-Z][A-Z0-9-]{1,30}\z", "Grant") { }
}
